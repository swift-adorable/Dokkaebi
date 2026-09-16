using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mutation 획득 관리자 — Mutation System v5 구조.
///
/// 흐름: 도감 → 적재(Loadout) → 레벨업 시 적재한 것 중 3장 → 선택 → RunMutationState
///
/// 한 번에 여러 레벨이 올라가도 선택창이 중복으로 열리지 않도록
/// 대기 건수를 누적한 뒤 한 번에 하나씩 순차 처리한다.
/// </summary>
public class MutationManager : Singleton<MutationManager>
{
    [Header("Catalog")]
    [Tooltip("비워두면 Resources/MutationCatalog 에셋을 자동으로 불러온다.")]
    [SerializeField] private MutationCatalog catalog;

    [Header("Loadout (6단계 도감/적재 UI 구현 전 임시)")]
    [Tooltip("※ 임시 — 6단계에서 벙커 적재 UI로 대체된다. " +
             "켜 두면 카탈로그에서 슬롯 수만큼 자동으로 적재를 채워 테스트할 수 있다.")]
    [SerializeField] private bool autoFillLoadout = true;

    [Tooltip("적재 슬롯 수. 영구 성장으로 8 → 14까지 늘어난다.")]
    [Range(MutationLoadout.MinSlotCapacity, MutationLoadout.MaxSlotCapacity)]
    [SerializeField] private int loadoutSlots = MutationLoadout.MinSlotCapacity;

    [Header("Selection")]
    [Tooltip("레벨업 시 제시할 선택지 개수. v5 확정값은 3이다.")]
    [Min(1)]
    [SerializeField] private int choiceCount = 3;

    [Tooltip("선택창이 열려 있는 동안 게임을 정지할지. v5 확정: 일시정지 O, 제한시간 없음.")]
    [SerializeField] private bool pauseGameDuringSelection = true;

    private readonly MutationLoadout loadout = new();
    private readonly RunMutationState runState = new();
    private readonly List<MutationDefinition> currentChoices = new();

    private System.Random random;

    /// <summary>현재 선택창이 열려 있는지.</summary>
    public bool IsSelecting { get; private set; }

    /// <summary>처리 대기 중인 선택 건수.</summary>
    public int PendingSelectionCount { get; private set; }

    /// <summary>이번 레이드에 가져온 적재 구성.</summary>
    public MutationLoadout Loadout => loadout;

    /// <summary>이번 런에서 실제로 획득·장착된 상태.</summary>
    public RunMutationState RunState => runState;

    /// <summary>전체 정의 카탈로그. 로드 실패 시 null일 수 있다.</summary>
    public MutationCatalog Catalog => catalog;

    /// <summary>현재 제시된 선택지. 선택창이 닫혀 있으면 비어 있다.</summary>
    public IReadOnlyList<MutationDefinition> CurrentChoices => currentChoices;

    /// <summary>선택창을 열어야 할 때 발행된다. UI가 구독한다.</summary>
    public event Action<IReadOnlyList<MutationDefinition>> OnSelectionOpened;

    /// <summary>선택창을 닫아야 할 때 발행된다.</summary>
    public event Action OnSelectionClosed;

    /// <summary>Mutation을 획득했을 때 발행된다.</summary>
    public event Action<MutationDefinition> OnMutationGained;

    /// <summary>인스턴스를 보장한다. 씬 배치를 강제하지 않는다.</summary>
    public static MutationManager EnsureInstance()
    {
        if (HasInstance)
            return Instance;

        var existing = FindAnyObjectByType<MutationManager>(FindObjectsInactive.Include);

        if (existing != null)
            return existing;

        return new GameObject("MutationManager (Runtime)").AddComponent<MutationManager>();
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
        if (FindAnyObjectByType<MutationSelectionUI>(FindObjectsInactive.Include) == null)
            MutationSelectionUI.Create();
    }

    private void LoadCatalogIfNeeded()
    {
        if (catalog != null)
            return;

        catalog = MutationCatalog.Load();

        if (catalog == null)
        {
            GameLogger.Error(
                $"[MutationManager] Resources/{MutationCatalog.ResourcePath} 에셋이 없습니다. " +
                "메뉴 Blob > Mutation > 카탈로그 다시 만들기 를 실행하십시오.", this);
            return;
        }

        GameLogger.Log($"[MutationManager] 카탈로그 로드: {catalog.Count}종");
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
        FillFrom(catalog.GetByCategory(MutationCategory.Core), 2);
        FillFrom(catalog.Definitions, loadout.SlotCapacity);

        if (!loadout.IsValid)
        {
            GameLogger.Error($"[MutationManager] 적재 구성 실패: {loadout.ValidationMessage}", this);
            return;
        }

        GameLogger.Log($"[MutationManager] 적재 자동 구성: {loadout.Count}/{loadout.SlotCapacity}칸");
    }

    private void FillFrom(IReadOnlyList<MutationDefinition> source, int limit)
    {
        for (int i = 0; i < source.Count && loadout.Count < limit; i++)
            loadout.TryAdd(source[i]);
    }

    /// <summary>레벨업 횟수를 누적한다. PlayerStats가 레벨업 시 호출한다.</summary>
    public void EnqueueLevelUp(int count = 1)
    {
        if (count <= 0)
        {
            GameLogger.Warning($"[MutationManager] 유효하지 않은 레벨업 횟수: {count}");
            return;
        }

        PendingSelectionCount += count;

        GameLogger.Log($"[MutationManager] 선택 대기 {PendingSelectionCount}건");

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

        MutationDraft.Draw(loadout.Entries, runState, playerLevel, choiceCount, random, currentChoices);

        // 더 이상 얻을 수 있는 Mutation이 없으면 대기 건을 소진하고 조용히 넘어간다.
        if (currentChoices.Count == 0)
        {
            GameLogger.Log("[MutationManager] 획득 가능한 Mutation이 없어 선택을 건너뜁니다.");

            PendingSelectionCount = 0;
            CloseSession();
            return;
        }

        PendingSelectionCount--;
        IsSelecting = true;

        if (pauseGameDuringSelection && GameManager.HasInstance)
            GameManager.Instance.OpenMutation();

        GameLogger.Log($"[MutationManager] 선택지 {currentChoices.Count}개 제시 (Lv.{playerLevel})");

        OnSelectionOpened?.Invoke(currentChoices);
    }

    /// <summary>선택지 중 하나를 고른다. UI가 호출한다.</summary>
    public bool Select(MutationDefinition definition)
    {
        if (!IsSelecting)
            return false;

        if (definition == null || !currentChoices.Contains(definition))
        {
            GameLogger.Warning("[MutationManager] 제시되지 않은 Mutation은 선택할 수 없습니다.");
            return false;
        }

        if (!runState.TryAcquire(definition))
        {
            GameLogger.Warning($"[MutationManager] 획득할 수 없습니다: {definition.DisplayName}");
            return false;
        }

        GameLogger.Log($"[MutationManager] 획득: {definition.DisplayName} " +
                       $"(누적 {runState.AcquiredCount}종)");

        OnMutationGained?.Invoke(definition);

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
            GameManager.Instance.CloseMutation();
    }

    /// <summary>런 종료 시 획득한 Mutation을 전부 초기화한다. 적재 구성은 남는다. (v5 §1-5)</summary>
    public void ResetRun()
    {
        PendingSelectionCount = 0;
        IsSelecting = false;
        currentChoices.Clear();
        runState.Clear();
    }
}
