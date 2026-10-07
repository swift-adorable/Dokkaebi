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

    /// <summary>난이도 배율을 곱하기 전의 최대 체력. 재사용 시 누적을 막는다.</summary>
    private int baseMaxHealth;
    private EnemyAttack attack;
    private EnemyBrain brain;
    private EnemyIdentity identity;
    private PooledObject pooledObject;
    private PoolManager poolManager;
    private EnemyManager enemyManager;

    private bool isReturning;

    public Health Health => health;

    /// <summary>
    /// 적이 죽었다 — 시체를 만들고 풀로 돌아가기 【전에】 부른다. 상태이상이 아직 남아 있다.
    /// 기폭 · 잔류물(SkillZoneDirector)이 듣는다 (Audit A5 · A6).
    /// </summary>
    public static event System.Action<EnemyController> Killed;

    private void Awake()
    {
        health = GetComponent<Health>();
        attack = GetComponent<EnemyAttack>();
        brain = GetComponent<EnemyBrain>();
        identity = GetComponent<EnemyIdentity>();
        pooledObject = GetComponent<PooledObject>();

        // 【임시 표시】 머리 위 상태 글자 (결정 2-77).
        if (!TryGetComponent(out EnemyStatusTag _))
            gameObject.AddComponent<EnemyStatusTag>();
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

        // 난이도에 따라 최대 체력을 조정한다. 【스폰 시점에 한 번만】 건다.
        // 장(Stage)이 아니라 난이도만 적 수치에 배율을 곱한다 —
        // 구역이 올라가면 바뀌는 것은 구성비와 무장 티어다.
        ApplyDifficultyHealth();

        // Health.OnSpawned는 IPoolable 통지로 별도 호출되므로 여기서 중복 처리하지 않는다.
        EnemyManager.EnsureInstance().Register(this);
    }

    /// <summary>
    /// 난이도 배율을 최대 체력에 적용한다.
    ///
    /// 원본 최대 체력을 따로 기억하는 이유 — 풀에서 재사용될 때마다
    /// 배율을 다시 곱하면 체력이 계속 줄어든다.
    /// </summary>
    private void ApplyDifficultyHealth()
    {
        if (health == null)
            return;

        // 【EnemyIdentity가 있으면 여기서 체력을 만지지 않는다.】
        // 유형·등급이 정한 절대값에 난이도 배율까지 EnemyIdentity가 함께 건다.
        // 두 곳이 같은 값을 쓰면 IPoolable 통지 순서에 따라 결과가 달라진다 —
        // 순서에 기대는 코드는 언젠가 반드시 틀린다.
        if (identity != null)
            return;

        if (baseMaxHealth <= 0)
            baseMaxHealth = health.Max;

        int scaled = Mathf.Max(1,
            Mathf.RoundToInt(baseMaxHealth * GameManager.EnemyHealthMultiplier));

        if (health.Max != scaled)
            health.SetMaxHealth(scaled, refill: true);
    }

    public void OnDespawned()
    {
        // 【씬을 닫는 중이면 새로 만들지 않는다.】 풀이 치워지면서 반납이 불리는데,
        // 그때 EnemyManager는 이미 사라졌다. EnsureInstance로 새로 만들면 닫히는 씬에
        // 「EnemyManager (Runtime)」이 남는다 — 벙커로 철수할 때 실제로 났다.
        if (enemyManager != null)
            enemyManager.Unregister(this);
        else if (EnemyManager.HasInstance)
            EnemyManager.Instance.Unregister(this);
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

        Killed?.Invoke(this);

        SpawnCorpse();

        ReturnToPool();
    }

    private void SpawnCorpse()
    {
        if (corpsePrefab == null)
            return;

        if (poolManager == null)
            poolManager = PoolManager.EnsureInstance();

        GameObject corpse =
            poolManager.Spawn(corpsePrefab, transform.position, Quaternion.identity);

        if (corpse == null)
            return;

        // 【보상은 시체가 아니라 죽은 적이 정한다.】
        // 전에는 시체 프리팹의 valueMultiplier를 손으로 적어 두었다.
        // 그러면 희귀 개체를 잡아도 일반과 같은 경험치가 나온다 —
        // 「등급이 곧 드롭 품질이다」가 성립하지 않는다. (Hunting 6절)
        if (!corpse.TryGetComponent(out CorpseController controller))
            return;

        EnemyRarity rarity = identity != null ? identity.Profile.rarity : EnemyRarity.Normal;

        controller.SetReward(rarity, health != null ? health.KillContext : default);
    }
}
