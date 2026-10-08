using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 투사체. 충돌 시 Skill이 부여한 행동을 우선순위에 따라 하나만 해결한다.
///
///     적 충돌  : Split → Pierce → Fork → Chain → Return  (한 충돌에 단 하나)
///     지형 충돌: 튕겨 쏘기 — 위 큐와 별개로 동작하며 큐를 소모하지 않는다. (v5 §10)
///
/// 판정 로직 자체는 ProjectileCollisionResolver / ChainTargetSelector /
/// ProjectileRicochetState에 순수 로직으로 분리되어 있다.
/// 이 컴포넌트는 그 결과를 씬에 옮기는 역할만 한다.
/// </summary>
public class BulletController : MonoBehaviour, IPoolable
{
    [Header("Base Stats")]
    [SerializeField] private float speed = 20f;

    [Tooltip("무기가 정하는 기본 피해. 기준값은 CombatConstants.BaseWeaponDamage(10)이다.")]
    [SerializeField] private int damage = CombatConstants.BaseWeaponDamage;

    [Tooltip("유효 사거리(m). 절반을 넘으면 피해가 절반이 된다.")]
    [SerializeField] private float effectiveRange = CombatConstants.BaseEffectiveRange;

    [Tooltip("무기가 제공하는 방어 관통 레벨. Pierce(관통)와 다른 개념이다.")]
    [SerializeField] private int armourPenetration = 0;

    /// <summary>스킬 합산 결과. 참조만 든다. null이면 보정이 없다(적 탄 등).</summary>
    private WeaponModifiers skillEffects;

    /// <summary>치명타 확률(0~1). 각인 「정밀」만 이 값을 올린다. 기본은 0이다.</summary>
    private float criticalChance;

    /// <summary>치명타 배율.</summary>
    private float criticalMultiplier = CombatConstants.BaseCriticalMultiplier;

    [Tooltip("자동 소멸까지의 시간(초)")]
    [SerializeField] private float lifetime = 3f;

    [Tooltip("이 소속의 대상에게만 피해를 준다. 적 탄은 소속 대신 진영으로 거른다.")]
    [SerializeField] private Team targetTeam = Team.Enemy;

    /// <summary>
    /// 쏜 쪽의 진영. 적 탄만 쓴다.
    ///
    /// 【Team만으로는 난전이 성립하지 않는다.】
    /// 적 탄은 targetTeam = Team.Player로 고정돼 있어, 진영을 넣어도
    /// 적의 총알이 다른 적을 맞히는 일이 영영 없었다.
    /// Team은 「총알이 누구를 때리는가」의 큰 구분이고,
    /// Faction은 그 안에서 「누가 누구와 싸우는가」다. 축이 둘이다.
    /// </summary>
    private Faction shooterFaction = Faction.Wild;

    /// <summary>적이 쏜 탄인가. 켜지면 Team 대신 진영으로 거른다.</summary>
    private bool useFactionGate;

    /// <summary>신기전 — 맞은 자리 폭발 반경 (m) · 둘레가 받는 몫 (결정 2-80). 0이면 터지지 않는다.</summary>
    private float explosionRadius;
    private float explosionShare;
    private static readonly Collider[] explosionHits = new Collider[24];
    private static readonly System.Collections.Generic.List<Health> explosionDone = new();

    [Header("Behaviour Tuning")]
    [Tooltip("분열(Split) 시 좌우 최대 각도(도). 3갈래가 -각도 / 0 / +각도로 퍼진다.")]
    [SerializeField] private float splitAngle = 60f;

    [Tooltip("Fork 분열 시 원 궤도 기준 좌우 각도(도). PoE2 기준 60도.")]
    [SerializeField] private float forkAngle = 60f;

    [Tooltip("Chain이 다음 대상을 찾는 반경(m)")]
    [SerializeField] private float chainRadius = 6f;

    [Header("Point-Blank (근접 사격)")]
    [Tooltip("총구와 발사자 사이 구간을 즉시 판정할 때 쓰는 반경(m). " +
             "적이 몸에 붙었을 때 총알이 적 몸 안에서 태어나 판정이 통째로 사라지는 것을 막는다.")]
    [Min(0f)]
    [SerializeField] private float muzzleCheckRadius = 0.25f;

    [Header("Ricochet (튕겨 쏘기)")]
    [Tooltip("벽 · 튕김 판정 대상 레이어. 비워 두면 맵의 키 큰 덩어리(GameLayers.ShotBlockMask — 결정 2-89).")]
    [SerializeField] private LayerMask terrainMask = 0;

    [Tooltip("튕긴 직후 벽에 다시 박히지 않도록 법선 방향으로 밀어내는 거리(m)")]
    [SerializeField] private float ricochetSkin = 0.05f;

    // 모든 투사체가 공유하는 재사용 버퍼. 체인 판정마다 새 리스트를 만들지 않는다.
    private static readonly List<Vector3> ChainPositions = new(64);
    private static readonly List<bool> ChainExcluded = new(64);
    private static readonly List<Transform> ChainTransforms = new(64);

    /// <summary>근접 사격 판정용 공유 버퍼. 발사마다 배열을 만들지 않는다.</summary>
    private static readonly Collider[] MuzzleOverlapBuffer = new Collider[16];

    private PooledObject pooledObject;
    private PoolManager poolManager;

    private readonly List<Transform> hitTargets = new();

    private ProjectileBehaviourState behaviourState;
    private ProjectileRicochetState ricochetState;

    private float currentSpeed;
    private float despawnTime;
    private Vector3 originPoint;
    private bool isConsumed;
    private bool isReturning;

    private Renderer[] tintRenderers;
    private Color[] baseColors;

    private void Awake()
    {
        pooledObject = GetComponent<PooledObject>();

        // 【임시 표시】 탄 색 = 실은 상태 (결정 2-77). 원래 색을 기억해 두고 풀에서 돌아올 때 되돌린다.
        tintRenderers = GetComponentsInChildren<Renderer>(true);
        baseColors = new Color[tintRenderers.Length];
        for (int i = 0; i < tintRenderers.Length; i++)
            baseColors[i] = tintRenderers[i].material.color;
    }

    /// <summary>【임시】 실은 상태의 색으로 탄을 칠한다 — 화염 주황 · 역병 초록 · 서리 하늘 · 뇌전 노랑 · 열상 빨강.</summary>
    private void ApplyTint()
    {
        if (tintRenderers == null)
            return;

        Color? color = StatusTint.Of(AppliedStatus);

        for (int i = 0; i < tintRenderers.Length; i++)
        {
            if (tintRenderers[i] == null)
                continue;

            Material m = tintRenderers[i].material;
            Color c = color ?? baseColors[i];
            m.color = c;
            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", c);
        }
    }

    public void OnSpawned()
    {
        isConsumed = false;
        isReturning = false;

        hitTargets.Clear();

        // Configure가 호출되지 않는 경우(씬 배치 등)를 대비한 기본값.
        currentSpeed = speed;
        despawnTime = Time.time + lifetime;
        originPoint = transform.position;

        behaviourState.Clear();
        ricochetState.Clear();

        AppliedStatus = StatusEffectType.None;
        ApplyTint();

        // 【풀에서 재사용될 때 반드시 끈다.】
        // 적 탄으로 쓰인 오브젝트가 플레이어 탄으로 돌아왔을 때
        // 진영 판정이 켜진 채면 플레이어 총알이 적을 못 맞힌다.
        // 켜는 쪽(ConfigureAsEnemyShot)만 두고 끄는 쪽을 잊는 것이
        // 풀링에서 가장 흔한 실패다.
        useFactionGate = false;
        shooterFaction = Faction.Wild;

        explosionRadius = 0f;
        explosionShare = 0f;
    }

    /// <summary>신기전 — 맞으면 반경 안의 다른 적이 몫만큼 피해를 받는다 (결정 2-80).</summary>
    public void SetExplosion(float radius, float share)
    {
        explosionRadius = Mathf.Max(0f, radius);
        explosionShare = Mathf.Clamp01(share);
    }

    public void OnDespawned()
    {
        isConsumed = true;
        hitTargets.Clear();
    }

    /// <summary>
    /// 합성 발사로 이 탄이 부여하는 상태. (확정 기획 — 합성 발사)
    ///
    /// 부여 계열 핵심 젬이 준 상태와 보조 젬이 준 행동이 한 발에 합쳐진다.
    /// 부여 핵심 젬이 2개면 발사마다 번갈아 실린다. (각 50% 빈도)
    /// </summary>
    public StatusEffectType AppliedStatus { get; private set; }

    /// <summary>이 탄의 피해 속성. 실린 상태에서 자동으로 따라온다.</summary>
    public DamageElement Element
    {
        get
        {
            switch (AppliedStatus)
            {
                case StatusEffectType.Ignite: return DamageElement.Fire;
                case StatusEffectType.Poison: return DamageElement.Chaos;
                case StatusEffectType.Freeze:
                case StatusEffectType.Chill: return DamageElement.Cold;
                case StatusEffectType.Shock: return DamageElement.Lightning;
                default: return DamageElement.Physical;
            }
        }
    }

    /// <summary>
    /// 적이 쏘는 탄으로 설정한다. EnemyAttack이 호출한다.
    ///
    /// 플레이어 탄과 같은 BulletController를 쓰는 이유 —
    /// 사거리 보정·방어도·속성 상성이 양쪽에 똑같이 적용되어야
    /// "적 피해가 반감되는 거리에서 교전한다"가 플레이어의 방어 기술이 된다.
    /// (docs/Dokkaebi_Combat_Baseline.md 4절)
    /// </summary>
    public void ConfigureAsEnemyShot(
        int shotDamage, float range, int penetration, StatusEffectType status, Vector3 origin,
        Faction faction = Faction.Wild)
    {
        // 적 탄만 진영으로 거른다. 여기서 켠다.
        shooterFaction = faction;
        useFactionGate = true;

        damage = Mathf.Max(1, shotDamage);
        effectiveRange = Mathf.Max(0f, range);
        armourPenetration = Mathf.Max(0, penetration);

        AppliedStatus = status;
        originPoint = origin;
        ApplyTint();

        // 적 탄은 행동(관통·갈래 등)도 스킬 보정도 치명타도 갖지 않는다.
        skillEffects = null;
        criticalChance = 0f;
        criticalMultiplier = CombatConstants.BaseCriticalMultiplier;
        behaviourState.Clear();
        ricochetState.Clear();

        currentSpeed = speed;
        despawnTime = Time.time + lifetime;
    }

    /// <summary>
    /// 착용 무기가 정한 기본값을 주입한다. PlayerWeapon이 Configure 직전에 호출한다.
    ///
    /// Configure와 나눠 둔 이유 — 분열된 자식 탄은 부모의 무기 기본값을 그대로
    /// 물려받아야 하지만 Skill 보정(잔여 횟수 등)은 다르게 받는다. 축이 둘이다.
    /// </summary>
    public void SetWeaponBase(int shotDamage, float range, int penetration)
    {
        damage = Mathf.Max(1, shotDamage);
        effectiveRange = Mathf.Max(0f, range);
        armourPenetration = Mathf.Clamp(penetration, 0, CombatConstants.MaxArmour);
    }

    /// <summary>
    /// 치명타 정보를 주입한다.
    ///
    /// 【기본 확률은 0이다.】 각인 「정밀」을 껴야만 크리가 뜬다.
    /// 그래야 크리가 운이 아니라 빌드 선택의 결과가 된다.
    /// </summary>
    public void SetCritical(float chance, float multiplier)
    {
        criticalChance = Mathf.Clamp(chance, 0f, CombatConstants.MaxCriticalChance);
        criticalMultiplier = Mathf.Max(1f, multiplier);
    }

    /// <summary>
    /// 스킬 합산 결과를 연결한다. 피해 증가·상태이상 위력·조건부 효과가 여기서 온다.
    ///
    /// 값을 복사하지 않고 참조를 드는 이유 — 탄마다 배열을 복사하면
    /// 매 발사가 GC Alloc이 된다. 소켓은 가방 화면에서만 바뀌므로
    /// 비행 중 바뀌어도 문제가 되지 않는다.
    /// </summary>
    public void SetSkillEffects(WeaponModifiers source)
    {
        skillEffects = source;
    }

    /// <summary>발사 직후 Skill 보정치를 주입한다. PlayerWeapon이 호출한다.</summary>
    public void Configure(
        ProjectileBehaviourState state,
        int ricochetBounces,
        float speedMultiplier,
        float lifetimeMultiplier,
        Vector3 origin,
        StatusEffectType appliedStatus = StatusEffectType.None)
    {
        behaviourState = state;
        ricochetState.Set(ricochetBounces);

        currentSpeed = speed * Mathf.Max(0.1f, speedMultiplier);
        despawnTime = Time.time + lifetime * Mathf.Max(0.1f, lifetimeMultiplier);
        originPoint = origin;

        AppliedStatus = appliedStatus;
        ApplyTint();
    }

    private void Update()
    {
        if (isConsumed)
            return;

        if (Time.time >= despawnTime)
        {
            ReturnToPool();
            return;
        }

        if (isReturning)
            UpdateReturnHoming();

        float step = currentSpeed * Time.deltaTime;

        // 덩어리에 닿으면 튕기거나(튕김이 남았으면) 그 자리에서 멈춘다 (결정 2-89). 탄 하나에 레이 하나.
        if (TryHitWall(step))
            return;

        transform.position += transform.forward * step;
    }

    /// <summary>벽으로 보는 레이어 — 프리팹이 비워 두면 맵의 키 큰 덩어리.</summary>
    private int WallMask => terrainMask.value != 0 ? terrainMask.value : GameLayers.ShotBlockMask;

    /// <summary>
    /// 이번 프레임 이동 구간에 벽(키 큰 덩어리)이 있으면 반사하거나 그 자리에서 멈춘다 (결정 2-89).
    ///
    /// 물리 충돌 콜백이 아니라 전방 레이캐스트를 쓰는 이유:
    /// 빠른 탄이 얇은 벽을 통과(터널링)하는 것을 막고, 지형 법선을 정확히 얻기 위해서다.
    /// 【멈출 때 바로 풀로 돌리지 않는다】 벽 앞까지 옮겨 두고 다음 프레임에 사라진다 —
    /// 벽 바로 앞에 선 적도 이번 물리 단계의 충돌(OnTriggerEnter)로 맞는다.
    /// </summary>
    private bool TryHitWall(float step)
    {
        if (!Physics.Raycast(transform.position, transform.forward, out RaycastHit hit,
                step, WallMask, QueryTriggerInteraction.Ignore))
            return false;

        // 튕김이 없거나 다 썼으면 벽 앞에서 멈추고 다음 프레임에 사라진다.
        if (!ricochetState.HasAny || !ricochetState.TryConsume())
        {
            transform.position = hit.point - transform.forward * ricochetSkin;
            despawnTime = Time.time;
            return true;
        }

        Vector3 reflected = ProjectileRicochetState.Reflect(transform.forward, hit.normal);

        transform.position = hit.point + reflected * ricochetSkin;
        transform.rotation = Quaternion.LookRotation(reflected);

        // 튕길 때마다 상태 부여 판정이 새로 발생한다. (v5 §6-1)
        hitTargets.Clear();

        return true;
    }

    /// <summary>Return 행동 중에는 발사 지점으로 유도된다.</summary>
    private void UpdateReturnHoming()
    {
        Vector3 toOrigin = originPoint - transform.position;
        toOrigin.y = 0f;

        // 발사 지점에 도달하면 소멸한다.
        if (toOrigin.sqrMagnitude < 0.25f)
        {
            ReturnToPool();
            return;
        }

        transform.rotation = Quaternion.LookRotation(toOrigin.normalized);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHit(other);
    }

    /// <summary>
    /// 총구와 발사자 사이 구간을 발사 즉시 판정한다. PlayerWeapon / EnemyAttack이 호출한다.
    ///
    /// 【이것이 없으면 적이 몸에 붙었을 때 총알이 나가도 맞지 않는다.】
    /// 총구(firePoint)는 발사자보다 앞에 있다. 적이 밀착하면 그 지점이 이미
    /// 적 콜라이더 안이고, 총알은 적 몸 안에서 태어나 앞으로 빠져나간다.
    /// OnTriggerEnter는 "밖에서 안으로 들어오는" 순간에만 발생하므로
    /// 이 경우 판정 자체가 일어나지 않는다. 플레이어 눈에는 "총알이 안 나간다"로 보인다.
    ///
    /// 그래서 이동에 의존하지 않고 발사자 중심 → 총구 구간을 캡슐로 직접 훑는다.
    /// OverlapCapsule은 이미 겹쳐 있는 콜라이더도 보고하므로 SphereCast와 달리
    /// "처음부터 안에 있던" 경우를 놓치지 않는다.
    /// </summary>
    public void ResolveMuzzleOverlap(Vector3 shooterCenter)
    {
        if (isConsumed)
            return;

        Vector3 muzzle = transform.position;
        Vector3 forward = transform.forward;

        // 벽에 붙어 쏘면 총구가 벽 안이나 너머에서 태어난다 — 쏜 사람과 총구 사이에 벽이 있으면 나가지 않는다 (결정 2-89).
        if (Physics.Linecast(shooterCenter, muzzle, WallMask, QueryTriggerInteraction.Ignore))
        {
            ReturnToPool();
            return;
        }

        if (muzzleCheckRadius <= 0f)
            return;

        int count = Physics.OverlapCapsuleNonAlloc(
            shooterCenter, muzzle, muzzleCheckRadius, MuzzleOverlapBuffer,
            ~0, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count && !isConsumed; i++)
        {
            Collider other = MuzzleOverlapBuffer[i];

            if (other == null)
                continue;

            // 발사자 뒤에 있는 대상은 맞히지 않는다. 캡슐 반경이 뒤쪽까지 조금 물기 때문이다.
            if (Vector3.Dot(other.bounds.center - shooterCenter, forward) < 0f)
                continue;

            TryHit(other);
        }
    }

    /// <summary>
    /// 한 콜라이더에 대한 명중 판정. 트리거 충돌과 근접 사격 판정이 이 경로를 공유한다.
    /// 두 경로가 갈라지면 한쪽만 고치는 버그가 반드시 생긴다.
    /// </summary>
    private bool TryHit(Collider other)
    {
        if (isConsumed || other == null)
            return false;

        if (!other.TryGetComponent(out Health targetHealth))
            return false;

        if (!CanHit(targetHealth, other.transform))
            return false;

        // 같은 대상을 다시 때리지 않는다. 귀환 중에만 재타격이 허용된다. (v5 §10)
        if (!isReturning && hitTargets.Contains(other.transform))
            return false;

        if (!isReturning)
            hitTargets.Add(other.transform);

        ApplyHit(targetHealth);

        if (explosionRadius > 0f && explosionShare > 0f)
            Explode(targetHealth);

        ResolveBehaviour(other.transform);

        return true;
    }

    /// <summary>
    /// 이 탄이 이 대상을 때릴 수 있는가.
    ///
    /// 플레이어 탄은 예전 그대로 Team만 본다 — 플레이어가 무엇을 쏠지는
    /// 진영이 정하는 것이 아니라 플레이어가 정한다.
    /// 적 탄만 진영을 본다. 그래야 같은 편을 쏘지 않으면서
    /// 다른 소속은 쏠 수 있다.
    /// </summary>
    private bool CanHit(Health targetHealth, Transform target)
    {
        if (!useFactionGate)
            return targetHealth.Team == targetTeam;

        if (targetHealth.Team == Team.Player)
            return FactionTable.IsHostileToPlayer(shooterFaction);

        if (targetHealth.Team != Team.Enemy)
            return false;

        // 소속을 모르는 적은 야생으로 본다. EnemyIdentity가 붙기 전의 프리팹이다.
        Faction otherFaction = target.TryGetComponent(out EnemyIdentity identity)
            ? identity.Faction
            : Faction.Wild;

        return FactionTable.IsHostile(shooterFaction, otherFaction);
    }

    /// <summary>
    /// 피해와 상태를 한 번에 적용한다.
    ///
    /// 순서가 중요하다 — 상태를 먼저 걸고 피해를 준다.
    /// 반대로 하면 이 탄으로 죽는 적에게 상태가 남지 않아
    /// 전령(처치 시 연쇄)과 잔류물이 발동하지 않는다.
    /// </summary>
    /// <summary>
    /// 맞은 자리에서 터진다 — 둘레의 다른 대상이 몫만큼 받는다. 상태는 싣지 않는다(직접 맞은 쪽만).
    /// </summary>
    private void Explode(Health direct)
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, explosionRadius, explosionHits,
            ~0, QueryTriggerInteraction.Collide);

        explosionDone.Clear();

        for (int i = 0; i < count; i++)
        {
            Collider c = explosionHits[i];

            if (c == null || !c.TryGetComponent(out Health h) || h == direct || h.IsDead)
                continue;

            // 콜라이더가 여럿인 몸은 한 번만.
            if (explosionDone.Contains(h) || !CanHit(h, c.transform))
                continue;

            explosionDone.Add(h);
            ApplyHit(h, explosionShare, applyStatus: false);
        }
    }

    private void ApplyHit(Health target) => ApplyHit(target, 1f, applyStatus: true);

    private void ApplyHit(Health target, float scale, bool applyStatus)
    {
        float distance = Vector3.Distance(originPoint, transform.position);

        // 「증가%」는 전부 가산이다. 조건부는 지금 이 명중에만 적용된다.
        float increased = 0f;

        if (skillEffects != null)
        {
            increased = skillEffects.DamageIncrease
                + skillEffects.ConditionalDamageIncrease(distance, effectiveRange, target.Status);
        }

        if (applyStatus && AppliedStatus != StatusEffectType.None)
        {
            // 상태이상 위력은 직접 피해와 분리된 축이다.
            // 「연소」 각인과 속성 보조 젬이 여기만 키운다.
            float ailmentBase = damage * Mathf.Max(0f, 1f + (skillEffects?.AilmentPower ?? 0f));

            target.ApplyStatus(
                AppliedStatus, ailmentBase, skillEffects?.AilmentDurationMultiplier ?? 1f);
        }

        // 치명타는 명중마다 따로 굴린다. 발사 시점에 굴리면
        // 관통·분열로 여러 대상을 때릴 때 전부 같은 결과가 나온다.
        bool isCritical = criticalChance > 0f && Random.value < criticalChance;

        var request = new DamageRequest
        {
            source = useFactionGate ? DamageSource.Enemy : DamageSource.Player,
            baseDamage = Mathf.Max(1, Mathf.RoundToInt(damage * scale)),
            increasedPercent = increased,
            element = Element,
            hitKind = HitKind.Ranged,
            armourPenetration = armourPenetration,
            distance = distance,
            effectiveRange = effectiveRange,
            isCritical = isCritical,
            criticalMultiplier = criticalMultiplier,
            bypassArmour = false
        };

        // 난이도 배율은 【플레이어가 맞을 때】에만 곱한다.
        // 플레이어 탄에 곱하면 "쉬운 난이도에서 내가 더 세진다"가 되어
        // 난이도가 적을 약하게 만드는 축이 아니라 나를 강하게 만드는 축이 된다.
        //
        // 【targetTeam이 아니라 맞은 쪽을 본다.】
        // 적 탄이 다른 적을 맞힐 수 있게 된 뒤로는 targetTeam이
        // "누가 맞았는가"를 더 이상 말해 주지 않는다. 그대로 뒀다면
        // 적끼리의 싸움에까지 난이도 배율이 곱해졌을 것이다.
        float difficulty = target.Team == Team.Player ? GameManager.EnemyDamageMultiplier : 1f;

        target.TakeDamage(request, difficulty);
    }

    /// <summary>충돌 1회당 행동 하나만 해결한다. 이 배타성이 조합 설계의 핵심이다.</summary>
    private void ResolveBehaviour(Transform hitTarget)
    {
        ProjectileCollisionResult result = ProjectileCollisionResolver.Resolve(
            ref behaviourState, isReturning, splitAngle, forkAngle);

        if (result.ChildCount > 0)
        {
            SpawnChildren(result.ChildCount, result.SpreadAngle);
            ReturnToPool();
            return;
        }

        if (result.BeginReturn)
        {
            isReturning = true;
            hitTargets.Clear();
            return;
        }

        if (result.SeekNextTarget && !SeekNextChainTarget(hitTarget))
        {
            // 재유도할 대상이 없으면 소멸한다.
            ReturnToPool();
            return;
        }

        if (!result.KeepAlive)
            ReturnToPool();
    }

    /// <summary>자식을 좌우 대칭으로 생성한다. 자식은 남은 행동을 물려받는다.</summary>
    private void SpawnChildren(int count, float spreadAngle)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = ProjectileCollisionResolver.GetChildAngle(i, count, spreadAngle);

            SpawnChild(angle);
        }
    }

    private void SpawnChild(float angleOffset)
    {
        if (pooledObject == null || pooledObject.SourcePrefab == null)
            return;

        if (poolManager == null)
            poolManager = PoolManager.EnsureInstance();

        Quaternion rotation = transform.rotation * Quaternion.Euler(0f, angleOffset, 0f);

        GameObject child = poolManager.Spawn(pooledObject.SourcePrefab, transform.position, rotation);

        if (child == null)
            return;

        if (child.TryGetComponent(out BulletController bullet))
        {
            // 무기 기본값을 먼저 물려준다. 이것이 없으면 분열된 탄만
            // 프리팹 기본 피해(10)로 때려 티어 6 무기가 갈래마다 약해진다.
            bullet.SetWeaponBase(damage, effectiveRange, armourPenetration);
            bullet.SetSkillEffects(skillEffects);
            bullet.SetCritical(criticalChance, criticalMultiplier);

            bullet.Configure(
                behaviourState.CreateChildState(),
                ricochetState.Remaining,
                currentSpeed / Mathf.Max(0.0001f, speed),
                Mathf.Max(0.1f, (despawnTime - Time.time) / Mathf.Max(0.0001f, lifetime)),
                originPoint,
                AppliedStatus);
        }
    }

    /// <summary>아직 때리지 않은 가장 가까운 적으로 방향을 튼다. 대상이 없으면 false.</summary>
    private bool SeekNextChainTarget(Transform current)
    {
        EnemyManager manager = EnemyManager.EnsureInstance();

        IReadOnlyList<EnemyController> enemies = manager.ActiveEnemies;

        ChainPositions.Clear();
        ChainExcluded.Clear();
        ChainTransforms.Clear();

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyController enemy = enemies[i];

            if (enemy == null)
                continue;

            Transform candidate = enemy.transform;

            ChainTransforms.Add(candidate);
            ChainPositions.Add(candidate.position);

            // v5 §10: 같은 시퀀스에서 동일 적 재타격 불가
            ChainExcluded.Add(candidate == current || hitTargets.Contains(candidate));
        }

        int index = ChainTargetSelector.SelectNearestIndex(
            transform.position, chainRadius, ChainPositions, ChainExcluded);

        if (index < 0)
            return false;

        Vector3 direction = ChainTransforms[index].position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.000001f)
            return false;

        transform.rotation = Quaternion.LookRotation(direction.normalized);

        return true;
    }

    private void ReturnToPool()
    {
        if (isConsumed)
            return;

        isConsumed = true;

        if (pooledObject != null && pooledObject.Despawn())
            return;

        Destroy(gameObject);
    }
}
