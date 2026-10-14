using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【창고 화면】 (결정 2-123) — 가게와 같은 틀(StationFrame). 고목 뿌리 굴 창고.
///   · 왼쪽: 창고 칸 — 누르면 꺼내기 · 상세보기
///   · 가운데: 영감(본문 대사) · 계산대 앞판에 「창고 n / m칸」
///   · 오른쪽: 내 가방 — 누르면 창고에 넣기 · 상세보기 · 아래 「전부 꺼내기」
/// 옛 옆 패널(ExchangeWindowUI 창고 모드)을 대신한다. 「창고에서 꺼냈다」 튜토리얼은 그대로 듣는다.
/// </summary>
public class StashScreenUI : MonoBehaviour
{
    private static StashScreenUI instance;

    public static bool IsOpen => instance != null && instance.frame != null && instance.frame.IsOpen;

    private const int Columns = 5;

    private StationFrame frame;
    private Button takeAllButton;
    private bool opening;

    public static void Open()
    {
        if (instance == null)
        {
            Canvas canvas = UIFactory.CreateCanvas("StashScreenCanvas (Runtime)", 1050);
            instance = canvas.gameObject.AddComponent<StashScreenUI>();
            instance.Build(canvas);
        }

        if (instance.opening)
            return;

        instance.opening = true;
        instance.StartCoroutine(instance.frame.Show(() =>
        {
            instance.opening = false;
            instance.Refresh();
        }));
    }

    public static void Close()
    {
        if (instance != null)
            instance.frame.Hide();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            frame.Hide();
    }

    private void Build(Canvas canvas)
    {
        frame = StationFrame.Build(canvas, () => frame.Hide());
        frame.UseCounterAsNote(true);
        takeAllButton = StationFrame.Pill(frame.Buttons, "전부 꺼내기", Vector2.zero, Vector2.one, StationFrame.PillAction, TakeAll);
    }

    private static Inventory Stash => PlayerInventory.EnsureInstance().Stash;
    private static Inventory Bag => PlayerInventory.EnsureInstance().Bag;

    private void Refresh()
    {
        if (!frame.IsOpen)
            return;

        ItemActionMenu.Close();

        frame.Title.text = "창고";
        frame.SetKeeper(KeeperTable.Of(Station.Stash));
        frame.SetGold(PassiveManager.EnsureInstance().Gold);
        frame.DrawTabs(new[] { "창고" }, 0, null);

        Inventory stash = Stash;
        frame.CounterNote.text = $"창고 <b>{stash.UsedSlots} / {stash.SlotCapacity}</b>칸\n<size=18>창고에 둔 것은 쓰러져도 남는다</size>";

        Canvas.ForceUpdateCanvases();
        DrawStash();
        frame.DrawBag(null, OpenBagMenu);
        takeAllButton.interactable = stash.Stacks.Count > 0;
    }

    /// <summary>창고 칸 — 칸 수보다 물건이 많을 수도 있다(젬 · 패시브가 줄어든 경우). 둘 다 보인다.</summary>
    private void DrawStash()
    {
        UIFactory.ClearChildren(frame.List.Content);

        Inventory stash = Stash;
        IReadOnlyList<ItemStack> stacks = stash.Stacks;
        int cells = Mathf.Max(stash.SlotCapacity, stacks.Count);

        UIFactory.SquareGridMetrics(frame.List.Viewport, Columns, cells, 3,
            out int rows, out float height, out float padX, out float padY);
        frame.List.Content.sizeDelta = new Vector2(0f, height);

        for (int i = 0; i < rows * Columns; i++)
        {
            UIFactory.GetCellAnchors(i, Columns, rows, padX, padY, out Vector2 min, out Vector2 max);
            ItemStack stack = i < stacks.Count ? stacks[i] : null;

            Image cell = ItemCell.Draw($"Stash_{i}", frame.List.Content, min, max, stack, chosen: false, null);
            if (stack?.Definition == null || !cell.TryGetComponent(out Button button))
                continue;

            RectTransform cellRect = cell.rectTransform;
            ItemStack captured = stack;
            button.onClick.AddListener(() => ItemActionMenu.Open(cellRect, new List<ItemActionMenu.Entry>
            {
                new("꺼내기", UIPalette.Action, () => Move(captured, Stash, Bag, "꺼내기", true)),
                new("상세보기", UIPalette.Subtle, () => InventoryScreenUI.OpenReadOnlyDetail(captured, "창고")),
            }));
        }
    }

    private void OpenBagMenu(RectTransform cell, ItemStack stack)
        => ItemActionMenu.Open(cell, new List<ItemActionMenu.Entry>
        {
            new("창고에 넣기", UIPalette.Action, () => Move(stack, Bag, Stash, "넣기", false)),
            new("상세보기", UIPalette.Subtle, () => InventoryScreenUI.OpenReadOnlyDetail(stack, "내 가방")),
        });

    /// <summary>옮긴다 — 겹친 물건은 몇 개를 옮길지 묻는다 (결정 2-77).</summary>
    private void Move(ItemStack stack, Inventory from, Inventory to, string verb, bool taking)
    {
        if (stack == null || stack.IsEmpty)
            return;

        QuantityPopupUI.Ask(stack.Definition.DisplayName, verb, stack.Count, null, n =>
        {
            if (!QuantityTransfer.Move(from, to, stack, n))
                Toast(taking ? "가방에 자리가 없습니다." : "창고에 자리가 없습니다.");
            else if (taking)
                ExchangeWindowUI.RaiseStashTaken();

            PlayerInventory.EnsureInstance().RefreshCapacity();
            Refresh();
        });
    }

    private void TakeAll()
    {
        int moved = 0;
        Inventory stash = Stash, bag = Bag;

        // 뒤에서부터 — 옮기면 목록이 줄어든다.
        for (int i = stash.Stacks.Count - 1; i >= 0; i--)
            if (Inventory.MoveStack(stash, bag, stash.Stacks[i]))
                moved++;

        if (moved > 0)
            ExchangeWindowUI.RaiseStashTaken();
        else
            Toast("가방에 자리가 없습니다.");

        PlayerInventory.EnsureInstance().RefreshCapacity();
        Refresh();
    }

    private static void Toast(string text) => StoryDialogueUI.ShowBanner(text, 2f);
}
