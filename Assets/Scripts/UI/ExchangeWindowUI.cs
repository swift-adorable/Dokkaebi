using System.Collections.Generic;
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
/// 같은 격자·같은 상세를 두 벌 만들 이유가 없고, 플레이어도 한 번만 배우면 된다.
/// 이 컴포넌트는 오른쪽 한 칸만 맡는다.
///
/// 【창고 · 잡화 상점도 이 패널이다.】 (로드맵 8-I)
/// 오른쪽 칸의 출처만 바뀐다 — 전리품은 LootContainer, 창고는 창고 Inventory,
/// 상점은 ShopTable의 줄들. 가방 쪽에서 「창고에 넣기」 · 「판매」를 누르는 것은
/// 가방 화면(InventoryScreenUI)의 사이드 메뉴가 이 패널의 모드를 보고 정한다.
/// </summary>
public class ExchangeWindowUI : MonoBehaviour
{
    /// <summary>이 패널이 무엇을 하는 중인가. 방향과 버튼 이름만 달라진다.</summary>
    public enum Mode
    {
        /// <summary>파밍. 【가져오기만 한다.】 시체에 물건을 도로 넣는 조작은 없다.</summary>
        Loot = 0,

        /// <summary>창고. 양방향. 맡기고 찾는다.</summary>
        Stash = 1,

        /// <summary>잡화 상점. 이쪽 칸은 사고, 가방 칸은 판다.</summary>
        Shop = 2
    }

    private const int Columns = 5;

    /// <summary>격자가 최소한 이만큼은 줄을 그린다. 빈 칸이 곧 남은 자리다.</summary>
    private const int MinRows = 2;

    // ── 자리 (안전 영역 기준 0~1) ─────────────────────────────────────
    // 가운데에 뜬다 (결정 2-82) — 왼쪽은 착용 장비, 오른쪽 끝은 가방 기둥이 쓴다.

    private const float PanelLeft = 0.31f;
    private const float PanelRight = 0.69f;
    private const float PanelBottom = 0.30f;
    private const float PanelTop = 0.965f;

    private static ExchangeWindowUI instance;

    private GameObject panel;
    private Text titleLabel;
    private Button takeAllButton;
    private Text footLabel;

    private UIFactory.ScrollList grid;

    private Mode mode = Mode.Loot;
    private CorpseController source;
    private LootContainer other;

    /// <summary>
    /// 【겹친 시체를 한 창에】 (결정 2-77) — 파밍을 누른 시체 둘레(MergeRadius)의 시체 전리품을 함께 보여 준다.
    /// 칸 번호는 lootCells(상자 · 상자 안 칸)를 거쳐 원래 상자로 간다.
    /// </summary>
    private readonly List<CorpseController> sources = new();
    private readonly List<(LootContainer box, int index)> lootCells = new();

    /// <summary>함께 여는 시체의 거리 (m).</summary>
    public const float MergeRadius = 2.5f;
    private Inventory stash;
    private ShopKind shopKind = ShopKind.General;
    private string otherName = "전리품";

    public bool IsOpen => panel != null && panel.activeSelf;

    /// <summary>이 모드로 열려 있는가. 가방 화면이 사이드 메뉴 줄을 고를 때 쓴다.</summary>
    /// <summary>
    /// 물건을 사 주는 상점이 열려 있는가 — 지금은 세 상점 모두다(ShopTable.BuysFromPlayer).
    /// 가방 칸의 「판매」 줄이 이것을 본다.
    /// </summary>
    public static bool IsBuyingShopOpen
        => IsOpenIn(Mode.Shop) && ShopTable.BuysFromPlayer(instance.shopKind);

    public static bool IsOpenIn(Mode windowMode)
        => instance != null && instance.IsOpen && instance.mode == windowMode;

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

        // 【제목 뒤에 띠를 깔지 않는다.】 유리판 위에 색 띠를 하나 더 얹으면
        // 판이 두 겹으로 보이고, 위쪽 광택과 겹쳐 모서리가 지저분해진다.
        // 제목은 글자만으로 충분히 읽힌다.
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

        // 상점에는 「전부」 버튼이 없다. 그 자리에 재고가 언제 차는지를 적는다.
        footLabel = UIFactory.CreateLabel(box, "파밍이 끝나면 재고가 다시 찹니다.", 20,
            FontStyle.Normal, new Vector2(0.04f, 0.035f), new Vector2(0.96f, 0.130f),
            TextAnchor.MiddleCenter, UIPalette.Subtle);

        panel.SetActive(false);
    }

    // ────────────────────────────────── 열고 닫기

    /// <summary>시체·상자를 파밍한다.</summary>
    public void Open(CorpseController corpse)
    {
        if (corpse == null)
            return;

        source = corpse;

        // 둘레의 다른 시체도 함께 — 먼저 누른 시체가 맨 앞이다.
        sources.Clear();
        sources.Add(corpse);

        foreach (CorpseController near in FindObjectsByType<CorpseController>(FindObjectsInactive.Exclude))
        {
            if (near == null || near == corpse || !near.HasLoot)
                continue;

            Vector3 d = near.transform.position - corpse.transform.position;
            d.y = 0f;

            if (d.sqrMagnitude <= MergeRadius * MergeRadius)
                sources.Add(near);
        }

        Open(corpse.Loot, sources.Count > 1 ? $"전리품 (시체 {sources.Count})" : "전리품", Mode.Loot);
    }

    /// <summary>지금 열려 있는 전리품 상자들. 겹친 시체가 없으면 하나뿐이다.</summary>
    private IEnumerable<LootContainer> Boxes
    {
        get
        {
            if (mode == Mode.Loot && sources.Count > 0)
            {
                foreach (CorpseController c in sources)
                    if (c != null)
                        yield return c.Loot;
            }
            else if (other != null)
            {
                yield return other;
            }
        }
    }

    private bool AllLootEmpty
    {
        get
        {
            foreach (LootContainer box in Boxes)
                if (!box.IsEmpty)
                    return false;
            return true;
        }
    }

    /// <summary>임의의 상자를 연다. 창고가 8단계에 이 길로 들어온다.</summary>
    public void Open(LootContainer container, string label, Mode windowMode)
    {
        if (container == null)
            return;

        if (source == null || container != source.Loot)
            sources.Clear();

        other = container;
        stash = null;
        otherName = string.IsNullOrEmpty(label) ? "상대" : label;

        OpenPanel(windowMode);
    }

    /// <summary>창고를 연다. 벙커가 생기면(8단계 뒤쪽) 보관고가 이 길을 부른다.</summary>
    public void OpenStash()
    {
        sources.Clear();
        source = null;
        other = null;
        stash = PlayerInventory.EnsureInstance().Stash;
        otherName = "창고";

        OpenPanel(Mode.Stash);
    }

    /// <summary>상점을 연다. 【종류별로 나뉜다】 — 잡화 · 무기 · 방어구 (8-K).</summary>
    public void OpenShop(ShopKind kind = ShopKind.General)
    {
        sources.Clear();
        source = null;
        other = null;
        stash = null;
        shopKind = kind;
        otherName = ShopTable.NameOf(kind);

        OpenPanel(Mode.Shop);
    }

    private void OpenPanel(Mode windowMode)
    {
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

        ItemActionMenu.Close();

        panel.SetActive(false);

        // 다 집었으면 시체를 정리한다. 남아 있으면 그대로 두어
        // 나중에 돌아와 마저 집을 수 있게 한다.
        if (mode == Mode.Loot)
            DespawnEmptySources();

        sources.Clear();
        source = null;
        other = null;
        stash = null;

        // 왼쪽 패널도 같이 닫는다. 파밍이 끝났는데 가방만 남아 있으면
        // 「무엇을 닫는 중인지」가 헷갈린다.
        if (InventoryScreenUI.HasInstance && InventoryScreenUI.Instance.IsOpen)
            InventoryScreenUI.Instance.Close();

        // 감춘 것을 되돌린다. Close보다 뒤에 둬야 한다 —
        // 가방 화면의 Close가 이 값을 읽어 버튼을 다시 그린다.
        InventoryScreenUI.SetHudSuppressed(false);

        closing = false;
    }

    /// <summary>다 집은 시체를 정리한다 — 남은 것이 있으면 그대로 두어 나중에 마저 집게 한다.</summary>
    private void DespawnEmptySources()
    {
        var absorber = FindAnyObjectByType<PlayerAbsorber>(FindObjectsInactive.Include);

        if (absorber == null)
            return;

        if (sources.Count == 0 && source != null && other != null && other.IsEmpty)
        {
            absorber.Despawn(source);
            return;
        }

        foreach (CorpseController c in sources)
            if (c != null && !c.HasLoot)
                absorber.Despawn(c);
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

    private bool HasSource
        => mode switch
        {
            Mode.Loot => other != null,
            Mode.Stash => stash != null,
            _ => true
        };

    private void Refresh()
    {
        if (!HasSource)
            return;

        switch (mode)
        {
            case Mode.Stash:
                titleLabel.text = $"{otherName} ({stash.UsedSlots}/{stash.SlotCapacity})";
                break;

            case Mode.Shop:
                titleLabel.text = $"{otherName} · {PassiveManager.EnsureInstance().Gold:N0}엽전";
                break;

            default:
                titleLabel.text = $"{otherName} ({other.UsedSlots}/{other.Capacity})";
                break;
        }

        takeAllButton.gameObject.SetActive(mode != Mode.Shop);
        footLabel.gameObject.SetActive(mode == Mode.Shop);

        var label = takeAllButton.GetComponentInChildren<Text>();

        if (label != null)
            label.text = mode == Mode.Stash ? "전부 꺼내기" : "전부 줍기";

        takeAllButton.interactable = mode == Mode.Stash
            ? stash.Stacks.Count > 0
            : mode == Mode.Loot && !AllLootEmpty;

        // 칸 크기를 픽셀로 환산하려면 실제 크기가 확정되어 있어야 한다.
        // 다시 그리면 사이드 메뉴가 가리키던 칸이 사라진다.
        ItemActionMenu.Close();

        Canvas.ForceUpdateCanvases();

        DrawGrid();
    }

    private void DrawGrid()
    {
        UIFactory.ClearChildren(grid.Content);

        if (mode == Mode.Shop)
        {
            DrawShopGrid();
            return;
        }

        // 창고는 칸 수보다 물건 줄이 많을 수 있다 — 젬은 칸을 먹지 않고,
        // 패시브가 줄어 칸이 모자라도 이미 든 것은 그대로다. 둘 다 보여야 한다.
        IReadOnlyList<ItemStack> stashStacks = mode == Mode.Stash ? stash.Stacks : null;

        // 전리품은 열린 상자들의 물건을 이어 붙인다 (겹친 시체 — 결정 2-77).
        if (mode == Mode.Loot)
        {
            lootCells.Clear();
            foreach (LootContainer box in Boxes)
                for (int b = 0; b < box.Capacity; b++)
                    if (box.Get(b) != null)
                        lootCells.Add((box, b));
        }

        int cells = mode == Mode.Stash
            ? Mathf.Max(stash.SlotCapacity, stashStacks.Count)
            : Mathf.Max(other.Capacity, lootCells.Count);

        UIFactory.SquareGridMetrics(grid.Viewport, Columns, cells, MinRows,
            out int rows, out float height, out float padX, out float padY);

        grid.Content.sizeDelta = new Vector2(0f, height);

        // 【용량만큼 전부 그린다.】 잘라내면 그 칸의 물건은 보이지도 눌리지도 않는다.
        for (int i = 0; i < rows * Columns; i++)
        {
            UIFactory.GetCellAnchors(i, Columns, rows, padX, padY,
                out Vector2 min, out Vector2 max);

            ItemStack stack = mode == Mode.Stash
                ? (i < stashStacks.Count ? stashStacks[i] : null)
                : (i < lootCells.Count ? lootCells[i].box.Get(lootCells[i].index) : null);

            int captured = i;

            // 【누르면 칸 옆에 사이드 메뉴가 뜬다.】 바로 줍지 않는다.
            // 한 번 눌러 바로 줍게 해 뒀더니 「줍기」 말고는 아무것도 할 수
            // 없었다. 총을 주울지, 먼저 볼지, 그 자리에서 갈아 끼울지를
            // 고를 수 없으면 창이 버튼 하나짜리 목록이 된다.
            Image cell = ItemCell.Draw($"Loot_{i}", grid.Content, min, max, stack,
                chosen: false, null);

            if (stack?.Definition == null)
                continue;

            var button = cell.GetComponent<Button>();

            if (button == null)
                continue;

            RectTransform cellRect = cell.rectTransform;
            ItemStack capturedStack = stack;

            button.onClick.AddListener(() => OpenMenu(cellRect, captured, capturedStack));
        }
    }

    /// <summary>
    /// 상점 칸. 【팔지 않게 된 것도 자리를 지킨다】 — 품절이 되면 칸이 사라지는
    /// 대신 흐려진다. 자리가 바뀌면 「아까 그거 어디 갔지」를 매번 다시 찾는다.
    /// </summary>
    private void DrawShopGrid()
    {
        ItemCatalog catalog = ItemCatalog.Load();
        IReadOnlyList<ShopEntry> entries = ShopTable.For(shopKind);

        UIFactory.SquareGridMetrics(grid.Viewport, Columns, entries.Count, MinRows,
            out int rows, out float height, out float padX, out float padY);

        grid.Content.sizeDelta = new Vector2(0f, height);

        for (int i = 0; i < rows * Columns; i++)
        {
            UIFactory.GetCellAnchors(i, Columns, rows, padX, padY,
                out Vector2 min, out Vector2 max);

            ItemDefinition definition = i < entries.Count && catalog != null
                ? catalog.Find(entries[i].ItemId)
                : null;

            // 한 개짜리로 그린다 — 재고 수는 아래 표로 따로 붙인다.
            // 겹치지 않는 물건(환단)은 개수를 셀 수 없어서다.
            ItemStack shown = definition != null ? new ItemStack(definition) : null;

            Image cell = ItemCell.Draw($"Shop_{i}", grid.Content, min, max, shown,
                chosen: false, null);

            if (definition == null)
                continue;

            ShopEntry entry = entries[i];
            int remaining = ShopManager.Of(shopKind).Remaining(entry.ItemId);

            UIFactory.CreateBadge(cell.transform, remaining > 0 ? $"×{remaining}" : "품절",
                new Vector2(0.56f, 0.06f), new Vector2(0.96f, 0.30f), 20,
                remaining > 0 ? Color.white : UIPalette.Warning);

            if (remaining <= 0)
                cell.gameObject.AddComponent<CanvasGroup>().alpha = 0.45f;

            var button = cell.GetComponent<Button>();

            if (button == null)
                continue;

            RectTransform cellRect = cell.rectTransform;

            button.onClick.AddListener(() => OpenShopMenu(cellRect, definition, entry));
        }
    }

    // ────────────────────────────────── 사이드 메뉴

    /// <summary>
    /// 칸의 사이드 메뉴를 연다. 【무엇을 넣을지는 아이템 성격이 정한다.】
    ///
    /// 「사용」은 아직 넣지 않는다 — 소모품을 쓰는 시스템 자체가 8단계다.
    /// 누르면 아무 일도 없는 버튼을 두는 것보다 없는 편이 낫다.
    /// </summary>
    private void OpenMenu(RectTransform cell, int index, ItemStack stack)
    {
        ItemDefinition definition = stack.Definition;

        bool equippable = definition is EquipmentDefinition;

        var entries = ItemActionMenu.ForContainerItem(
            definition,
            take: () => AskTake(index, stack),
            equip: equippable ? () => TakeAndEquip(index, stack) : null,
            use: null,
            detail: () => InventoryScreenUI.OpenReadOnlyDetail(stack, otherName),
            takeLabel: mode == Mode.Stash ? "꺼내기" : "줍기");

        ItemActionMenu.Open(cell, entries);
    }

    /// <summary>
    /// 상점 칸의 사이드 메뉴 — 「구매」와 「상세보기」.
    /// 【값을 줄에 적는다.】 누르기 전에 얼마가 빠지는지 알아야 한다.
    /// 못 사는 이유가 있으면 줄을 흐리게 두고, 눌렀을 때 이유를 알린다.
    /// </summary>
    private void OpenShopMenu(RectTransform cell, ItemDefinition definition, ShopEntry entry)
    {
        int price = TradeRules.BuyPrice(definition, entry, ShopManager.BuyDiscountPercent);

        TradeError error = TradeRules.CanBuy(definition, entry,
            ShopManager.Of(shopKind).Remaining(entry.ItemId),
            PassiveManager.EnsureInstance().Gold,
            PlayerInventory.EnsureInstance().Bag,
            ShopManager.BuyDiscountPercent);

        var entries = new List<ItemActionMenu.Entry>
        {
            new($"구매 {price:N0}엽전", UIPalette.Action, () => AskBuy(definition, entry, price), error == TradeError.None),
            new("상세보기", UIPalette.Subtle,
                () => InventoryScreenUI.OpenReadOnlyDetail(new ItemStack(definition), otherName))
        };

        ItemActionMenu.Open(cell, entries);

        if (error != TradeError.None)
            InventoryScreenUI.ShowToastIfOpen(TradeRules.Explain(error));
    }

    private void Buy(ItemDefinition definition)
    {
        TradeError error = ShopManager.Buy(shopKind, definition);

        InventoryScreenUI.ShowToastIfOpen(error == TradeError.None
            ? $"구매 — 「{definition.DisplayName}」"
            : TradeRules.Explain(error));

        AfterMove();
    }

    /// <summary>
    /// 그 자리에서 갈아 끼운다. 가방을 거쳐 간다 —
    /// 장착 경로가 「가방에 있는 것」을 전제로 하고, 벗은 장비가 갈 곳도 가방이다.
    ///
    /// 가방이 꽉 차 있어도 대부분 통한다. 끼울 것이 빠지고 벗은 것이
    /// 들어오므로 칸 수가 그대로이기 때문이다.
    /// </summary>
    private void TakeAndEquip(int index, ItemStack stack)
    {
        if (!TakeInto(index, stack))
            return;

        InventoryScreenUI.EquipFromOutside(stack);

        AfterMove();
    }

    /// <summary>가방으로 옮긴다. 자리가 없으면 알리고 false.</summary>
    private bool TakeInto(int index, ItemStack stack = null)
    {
        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        if (mode == Mode.Stash)
        {
            if (stash != null && Inventory.MoveStack(stash, bag, stack))
                return true;

            InventoryScreenUI.ShowToastIfOpen("가방에 자리가 없습니다.");
            return false;
        }

        if (index < 0 || index >= lootCells.Count)
            return false;

        (LootContainer box, int local) = lootCells[index];

        if (box == null || box.Get(local) == null)
            return false;

        if (box.TryTakeTo(local, bag))
            return true;

        // 못 옮기면 아이템은 있던 자리에 그대로 남는다. 사라지지 않는다.
        InventoryScreenUI.ShowToastIfOpen("가방에 자리가 없습니다.");

        return false;
    }

    // ────────────────────────────────── 옮기기

    private void Take(int index, ItemStack stack)
    {
        if (TakeInto(index, stack))
            AfterMove();
    }

    /// <summary>【수량을 고른다】 (결정 2-77) — 겹친 물건은 몇 개를 꺼낼지 · 주울지 묻는다.</summary>
    private void AskTake(int index, ItemStack stack)
    {
        if (stack == null || stack.Count <= 1)
        {
            Take(index, stack);
            return;
        }

        string verb = mode == Mode.Stash ? "꺼내기" : "줍기";
        QuantityPopupUI.Ask(stack.Definition.DisplayName, verb, stack.Count,
            n => $"{stack.Definition.WeightCost * n:0.0} kg",
            n => TakeCount(index, stack, n));
    }

    private void TakeCount(int index, ItemStack stack, int count)
    {
        Inventory bag = PlayerInventory.EnsureInstance().Bag;
        bool moved;

        if (mode == Mode.Stash)
        {
            moved = QuantityTransfer.Move(stash, bag, stack, count);
        }
        else
        {
            moved = index >= 0 && index < lootCells.Count
                    && QuantityTransfer.Take(lootCells[index].box, lootCells[index].index, bag, count);
        }

        if (!moved)
            InventoryScreenUI.ShowToastIfOpen("가방에 자리가 없습니다.");

        AfterMove();
    }

    /// <summary>몇 개를 살지 고른다 — 재고 · 엽전이 허락하는 만큼까지.</summary>
    private void AskBuy(ItemDefinition definition, ShopEntry entry, int price)
    {
        int remaining = ShopManager.Of(shopKind).Remaining(entry.ItemId);
        int affordable = price > 0 ? PassiveManager.EnsureInstance().Gold / price : remaining;
        int max = Mathf.Min(remaining, affordable);

        if (!definition.IsStackable)
            max = Mathf.Min(max, 1);

        QuantityPopupUI.Ask(definition.DisplayName, "사기", Mathf.Max(1, max),
            n => $"−{price * n:N0}엽전",
            n => BuyCount(definition, n));
    }

    private void BuyCount(ItemDefinition definition, int count)
    {
        int bought = 0;
        TradeError last = TradeError.None;

        for (int i = 0; i < count; i++)
        {
            last = ShopManager.Buy(shopKind, definition);
            if (last != TradeError.None)
                break;
            bought++;
        }

        InventoryScreenUI.ShowToastIfOpen(bought > 0
            ? $"구매 — 「{definition.DisplayName}」 ×{bought}" + (bought < count ? $" ({TradeRules.Explain(last)})" : string.Empty)
            : TradeRules.Explain(last));

        AfterMove();
    }

    private void TakeAll()
    {
        Inventory bag = PlayerInventory.EnsureInstance().Bag;
        int moved = 0;

        if (mode == Mode.Stash && stash != null)
        {
            // 뒤에서부터 — 옮기면 목록이 줄어든다.
            for (int i = stash.Stacks.Count - 1; i >= 0; i--)
            {
                if (Inventory.MoveStack(stash, bag, stash.Stacks[i]))
                    moved++;
            }
        }
        else
        {
            foreach (LootContainer box in Boxes)
                moved += box.TakeAllTo(bag);
        }

        GameLogger.Log($"[ExchangeWindowUI] {moved}칸 회수");

        // 다 집었으면 AfterMove가 알아서 닫는다.
        AfterMove();
    }

    /// <summary>
    /// 가방 칸을 창고에 넣는다. 가방 화면의 사이드 메뉴 「창고에 넣기」가 부른다.
    /// </summary>
    public static void PutIntoStash(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty)
            return;

        // 겹친 물건은 몇 개를 넣을지 고른다 (결정 2-77).
        QuantityPopupUI.Ask(stack.Definition.DisplayName, "넣기", stack.Count, null, n =>
        {
            Inventory bag = PlayerInventory.EnsureInstance().Bag;

            if (!QuantityTransfer.Move(bag, PlayerInventory.Instance.Stash, stack, n))
                InventoryScreenUI.ShowToastIfOpen("창고에 자리가 없습니다.");

            RefreshAfterBagChange();
        });
    }

    /// <summary>가방 칸을 판다. 가방 화면의 사이드 메뉴 「판매」가 부른다.</summary>
    public static void SellFromBag(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty)
            return;

        string name = stack.Definition.DisplayName;
        float bonus = ShopManager.SellBonusPercent;

        // 겹친 물건은 몇 개를 팔지 고른다 (결정 2-77).
        QuantityPopupUI.Ask(name, "팔기", stack.Count,
            n => $"+{QuantityTransfer.SellPrice(stack, n, bonus):N0}엽전",
            n =>
            {
                TradeError error = ShopManager.Sell(stack, n, out int earned);

                InventoryScreenUI.ShowToastIfOpen(error == TradeError.None
                    ? $"판매 — 「{name}」 ×{n} +{earned:N0}엽전"
                    : TradeRules.Explain(error));

                RefreshAfterBagChange();
            });
    }

    private static void RefreshAfterBagChange()
    {
        if (instance != null && instance.IsOpen)
        {
            instance.AfterMove();
            return;
        }

        PlayerInventory.EnsureInstance().RefreshCapacity();
        InventoryScreenUI.RefreshIfOpen();
    }

    private void AfterMove()
    {
        PlayerInventory.EnsureInstance().RefreshCapacity();

        // 왼쪽 가방도 같이 갱신한다 — 방금 넣은 것이 바로 보여야 한다.
        InventoryScreenUI.RefreshIfOpen();

        // 【하나씩 주워 다 비워도 닫힌다.】 「전부 줍기」에만 있던 규칙이라,
        // 마지막 한 칸을 손으로 집으면 빈 창이 남아 있었다.
        if (mode == Mode.Loot && other != null && AllLootEmpty)
        {
            Close();
            return;
        }

        Refresh();
    }
}
