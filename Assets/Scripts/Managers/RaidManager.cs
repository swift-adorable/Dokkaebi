using UnityEngine;

/// <summary>
/// 이번 판의 조건 — 시설 상태와 레이드 특성. (docs/Blob_Decisions.md 2-25 · 7-G)
///
/// 【판마다 한 번만 굴린다.】
/// 적이 스폰될 때마다 굴리면 같은 판 안에서 조건이 달라져,
/// 「이번 판은 안개가 짙다」 같은 판 전체의 성격이 생기지 않는다.
/// 굴린 결과를 여기 들고 있고, 스폰기와 적이 읽어 간다.
///
/// 9단계에서 출격 전 화면이 생기면 그 화면이 이 값을 보여 주고,
/// 「이 조건으로 들어갈까」가 출격 판단이 된다.
/// </summary>
public class RaidManager : Singleton<RaidManager>
{
    [Header("Raid")]
    [Tooltip("0이면 매번 다르게 굴린다. 값을 넣으면 같은 조건이 재현된다 — 검증용.")]
    [SerializeField] private int seed;

    [Tooltip("끄면 조건 없이(RaidConditions.None) 돈다. 조건을 뺀 상태를 비교할 때 쓴다.")]
    [SerializeField] private bool rollConditions = true;

    private bool rolled;

    private RaidConditions conditions = RaidConditions.None;

    /// <summary>이번 판의 조건. 처음 읽을 때 굴린다.</summary>
    public RaidConditions Conditions
    {
        get
        {
            if (!rolled)
                Reroll();

            return conditions;
        }
    }

    /// <summary>
    /// 조건을 다시 굴린다. 출격할 때마다 한 번 부른다.
    /// 검증 도구도 이것으로 판을 바꿔 본다.
    /// </summary>
    public void Reroll()
    {
        rolled = true;

        if (!rollConditions)
        {
            conditions = RaidConditions.None;
            return;
        }

        var random = seed != 0
            ? new System.Random(seed)
            : new System.Random(System.Environment.TickCount);

        conditions = RaidConditionRoller.Roll(random);

        GameLogger.Log($"[RaidManager] 이번 판 — {string.Join(" · ", conditions.Describe())}");
    }

    /// <summary>
    /// 조건을 직접 정한다. 출격 전 화면(9단계)과 검증 도구가 쓴다.
    /// </summary>
    public void Set(RaidConditions value)
    {
        rolled = true;
        conditions = value;
    }

    /// <summary>
    /// 지금 판의 조건. 매니저가 없으면 조건 없음 —
    /// 테스트 씬이나 벙커에서 적이 스폰되어도 터지지 않아야 한다.
    /// </summary>
    public static RaidConditions Current
        => HasInstance ? Instance.Conditions : RaidConditions.None;

    /// <summary>씬에 없으면 만든다. 다른 매니저들과 같은 방식이다.</summary>
    public static RaidManager EnsureInstance()
    {
        if (HasInstance)
            return Instance;

        var existing = FindAnyObjectByType<RaidManager>(FindObjectsInactive.Include);

        if (existing != null)
            return existing;

        var created = new GameObject("RaidManager (Runtime)");

        return created.AddComponent<RaidManager>();
    }
}
