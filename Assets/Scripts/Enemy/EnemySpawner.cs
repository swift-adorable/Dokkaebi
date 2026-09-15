using UnityEngine;

/// <summary>
/// 플레이어 주변에 적을 주기적으로 생성한다. 생성은 오브젝트 풀을 경유한다.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform player;

    [Header("Spawn")]
    [Tooltip("스폰 간격(초)")]
    [SerializeField] private float spawnInterval = 2f;

    [Tooltip("플레이어로부터의 스폰 거리")]
    [SerializeField] private float spawnRadius = 10f;

    [Tooltip("체크 시 정확히 spawnRadius 거리(원주)에만 스폰. 해제 시 원 내부에 분산 스폰.")]
    [SerializeField] private bool spawnOnRingOnly = true;

    [Header("Pooling")]
    [Tooltip("시작 시 미리 생성해둘 적 개수. 첫 스폰의 프레임 스파이크를 없앤다.")]
    [SerializeField] private int prewarmCount = 16;

    private GameManager gameManager;
    private PoolManager poolManager;
    private float nextSpawnTime;

    private void Start()
    {
        gameManager = GameManager.Instance;
        poolManager = PoolManager.EnsureInstance();

        if (enemyPrefab == null)
            GameLogger.Error("[EnemySpawner] enemyPrefab이 할당되지 않았습니다.", this);

        if (player == null)
            GameLogger.Error("[EnemySpawner] player 참조가 할당되지 않았습니다.", this);

        if (enemyPrefab != null)
            poolManager.Prewarm(enemyPrefab, prewarmCount);
    }

    private void Update()
    {
        if (gameManager == null || !gameManager.IsPlaying)
            return;

        if (enemyPrefab == null || player == null)
            return;

        if (Time.time < nextSpawnTime)
            return;

        SpawnEnemy();

        nextSpawnTime = Time.time + spawnInterval;
    }

    private void SpawnEnemy()
    {
        Vector2 randomCircle = Random.insideUnitCircle;

        if (spawnOnRingOnly)
            randomCircle = randomCircle.normalized;

        randomCircle *= spawnRadius;

        Vector3 spawnPosition = player.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

        poolManager.Spawn(enemyPrefab, spawnPosition, Quaternion.identity);
    }
}
