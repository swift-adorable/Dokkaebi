using UnityEngine;

/// <summary>
/// 플레이어 주변에 적을 주기적으로 생성한다. 생성은 오브젝트 풀을 경유한다.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private GameObject enemyPrefab;

    [Tooltip("원거리 적 프리팹. 비워 두면 근접만 나온다. "
             + "「Blob/Enemy/원거리 적 프리팹 생성」으로 만든다.")]
    [SerializeField] private GameObject rangedEnemyPrefab;

    [Tooltip("원거리 적이 나올 비율(0~1). 근접만 나오면 전투가 "
             + "「붙는다 / 뺀다」 두 동작으로 끝난다.")]
    [Range(0f, 1f)]
    [SerializeField] private float rangedRatio = 0.35f;

    [Tooltip("비워두면 씬에서 PlayerMovement를 가진 오브젝트를 자동으로 찾는다.")]
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
    private EnemyManager enemyManager;
    private float nextSpawnTime;

    private void Start()
    {
        gameManager = GameManager.Instance;
        poolManager = PoolManager.EnsureInstance();
        enemyManager = EnemyManager.EnsureInstance();

        if (player == null)
        {
            // 씬 참조를 수동으로 연결하지 않아도 동작하도록 자동 탐색한다.
            var playerMovement = FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Exclude);

            if (playerMovement != null)
            {
                player = playerMovement.transform;
                GameLogger.Log("[EnemySpawner] player 자동 연결");
            }
        }

        if (player != null)
            enemyManager.SetPlayer(player);

        if (enemyPrefab == null)
            GameLogger.Error("[EnemySpawner] enemyPrefab이 할당되지 않았습니다.", this);

        if (player == null)
            GameLogger.Error("[EnemySpawner] player 참조가 할당되지 않았습니다.", this);

        if (enemyPrefab != null)
            poolManager.Prewarm(enemyPrefab, prewarmCount);

        // 원거리 풀도 미리 데운다. 첫 원거리 적에서 프레임이 튀지 않게.
        if (rangedEnemyPrefab != null)
            poolManager.Prewarm(rangedEnemyPrefab, Mathf.Max(4, prewarmCount / 2));
    }

    private void Update()
    {
        if (gameManager == null || !gameManager.IsPlaying)
            return;

        if (enemyPrefab == null || player == null)
            return;

        if (Time.time < nextSpawnTime)
            return;

        // 동시 생존 수 상한을 넘으면 스폰을 건너뛴다. 모바일 프레임 방어선이다.
        if (!enemyManager.CanSpawn)
        {
            nextSpawnTime = Time.time + spawnInterval;
            return;
        }

        SpawnEnemy();

        nextSpawnTime = Time.time + spawnInterval;
    }

    /// <summary>
    /// 이번에 무엇을 낼지 고른다.
    ///
    /// 섞는 이유 — 근접만 나오면 플레이어가 배우는 것은 「거리 유지」 하나뿐이다.
    /// 원거리가 섞여야 「어느 쪽을 먼저 처리할까」가 생기고, 대시가
    /// 회피 기술로도 접근 기술로도 쓰인다. (Combat_Baseline 5절)
    /// </summary>
    private GameObject PickPrefab()
    {
        if (rangedEnemyPrefab == null || rangedRatio <= 0f)
            return enemyPrefab;

        return Random.value < rangedRatio ? rangedEnemyPrefab : enemyPrefab;
    }

    private void SpawnEnemy()
    {
        Vector2 randomCircle = Random.insideUnitCircle;

        if (spawnOnRingOnly)
            randomCircle = randomCircle.normalized;

        randomCircle *= spawnRadius;

        Vector3 spawnPosition = player.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

        poolManager.Spawn(PickPrefab(), spawnPosition, Quaternion.identity);
    }
}
