using UnityEngine;

/// <summary>적의 공격 방식. 원형마다 다르다. (docs/Blob_Hunting_System.md 1절)</summary>
public enum EnemyAttackKind
{
    /// <summary>근접 — 사거리 안에서 직접 판정한다. 포자충 · 사냥개</summary>
    Melee = 0,

    /// <summary>원거리 — 투사체를 발사한다. 자전체 · 검체 · 감시자</summary>
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
    [Tooltip("근접인지 원거리인지. 원형마다 다르다.")]
    [SerializeField] private EnemyAttackKind attackKind = EnemyAttackKind.Melee;

    [Tooltip("기본 피해. 원형별 수치는 Combat_Baseline 8절을 따른다.")]
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

    [Tooltip("원거리 예비동작 중의 이동 속도 배수. 0이면 완전히 멈춘다.")]
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

    /// <summary>이 적의 공격 방식.</summary>
    public EnemyAttackKind Kind => attackKind;

    private PoolManager poolManager;

    private void Awake()
    {
        movement = GetComponent<EnemyMovement>();
        brain = GetComponent<EnemyBrain>();

        // 원거리 적은 사거리 안에서 멈춰 쏜다. 근접까지 붙으면 원거리의 의미가 없다.
        if (attackKind == EnemyAttackKind.Ranged && movement != null)
            movement.SetStoppingDistance(preferredDistance);

        if (attackKind == EnemyAttackKind.Ranged && projectilePrefab == null)
            GameLogger.Error("[EnemyAttack] 원거리 적인데 projectilePrefab이 비어 있습니다.", this);
    }

    /// <summary>풀에서 재사용될 때 공격 상태를 초기화한다.</summary>
    public void ResetState()
    {
        CancelWindup();
        cooldown.Reset();
    }

    private void Update()
    {
        if (movement == null)
            return;

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
        Transform target = EnemyManager.EnsureInstance().PlayerTransform;

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

        if (targetHealth.Team == Team.Enemy)
            return;

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

        int applied = targetHealth.TakeDamage(request);

        if (applied > 0)
            GameLogger.Log($"[EnemyAttack] 명중 {applied}");
    }

    /// <summary>
    /// 플레이어 방향으로 투사체를 발사한다.
    ///
    /// 발사 시점의 방향으로 직선 비행하므로, 예비동작을 보고 옆으로 움직이면
    /// 빗나간다. 근접의 대시 회피와 같은 규칙이 원거리에도 적용된다.
    /// </summary>
    private void FireProjectile(Vector3 toTarget)
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
            bullet.ConfigureAsEnemyShot(damage, attackRange, armourPenetration, appliedStatus, origin);

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
            case StatusEffectType.Freeze: return DamageElement.Cold;
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
