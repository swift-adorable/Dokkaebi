using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 투사체. 충돌 시 Mutation이 부여한 행동을 우선순위에 따라 하나만 해결한다.
///
/// 행동 판정 순서: Split → Pierce → Fork → Chain → Return
/// 남은 행동이 없으면 소멸한다.
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
    [Tooltip("Fork로 분열할 때 원 궤도 기준 좌우 각도(도)")]
    [SerializeField] private float forkAngle = 60f;

    [Tooltip("Chain이 다음 대상을 찾는 반경(m)")]
    [SerializeField] private float chainRadius = 6f;

    private PooledObject pooledObject;
    private PoolManager poolManager;

    private readonly List<Transform> hitTargets = new();

    private ProjectileBehaviourState behaviourState;
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
    }

    public void OnDespawned()
    {
        isConsumed = true;
        hitTargets.Clear();
    }

    /// <summary>
    /// 발사 직후 Mutation 보정치를 주입한다. PlayerWeapon이 호출한다.
    /// </summary>
    public void Configure(
        ProjectileBehaviourState state,
        float speedMultiplier,
        float lifetimeMultiplier,
        Vector3 origin)
    {
        behaviourState = state;
        currentSpeed = speed * Mathf.Max(0.1f, speedMultiplier);
        despawnTime = Time.time + lifetime * Mathf.Max(0.1f, lifetimeMultiplier);
        originPoint = origin;
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

        transform.position += transform.forward * (currentSpeed * Time.deltaTime);
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

        // 같은 대상을 다시 때리지 않는다. Return만 예외적으로 재타격을 허용한다.
        if (!isReturning && hitTargets.Contains(other.transform))
            return;

        hitTargets.Add(other.transform);

        targetHealth.TakeDamage(damage);

        ResolveBehaviour(other.transform);
    }

    /// <summary>충돌 1회당 행동 하나만 해결한다. 이 배타성이 조합 설계의 핵심이다.</summary>
    private void ResolveBehaviour(Transform hitTarget)
    {
        ProjectileBehaviourType behaviour = behaviourState.ConsumeNext();

        switch (behaviour)
        {
            case ProjectileBehaviourType.Split:
            case ProjectileBehaviourType.Fork:
                ExecuteFork();
                break;

            case ProjectileBehaviourType.Pierce:
                // 궤도를 유지한 채 계속 나아간다. 아무것도 하지 않는 것이 곧 관통이다.
                break;

            case ProjectileBehaviourType.Chain:
                if (!ExecuteChain(hitTarget))
                    ReturnToPool();
                break;

            case ProjectileBehaviourType.Return:
                isReturning = true;
                hitTargets.Clear();
                break;

            default:
                ReturnToPool();
                break;
        }
    }

    /// <summary>좌우로 분열한다. 자식은 남은 행동을 물려받고, 자신은 소멸한다.</summary>
    private void ExecuteFork()
    {
        SpawnChild(forkAngle);
        SpawnChild(-forkAngle);

        ReturnToPool();
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
                currentSpeed / Mathf.Max(0.0001f, speed),
                Mathf.Max(0.1f, (despawnTime - Time.time) / Mathf.Max(0.0001f, lifetime)),
                originPoint);
        }
    }

    /// <summary>아직 때리지 않은 가장 가까운 적으로 방향을 튼다. 대상이 없으면 false.</summary>
    private bool ExecuteChain(Transform current)
    {
        EnemyManager manager = EnemyManager.EnsureInstance();

        var enemies = manager.ActiveEnemies;

        float sqrRadius = chainRadius * chainRadius;
        float bestSqrDistance = float.MaxValue;
        Transform best = null;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyController enemy = enemies[i];

            if (enemy == null)
                continue;

            Transform candidate = enemy.transform;

            if (candidate == current || hitTargets.Contains(candidate))
                continue;

            Vector3 offset = candidate.position - transform.position;
            offset.y = 0f;

            float sqrDistance = offset.sqrMagnitude;

            if (sqrDistance > sqrRadius || sqrDistance >= bestSqrDistance)
                continue;

            bestSqrDistance = sqrDistance;
            best = candidate;
        }

        if (best == null)
            return false;

        Vector3 direction = best.position - transform.position;
        direction.y = 0f;

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
