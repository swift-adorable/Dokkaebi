using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mutation 획득 관리자.
///
/// 한 번에 여러 레벨이 올라가도 선택창이 중복으로 열리지 않도록
/// 대기 건수를 누적한 뒤 한 번에 하나씩 순차 처리한다.
/// </summary>
public class MutationManager : Singleton<MutationManager>
{
    /// <summary>Resources 하위의 Mutation 폴더 경로. 이 아래 에셋은 자동으로 카탈로그에 들어간다.</summary>
    public const string CatalogResourcePath = "Mutations";

    [Header("Catalog")]
    [Tooltip("비워두면 Resources/Mutations 폴더의 모든 MutationDefinition을 자동으로 불러온다.")]
    [SerializeField] private List<MutationDefinition> catalog = new();

    [Header("Selection")]
    [Tooltip("레벨업 시 제시할 선택지 개수")]
    [Min(1)]
    [SerializeField] private int choiceCount = 3;

    [Tooltip("선택창이 열려 있는 동안 게임을 정지할지")]
    [SerializeField] private bool pauseGameDuringSelection = true;

    private readonly MutationInventory inventory = new();
    private readonly List<MutationDefinition> currentChoices = new();

    private System.Random random;

    /// <summary>현재 선택창이 열려 있는지.</summary>
    public bool IsSelecting { get; private set; }

    /// <summary>처리 대기 중인 선택 건수.</summary>
    public int PendingSelectionCount { get; private set; }

    /// <summary>플레이어가 보유한 Mutation.</summary>
    public MutationInventory Inventory => inventory;

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
        LoadCatalogIfEmpty();

        // 선택 UI가 씬에 없으면 런타임에 생성한다.
        if (FindAnyObjectByType<MutationSelectionUI>(FindObjectsInactive.Include) == null)
            MutationSelectionUI.Create();
    }

    /// <summary>
    /// 카탈로그가 비어 있으면 Resources에서 자동으로 채운다.
    ///
    /// Inspector에 일일이 드래그하는 방식은 Mutation을 추가할 때마다 연결을 잊는
    /// 실패 지점을 만든다. 폴더에 에셋을 넣으면 자동으로 선택지에 포함되게 한다.
    /// </summary>
    private void LoadCatalogIfEmpty()
    {
        // null 항목만 정리하고, 수동 지정이 하나라도 있으면 그대로 존중한다.
        catalog.RemoveAll(definition => definition == null);

        if (catalog.Count > 0)
            return;

        MutationDefinition[] loaded = Resources.LoadAll<MutationDefinition>(CatalogResourcePath);

        if (loaded == null || loaded.Length == 0)
        {
            GameLogger.Error(
                $"[MutationManager] Resources/{CatalogResourcePath} 에 MutationDefinition이 없습니다. " +
                "레벨업을 해도 선택지가 나오지 않습니다.", this);
            return;
        }

        catalog.AddRange(loaded);

        GameLogger.Log($"[MutationManager] 카탈로그 자동 로드: {catalog.Count}종");
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

        MutationDraft.Draw(catalog, inventory, choiceCount, random, currentChoices);

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

        GameLogger.Log($"[MutationManager] 선택지 {currentChoices.Count}개 제시");

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

        if (!inventory.Add(definition))
        {
            GameLogger.Warning($"[MutationManager] 중첩 상한에 도달했습니다: {definition.DisplayName}");
            return false;
        }

        GameLogger.Log($"[MutationManager] 획득: {definition.DisplayName} " +
                       $"({inventory.GetStacks(definition)}/{definition.MaxStacks})");

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

    /// <summary>런 종료 시 보유 Mutation을 초기화한다.</summary>
    public void ResetRun()
    {
        PendingSelectionCount = 0;
        IsSelecting = false;
        currentChoices.Clear();
        inventory.Clear();
    }
}
