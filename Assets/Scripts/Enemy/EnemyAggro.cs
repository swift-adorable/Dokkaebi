using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이 개체가 지금 누구를 노리는가. (docs/Blob_Hunting_System.md 4절)
///
/// 판단은 AggroSelector(순수 클래스)가 하고, 여기서는
/// 【언제 다시 판단할지】와 【후보를 모으는 일】만 맡는다.
///
/// ⚠️ 문서 4절 — "진영 판정은 프레임마다 돌리지 않는다.
///    어그로 변경 시에만 갱신한다."
/// 그래서 주기마다 한 번, 그리고 대상을 잃었을 때만 다시 고른다.
/// 개체마다 첫 판단 시점을 흩어 두어 같은 프레임에 몰리지 않게 한다.
/// </summary>
[RequireComponent(typeof(Health))]
public class EnemyAggro : MonoBehaviour, IPoolable
{
    [Header("Aggro")]
    [Tooltip("새 대상을 찾는 거리(m). EnemyBrain의 감지 거리와 맞춘다.")]
    [Min(1f)]
    [SerializeField] private float detectRange = 18f;

    [Tooltip("대상을 다시 고르는 간격(초). 프레임마다 돌리지 않는다.")]
    [Min(0.05f)]
    [SerializeField] private float reevaluateInterval = 0.4f;

    /// <summary>
    /// 지금 살아 있는 전부.
    ///
    /// 【EnemyManager.ActiveEnemies를 훑지 않는 이유】
    /// 그쪽을 쓰면 후보마다 GetComponent&lt;EnemyAggro&gt;를 해야 한다.
    /// 적 60마리 × 판단 주기마다면 적지 않다. 자기가 등록한다.
    /// </summary>
    private static readonly List<EnemyAggro> Live = new(64);

    /// <summary>
    /// 후보를 담는 공용 버퍼. 판단은 한 번에 하나만 도므로 하나를 돌려 쓴다.
    /// 매 프레임 경로에서 GC Alloc을 만들지 않는다. (마스터 프롬프트 7-2)
    /// </summary>
    private static readonly List<AggroCandidate> Buffer = new(64);

    /// <summary>
    /// 식별자 발급기.
    ///
    /// 【GetInstanceID를 쓰지 않는 이유】
    /// Unity 6에서 폐기 예정이고, 후속인 EntityId는 int로 담을 수 없다.
    /// AggroSelector는 순수 클래스라 Unity 타입에 묶이면 안 된다.
    /// 1부터 센다 — 0은 플레이어 자리다.
    /// </summary>
    private static int nextId = 1;

    private Health health;
    private EnemyIdentity identity;
    private EnemyManager enemyManager;

    private float nextEvaluateTime;
    private int targetId = AggroSelector.NoTarget;

    /// <summary>이 개체의 식별자. 살아 있는 동안 바뀌지 않는다.</summary>
    public int SelfId { get; private set; }

    /// <summary>지금 노리는 대상. 없으면 null.</summary>
    public Transform Target { get; private set; }

    /// <summary>노리는 대상의 소속. 대상이 없으면 우호를 돌려준다(= 치지 않음).</summary>
    public Faction TargetFaction { get; private set; } = Faction.Friendly;

    /// <summary>이 개체의 소속. EnemyIdentity가 없으면 야생으로 본다.</summary>
    public Faction Faction => identity != null ? identity.Faction : Faction.Wild;

    private void Awake()
    {
        health = GetComponent<Health>();
        identity = GetComponent<EnemyIdentity>();

        SelfId = nextId++;
    }

    // 등록은 OnEnable/OnDisable에서 한다 —
    // 씬에 직접 놓인 적은 OnSpawned가 오지 않는다.
    private void OnEnable() => Live.Add(this);

    private void OnDisable() => Live.Remove(this);

    public void OnSpawned()
    {
        targetId = AggroSelector.NoTarget;
        Target = null;
        TargetFaction = Faction.Friendly;

        // 첫 판단을 흩는다. 한 무리가 같이 스폰되면 같은 프레임에 몰린다.
        nextEvaluateTime = Time.time + Random.Range(0f, reevaluateInterval);
    }

    public void OnDespawned()
    {
        Target = null;
        targetId = AggroSelector.NoTarget;
    }

    private void Update()
    {
        // 대상을 잃었으면 주기를 기다리지 않는다 —
        // 죽은 것을 계속 보고 서 있는 것처럼 보인다.
        bool lost = Target == null || !Target.gameObject.activeInHierarchy;

        if (!lost && Time.time < nextEvaluateTime)
            return;

        nextEvaluateTime = Time.time + reevaluateInterval;

        Evaluate();
    }

    private void Evaluate()
    {
        if (enemyManager == null)
            enemyManager = EnemyManager.EnsureInstance();

        Buffer.Clear();

        Transform player = enemyManager.PlayerTransform;

        if (player != null)
        {
            Buffer.Add(new AggroCandidate
            {
                id = AggroSelector.PlayerId,
                faction = Faction.Friendly,
                position = player.position,
                isPlayer = true,
                isAlive = !player.TryGetComponent(out Health playerHealth) || !playerHealth.IsDead
            });
        }

        for (int i = 0; i < Live.Count; i++)
        {
            EnemyAggro other = Live[i];

            if (other == null || other == this)
                continue;

            Buffer.Add(new AggroCandidate
            {
                id = other.SelfId,
                faction = other.Faction,
                position = other.transform.position,
                isPlayer = false,
                isAlive = other.health == null || !other.health.IsDead
            });
        }

        bool infinite = identity != null && identity.Profile.chasesForever;

        int picked = AggroSelector.Select(
            Faction, transform.position, detectRange, targetId, infinite, Buffer, SelfId);

        if (picked == targetId && Target != null)
            return;

        targetId = picked;

        Resolve(picked, player);
    }

    /// <summary>고른 식별자를 실제 Transform으로 돌린다.</summary>
    private void Resolve(int picked, Transform player)
    {
        Target = null;
        TargetFaction = Faction.Friendly;

        if (picked == AggroSelector.NoTarget)
            return;

        if (picked == AggroSelector.PlayerId)
        {
            Target = player;

            // 플레이어는 진영이 아니다. 「우호가 아니다」만 표시하면 되므로
            // 자기와 다른 값을 넣어 IsHostile이 참이 되게 한다.
            TargetFaction = Faction == Faction.Wild ? Faction.Facility : Faction.Wild;
            return;
        }

        for (int i = 0; i < Live.Count; i++)
        {
            EnemyAggro other = Live[i];

            if (other == null || other.SelfId != picked)
                continue;

            Target = other.transform;
            TargetFaction = other.Faction;
            return;
        }
    }
}
