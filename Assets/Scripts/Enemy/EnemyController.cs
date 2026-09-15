using UnityEngine;

/// <summary>
/// 적 개체의 체력/사망 처리.
/// 오브젝트 풀에서 재사용되므로 체력 초기화를 OnSpawned에서 수행한다.
///
/// 주의: 이동/추격 AI는 아직 미구현이다. (로드맵 4단계 예정)
/// </summary>
public class EnemyController : MonoBehaviour, IPoolable
{
    [Header("Stats")]
    [SerializeField] private int maxHP = 3;

    [Header("Death")]
    [SerializeField] private GameObject corpsePrefab;

    private PooledObject pooledObject;
    private PoolManager poolManager;
    private int currentHP;
    private bool isDead;

    public int CurrentHP => currentHP;

    private void Awake()
    {
        pooledObject = GetComponent<PooledObject>();
        currentHP = maxHP;
    }

    private void Start()
    {
        poolManager = PoolManager.EnsureInstance();

        if (corpsePrefab == null)
            GameLogger.Error("[EnemyController] corpsePrefab이 할당되지 않았습니다.", this);
    }

    public void OnSpawned()
    {
        // 체력을 되돌리지 않으면 이전에 죽은 상태 그대로 재사용되어 즉사한다.
        currentHP = maxHP;
        isDead = false;
    }

    public void OnDespawned()
    {
        isDead = true;
    }

    public void TakeDamage(int damage)
    {
        if (isDead || damage <= 0)
            return;

        currentHP -= damage;

        GameLogger.Log($"[EnemyController] HP {currentHP}/{maxHP}");

        if (currentHP <= 0)
            Die();
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

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

    private void ReturnToPool()
    {
        if (pooledObject != null && pooledObject.Despawn())
            return;

        Destroy(gameObject);
    }
}
