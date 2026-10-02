using UnityEngine;

/// <summary>적의 공격 방식. 유형마다 다르다. (docs/Dokkaebi_Hunting_System.md 1절)</summary>
public enum EnemyAttackKind
{
    /// <summary>근접 — 사거리 안에서 직접 판정한다. 포자충 · 사냥개</summary>
    Melee = 0,

    /// <summary>원거리 — 투사체를 발사한다. 번개귀 · 침귀 · 감시자</summary>
    Ranged = 1
}

/// <summary>
/// 적 공격. 근접과 원거리를 같은 컴포넌트가 담당한다.
///
/// 접촉 즉시 피해를 주는 방식(뱀서류)이 아니라
/// '사거리 진입 → 예비동작 → 판정 → 쿨다운' 순서를 갖는다.
///
/// 이유: 접촉 피해는 회피라는 선택지를 없앤다. 예비동작이 있어야
/// 플레이어가 대시로 빠져나갈 수 있고, 그래야 대시가 전투 기술로서 의미를 갖는다.
///
/// 원거리를 별도 컴포넌트로 나누지 않은 이유 —
/// 「예비동작 → 판정」은 이 게임의 방어 규칙 그 자체다. 두 곳에 두면
/// 한쪽만 고쳐져 "어떤 적은 피할 수 없는" 상태가 조용히 생긴다.
/// 공격 방식만 갈리고 텔레그래프 규칙은 한 곳에 남긴다.
/// </summary>
public class EnemyAttack : MonoBehaviour
{
    [Header("Attack")]
    [Tooltip("근접인지 원거리인지. 유형마다 다르다.")]
    [SerializeField] private EnemyAttackKind attackKind = EnemyAttackKind.Melee;

    [Tooltip("기본 피해. 유형별 수치는 Combat_Baseline 8절을 따른다.")]
    [SerializeField] private int damage = 8;

    [Tooltip("공격 판정이 닿는 거리. 원거리는 훨씬 길다.")]
    [SerializeField] private float attackRange = 1.6f;

    [Tooltip("방어 관통 레벨. Pierce(관통)와 다른 개념이다.")]
    [SerializeField] private int armourPenetration = 0;

    [Tooltip("이 공격이 거는 상태이상. 없으면 None.")]
    [SerializeField] private StatusEffectType appliedStatus = StatusEffectType.None;

    [Header("Ranged")]
    [Tooltip("원거리일 때 발사할 투사체 프리팹. 풀에서 꺼낸다.")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("발사 지점 오프셋(로컬). 발밑에서 나가지 않게 살짝 띄운다.")]
    [SerializeField] private Vector3 muzzleOffset = new Vector3(0f, 0.8f, 0f);

    [Tooltip("원거리 적이 유지하려는 거리. 이보다 가까우면 더 접근하지 않는다.")]
    [SerializeField] private float preferredDistance = 7f;

    [Tooltip("예비동작 시간(초). 이 시간 동안 플레이어는 회피할 수 있다.")]
    [SerializeField] private float windupDuration = 0.35f;

    [Tooltip("공격 후 재사용 대기시간(초)")]
    [SerializeField] private float attackCooldown = 1.2f;

    [Header("Feedback")]
    [Tooltip("예비동작 중 표시할 오브젝트(선택). 없으면 표시하지 않는다.")]
    [SerializeField] private GameObject windupIndicator;

    [Tooltip("원거리 예비동작 중의 이동 속도 배율. 0이면 완전히 멈춘다.")]
    [Range(0f, 1f)]
    [SerializeField] private float rangedWindupSpeedScale = 0.45f;

    private EnemyMovement movement;
    private EnemyBrain brain;
    private CooldownTimer cooldown;

    /// <summary>공격 판정이 끝났을 때 발행된다. 두뇌가 탄창 소모를 센다.</summary>
    public event System.Action OnAttackResolved;

    private float windupEndTime;

    /// <summary>현재 예비동작 중인지.</summary>
    public bool IsWindingUp { get; private set; }

    public float AttackRange => attackRange;

    /// <summary>기본 피해. 프리팹 수치가 전투 기준과 맞는지 테스트가 읽는다.</summary>
    public int Damage => damage;

    /// <summary>방어 관통 레벨.</summary>
    public int ArmourPenetration => armourPenetration;

    /// <summary>이 적의 공격 방식.</summary>
    public EnemyAttackKind Kind => attackKind;

    /// <summary>원거리일 때 쏘는 것. 비어 있으면 공격이 아무 일도 하지 않는다.</summary>
    public GameObject ProjectilePrefab => projectilePrefab;

    /// <summary>
    /// 유형 · 등급이 정한 공격 수치를 주입한다. (EnemyIdentity)
    ///
    /// 【더하지 않고 덮어쓴다.】 풀에서 재사용될 때마다 등급 배율을 더하면
    /// 같은 프리팹이 돌 때마다 피해가 계속 커진다. 인스펙터 값은
    /// EnemyIdentity가 없는 프리팹의 기본값으로만 남는다.
    /// </summary>
    public void SetOffence(int newDamage, int newArmourPenetration)
    {
        damage = Mathf.Max(1, newDamage);
        armourPenetration = Mathf.Max(0, newArmourPenetration);
    }

    private PoolManager poolManager;
    private EnemyAggro aggro;

    private void Awake()
    {
        movement = GetComponent<EnemyMovement>();
        brain = GetComponent<EnemyBrain>();
        aggro = GetComponent<EnemyAggro>();

        // 원거리 적은 사거리 안에서 멈춰 쏜다. 근접까지 붙으면 원거리의 의미가 없다.
        if (attackKind == EnemyAttackKind.Ranged && movement != null)
            movement.SetStoppingDistance(preferredDistance);

        if (attackKind == EnemyAttackKind.Ranged && projectilePrefab == null)
            GameLogger.Error("[EnemyAttack] 원거리 적인데 projectilePrefab이 비어 있습니다.", this);
    }

    /// <summary>풀에서 재사용될 때 공격 상태를 초기화한다.</summary>
    /// <summary>풀로 돌아갈 때 점사를 끊는다. 사라진 적이 총을 쏘면 안 된다.</summary>
    private void StopBurst()
    {
        burstRemaining = 0;
    }

    public void ResetState()
    {
        CancelWindup();
        StopBurst();
        cooldown.Reset();
    }

    private void Update()
    {
        if (movement == null)
            return;

        // 【동결·마비면 예비동작도 점사도 끊는다.】 점사는 「이미 방아쇠가
        // 당겨진 것」이라 평소에는 흘려보내지만, 얼어붙은 손가락은 방아쇠를
        // 당기지 못한다. 동결을 건 보람이 여기서 나온다.
        //
        // 쿨다운은 되돌리지 않는다 — 풀리자마자 쏘면 동결이 공격을 「미룬」
        // 것에 그치지만, 한 번을 통째로 날리면 「막은」 것이 된다.
        if (movement.IsIncapacitated)
        {
            if (IsWindingUp)
                CancelWindup();

            StopBurst();
            return;
        }

        // 점사는 예비동작과 무관하게 흘러간다 — 이미 방아쇠가 당겨진 것이다.
        UpdateBurst();

        if (IsWindingUp)
        {
            UpdateWindup();
            return;
        }

        TryStartWindup();
    }

    private void TryStartWindup()
    {
        if (movement.DistanceToTarget > attackRange)
            return;

        // 지금 내 차례가 아니면 쏘지 않는다. 동시에 달려드는 그림을 막는 핵심이다.
        if (brain != null && !brain.MayAttack)
            return;

        if (!cooldown.TryConsume(Time.time, attackCooldown + windupDuration))
            return;

        IsWindingUp = true;
        windupEndTime = Time.time + windupDuration;

        // 근접은 완전히 멈춰 '공격이 온다'는 신호를 명확히 준다.
        // 원거리는 느려질 뿐이다. 멈춰 서면 과녁이 되어 사람처럼 보이지 않는다.
        if (attackKind == EnemyAttackKind.Melee)
            movement.IsHalted = true;
        else
            movement.SpeedScale = rangedWindupSpeedScale;

        SetIndicator(true);
    }

    private void UpdateWindup()
    {
        if (Time.time < windupEndTime)
            return;

        ResolveAttack();

        // 빗나가도 한 발 쓴 것으로 센다. 그래야 재장전이 「명중 수」가 아니라
        // 「쏜 횟수」에 걸려 플레이어의 회피가 적을 오히려 유리하게 만들지 않는다.
        OnAttackResolved?.Invoke();

        CancelWindup();
    }

    /// <summary>
    /// 예비동작이 끝난 시점에 사거리를 다시 확인한다. 벗어났으면 빗나간다.
    ///
    /// 이 재확인이 대시 회피를 성립시킨다. 예비동작 시작 시점에 판정하면
    /// 피해도 소용이 없어 대시가 전투 기술이 되지 못한다.
    /// </summary>
    private void ResolveAttack()
    {
        // 이동과 같은 대상을 때린다. 두 곳이 다른 대상을 보면
        // 옆 적에게 다가가서 플레이어를 쏘는 장면이 나온다.
        Transform target = aggro != null
            ? aggro.Target
            : EnemyManager.EnsureInstance().PlayerTransform;

        if (target == null)
            return;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude > attackRange)
        {
            GameLogger.Log("[EnemyAttack] 회피됨");
            return;
        }

        if (attackKind == EnemyAttackKind.Ranged)
        {
            FireProjectile(toTarget);
            return;
        }

        if (!target.TryGetComponent(out Health targetHealth))
            return;

        // 【Team이 아니라 Faction으로 거른다.】
        // 전에는 Team.Enemy면 무조건 돌아섰다. 그래서 진영을 넣어도
        // 적끼리는 영원히 서로를 때릴 수 없었다.
        // Team은 「총알이 누구를 때리는가」, Faction은 「누가 누구와 싸우는가」다.
        if (targetHealth.Team == Team.Enemy)
        {
            bool hostile = aggro != null
                           && FactionTable.IsHostile(aggro.Faction, aggro.TargetFaction);

            if (!hostile)
                return;
        }

        // 근접은 몸통 방어도를 쓴다. 사거리 보정은 적용하지 않는다.
        var request = new DamageRequest
        {
            baseDamage = damage,
            increasedPercent = 0f,
            element = ElementOf(appliedStatus),
            hitKind = HitKind.Melee,
            armourPenetration = armourPenetration,
            distance = -1f,
            effectiveRange = 0f,
            isCritical = false,
            criticalMultiplier = 1f,
            bypassArmour = false
        };

        if (appliedStatus != StatusEffectType.None)
            targetHealth.ApplyStatus(appliedStatus, damage);

        // 난이도 배율은 여기서만 곱한다. 적이 주는 피해의 유일한 관문이다.
        int applied = targetHealth.TakeDamage(request, GameManager.EnemyDamageMultiplier);

        if (applied > 0)
            GameLogger.Log($"[EnemyAttack] 명중 {applied}");
    }

    /// <summary>
    /// 플레이어 방향으로 투사체를 발사한다.
    ///
    /// 발사 시점의 방향으로 직선 비행하므로, 예비동작을 보고 옆으로 움직이면
    /// 빗나간다. 근접의 대시 회피와 같은 규칙이 원거리에도 적용된다.
    /// </summary>
    /// <summary>
    /// 유형 고유의 기믹. 프리팹 연결이 정한다. (EnemyGimmickTable)
    /// None이면 평범하게 한 발 쏜다.
    /// </summary>
    [Header("Gimmick")]
    [Tooltip("유형 고유 기믹. 왕지네 8방향 · 순라귀 3점사 · 허깨비 중력·은신.")]
    [SerializeField] private EnemyGimmick gimmick = EnemyGimmick.None;

    public EnemyGimmick Gimmick => gimmick;

    public void SetGimmick(EnemyGimmick value) => gimmick = value;

    /// <summary>
    /// 점사 중 남은 발수. 0이면 점사가 아니거나 끝났다.
    /// 예비동작을 다시 거치지 않는다 — 【한 번 걸리면 세 방 맞는다】가 핵심이다.
    /// </summary>
    private int burstRemaining;
    private float nextBurstTime;
    private Vector3 burstDirection;

    /// <summary>
    /// 점사의 남은 발을 흘려보낸다. Update에서 매 프레임 부른다.
    ///
    /// 코루틴을 쓰지 않는 이유 — 적이 죽거나 풀로 돌아갈 때
    /// 코루틴만 살아남아 사라진 적이 총을 쏘는 일이 생긴다.
    /// </summary>
    private void UpdateBurst()
    {
        if (burstRemaining <= 0 || Time.time < nextBurstTime)
            return;

        burstRemaining--;
        nextBurstTime = Time.time + EnemyGimmickTable.Get(gimmick).BurstInterval;

        Spray(burstDirection);
    }

    private void FireProjectile(Vector3 toTarget)
    {
        if (projectilePrefab == null)
            return;

        GimmickSpec spec = EnemyGimmickTable.Get(gimmick);

        // 첫 발은 지금 나간다. 나머지는 UpdateBurst가 간격을 두고 흘린다.
        Spray(toTarget);

        if (spec.BurstCount <= 1)
            return;

        burstRemaining = spec.BurstCount - 1;
        burstDirection = toTarget;
        nextBurstTime = Time.time + spec.BurstInterval;
    }

    /// <summary>
    /// 한 번의 발사. 기믹이 여러 방향이면 그만큼 나간다.
    /// 【왕지네의 8방향이 여기서 갈린다.】
    /// </summary>
    private void Spray(Vector3 toTarget)
    {
        GimmickSpec spec = EnemyGimmickTable.Get(gimmick);

        if (spec.ProjectilesPerShot <= 1)
        {
            LaunchOne(toTarget);
            return;
        }

        for (int i = 0; i < spec.ProjectilesPerShot; i++)
        {
            LaunchOne(EnemyGimmickTable.SprayDirection(
                toTarget, i, spec.ProjectilesPerShot));
        }
    }

    private void LaunchOne(Vector3 toTarget)
    {
        if (projectilePrefab == null)
            return;

        if (poolManager == null)
            poolManager = PoolManager.EnsureInstance();

        Vector3 origin = transform.position + transform.TransformVector(muzzleOffset);
        Quaternion rotation = Quaternion.LookRotation(toTarget.normalized);

        GameObject projectile = poolManager.Spawn(projectilePrefab, origin, rotation);

        if (projectile == null)
            return;

        // 적의 탄은 플레이어를 향한다. 탄 프리팹의 targetTeam이 Player여야 한다.
        if (projectile.TryGetComponent(out BulletController bullet))
        {
            bullet.ConfigureAsEnemyShot(damage, attackRange, armourPenetration, appliedStatus,
                origin, aggro != null ? aggro.Faction : Faction.Wild);

            // 플레이어와 겹친 상태로 쏠 때도 같은 문제가 생긴다. 같은 규칙을 적용한다.
            bullet.ResolveMuzzleOverlap(
                new Vector3(transform.position.x, origin.y, transform.position.z));
        }
    }

    private static DamageElement ElementOf(StatusEffectType status)
    {
        switch (status)
        {
            case StatusEffectType.Ignite: return DamageElement.Fire;
            case StatusEffectType.Poison: return DamageElement.Chaos;
            case StatusEffectType.Freeze:
                case StatusEffectType.Chill: return DamageElement.Cold;
            case StatusEffectType.Shock: return DamageElement.Lightning;
            default: return DamageElement.Physical;
        }
    }

    private void CancelWindup()
    {
        IsWindingUp = false;

        if (movement != null)
        {
            movement.IsHalted = false;
            movement.SpeedScale = 1f;
        }

        SetIndicator(false);
    }

    private void SetIndicator(bool visible)
    {
        if (windupIndicator != null)
            windupIndicator.SetActive(visible);
    }

    private void OnDisable()
    {
        CancelWindup();
    }
}
