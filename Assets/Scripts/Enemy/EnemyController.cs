using UnityEngine;

/// <summary>
/// 적 개체의 생명주기 조율.
///
/// 체력 계산은 Health가, 이동은 EnemyMovement가, 공격은 EnemyAttack이 담당한다.
/// 이 클래스는 사망 시 시체 생성과 풀 반납, 그리고 EnemyManager 등록만 맡는다.
/// </summary>
[RequireComponent(typeof(Health))]
public class EnemyController : MonoBehaviour, IPoolable
{
    [Header("Death")]
    [SerializeField] private GameObject corpsePrefab;

    private Health health;
    private EnemyAttack attack;
    private EnemyBrain brain;
    private PooledObject pooledObject;
    private PoolManager poolManager;
    private EnemyManager enemyManager;

    private bool isReturning;

    public Health Health => health;

    private void Awake()
    {
        health = GetComponent<Health>();
        attack = GetComponent<EnemyAttack>();
        brain = GetComponent<EnemyBrain>();
        pooledObject = GetComponent<PooledObject>();
    }

    private void OnEnable()
    {
        health.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        health.OnDied -= HandleDied;
    }

    private void Start()
    {
        poolManager = PoolManager.EnsureInstance();
        enemyManager = EnemyManager.EnsureInstance();

        if (corpsePrefab == null)
            GameLogger.Error("[EnemyController] corpsePrefab이 할당되지 않았습니다.", this);
    }

    public void OnSpawned()
    {
        isReturning = false;

        if (attack != null)
            attack.ResetState();

        // 두뇌도 초기화한다. 재장전 중이던 적이 그 상태로 재사용되면
        // 스폰 직후 이유 없이 뒷걸음질한다.
        if (brain != null)
            brain.ResetState();

        // Health.OnSpawned는 IPoolable 통지로 별도 호출되므로 여기서 중복 처리하지 않는다.
        EnemyManager.EnsureInstance().Register(this);
    }

    public void OnDespawned()
    {
        if (enemyManager != null)
            enemyManager.Unregister(this);
        else
            EnemyManager.EnsureInstance().Unregister(this);
    }

    /// <summary>외부(EnemyManager 등)에서 즉시 반납시킬 때 호출한다.</summary>
    public void ReturnToPool()
    {
        if (isReturning)
            return;

        isReturning = true;

        if (pooledObject != null && pooledObject.Despawn())
            return;

        Destroy(gameObject);
    }

    private void HandleDied()
    {
        GameLogger.Log("[EnemyController] Die");

        SpawnCorpse();

        ReturnToPool();
    }

    private void SpawnCorpse()
    {
        if (corpsePrefab == null)
            return;

        if (poolManager == null)
            poolManager = PoolManager.EnsureInstance();

        poolManager.Spawn(corpsePrefab, transform.position, Quaternion.identity);
    }
}
