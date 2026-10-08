using UnityEngine;

/// <summary>
/// 플레이어 주변에 적을 주기적으로 생성한다. 생성은 오브젝트 풀을 경유한다.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private GameObject enemyPrefab;

    [Tooltip("원거리 적 프리팹. 비워 두면 근접만 나온다. "
             + "「Dokkaebi/Enemy/원거리 적 프리팹 생성」으로 만든다.")]
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

    [Header("Archetypes")]
    [Tooltip("켜면 유형 9종 카탈로그에서 골라 낸다. 끄면 근접/원거리 둘만 나온다.")]
    [SerializeField] private bool useArchetypes = true;

    private GameManager gameManager;
    private PoolManager poolManager;
    private EnemyManager enemyManager;
    private float nextSpawnTime;

    /// <summary>유형 9종 프리팹. 비어 있으면 예전처럼 둘만 낸다.</summary>
    private EnemyPrefabCatalog catalog;

    /// <summary>이번 판의 조건. 무리 크기와 퍼짐이 여기서 온다.</summary>
    private RaidConditions Conditions => RaidManager.Current;

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
        {
            enemyManager.SetPlayer(player);

            // 【임시】 열매 나무 2~3그루 — 산열매는 몬스터가 아니라 나무에서 딴다 (결정 2-76). 맵(9단계) 때 제자리로.
            BerryTree.SpawnForRaid(player.position);

            // 【임시】 철수 지점 2곳 — 25~35m · 원 안에서 5초 버티면 철수 (결정 2-78). 맵(9단계) 때 제자리로.
            if (!SceneFlow.InBunker)
            {
                ExtractionDirector.SpawnForRaid(player.position);

                // 쓰러진 자리 (결정 2-93) — 이 장에 남아 있으면 이 판에 한 번 세운다.
                string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (ZoneMapTable.IsMapScene(scene))
                    FallenStash.SpawnForRaid(ZoneMapTable.ChapterOfScene(scene));
            }
        }

        if (enemyPrefab == null)
            GameLogger.Error("[EnemySpawner] enemyPrefab이 할당되지 않았습니다.", this);

        if (player == null)
            GameLogger.Error("[EnemySpawner] player 참조가 할당되지 않았습니다.", this);

        if (enemyPrefab != null)
            poolManager.Prewarm(enemyPrefab, prewarmCount);

        // 원거리 풀도 미리 데운다. 첫 원거리 적에서 프레임이 튀지 않게.
        if (rangedEnemyPrefab != null)
            poolManager.Prewarm(rangedEnemyPrefab, Mathf.Max(4, prewarmCount / 2));

        if (useArchetypes)
            LoadArchetypes();
    }

    /// <summary>
    /// 유형 9종을 불러 온다. 카탈로그가 없거나 비었으면 예전처럼 둘만 낸다 —
    /// 카탈로그를 안 만들었다고 적이 하나도 안 나오면 안 된다.
    /// </summary>
    private void LoadArchetypes()
    {
        catalog = EnemyPrefabCatalog.Load();

        if (catalog == null || catalog.FilledCount == 0)
        {
            GameLogger.Warning("[EnemySpawner] 유형 카탈로그가 없습니다. "
                               + "「Dokkaebi/Enemy/유형 프리팹 카탈로그 생성」을 실행하십시오. "
                               + "지금은 근접·원거리 둘만 나옵니다.");

            catalog = null;
            return;
        }

        // 조건을 여기서 한 번 굴려 둔다. 첫 적이 스폰될 때 굴리면
        // 그 프레임에 로그와 계산이 몰린다.
        RaidManager.EnsureInstance();

        // 유형마다 풀을 조금씩 데운다. 아홉 종을 prewarmCount씩 데우면
        // 시작할 때 프레임이 통째로 날아간다.
        int each = Mathf.Max(2, prewarmCount / EnemyArchetypeTable.Count);

        for (int i = 0; i < EnemyArchetypeTable.Count; i++)
        {
            GameObject prefab = catalog.Get((EnemyArchetype)i);

            if (prefab != null)
                poolManager.Prewarm(prefab, each);
        }
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
    /// <summary>이번 구역이 속한 장. 모르면 0장.</summary>
    private static int CurrentChapter()
    {
        ChapterData c = ZoneDataTable.ChapterOfZone(StoryManager.TargetZone);
        return c != null ? c.Chapter : 0;
    }

    private GameObject PickPrefab(out EnemyArchetype archetype)
    {
        archetype = EnemyArchetype.Scav;

        // 카탈로그가 있으면 이번 구역이 속한 장의 일반 적 풀에서 고른다 (Zone Data · 결정 2-57).
        // 예전에는 9종에서 고르게 뽑았다 — 1장에 무주귀가 나왔다.
        if (catalog != null)
        {
            // 달 · 날씨가 비중을 바꾼다 — 삭 무주귀 · 보름 물가 수귀 · 독안개 왕지네 (결정 2-62 · 2-64).
            RaidConditions conditions = Conditions;
            bool waterside = ZoneDataTable.IsWaterside(StoryManager.TargetZone);
            archetype = ZoneDataTable.Pick(CurrentChapter(), Random.value, a => conditions.SpawnWeight(a, waterside));

            GameObject picked = catalog.Get(archetype);

            if (picked != null)
                return picked;
        }

        if (rangedEnemyPrefab == null || rangedRatio <= 0f)
            return enemyPrefab;

        return Random.value < rangedRatio ? rangedEnemyPrefab : enemyPrefab;
    }

    /// <summary>
    /// 한 번에 몇을 낼지. 【밀집·산개 특성이 여기서 보인다.】
    ///
    /// 표의 배율이 1.6이면 「가끔 둘이 같이 나온다」로 읽힌다 —
    /// 1.6을 그대로 반올림하면 늘 둘이라 「밀집」이 상시가 되어 버린다.
    /// 소수부를 확률로 쓴다.
    /// </summary>
    private int RollGroupSize()
    {
        float scale = Mathf.Max(0.1f, Conditions.GroupSizeScale);

        int whole = Mathf.FloorToInt(scale);

        if (Random.value < scale - whole)
            whole++;

        return Mathf.Max(1, whole);
    }

    private void SpawnEnemy()
    {
        // 무리 크기 = 장의 무리 크기(Zone Data · 결정 2-69) × 이번 판 조건의 배율(밀집 · 산개).
        // 첫 마리를 먼저 골라야 4장 잡귀 떼를 안다.
        GameObject firstPrefab = PickPrefab(out EnemyArchetype first);
        int count = catalog != null
            ? Mathf.Max(1, Mathf.RoundToInt(ZoneDataTable.PackSize(CurrentChapter(), first, Random.value)
                                             * Mathf.Max(0.1f, Conditions.GroupSizeScale)))
            : RollGroupSize();

        // 퍼짐 배율 — 산개는 넓게, 밀집은 좁게 나온다.
        float spread = spawnRadius * Mathf.Max(0.1f, Conditions.SpreadScale);

        Vector3 origin = PickSpawnPoint(spread);

        for (int i = 0; i < count; i++)
        {
            if (!enemyManager.CanSpawn)
                return;

            // 무리는 한 자리에 겹쳐 나오지 않는다. 첫 마리 주변에 흩는다.
            Vector3 offset = i == 0
                ? Vector3.zero
                : RandomFlat(Random.insideUnitCircle * GroupSpacing);

            poolManager.Spawn(i == 0 ? firstPrefab : PickPrefab(out _), origin + offset, Quaternion.identity);
        }
    }

    /// <summary>무리가 흩어지는 반경(m). 겹쳐 나오면 한 마리처럼 보인다.</summary>
    private const float GroupSpacing = 2.5f;

    /// <summary>처음 쓸 때 만든다 — 필드 초기화 중에는 NavMeshPath를 만들 수 없다.</summary>
    private UnityEngine.AI.NavMeshPath spawnPath;

    /// <summary>
    /// 낳을 자리. 장 맵에서는 덩어리 안 · 닫힌 구역(금줄 너머) · 바닥 밖에 나오지 않게 몇 번 다시 고른다 (결정 2-86).
    /// 걸어서 플레이어에게 닿는 자리만 쓰고(덩어리 안쪽에 갇힌 칸 · 지붕 위 제외), 되도록 덩어리 뒤
    /// (플레이어 눈에 안 보이는 곳)를 고른다 (결정 2-89). NavMesh가 없는 씬은 예전 그대로.
    /// </summary>
    private Vector3 PickSpawnPoint(float radius)
    {
        Vector3 candidate = player.position;
        Vector3? reachable = null;
        Vector3 eye = player.position + Vector3.up;

        for (int attempt = 0; attempt < 16; attempt++)
        {
            Vector2 circle = Random.insideUnitCircle;

            if (spawnOnRingOnly)
                circle = circle.normalized;

            candidate = player.position + RandomFlat(circle * radius);

            if (!ZoneMap.CanSpawnAt(candidate) || !EnemyPathing.IsReachable(candidate, player.position, spawnPath ??= new UnityEngine.AI.NavMeshPath()))
                continue;

            bool hidden = Physics.Linecast(eye, candidate + Vector3.up, GameLayers.ShotBlockMask,
                QueryTriggerInteraction.Ignore);

            if (hidden)
                return candidate;

            reachable ??= candidate;
        }

        return reachable ?? candidate;
    }

    private static Vector3 RandomFlat(Vector2 circle)
    {
        return new Vector3(circle.x, 0f, circle.y);
    }
}
