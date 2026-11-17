using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【가게 화면】 (결정 2-121 · 2-123) — 사용자 참고 그림(2026-10-13)의 배치. 틀은 StationFrame — 시설 화면들과 같은 자리 · 같은 비율.
/// 떠 있는 동안 게임이 멈춘다(결정 2-92).
///
///   · 왼쪽: 종류 탭(결정 2-120) + 물건 줄 — 그림 · 이름 · 값(못 사면 붉게) · 남은 재고(노랑).
///     품절은 어둡고 「구매 불가」 + 붉은 이유. 줄을 누르면 담기 · 바로 사기 · 빼기 · 상세보기.
///   · 가운데: 주인(이름표 · 본문 대사 말풍선) · 계산대 위 담은 것 여섯 칸씩(쪽 점) · 「합계 / 보유」 · 막히면 붉은 이유
///   · 오른쪽: 「내 가방」 — 팔 수 있는 것은 판매 · 상세보기, 팔 수 없는 것은 어둡게 (사용자) · 아래 「비우기 · 구매」
/// </summary>
public class ShopScreenUI : MonoBehaviour
{
    private static ShopScreenUI instance;

    public static bool IsOpen => instance != null && instance.frame != null && instance.frame.IsOpen;

    private const int CounterSlots = 6;
    private const float IconInset = 9f;

    /// <summary>계산대 칸 한 변의 최대 (화소) — 앞판이 넓어도 담은 물건 칸이 커지지 않게.</summary>
    private const float CounterSlotMax = 84f;

    private StationFrame frame;
    private RectTransform counterSlots;
    private RectTransform counterDots;
    private Text totalLabel;
    private Text ownedLabel;
    private Button clearButton;
    private Button buyButton;
    private Text buyLabel;

    private ShopKind kind = ShopKind.General;
    private readonly ShopCart cart = new();
    private int shopTab;
    private readonly List<ItemKind> kinds = new();
    private int counterPage;
    private bool opening;

    /// <summary>가게를 연다 — 흐린 뒤 화면을 찍고 한 프레임 뒤에 뜬다.</summary>
    public static void Open(ShopKind shop)
    {
        ShopScreenUI ui = EnsureInstance();
        if (ui.opening)
            return;

        ui.kind = shop;
        ui.cart.Clear();
        ui.shopTab = 0;
        ui.counterPage = 0;
        ui.opening = true;
        ui.StartCoroutine(ui.frame.Show(() =>
        {
            ui.opening = false;
            ui.Refresh();
        }));
    }

    public static void Close()
    {
        if (instance != null)
            instance.Hide();
    }

    /// <summary>바깥(가방 · 상세)에서 무엇이 바뀌었을 때.</summary>
    public static void RefreshIfOpen()
    {
        if (IsOpen)
            instance.Refresh();
    }

    private static ShopScreenUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        // 가방(1000)보다 위, 상세(1100) · 사이드 메뉴(1200) · 수량(1300)보다 아래.
        Canvas canvas = UIFactory.CreateCanvas("ShopScreenCanvas (Runtime)", 1050);
        instance = canvas.gameObject.AddComponent<ShopScreenUI>();
        instance.Build(canvas);
        return instance;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Hide()
    {
        if (!frame.IsOpen)
            return;

        cart.Clear();
        frame.Hide();
    }

    private void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            Hide();
    }

    // ────────────────────────────────── 만들기

    private void Build(Canvas canvas)
    {
        frame = StationFrame.Build(canvas, Hide);
        frame.UseCounterAsNote(false);

        counterSlots = UIFactory.CreateRegion("Slots", frame.Counter, new Vector2(0.03f, 0.24f), new Vector2(0.97f, 0.92f));
        counterDots = UIFactory.CreateRegion("Dots", frame.Counter, new Vector2(0.3f, 0.04f), new Vector2(0.7f, 0.18f));

        RectTransform box = frame.TotalsBox;
        UIFactory.CreateLabel(box, "합계", 24, FontStyle.Bold,
            new Vector2(0.08f, 0.5f), new Vector2(0.4f, 1f), TextAnchor.MiddleLeft, Color.white);
        UIFactory.CreateLabel(box, "보유", 24, FontStyle.Bold,
            new Vector2(0.08f, 0f), new Vector2(0.4f, 0.5f), TextAnchor.MiddleLeft, Color.white);

        foreach (float y in new[] { 0.75f, 0.25f })
        {
            Image coin = UIFactory.CreatePanel("Coin", box, StationFrame.Coin, new Vector2(0.44f, y), new Vector2(0.44f, y), 0);
            coin.sprite = UISprites.Of(UISprites.Glyph.Circle);
            coin.rectTransform.sizeDelta = new Vector2(20f, 20f);
            coin.raycastTarget = false;
        }

        totalLabel = UIFactory.CreateLabel(box, "0", 24, FontStyle.Bold,
            new Vector2(0.5f, 0.5f), new Vector2(0.92f, 1f), TextAnchor.MiddleRight, StationFrame.Coin);
        ownedLabel = UIFactory.CreateLabel(box, "0", 24, FontStyle.Bold,
            new Vector2(0.5f, 0f), new Vector2(0.92f, 0.5f), TextAnchor.MiddleRight, Color.white);

        clearButton = StationFrame.Pill(frame.Buttons, "비우기", new Vector2(0f, 0f), new Vector2(0.36f, 1f), StationFrame.PillDark, () =>
        {
            cart.Clear();
            counterPage = 0;
            Refresh();
        });

        buyButton = StationFrame.Pill(frame.Buttons, "구매", new Vector2(0.39f, 0f), new Vector2(1f, 1f), StationFrame.PillAction, BuyCart, 30);
        buyLabel = buyButton.GetComponentInChildren<Text>();

        // 여러 개 골라 한 번에 팔기 — 고른 묶음은 통째로 판다.
        frame.EnableMultiSelect(
            chosen => $"{chosen.Count}개 팔기  +{SellTotal(chosen):N0}엽전",
            SellAll);
    }

    // ────────────────────────────────── 다시 그리기

    private void Refresh()
    {
        if (!frame.IsOpen)
            return;

        ItemActionMenu.Close();

        frame.Title.text = ShopTable.NameOf(kind);
        frame.SetKeeper(KeeperTable.OfShop(kind));

        int gold = PassiveManager.EnsureInstance().Gold;
        frame.SetGold(gold);

        Canvas.ForceUpdateCanvases();
        DrawTabs();
        DrawList(gold);
        DrawCounter();
        DrawTotals(gold);

        Inventory bag = PlayerInventory.EnsureInstance().Bag;
        frame.DrawBag(stack => TradeRules.CanSell(stack, ShopManager.SellBonusPercent) == TradeError.None, OpenBagMenu,
            $"무게 {bag.TotalWeight:0.0} / {bag.WeightLimit:0.0} kg · 팔면 가치의 절반");
    }

    private void DrawTabs()
    {
        ItemCatalog catalog = ItemCatalog.Load();
        var present = new List<ItemKind>();

        foreach (ShopEntry e in ShopManager.EntriesOf(kind))
        {
            ItemDefinition d = catalog != null ? catalog.Find(e.ItemId) : null;
            if (d != null)
                present.Add(d.Kind);
        }

        kinds.Clear();
        kinds.AddRange(ShopTabs.KindsOf(present));

        // 종류가 하나뿐이어도 「구매」 탭 하나는 둔다 — 참고 그림의 맨 위 탭.
        bool split = ShopTabs.Shows(kinds);
        if (!split || shopTab > kinds.Count)
            shopTab = 0;

        var names = new List<string> { split ? "전체" : "구매" };
        if (split)
            foreach (ItemKind k in kinds)
                names.Add(ShopTabs.Name(k));

        frame.DrawTabs(names, shopTab, index =>
        {
            shopTab = index;
            Refresh();
        });
    }

    /// <summary>
    /// 왼쪽 목록. 【품절도 자리를 지킨다】 — 어둡게 「구매 불가」와 이유. 값은 남은 엽전(담은 합계를 뺀 것)으로 못 사면 붉다.
    /// </summary>
    private void DrawList(int gold)
    {
        UIFactory.ClearChildren(frame.List.Content);

        ItemCatalog catalog = ItemCatalog.Load();
        ItemKind? only = shopTab > 0 && shopTab <= kinds.Count ? kinds[shopTab - 1] : null;
        int budget = gold - CartRules.Total(ShopManager.Resolve(kind, cart), ShopManager.BuyDiscountPercent);

        int row = 0;
        foreach (ShopEntry entry in ShopManager.EntriesOf(kind))
        {
            ItemDefinition definition = catalog != null ? catalog.Find(entry.ItemId) : null;
            if (definition == null || (only.HasValue && definition.Kind != only.Value))
                continue;

            DrawRow(row++, definition, entry, budget);
        }

        frame.List.Content.sizeDelta = new Vector2(0f, row * (StationFrame.RowHeight + StationFrame.RowGap));
    }

    private void DrawRow(int index, ItemDefinition definition, ShopEntry entry, int budget)
    {
        int remaining = ShopManager.Of(kind).Remaining(entry.ItemId);
        int inCart = cart.CountOf(entry.ItemId);
        int price = TradeRules.BuyPrice(definition, entry, ShopManager.BuyDiscountPercent);
        bool soldOut = remaining <= 0;

        Image row = frame.Row(index, soldOut);
        var button = row.gameObject.AddComponent<Button>();
        button.targetGraphic = row;
        RectTransform rowRect = row.rectTransform;
        button.onClick.AddListener(() => OpenRowMenu(rowRect, definition, entry));

        // 그림 칸 — 줄 높이에 맞춘 정사각형, 위아래 같은 여백.
        float icon = StationFrame.RowHeight - IconInset * 2f;
        Image cell = ItemCell.Draw("Icon", row.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new ItemStack(definition), chosen: false, null);
        cell.rectTransform.pivot = new Vector2(0f, 0.5f);
        cell.rectTransform.sizeDelta = new Vector2(icon, icon);
        cell.rectTransform.anchoredPosition = new Vector2(IconInset, 0f);
        cell.raycastTarget = false;
        if (cell.TryGetComponent(out Button cellButton))
            cellButton.enabled = false;

        float left = IconInset * 2f + icon;

        // 세 줄 — 같은 간격(위 · 가운데 · 아래 1/3씩).
        Text name = UIFactory.CreateLabel(row.transform, definition.DisplayName, 22, FontStyle.Bold,
            new Vector2(0f, 0.64f), new Vector2(1f, 0.94f), TextAnchor.MiddleLeft, soldOut ? UIPalette.TextDim : Color.white);
        name.rectTransform.offsetMin = new Vector2(left, 0f);
        name.rectTransform.offsetMax = new Vector2(inCart > 0 ? -94f : -12f, 0f);

        Image coin = UIFactory.CreatePanel("Coin", row.transform, StationFrame.Coin, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), 0);
        coin.sprite = UISprites.Of(UISprites.Glyph.Circle);
        coin.rectTransform.sizeDelta = new Vector2(16f, 16f);
        coin.rectTransform.anchoredPosition = new Vector2(left + 8f, 0f);
        coin.raycastTarget = false;

        Text cost = UIFactory.CreateLabel(row.transform, $"{price:N0}", 21, FontStyle.Bold,
            new Vector2(0f, 0.35f), new Vector2(1f, 0.65f), TextAnchor.MiddleLeft,
            price > budget ? UIPalette.Warning : StationFrame.Coin);
        cost.rectTransform.offsetMin = new Vector2(left + 22f, 0f);

        string third = soldOut
            ? "재고 없음 — 파밍이 끝나면 채워집니다"
            : ShopManager.IsDaily(kind, entry.ItemId) ? $"오늘의 물건 · 남은 재고 {remaining}" : $"남은 재고 {remaining}";

        Text stock = UIFactory.CreateLabel(row.transform, third, 16, FontStyle.Normal,
            new Vector2(0f, 0.06f), new Vector2(1f, 0.35f), TextAnchor.MiddleLeft,
            soldOut ? UIPalette.Warning : StationFrame.Limit);
        stock.rectTransform.offsetMin = new Vector2(left, 0f);

        if (soldOut)
        {
            // 참고 그림처럼 그림 칸 위에 「구매 불가」.
            Image veil = UIFactory.CreatePanel("Veil", cell.transform, new Color(0f, 0f, 0f, 0.62f), Vector2.zero, Vector2.one, UIFactory.Radius);
            veil.raycastTarget = false;
            UIFactory.CreateLabel(veil.transform, "구매\n불가", 17, FontStyle.Bold, Vector2.zero, Vector2.one,
                TextAnchor.MiddleCenter, Color.white);
        }

        if (inCart > 0)
        {
            Text badge = UIFactory.CreateBadge(row.transform, $"담음 {inCart}",
                new Vector2(1f, 0.62f), new Vector2(1f, 0.92f), 16, UIPalette.Gain);
            var pill = (RectTransform)badge.transform.parent;
            pill.offsetMin = new Vector2(-86f, 0f);
            pill.offsetMax = new Vector2(-10f, 0f);
        }
    }

    private void OpenRowMenu(RectTransform rowRect, ItemDefinition definition, ShopEntry entry)
    {
        int price = TradeRules.BuyPrice(definition, entry, ShopManager.BuyDiscountPercent);
        int remaining = ShopManager.Of(kind).Remaining(entry.ItemId);
        int inCart = cart.CountOf(entry.ItemId);

        TradeError error = TradeRules.CanBuy(definition, entry, remaining,
            PassiveManager.EnsureInstance().Gold, PlayerInventory.EnsureInstance().Bag, ShopManager.BuyDiscountPercent);

        var entries = new List<ItemActionMenu.Entry>
        {
            new(inCart > 0 ? $"더 담기 (담음 {inCart})" : "담기", UIPalette.Action,
                () => AskAddToCart(definition, entry, price), remaining - inCart > 0),
            new($"바로 사기 {price:N0}엽전", UIPalette.Subtle, () => AskBuyNow(definition, entry, price), error == TradeError.None),
        };

        if (inCart > 0)
            entries.Add(new ItemActionMenu.Entry("빼기", UIPalette.Subtle, () =>
            {
                cart.Remove(entry.ItemId);
                Refresh();
            }));

        entries.Add(new ItemActionMenu.Entry("상세보기", UIPalette.Subtle,
            () => InventoryScreenUI.OpenReadOnlyDetail(new ItemStack(definition), ShopTable.NameOf(kind))));

        ItemActionMenu.Open(rowRect, entries);
    }

    private void AskAddToCart(ItemDefinition definition, ShopEntry entry, int price)
    {
        int room = ShopManager.Of(kind).Remaining(entry.ItemId) - cart.CountOf(entry.ItemId);
        if (room <= 0)
        {
            Toast("남은 재고를 다 담았습니다.");
            return;
        }

        QuantityPopupUI.Ask(definition.DisplayName, "담기", room,
            n => $"{price * n:N0}엽전",
            n =>
            {
                cart.Add(entry.ItemId, n);
                counterPage = (cart.Lines.Count - 1) / CounterSlots;
                Refresh();
            });
    }

    /// <summary>바로 산다 — 재고 · 엽전이 허락하는 만큼까지 수량을 고른다.</summary>
    private void AskBuyNow(ItemDefinition definition, ShopEntry entry, int price)
    {
        int remaining = ShopManager.Of(kind).Remaining(entry.ItemId);
        int affordable = price > 0 ? PassiveManager.EnsureInstance().Gold / price : remaining;
        int max = Mathf.Min(remaining, affordable);

        if (!definition.IsStackable)
            max = Mathf.Min(max, 1);

        QuantityPopupUI.Ask(definition.DisplayName, "사기", Mathf.Max(1, max),
            n => $"−{price * n:N0}엽전",
            n =>
            {
                int bought = 0;
                TradeError last = TradeError.None;

                for (int i = 0; i < n; i++)
                {
                    last = ShopManager.Buy(kind, definition);
                    if (last != TradeError.None)
                        break;
                    bought++;
                }

                Toast(bought > 0
                    ? $"구매 — 「{definition.DisplayName}」 ×{bought}" + (bought < n ? $" ({TradeRules.Explain(last)})" : string.Empty)
                    : TradeRules.Explain(last));
                AfterTrade();
            });
    }

    /// <summary>계산대 — 담은 것을 여섯 칸씩. 칸을 누르면 빼기 · 상세보기. 여러 쪽이면 아래 점을 눌러 넘긴다.</summary>
    private void DrawCounter()
    {
        UIFactory.ClearChildren(counterSlots);
        UIFactory.ClearChildren(counterDots);

        ItemCatalog catalog = ItemCatalog.Load();
        IReadOnlyList<CartLine> lines = cart.Lines;
        int pages = Mathf.Max(1, (lines.Count + CounterSlots - 1) / CounterSlots);
        counterPage = Mathf.Clamp(counterPage, 0, pages - 1);

        // 정사각형 칸 여섯 — 한 변은 CounterSlotMax까지, 간격은 한 변의 1/6, 여섯 칸 묶음을 앞판 가운데에.
        Rect area = counterSlots.rect;
        float size = Mathf.Min(CounterSlotMax, area.height, area.width / (CounterSlots + (CounterSlots - 1) / 6f));
        float gap = size / 6f;
        float startX = (area.width - (size * CounterSlots + gap * (CounterSlots - 1))) * 0.5f;

        for (int s = 0; s < CounterSlots; s++)
        {
            int i = counterPage * CounterSlots + s;
            CartLine? line = i < lines.Count ? lines[i] : null;
            ItemDefinition d = line.HasValue && catalog != null ? catalog.Find(line.Value.ItemId) : null;

            RectTransform slot = UIFactory.CreateRegion($"Slot_{s}", counterSlots, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            slot.pivot = new Vector2(0f, 0.5f);
            slot.sizeDelta = new Vector2(size, size);
            slot.anchoredPosition = new Vector2(startX + s * (size + gap), 0f);

            if (d == null)
            {
                UIFactory.CreatePanel("Empty", slot, new Color(0f, 0f, 0f, 0.28f), Vector2.zero, Vector2.one, UIFactory.Radius)
                    .raycastTarget = false;
                continue;
            }

            Image cell = ItemCell.Draw("Item", slot, Vector2.zero, Vector2.one, new ItemStack(d), chosen: false, null);
            UIFactory.CreateBadge(cell.transform, $"×{line.Value.Count}",
                new Vector2(0.36f, 0.04f), new Vector2(0.96f, 0.30f), 18, Color.white);

            if (cell.TryGetComponent(out Button button))
            {
                RectTransform cellRect = cell.rectTransform;
                string id = line.Value.ItemId;
                button.onClick.AddListener(() => ItemActionMenu.Open(cellRect, new List<ItemActionMenu.Entry>
                {
                    new("빼기", UIPalette.Action, () =>
                    {
                        cart.Remove(id);
                        Refresh();
                    }),
                    new("상세보기", UIPalette.Subtle,
                        () => InventoryScreenUI.OpenReadOnlyDetail(new ItemStack(d), ShopTable.NameOf(kind))),
                }));
            }
        }

        if (pages <= 1)
            return;

        float dw = 1f / pages;
        for (int p = 0; p < pages; p++)
        {
            int page = p;
            UIFactory.CreateButton(counterDots, string.Empty,
                new Vector2(p * dw + dw * 0.35f, 0.15f), new Vector2((p + 1) * dw - dw * 0.35f, 0.85f),
                p == counterPage ? Color.white : new Color(1f, 1f, 1f, 0.35f),
                () =>
                {
                    counterPage = page;
                    Refresh();
                }, 10, 6).name = $"Page_{p}";
        }
    }

    /// <summary>합계 / 보유 · 막히면 붉은 이유 · 「구매」가 꺼진다.</summary>
    private void DrawTotals(int gold)
    {
        Inventory bag = PlayerInventory.EnsureInstance().Bag;
        List<CartItem> items = ShopManager.Resolve(kind, cart);
        int total = CartRules.Total(items, ShopManager.BuyDiscountPercent);

        totalLabel.text = $"{total:N0}";
        totalLabel.color = total > gold ? UIPalette.Warning : StationFrame.Coin;
        ownedLabel.text = $"{gold:N0}";

        clearButton.interactable = !cart.IsEmpty;

        if (cart.IsEmpty)
        {
            frame.Status.text = string.Empty;
            buyButton.interactable = false;
            buyLabel.text = "구매";
            return;
        }

        TradeError error = CartRules.CanBuy(items, ShopManager.Of(kind).Remaining, gold, bag,
            ShopManager.BuyDiscountPercent, out int failed);

        string name = failed >= 0 && failed < items.Count ? items[failed].Definition?.DisplayName : null;
        frame.Status.text = error == TradeError.None
            ? string.Empty
            : CartRules.Explain(error, total - gold, CartRules.SlotsNeeded(items, bag) - bag.FreeSlots, name);
        frame.Status.color = UIPalette.Warning;

        buyButton.interactable = error == TradeError.None;
        buyLabel.text = $"구매 ({cart.Lines.Count})";
    }

    private void BuyCart()
    {
        int lines = cart.Lines.Count;
        List<CartItem> items = ShopManager.Resolve(kind, cart);
        TradeError error = ShopManager.BuyCart(kind, cart, out int spent, out int failed);

        if (error == TradeError.None)
        {
            counterPage = 0;
            Toast($"구매 — {lines}가지 −{spent:N0}엽전");
        }
        else
        {
            string name = failed >= 0 && failed < items.Count ? items[failed].Definition?.DisplayName : null;
            Toast(CartRules.Explain(error, 0, 0, name));
        }

        AfterTrade();
    }

    /// <summary>내 가방 칸 — 팔 수 있으면 판매 · 상세보기, 없으면 「판매 불가」(이유는 알림) · 상세보기.</summary>
    private void OpenBagMenu(RectTransform cell, ItemStack stack)
    {
        float bonus = ShopManager.SellBonusPercent;
        TradeError error = TradeRules.CanSell(stack, bonus);
        var entries = new List<ItemActionMenu.Entry>();

        if (error == TradeError.None)
        {
            entries.Add(new ItemActionMenu.Entry($"판매 +{TradeRules.SellPrice(stack, bonus):N0}엽전", UIPalette.Gain,
                () => AskSell(stack)));
        }
        else
        {
            entries.Add(new ItemActionMenu.Entry("판매 불가", UIPalette.Subtle, null, false));
            Toast(TradeRules.Explain(error));
        }

        entries.Add(new ItemActionMenu.Entry("상세보기", UIPalette.Subtle,
            () => InventoryScreenUI.OpenReadOnlyDetail(stack, "내 가방")));

        ItemActionMenu.Open(cell, entries);
    }

    private void AskSell(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty)
            return;

        string name = stack.Definition.DisplayName;
        float bonus = ShopManager.SellBonusPercent;

        QuantityPopupUI.Ask(name, "팔기", stack.Count,
            n => $"+{QuantityTransfer.SellPrice(stack, n, bonus):N0}엽전",
            n =>
            {
                TradeError error = ShopManager.Sell(stack, n, out int earned);
                Toast(error == TradeError.None ? $"판매 — 「{name}」 ×{n} +{earned:N0}엽전" : TradeRules.Explain(error));
                AfterTrade();
            });
    }

    private static int SellTotal(List<ItemStack> stacks)
    {
        float bonus = ShopManager.SellBonusPercent;
        int sum = 0;
        foreach (ItemStack stack in stacks)
            if (stack != null && !stack.IsEmpty)
                sum += QuantityTransfer.SellPrice(stack, stack.Count, bonus);
        return sum;
    }

    /// <summary>고른 묶음을 통째로 판다. 못 판 것은 남기고 몇 개를 못 팔았는지 알린다.</summary>
    private void SellAll(List<ItemStack> stacks)
    {
        int sold = 0, failed = 0, earnedTotal = 0;
        foreach (ItemStack stack in stacks)
        {
            if (stack == null || stack.IsEmpty)
                continue;
            TradeError error = ShopManager.Sell(stack, stack.Count, out int earned);
            if (error == TradeError.None)
            {
                sold++;
                earnedTotal += earned;
            }
            else
                failed++;
        }
        Toast(failed == 0
            ? $"판매 — {sold}개 +{earnedTotal:N0}엽전"
            : $"판매 — {sold}개 +{earnedTotal:N0}엽전 · {failed}개는 팔지 못했습니다");
        AfterTrade();
    }

    private void AfterTrade()
    {
        PlayerInventory.EnsureInstance().RefreshCapacity();
        Refresh();
    }

    private static void Toast(string text)
    {
        if (!string.IsNullOrEmpty(text))
            StoryDialogueUI.ShowBanner(text, 2f);
    }
}
