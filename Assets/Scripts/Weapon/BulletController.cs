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
    [SerializeField] private int damage = 1;

    [Tooltip("자동 소멸까지의 시간(초)")]
    [SerializeField] private float lifetime = 3f;

    [Tooltip("이 소속의 대상에게만 피해를 준다.")]
    [SerializeField] private Team targetTeam = Team.Enemy;

    [Header("Behaviour Tuning")]
    [Tooltip("분열(Split) 시 좌우 최대 각도(도). 3갈래가 -각도 / 0 / +각도로 퍼진다.")]
    [SerializeField] private float splitAngle = 60f;

    [Tooltip("Fork 분열 시 원 궤도 기준 좌우 각도(도). PoE2 기준 60도.")]
    [SerializeField] private float forkAngle = 60f;

    [Tooltip("Chain이 다음 대상을 찾는 반경(m)")]
    [SerializeField] private float chainRadius = 6f;

    [Header("Ricochet (튕겨 쏘기)")]
    [Tooltip("튕김 판정 대상 레이어. 지형/장애물 레이어를 지정한다.")]
    [SerializeField] private LayerMask terrainMask = 0;

    [Tooltip("튕긴 직후 벽에 다시 박히지 않도록 법선 방향으로 밀어내는 거리(m)")]
    [SerializeField] private float ricochetSkin = 0.05f;

    // 모든 투사체가 공유하는 재사용 버퍼. 체인 판정마다 새 리스트를 만들지 않는다.
    private static readonly List<Vector3> ChainPositions = new(64);
    private static readonly List<bool> ChainExcluded = new(64);
    private static readonly List<Transform> ChainTransforms = new(64);

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

    private void Awake()
    {
        pooledObject = GetComponent<PooledObject>();
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
    }

    public void OnDespawned()
    {
        isConsumed = true;
        hitTargets.Clear();
    }

    /// <summary>
    /// 합성 발사로 이 탄이 부여하는 적재 속성. (확정 기획 — 합성 발사)
    ///
    /// 전달 계열 Core가 준 행동과 적재 계열 Core가 준 상태가 한 발에 합쳐진다.
    /// ※ 실제 상태 부여는 상태이상 시스템과 함께 5-D에서 구현한다. 현재는 운반만 한다.
    /// </summary>
    public StatusEffectType AppliedStatus { get; private set; }

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

        // 튕김 잔여가 없으면 레이캐스트 자체를 하지 않는다. 대부분의 탄은 여기서 비용이 0이다.
        if (ricochetState.HasAny && TryRicochet(step))
            return;

        transform.position += transform.forward * step;
    }

    /// <summary>
    /// 이번 프레임 이동 구간에 지형이 있으면 반사한다.
    ///
    /// 물리 충돌 콜백이 아니라 전방 레이캐스트를 쓰는 이유:
    /// 빠른 탄이 얇은 벽을 통과(터널링)하는 것을 막고, 지형 법선을 정확히 얻기 위해서다.
    /// </summary>
    private bool TryRicochet(float step)
    {
        if (terrainMask.value == 0)
            return false;

        if (!Physics.Raycast(transform.position, transform.forward, out RaycastHit hit,
                step, terrainMask, QueryTriggerInteraction.Ignore))
            return false;

        // 튕김 횟수를 다 썼으면 벽에서 소멸한다.
        if (!ricochetState.TryConsume())
        {
            ReturnToPool();
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
        if (isConsumed)
            return;

        if (!other.TryGetComponent(out Health targetHealth))
            return;

        if (targetHealth.Team != targetTeam)
            return;

        // 같은 대상을 다시 때리지 않는다. 귀환 중에만 재타격이 허용된다. (v5 §10)
        if (!isReturning && hitTargets.Contains(other.transform))
            return;

        if (!isReturning)
            hitTargets.Add(other.transform);

        targetHealth.TakeDamage(damage);

        ResolveBehaviour(other.transform);
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
