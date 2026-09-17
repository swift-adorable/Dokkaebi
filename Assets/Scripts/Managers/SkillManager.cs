using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Skill 획득 관리자 — Skill System v5 구조.
///
/// 흐름: 도감 → 적재(Loadout) → 레벨업 시 적재한 것 중 3장 → 선택 → RunSkillState
///
/// 한 번에 여러 레벨이 올라가도 선택창이 중복으로 열리지 않도록
/// 대기 건수를 누적한 뒤 한 번에 하나씩 순차 처리한다.
/// </summary>
public class SkillManager : Singleton<SkillManager>
{
    [Header("Catalog")]
    [Tooltip("비워두면 Resources/SkillCatalog 에셋을 자동으로 불러온다.")]
    [SerializeField] private SkillCatalog catalog;

    [Header("Loadout (6단계 도감/적재 UI 구현 전 임시)")]
    [Tooltip("※ 임시 — 6단계에서 벙커 적재 UI로 대체된다. " +
             "켜 두면 카탈로그에서 슬롯 수만큼 자동으로 적재를 채워 테스트할 수 있다.")]
    [SerializeField] private bool autoFillLoadout = true;

    [Tooltip("적재 슬롯 수. 영구 성장으로 8 → 14까지 늘어난다.")]
    [Range(SkillLoadout.MinSlotCapacity, SkillLoadout.MaxSlotCapacity)]
    [SerializeField] private int loadoutSlots = SkillLoadout.MinSlotCapacity;

    [Header("Selection")]
    [Tooltip("레벨업 시 제시할 선택지 개수. v5 확정값은 3이다.")]
    [Min(1)]
    [SerializeField] private int choiceCount = 3;

    [Tooltip("선택창이 열려 있는 동안 게임을 정지할지. v5 확정: 일시정지 O, 제한시간 없음.")]
    [SerializeField] private bool pauseGameDuringSelection = true;

    private readonly SkillLoadout loadout = new();
    private readonly RunSkillState runState = new();
    private readonly List<SkillDefinition> currentChoices = new();

    private System.Random random;

    /// <summary>현재 선택창이 열려 있는지.</summary>
    public bool IsSelecting { get; private set; }

    /// <summary>처리 대기 중인 선택 건수.</summary>
    public int PendingSelectionCount { get; private set; }

    /// <summary>이번 레이드에 가져온 적재 구성.</summary>
    public SkillLoadout Loadout => loadout;

    /// <summary>이번 런에서 실제로 획득·장착된 상태.</summary>
    public RunSkillState RunState => runState;

    /// <summary>전체 정의 카탈로그. 로드 실패 시 null일 수 있다.</summary>
    public SkillCatalog Catalog => catalog;

    /// <summary>현재 제시된 선택지. 선택창이 닫혀 있으면 비어 있다.</summary>
    public IReadOnlyList<SkillDefinition> CurrentChoices => currentChoices;

    /// <summary>선택창을 열어야 할 때 발행된다. UI가 구독한다.</summary>
    public event Action<IReadOnlyList<SkillDefinition>> OnSelectionOpened;

    /// <summary>선택창을 닫아야 할 때 발행된다.</summary>
    public event Action OnSelectionClosed;

    /// <summary>Skill을 획득했을 때 발행된다.</summary>
    public event Action<SkillDefinition> OnSkillGained;

    /// <summary>인스턴스를 보장한다. 씬 배치를 강제하지 않는다.</summary>
    public static SkillManager EnsureInstance()
    {
        if (HasInstance)
            return Instance;

        var existing = FindAnyObjectByType<SkillManager>(FindObjectsInactive.Include);

        if (existing != null)
            return existing;

        return new GameObject("SkillManager (Runtime)").AddComponent<SkillManager>();
    }

    protected override void OnSingletonAwake()
    {
        random = new System.Random(Environment.TickCount);
    }

    private void Start()
    {
        LoadCatalogIfNeeded();
        BuildLoadout();

        // 선택 UI가 씬에 없으면 런타임에 생성한다.
        if (FindAnyObjectByType<SkillSelectionUI>(FindObjectsInactive.Include) == null)
            SkillSelectionUI.Create();
    }

    private void LoadCatalogIfNeeded()
    {
        if (catalog != null)
            return;

        catalog = SkillCatalog.Load();

        if (catalog == null)
        {
            GameLogger.Error(
                $"[SkillManager] Resources/{SkillCatalog.ResourcePath} 에셋이 없습니다. " +
                "메뉴 Blob > Skill > 카탈로그 다시 만들기 를 실행하십시오.", this);
            return;
        }

        GameLogger.Log($"[SkillManager] 카탈로그 로드: {catalog.Count}종");
    }

    /// <summary>
    /// 적재를 구성한다.
    ///
    /// ※ 임시 구현이다. 6단계에서 벙커의 도감/적재 UI가 세이브 데이터로 채운다.
    ///    v5 §11-1의 "Core 최소 1개 포함" 제약은 지금부터 지킨다.
    /// </summary>
    private void BuildLoadout()
    {
        loadout.SlotCapacity = loadoutSlots;

        if (!autoFillLoadout || catalog == null || loadout.Count > 0)
            return;

        // Core를 먼저 채워 "Core 최소 1개" 제약을 구조적으로 보장한다.
        FillFrom(catalog.GetByCategory(SkillCategory.Core), 2);
        FillFrom(catalog.Definitions, loadout.SlotCapacity);

        if (!loadout.IsValid)
        {
            GameLogger.Error($"[SkillManager] 적재 구성 실패: {loadout.ValidationMessage}", this);
            return;
        }

        GameLogger.Log($"[SkillManager] 적재 자동 구성: {loadout.Count}/{loadout.SlotCapacity}칸");
    }

    private void FillFrom(IReadOnlyList<SkillDefinition> source, int limit)
    {
        for (int i = 0; i < source.Count && loadout.Count < limit; i++)
            loadout.TryAdd(source[i]);
    }

    /// <summary>레벨업 횟수를 누적한다. PlayerStats가 레벨업 시 호출한다.</summary>
    public void EnqueueLevelUp(int count = 1)
    {
        if (count <= 0)
        {
            GameLogger.Warning($"[SkillManager] 유효하지 않은 레벨업 횟수: {count}");
            return;
        }

        PendingSelectionCount += count;

        GameLogger.Log($"[SkillManager] 선택 대기 {PendingSelectionCount}건");

        TryOpenNextSelection();
    }

    /// <summary>
    /// 선택창이 닫혀 있고 대기 건이 남아 있을 때만 다음 선택을 연다.
    /// IsSelecting 가드가 중복 오픈을 막는 핵심이다.
    /// </summary>
    private void TryOpenNextSelection()
    {
        if (IsSelecting)
            return;

        if (PendingSelectionCount <= 0)
        {
            CloseSession();
            return;
        }

        int playerLevel = PlayerStats.HasInstance ? PlayerStats.Instance.Level : 1;

        // 2번째 Core 슬롯은 Lv7에 열린다. 선택 풀 필터가 이 값을 그대로 쓴다.
        runState.CoreCapacity = RunSkillState.GetCoreCapacity(playerLevel);

        SkillDraft.Draw(loadout.Entries, runState, playerLevel, choiceCount, random, currentChoices);

        // 더 이상 얻을 수 있는 Skill이 없으면 대기 건을 소진하고 조용히 넘어간다.
        if (currentChoices.Count == 0)
        {
            GameLogger.Log("[SkillManager] 획득 가능한 Skill이 없어 선택을 건너뜁니다.");

            PendingSelectionCount = 0;
            CloseSession();
            return;
        }

        PendingSelectionCount--;
        IsSelecting = true;

        if (pauseGameDuringSelection && GameManager.HasInstance)
            GameManager.Instance.OpenSkill();

        GameLogger.Log($"[SkillManager] 선택지 {currentChoices.Count}개 제시 (Lv.{playerLevel})");

        OnSelectionOpened?.Invoke(currentChoices);
    }

    /// <summary>
    /// 선택지 중 하나를 고른다. UI가 호출한다.
    ///
    /// preferredCoreIndex는 Support를 어느 Core 소켓에 넣을지 지정한다.
    /// 두 Core가 모두 조건을 만족할 때 UI가 유저에게 물어 넘긴다.
    /// </summary>
    public bool Select(SkillDefinition definition, int preferredCoreIndex = -1)
    {
        if (!IsSelecting)
            return false;

        if (definition == null || !currentChoices.Contains(definition))
        {
            GameLogger.Warning("[SkillManager] 제시되지 않은 Skill은 선택할 수 없습니다.");
            return false;
        }

        if (!runState.TryAcquire(definition, preferredCoreIndex))
        {
            GameLogger.Warning($"[SkillManager] 획득할 수 없습니다: {definition.DisplayName}");
            return false;
        }

        GameLogger.Log($"[SkillManager] 획득: {definition.DisplayName} " +
                       $"(누적 {runState.AcquiredCount}종)");

        OnSkillGained?.Invoke(definition);

        IsSelecting = false;
        currentChoices.Clear();

        OnSelectionClosed?.Invoke();

        TryOpenNextSelection();

        return true;
    }

    /// <summary>선택을 건너뛴다. 대기 건은 소비된다.</summary>
    public void Skip()
    {
        if (!IsSelecting)
            return;

        IsSelecting = false;
        currentChoices.Clear();

        OnSelectionClosed?.Invoke();

        TryOpenNextSelection();
    }

    private void CloseSession()
    {
        if (pauseGameDuringSelection && GameManager.HasInstance)
            GameManager.Instance.CloseSkill();
    }

    /// <summary>런 종료 시 획득한 Skill을 전부 초기화한다. 적재 구성은 남는다. (v5 §1-5)</summary>
    public void ResetRun()
    {
        PendingSelectionCount = 0;
        IsSelecting = false;
        currentChoices.Clear();
        runState.Clear();
    }
}
