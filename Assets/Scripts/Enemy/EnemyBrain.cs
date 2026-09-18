using UnityEngine;

/// <summary>
/// 적 교전 두뇌 — 사람이 조작하는 것처럼 싸우게 만든다.
/// (판단 자체는 EngagementPlanner / AttackTokenPool에 순수 로직으로 분리되어 있다)
///
/// 【이전 행동】 플레이어를 향해 직선으로 달린다 → 사거리에서 멈춘다 → 때린다.
/// 여럿이면 전부 한 점으로 뭉쳐 동시에 때린다. 피할 방법이 없다.
///
/// 【지금 행동】
///   · 유지 거리를 지킨다. 너무 붙으면 쏘면서 물러난다
///   · 정면으로 붙지 않고 옆으로 돈다. 도는 방향은 주기적으로 바뀐다
///   · 한 탄창을 비우면 거리를 벌리고 재장전한다. 그동안 쏘지 않는다
///   · 【동시에 공격하는 수가 제한된다】 차례가 아닌 적은 쏘지 않고 자리를 잡는다
///   · 플레이어를 발견해도 즉시 반응하지 않는다. 짧은 반응 시간이 있다
///
/// 이 다섯 가지가 「사람처럼」의 실체다. 특히 세 번째와 네 번째가 없으면
/// 아무리 상태를 쪼개도 결국 벌레 떼처럼 보인다.
/// </summary>
[RequireComponent(typeof(EnemyMovement))]
public class EnemyBrain : MonoBehaviour, IEnemySteering
{
    [Header("Engagement")]
    [Tooltip("교전을 인지하는 최대 거리(m).")]
    [SerializeField] private float detectDistance = 18f;

    [Tooltip("유지하려는 거리(m). 0 이하면 EnemyAttack의 값에서 자동으로 정한다.")]
    [SerializeField] private float preferredDistance = 0f;

    [Tooltip("유지 거리의 허용 폭(m). 이 안에서는 붙지도 물러나지도 않는다.")]
    [Min(0.1f)]
    [SerializeField] private float band = 1.2f;

    [Header("Reaction")]
    [Tooltip("발견 후 움직이기까지의 지연(초). 0이면 즉시 반응해 기계처럼 보인다.")]
    [Min(0f)]
    [SerializeField] private float reactionTime = 0.35f;

    [Header("Strafe")]
    [Tooltip("도는 방향을 바꾸는 최소 간격(초).")]
    [Min(0.2f)]
    [SerializeField] private float strafeFlipMin = 1.6f;

    [Tooltip("도는 방향을 바꾸는 최대 간격(초).")]
    [Min(0.2f)]
    [SerializeField] private float strafeFlipMax = 3.6f;

    [Header("Burst / Reload")]
    [Tooltip("한 번에 이어 쏘는 횟수. 이만큼 쏘면 물러나 재장전한다.")]
    [Min(1)]
    [SerializeField] private int shotsPerBurst = 3;

    [Tooltip("재장전에 걸리는 시간(초). 이 동안 쏘지 않고 거리를 벌린다.")]
    [Min(0.1f)]
    [SerializeField] private float reloadDuration = 1.8f;

    [Header("Line of Sight")]
    [Tooltip("시야를 가리는 레이어. 비워 두면 항상 보이는 것으로 본다.")]
    [SerializeField] private LayerMask sightBlockMask = 0;

    [Tooltip("시야 검사 간격(초). 매 프레임 레이캐스트하지 않는다.")]
    [Min(0.02f)]
    [SerializeField] private float sightCheckInterval = 0.2f;

    [Tooltip("시야 검사 시 눈높이(로컬 y).")]
    [SerializeField] private float eyeHeight = 0.9f;

    private EnemyMovement movement;
    private EnemyAttack attack;
    private EnemyManager enemyManager;

    private int tokenId;

    private float strafeSign = 1f;
    private float nextStrafeFlipTime;

    private float reactionReadyTime = -1f;
    private bool hasSeenTarget;

    private int shotsFired;
    private float reloadEndTime;

    private float nextSightCheckTime;
    private bool hasLineOfSight = true;

    private EngagementPlan plan;

    /// <summary>지금 공격해도 되는지. EnemyAttack이 예비동작을 시작하기 전에 확인한다.</summary>
    public bool MayAttack => plan.mayAttack;

    /// <summary>현재 교전 단계. 디버그 표시와 테스트에 쓴다.</summary>
    public EngagementPhase Phase => plan.phase;

    /// <summary>재장전 중인지.</summary>
    public bool IsReloading => Time.time < reloadEndTime;

    private void Awake()
    {
        movement = GetComponent<EnemyMovement>();
        attack = GetComponent<EnemyAttack>();

        // 개체마다 고유한 정수. 차례표가 이 값으로 누가 쥐고 있는지 구분한다.
        tokenId = GetEntityId().GetHashCode();
    }

    private void OnEnable()
    {
        if (attack != null)
            attack.OnAttackResolved += HandleAttackResolved;

        ResetState();
    }

    private void OnDisable()
    {
        if (attack != null)
            attack.OnAttackResolved -= HandleAttackResolved;

        ReleaseToken();
    }

    /// <summary>풀에서 재사용될 때 교전 상태를 초기화한다.</summary>
    public void ResetState()
    {
        shotsFired = 0;
        reloadEndTime = 0f;
        hasSeenTarget = false;
        reactionReadyTime = -1f;
        hasLineOfSight = true;
        nextSightCheckTime = 0f;
        plan = default;

        strafeSign = Random.value < 0.5f ? -1f : 1f;
        nextStrafeFlipTime = Time.time + Random.Range(strafeFlipMin, strafeFlipMax);

        ReleaseToken();
    }

    /// <summary>유지 거리. 지정하지 않았으면 공격 방식에서 끌어온다.</summary>
    private float ResolvePreferredDistance()
    {
        if (preferredDistance > 0f)
            return preferredDistance;

        if (attack == null)
            return 2f;

        // 근접은 사거리보다 살짝 안쪽에서 버틴다. 사거리에 딱 맞추면
        // 플레이어가 한 발만 물러나도 계속 붙었다 떨어졌다를 반복한다.
        return attack.Kind == EnemyAttackKind.Melee
            ? attack.AttackRange * 0.75f
            : attack.AttackRange * 0.8f;
    }

    public bool TryGetSteering(Vector3 toTarget, float distance,
                               out Vector3 direction, out float speedScale)
    {
        direction = Vector3.zero;
        speedScale = 1f;

        UpdateStrafeSign();
        UpdateLineOfSight(toTarget, distance);

        var input = new EngagementInput
        {
            distance = distance,
            preferredDistance = ResolvePreferredDistance(),
            band = band,
            attackRange = attack != null ? attack.AttackRange : 1.6f,
            detectDistance = detectDistance,
            hasAttackToken = UpdateToken(distance),
            isReloading = IsReloading,
            hasLineOfSight = hasLineOfSight
        };

        plan = EngagementPlanner.Plan(in input);

        if (!UpdateReaction(input))
        {
            // 아직 반응 전이다. 움직이지도 쏘지도 않는다.
            plan.mayAttack = false;
            return true;
        }

        if (plan.phase == EngagementPhase.Idle)
            return false;

        direction = EngagementPlanner.ToDirection(in plan, toTarget, strafeSign);

        // 물러날 때는 조금 느리다. 게걸음으로 뒤로 빠지는 그림을 만든다.
        speedScale = plan.approach < 0f ? 0.8f : 1f;

        return true;
    }

    /// <summary>발견 후 반응 지연. 지연이 끝나기 전에는 false.</summary>
    private bool UpdateReaction(in EngagementInput input)
    {
        if (plan.phase == EngagementPhase.Idle)
        {
            hasSeenTarget = false;
            reactionReadyTime = -1f;

            return false;
        }

        if (!hasSeenTarget)
        {
            hasSeenTarget = true;
            reactionReadyTime = Time.time + reactionTime;
        }

        return Time.time >= reactionReadyTime;
    }

    private void UpdateStrafeSign()
    {
        if (Time.time < nextStrafeFlipTime)
            return;

        strafeSign = -strafeSign;
        nextStrafeFlipTime = Time.time + Random.Range(strafeFlipMin, strafeFlipMax);
    }

    private void UpdateLineOfSight(Vector3 toTarget, float distance)
    {
        if (sightBlockMask.value == 0)
        {
            hasLineOfSight = true;
            return;
        }

        if (Time.time < nextSightCheckTime)
            return;

        nextSightCheckTime = Time.time + sightCheckInterval;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;

        hasLineOfSight = !Physics.Raycast(
            eye, toTarget, distance, sightBlockMask, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// 공격 차례를 갱신한다. 재장전 중이거나 사거리 밖이면 차례를 놓아 준다.
    /// 놓아 주지 않으면 뒤에서 재장전하는 적이 남의 차례를 붙들고 있게 된다.
    /// </summary>
    private bool UpdateToken(float distance)
    {
        AttackTokenPool pool = TokenPool();

        if (pool == null)
            return true;

        float attackRange = attack != null ? attack.AttackRange : 1.6f;

        if (IsReloading || distance > attackRange * 1.4f || distance > detectDistance)
        {
            pool.Release(tokenId);
            return false;
        }

        return pool.TryAcquire(tokenId, Time.time);
    }

    private void ReleaseToken()
    {
        TokenPool()?.Release(tokenId);
    }

    private AttackTokenPool TokenPool()
    {
        if (enemyManager == null)
            enemyManager = EnemyManager.EnsureInstance();

        return enemyManager != null ? enemyManager.AttackTokens : null;
    }

    /// <summary>한 발 쐈다. 탄창을 다 쓰면 물러나 재장전한다.</summary>
    private void HandleAttackResolved()
    {
        shotsFired++;

        if (shotsFired < shotsPerBurst)
            return;

        shotsFired = 0;
        reloadEndTime = Time.time + reloadDuration;

        // 재장전에 들어가면 차례를 즉시 놓는다. 다음 적이 바로 들어올 수 있게.
        ReleaseToken();
    }
}
