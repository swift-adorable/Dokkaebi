using UnityEngine;

/// <summary>
/// Mutation(변이) 획득 관리자.
///
/// 한 번에 여러 레벨이 올라가도 선택창이 중복으로 열리지 않도록
/// 대기 건수를 큐처럼 누적한 뒤 '한 번에 하나씩' 순차 처리한다.
/// </summary>
public class MutationManager : Singleton<MutationManager>
{
    [Header("Selection")]
    [Tooltip("선택 UI 구현 전까지는 false로 둔다. true면 선택 중 게임이 정지한다.")]
    [SerializeField] private bool pauseGameDuringSelection = false;

    /// <summary>현재 선택창이 열려 있는지 여부.</summary>
    public bool IsSelecting { get; private set; }

    /// <summary>처리 대기 중인 선택 건수.</summary>
    public int PendingSelectionCount { get; private set; }

    public bool HasDoubleShot { get; private set; }

    /// <summary>
    /// 레벨업 횟수를 누적한다. PlayerStats가 레벨업 시 호출한다.
    /// </summary>
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
    /// IsSelecting 가드가 '중복 오픈' 문제(C-5)를 막는 핵심이다.
    /// </summary>
    private void TryOpenNextSelection()
    {
        if (IsSelecting)
            return;

        if (PendingSelectionCount <= 0)
        {
            if (pauseGameDuringSelection)
                GameManager.Instance.CloseMutation();

            return;
        }

        PendingSelectionCount--;
        IsSelecting = true;

        if (pauseGameDuringSelection)
            GameManager.Instance.OpenMutation();

        OpenSelectionUI();
    }

    private void OpenSelectionUI()
    {
        GameLogger.Log("===== MUTATION SELECTION =====");

        // TBD — 추후 결정: 실제 선택 UI (3지선다 카드 등)
        // 현재는 UI가 없으므로 Double Shot을 자동 부여하고 즉시 확정한다.
        // UI 구현 시 아래 두 줄을 제거하고, UI 버튼에서 ConfirmSelection()을 호출하면 된다.
        GainDoubleShot();
        ConfirmSelection();
    }

    /// <summary>선택이 확정되면 호출한다. (UI 버튼 OnClick에 연결 예정)</summary>
    public void ConfirmSelection()
    {
        if (!IsSelecting)
            return;

        IsSelecting = false;

        TryOpenNextSelection();
    }

    private void GainDoubleShot()
    {
        if (HasDoubleShot)
        {
            GameLogger.Log("[MutationManager] 이미 Double Shot 보유");
            return;
        }

        HasDoubleShot = true;

        GameLogger.Log("[MutationManager] Double Shot 획득");
    }
}
