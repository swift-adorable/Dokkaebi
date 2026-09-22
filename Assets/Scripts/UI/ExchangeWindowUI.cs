using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상대의 물건을 보는 패널 — 【화면 오른쪽에만 뜬다.】
///
/// 시체·상자·캐비닛을 파밍할 때, 창고에 맡길 때, 상인과 거래할 때가
/// 전부 같은 일이다. 「내 것과 저쪽 것을 나란히 놓고 옮긴다.」
///
/// 【화면을 덮는 큰 창을 버렸다.】
/// 처음에는 좌우 두 칸짜리 창 하나로 화면 전체를 덮었다. 덕코프는 그렇게
/// 하지 않는다 — **왼쪽에 장비·가방 패널, 오른쪽에 전리품 패널을 따로 띄우고
/// 가운데로는 게임 화면이 그대로 보인다.** 파밍은 위험한 곳에서 하는 일이라,
/// 창이 화면을 덮으면 무엇이 다가오는지 알 수 없다.
///
/// 그래서 왼쪽은 **이미 있는 가방 화면(InventoryScreenUI)을 그대로 연다.**
/// 같은 격자·같은 상세를 두 벌 만들 이유가 없고, 유저도 한 번만 배우면 된다.
/// 이 컴포넌트는 오른쪽 한 칸만 맡는다.
///
/// 【상점은 아직 이 패널을 쓰지 않는다.】 파는 값·재고·통화 흐름이
/// 8단계 설계에 걸려 있다. 틀은 여기 있으므로 그때 Mode 하나를 더한다.
/// </summary>
public class ExchangeWindowUI : MonoBehaviour
{
    /// <summary>이 패널이 무엇을 하는 중인가. 방향과 버튼 이름만 달라진다.</summary>
    public enum Mode
    {
        /// <summary>파밍. 【가져오기만 한다.】 시체에 물건을 도로 넣는 조작은 없다.</summary>
        Loot = 0,

        /// <summary>창고. 양방향. 맡기고 찾는다.</summary>
        Stash = 1
    }

    private const int Columns = 5;

    /// <summary>격자가 최소한 이만큼은 줄을 그린다. 빈 칸이 곧 남은 자리다.</summary>
    private const int MinRows = 2;

    // ── 자리 (안전 영역 기준 0~1) ─────────────────────────────────────
    // 오른쪽에 붙인다. 왼쪽은 가방 화면이 쓰고, 가운데는 비워 둔다.

    private const float PanelLeft = 0.615f;
    private const float PanelRight = 0.985f;
    private const float PanelBottom = 0.30f;
    private const float PanelTop = 0.965f;

    private static ExchangeWindowUI instance;

    private GameObject panel;
    private Text titleLabel;
    private Button takeAllButton;

    private UIFactory.ScrollList grid;

    private Mode mode = Mode.Loot;
    private CorpseController source;
    private LootContainer other;
    private string otherName = "전리품";

    public bool IsOpen => panel != null && panel.activeSelf;

    public static ExchangeWindowUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<ExchangeWindowUI>(FindObjectsInactive.Include);

        if (instance != null)
            return instance;

        // 가방 화면(1000)과 같은 층에 두되 살짝 위 — 두 패널이 나란히 떠 있어야 한다.
        Canvas canvas = UIFactory.CreateCanvas("ExchangePanelCanvas (Runtime)", 1010);

        instance = canvas.gameObject.AddComponent<ExchangeWindowUI>();
        instance.Build(canvas);

        return instance;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    // ────────────────────────────────── 생성

    private void Build(Canvas canvas)
    {
        RectTransform safe = UIFactory.CreateSafeArea(canvas);

        // 【덮개를 깔지 않는다.】 가운데로 게임 화면이 보여야 한다.
        // 패널 바깥의 터치는 가방 화면의 덮개가 이미 막고 있다.
        Image window = UIFactory.CreateGlass("LootPanel", safe, UIPalette.Panel,
            new Vector2(PanelLeft, PanelBottom), new Vector2(PanelRight, PanelTop),
            UIFactory.RadiusLarge);

        panel = window.gameObject;

        // 패널 위의 터치가 뒤의 조이스틱까지 내려가지 않게 막는다.
        window.raycastTarget = true;

        var box = window.rectTransform;

        UIFactory.CreatePanel("Header", box, UIPalette.Header,
            new Vector2(0f, 0.895f), new Vector2(1f, 1f), UIFactory.RadiusLarge);

        titleLabel = UIFactory.CreateLabel(box, "전리품", 28, FontStyle.Bold,
            new Vector2(0.04f, 0.895f), new Vector2(0.72f, 1f),
            TextAnchor.MiddleLeft, UIPalette.TextOnGlass);

        // 【닫기를 두지 않는다.】 파밍은 가방 화면과 한 벌로 여닫힌다.
        // 닫는 버튼이 양쪽에 하나씩 있으면 「어느 쪽이 무엇을 닫는가」를
        // 매번 생각해야 한다. 나가는 길은 가방 화면의 닫기 하나다.

        // 【무게 안내줄도 뺐다.】 왼쪽 아래에 소지 중량 막대가 이미 있다.
        // 같은 것을 두 군데서 다른 말로 적으면 어느 쪽이 맞는지 헷갈린다.
        grid = UIFactory.CreateScrollList("LootGrid", box,
            new Vector2(0.04f, 0.155f), new Vector2(0.96f, 0.875f));

        takeAllButton = UIFactory.CreateButton(box, "전부 줍기",
            new Vector2(0.04f, 0.035f), new Vector2(0.96f, 0.130f),
            UIPalette.Action, TakeAll, 26);

        panel.SetActive(false);
    }

    // ────────────────────────────────── 열고 닫기

    /// <summary>시체·상자를 파밍한다.</summary>
    public void Open(CorpseController corpse)
    {
        if (corpse == null)
            return;

        source = corpse;

        Open(corpse.Loot, "전리품", Mode.Loot);
    }

    /// <summary>임의의 컨테이너를 연다. 창고가 8단계에 이 길로 들어온다.</summary>
    public void Open(LootContainer container, string label, Mode windowMode)
    {
        if (container == null)
            return;

        other = container;
        otherName = string.IsNullOrEmpty(label) ? "상대" : label;
        mode = windowMode;

        panel.SetActive(true);

        // 【왼쪽은 가방 화면이 맡는다.】 같은 격자를 두 벌 만들지 않는다.
        // 가방 탭으로 연다 — 파밍 중에 보고 싶은 것은 「자리가 얼마나 남았나」다.
        InventoryScreenUI.EnsureInstance().Open(0);

        // 【패널이 뜨면 화면 버튼은 감춘다.】 (장비·스킬·패시브)
        // 세 버튼은 패널 밖의 화면 UI라 어떤 판을 깔아도 그 위로 떠오른다.
        // 열려 있는 패널 뒤로 글자가 비쳐 보이면 둘 다 읽히지 않는다.
        InventoryScreenUI.SetHudSuppressed(true);

        Refresh();
    }

    /// <summary>닫기가 서로를 다시 부르는 것을 막는다.</summary>
    private bool closing;

    public void Close()
    {
        if (closing)
            return;

        closing = true;

        panel.SetActive(false);

        // 다 집었으면 시체를 정리한다. 남아 있으면 그대로 두어
        // 나중에 돌아와 마저 집을 수 있게 한다.
        if (mode == Mode.Loot && source != null && other != null && other.IsEmpty)
            DespawnSource();

        source = null;
        other = null;

        // 왼쪽 패널도 같이 닫는다. 파밍이 끝났는데 가방만 남아 있으면
        // 「무엇을 닫는 중인지」가 헷갈린다.
        if (InventoryScreenUI.HasInstance && InventoryScreenUI.Instance.IsOpen)
            InventoryScreenUI.Instance.Close();

        // 감춘 것을 되돌린다. Close보다 뒤에 둬야 한다 —
        // 가방 화면의 Close가 이 값을 읽어 버튼을 다시 그린다.
        InventoryScreenUI.SetHudSuppressed(false);

        closing = false;
    }

    private void DespawnSource()
    {
        var absorber = FindAnyObjectByType<PlayerAbsorber>(FindObjectsInactive.Include);

        if (absorber != null)
            absorber.Despawn(source);
    }

    /// <summary>
    /// 가방 화면이 닫히면 이 패널도 닫힌다. 둘은 한 벌이다.
    ///
    /// 정리(시체 치우기)까지 같은 길로 지나가야 하므로 Close를 그대로 부른다.
    /// 서로를 다시 부르는 것은 closing 깃발이 막는다.
    /// </summary>
    public static void CloseIfOpen()
    {
        if (instance != null && instance.IsOpen)
            instance.Close();
    }

    // ────────────────────────────────── 다시 그리기

    /// <summary>바깥(줍기·버리기)에서 내용이 바뀌었을 때.</summary>
    public static void RefreshIfOpen()
    {
        if (instance != null && instance.IsOpen)
            instance.Refresh();
    }

    private void Refresh()
    {
        if (other == null)
            return;

        titleLabel.text = $"{otherName} ({other.UsedSlots}/{other.Capacity})";

        var label = takeAllButton.GetComponentInChildren<Text>();

        if (label != null)
            label.text = mode == Mode.Stash ? "전부 꺼내기" : "전부 줍기";

        takeAllButton.interactable = !other.IsEmpty;

        // 칸 크기를 픽셀로 환산하려면 실제 크기가 확정되어 있어야 한다.
        Canvas.ForceUpdateCanvases();

        DrawGrid();
    }

    private void DrawGrid()
    {
        UIFactory.ClearChildren(grid.Content);

        int cells = Mathf.Max(0, other.Capacity);

        UIFactory.SquareGridMetrics(grid.Viewport, Columns, cells, MinRows,
            out int rows, out float height, out float padX, out float padY);

        grid.Content.sizeDelta = new Vector2(0f, height);

        // 【용량만큼 전부 그린다.】 잘라내면 그 칸의 물건은 보이지도 눌리지도 않는다.
        for (int i = 0; i < rows * Columns; i++)
        {
            UIFactory.GetCellAnchors(i, Columns, rows, padX, padY,
                out Vector2 min, out Vector2 max);

            ItemStack stack = i < cells ? other.Get(i) : null;

            int captured = i;

            // 【한 번 누르면 줍는다.】 상세를 거치지 않는다.
            // 왼쪽 가방에서는 상세가 뜨지만, 여기 있는 것은 아직 내 물건이
            // 아니라서 「장착·버리기」가 나올 자리가 없다. 할 일이 하나뿐이면
            // 두 번 누르게 만들 이유가 없다.
            ItemCell.Draw($"Loot_{i}", grid.Content, min, max, stack, chosen: false,
                () => Take(captured));
        }
    }

    // ────────────────────────────────── 옮기기

    private void Take(int index)
    {
        if (other == null || other.Get(index) == null)
            return;

        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        if (!other.TryTakeTo(index, bag))
        {
            // 못 옮기면 아이템은 있던 자리에 그대로 남는다. 사라지지 않는다.
            InventoryScreenUI.ShowToastIfOpen("가방에 자리가 없습니다.");
            return;
        }

        AfterMove();
    }

    private void TakeAll()
    {
        if (other == null)
            return;

        int moved = other.TakeAllTo(PlayerInventory.EnsureInstance().Bag);

        GameLogger.Log($"[ExchangeWindowUI] {moved}칸 회수");

        // 다 집었으면 창을 닫는다. 빈 창을 보고 있을 이유가 없다.
        if (mode == Mode.Loot && other.IsEmpty)
        {
            AfterMove();
            Close();
            return;
        }

        AfterMove();
    }

    private void AfterMove()
    {
        PlayerInventory.Instance.RefreshCapacity();

        Refresh();

        // 왼쪽 가방도 같이 갱신한다 — 방금 넣은 것이 바로 보여야 한다.
        InventoryScreenUI.RefreshIfOpen();
    }
}
