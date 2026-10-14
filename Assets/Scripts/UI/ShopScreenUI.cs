using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【가게 화면】 (결정 2-121) — 사용자 참고 그림(2026-10-13)의 배치. 가게 문(BunkerStation)을 누르면 화면 전체로 뜬다.
/// 떠 있는 동안 게임이 멈춘다(결정 2-92). 뒤는 흐린 게임 화면(ScreenBlur).
///
///   · 위: ‹ · 가게 이름 · 가운데 가진 엽전 · ✕
///   · 왼쪽: 종류 탭(결정 2-120) + 물건 목록 — 줄마다 그림 · 이름 · 값(못 사면 붉게) · 남은 재고.
///     못 사는 줄(품절)은 어둡고 「구매 불가」 + 붉은 이유 한 줄. 줄을 누르면 담기 · 바로 사기 · 빼기 · 상세보기.
///   · 가운데: 계산대 뒤 상인(이름표 · 말풍선 — 본문 대사, ShopKeeperTable) · 계산대 위에 담은 것(여섯 칸씩, 아래 점)
///     · 그 아래 「합계 / 보유」 · 막히면 붉은 이유 한 줄
///   · 오른쪽: 「내 가방」 — 가방 칸 그대로. 팔 수 있는 것은 눌러서 판매 · 상세보기, 팔 수 없는 것은 어둡게 (사용자)
///   · 오른쪽 아래: 「비우기」 · 「구매」 — 담은 것을 전부 아니면 전혀로 산다(CartRules)
///
/// 상인 그림은 【임시 도형】 — 아트 때 바꾼다. 문파 · 주간 한정 · 자동 구매 · 창고 보관함은 우리 게임에 없어 뺐다.
/// </summary>
public class ShopScreenUI : MonoBehaviour
{
    private static ShopScreenUI instance;

    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

    // ── 빛깔 [임시 — 아트 때] ─────────────────────────────────────────
    private static readonly Color Backdrop = new(0.05f, 0.05f, 0.07f, 0.98f);
    private static readonly Color BlurTint = new(0.30f, 0.30f, 0.34f, 1f);
    private static readonly Color PanelColor = new(0.05f, 0.05f, 0.07f, 0.55f);
    private static readonly Color RowColor = new(0.13f, 0.13f, 0.16f, 0.94f);
    private static readonly Color RowEdge = new(1f, 1f, 1f, 0.07f);
    private static readonly Color TabOn = new(0.80f, 0.73f, 0.60f, 1f);
    private static readonly Color TabOff = new(0.18f, 0.17f, 0.17f, 0.92f);
    private static readonly Color TabInkOn = new(0.20f, 0.16f, 0.12f, 1f);
    private static readonly Color Limit = new(0.95f, 0.82f, 0.36f, 1f);
    private static readonly Color Accent = new(0.58f, 0.46f, 0.92f, 1f);
    private static readonly Color PillDark = new(0.44f, 0.38f, 0.33f, 0.95f);
    private static readonly Color Cloth1 = new(0.62f, 0.46f, 0.30f, 1f);
    private static readonly Color Cloth2 = new(0.50f, 0.36f, 0.24f, 1f);
    private static readonly Color RowDim = new(0.10f, 0.10f, 0.12f, 0.92f);
    private static readonly Color Wood = new(0.36f, 0.24f, 0.15f, 1f);
    private static readonly Color WoodDark = new(0.24f, 0.15f, 0.09f, 1f);
    private static readonly Color Coin = new(0.95f, 0.76f, 0.32f, 1f);
    private static readonly Color NameTag = new(1f, 0.84f, 0.30f, 1f);
    private static readonly Color BuyColor = new(0.40f, 0.68f, 0.72f, 0.98f);

    private const int CounterSlots = 6;
    private const int BagColumns = 4;
    private const float RowHeight = 112f;
    private const float RowGap = 8f;

    // ── 만든 것 ───────────────────────────────────────────────────────
    private GameObject root;
    private RawImage blurImage;
    private RenderTexture blurTexture;
    private Text titleLabel;
    private Text goldLabel;
    private RectTransform tabRow;
    private UIFactory.ScrollList list;
    private Text keeperName;
    private Text keeperLine;
    private Image keeperBody;
    private Image keeperHead;
    private RectTransform counterSlots;
    private RectTransform counterDots;
    private Text totalLabel;
    private Text ownedLabel;
    private Text reasonLabel;
    private Text bagTitle;
    private Text bagFoot;
    private UIFactory.ScrollList bagGrid;
    private Button clearButton;
    private Button buyButton;
    private Text buyLabel;

    // ── 상태 ──────────────────────────────────────────────────────────
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
        SetHudHidden(true);

        ui.StartCoroutine(ScreenBlur.Capture(shot =>
        {
            ui.opening = false;
            ScreenBlur.Release(ref ui.blurTexture);
            ui.blurTexture = shot;
            ui.blurImage.texture = shot;
            ui.blurImage.enabled = shot != null;
            ui.root.SetActive(true);
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

        ScreenBlur.Release(ref blurTexture);
    }

    private void Hide()
    {
        if (root == null || !root.activeSelf)
            return;

        ItemActionMenu.Close();
        root.SetActive(false);
        cart.Clear();
        SetHudHidden(false);
        blurImage.texture = null;
        blurImage.enabled = false;
        ScreenBlur.Release(ref blurTexture);
    }

    private static void SetHudHidden(bool hidden)
    {
        SurvivalHudUI.SetHiddenByScreen(hidden);
        QuestHudUI.SetHiddenByScreen(hidden);
        InventoryScreenUI.SetHudSuppressed(hidden);
    }

    private void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            Hide();
    }

    // ────────────────────────────────── 만들기

    private void Build(Canvas canvas)
    {
        Image dim = UIFactory.CreatePanel("Dim", canvas.transform, Backdrop, Vector2.zero, Vector2.one, 0);
        dim.raycastTarget = true;
        root = dim.gameObject;
        GamePause.Register(root);

        blurImage = UIFactory.CreateRegion("Blur", root.transform, Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
        blurImage.color = BlurTint;
        blurImage.raycastTarget = false;
        blurImage.enabled = false;

        RectTransform safe = UIFactory.CreateRegion("SafeArea", root.transform, Vector2.zero, Vector2.one);
        safe.gameObject.AddComponent<SafeAreaFitter>();

        BuildTopBar(safe);
        BuildList(safe);
        BuildKeeper(safe);
        BuildCounter(safe);
        BuildBag(safe);
        BuildButtons(safe);

        root.SetActive(false);
    }

    private static Text Inked(Text label)
    {
        label.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
        return label;
    }

    private void BuildTopBar(RectTransform safe)
    {
        RectTransform bar = UIFactory.CreateRegion("Top", safe, new Vector2(0f, 0.905f), new Vector2(1f, 1f));

        Button back = UIFactory.CreateButton(bar, "‹", new Vector2(0f, 0.1f), new Vector2(0f, 0.9f), new Color(0f, 0f, 0f, 0.001f), Hide, 56);
        Inked(back.GetComponentInChildren<Text>());
        var backRect = (RectTransform)back.transform;
        backRect.pivot = new Vector2(0f, 0.5f);
        backRect.sizeDelta = new Vector2(80f, 0f);
        backRect.anchoredPosition = new Vector2(16f, 0f);

        titleLabel = Inked(UIFactory.CreateLabel(bar, "", 38, FontStyle.Bold,
            new Vector2(0f, 0f), new Vector2(0.4f, 1f), TextAnchor.MiddleLeft, Color.white));
        titleLabel.rectTransform.offsetMin = new Vector2(100f, 0f);

        Image coin = UIFactory.CreatePanel("Coin", bar, Coin, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0);
        coin.sprite = UISprites.Of(UISprites.Glyph.Circle);
        coin.rectTransform.sizeDelta = new Vector2(28f, 28f);
        coin.rectTransform.anchoredPosition = new Vector2(-90f, 0f);
        coin.raycastTarget = false;

        goldLabel = Inked(UIFactory.CreateLabel(bar, "", 32, FontStyle.Bold,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), TextAnchor.MiddleLeft, Color.white));
        goldLabel.rectTransform.sizeDelta = new Vector2(300f, 0f);
        goldLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
        goldLabel.rectTransform.anchoredPosition = new Vector2(-66f, 0f);

        Button close = UIFactory.CreateButton(bar, "✕", new Vector2(1f, 0.1f), new Vector2(1f, 0.9f), new Color(0f, 0f, 0f, 0.001f), Hide, 40);
        Inked(close.GetComponentInChildren<Text>());
        var closeRect = (RectTransform)close.transform;
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.sizeDelta = new Vector2(80f, 0f);
        closeRect.anchoredPosition = new Vector2(-16f, 0f);
    }

    /// <summary>왼쪽 — 종류 탭 + 물건 목록.</summary>
    private void BuildList(RectTransform safe)
    {
        UIFactory.CreatePanel("ListBack", safe, PanelColor, new Vector2(0.01f, 0.03f), new Vector2(0.30f, 0.895f),
            UIFactory.RadiusLarge);

        tabRow = UIFactory.CreateRegion("Tabs", safe, new Vector2(0.018f, 0.83f), new Vector2(0.292f, 0.885f));

        list = UIFactory.CreateScrollList("Goods", safe, new Vector2(0.015f, 0.04f), new Vector2(0.295f, 0.82f), 6f);
    }

    /// <summary>가운데 위 — 상인 【임시 도형】 · 이름표 · 말풍선.</summary>
    private void BuildKeeper(RectTransform safe)
    {
        // 좌판 차양 【임시 도형】 — 상인 뒤의 줄무늬 천 · 기둥 둘 (참고 그림의 노점 수레 자리).
        RectTransform canopy = UIFactory.CreateRegion("Canopy", safe, new Vector2(0.37f, 0.53f), new Vector2(0.63f, 0.62f));
        const int stripes = 9;
        for (int i = 0; i < stripes; i++)
        {
            Image s = UIFactory.CreatePanel($"Stripe_{i}", canopy, i % 2 == 0 ? Cloth1 : Cloth2,
                new Vector2(i / (float)stripes, 0f), new Vector2((i + 1) / (float)stripes, 1f), 0);
            s.raycastTarget = false;
        }
        Image valance = UIFactory.CreatePanel("Valance", canopy, WoodDark, new Vector2(0f, -0.12f), new Vector2(1f, 0f), 0);
        valance.raycastTarget = false;
        foreach (float x in new[] { 0.375f, 0.615f })
        {
            Image post = UIFactory.CreatePanel("Post", safe, WoodDark, new Vector2(x, 0.37f), new Vector2(x + 0.01f, 0.53f), 0);
            post.raycastTarget = false;
        }

        // 몸 · 머리 — 계산대 뒤에 선다(계산대가 아랫몸을 가린다).
        keeperBody = UIFactory.CreatePanel("KeeperBody", safe, new Color(0.30f, 0.42f, 0.36f, 1f),
            new Vector2(0.445f, 0.36f), new Vector2(0.555f, 0.58f), 40);
        keeperHead = UIFactory.CreatePanel("KeeperHead", safe, new Color(0.86f, 0.74f, 0.60f, 1f),
            new Vector2(0.5f, 0.58f), new Vector2(0.5f, 0.58f), 0);
        keeperHead.sprite = UISprites.Of(UISprites.Glyph.Circle);
        keeperHead.rectTransform.sizeDelta = new Vector2(120f, 120f);
        keeperHead.rectTransform.anchoredPosition = new Vector2(0f, 40f);

        // 이름표 — 머리 위, 참고 그림의 노란 글씨.
        keeperName = Inked(UIFactory.CreateLabel(safe, "", 30, FontStyle.Bold,
            new Vector2(0.38f, 0.66f), new Vector2(0.62f, 0.71f), TextAnchor.MiddleCenter, NameTag));

        // 말풍선 — 이름표 위 흰 판 · 검은 글씨 · 아래 꼬리.
        Image bubble = UIFactory.CreatePanel("Bubble", safe, new Color(0.98f, 0.97f, 0.94f, 0.97f),
            new Vector2(0.39f, 0.715f), new Vector2(0.61f, 0.86f), 14);
        UIFactory.CreateOutline(bubble, new Color(0.15f, 0.13f, 0.12f, 0.9f), 14, 2);
        Image tail = UIFactory.CreatePanel("Tail", bubble.transform, bubble.color, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), 0);
        tail.sprite = UISprites.Of(UISprites.Glyph.Triangle);
        tail.rectTransform.sizeDelta = new Vector2(24f, 16f);
        tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        tail.rectTransform.anchoredPosition = new Vector2(0f, -6f);

        keeperLine = UIFactory.CreateLabel(bubble.transform, "", 22, FontStyle.Normal,
            new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.92f), TextAnchor.MiddleCenter, new Color(0.12f, 0.11f, 0.10f, 1f));
    }

    /// <summary>가운데 아래 — 계산대(담은 것 여섯 칸 · 쪽 점) · 합계 / 보유 · 이유 한 줄.</summary>
    private void BuildCounter(RectTransform safe)
    {
        Image top = UIFactory.CreatePanel("CounterTop", safe, Wood, new Vector2(0.32f, 0.355f), new Vector2(0.68f, 0.385f), 6);
        top.raycastTarget = false;
        Image front = UIFactory.CreatePanel("Counter", safe, WoodDark, new Vector2(0.335f, 0.19f), new Vector2(0.665f, 0.36f), 6);
        UIFactory.CreateOutline(front, Wood, 6, 3);

        counterSlots = UIFactory.CreateRegion("Slots", front.transform, new Vector2(0.03f, 0.22f), new Vector2(0.97f, 0.92f));
        counterDots = UIFactory.CreateRegion("Dots", front.transform, new Vector2(0.3f, 0.03f), new Vector2(0.7f, 0.18f));

        Image box = UIFactory.CreatePanel("Totals", safe, new Color(0f, 0f, 0f, 0.78f),
            new Vector2(0.415f, 0.085f), new Vector2(0.585f, 0.175f), 10);
        UIFactory.CreateOutline(box, Accent, 10, 2);

        UIFactory.CreateLabel(box.transform, "합계", 24, FontStyle.Bold,
            new Vector2(0.06f, 0.5f), new Vector2(0.4f, 1f), TextAnchor.MiddleLeft, Color.white);
        UIFactory.CreateLabel(box.transform, "보유", 24, FontStyle.Bold,
            new Vector2(0.06f, 0f), new Vector2(0.4f, 0.5f), TextAnchor.MiddleLeft, Color.white);

        foreach (float y in new[] { 0.75f, 0.25f })
        {
            Image coin = UIFactory.CreatePanel("Coin", box.transform, Coin, new Vector2(0.42f, y), new Vector2(0.42f, y), 0);
            coin.sprite = UISprites.Of(UISprites.Glyph.Circle);
            coin.rectTransform.sizeDelta = new Vector2(20f, 20f);
            coin.raycastTarget = false;
        }

        totalLabel = UIFactory.CreateLabel(box.transform, "0", 24, FontStyle.Bold,
            new Vector2(0.4f, 0.5f), new Vector2(0.94f, 1f), TextAnchor.MiddleRight, Coin);
        ownedLabel = UIFactory.CreateLabel(box.transform, "0", 24, FontStyle.Bold,
            new Vector2(0.4f, 0f), new Vector2(0.94f, 0.5f), TextAnchor.MiddleRight, Color.white);

        reasonLabel = Inked(UIFactory.CreateLabel(safe, "", 20, FontStyle.Bold,
            new Vector2(0.32f, 0.03f), new Vector2(0.68f, 0.08f), TextAnchor.MiddleCenter, UIPalette.Warning));
    }

    /// <summary>오른쪽 — 내 가방 (참고 그림의 보관함 자리 · 사용자).</summary>
    private void BuildBag(RectTransform safe)
    {
        // 참고 그림의 보관함처럼 — 큰 판 없이 위에 테두리 이름표 하나, 아래는 칸만.
        Image chip = UIFactory.CreatePanel("BagChip", safe, new Color(0.04f, 0.04f, 0.07f, 0.85f),
            new Vector2(0.80f, 0.835f), new Vector2(0.985f, 0.89f), 12);
        UIFactory.CreateOutline(chip, Accent, 12, 3);

        bagTitle = Inked(UIFactory.CreateLabel(chip.transform, "내 가방", 24, FontStyle.Bold,
            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, Color.white));

        bagGrid = UIFactory.CreateScrollList("Bag", safe, new Vector2(0.705f, 0.20f), new Vector2(0.985f, 0.83f), 6f);

        bagFoot = UIFactory.CreateLabel(safe, "", 18, FontStyle.Normal,
            new Vector2(0.71f, 0.15f), new Vector2(0.98f, 0.195f), TextAnchor.MiddleLeft, UIPalette.TextDim);
    }

    private void BuildButtons(RectTransform safe)
    {
        clearButton = UIFactory.CreateButton(safe, "비우기", new Vector2(0.70f, 0.035f), new Vector2(0.80f, 0.115f),
            PillDark, () =>
            {
                cart.Clear();
                counterPage = 0;
                Refresh();
            }, 26, 40);

        buyButton = UIFactory.CreateButton(safe, "구매", new Vector2(0.81f, 0.035f), new Vector2(0.99f, 0.115f),
            BuyColor, BuyCart, 30, 40);
        buyLabel = buyButton.GetComponentInChildren<Text>();

        // 꺼진 단추가 눈에 띄게 — 기본 회색 틴트는 너무 옅다.
        Dim(clearButton);
        Dim(buyButton);
    }

    private static void Dim(Button button)
    {
        ColorBlock colors = button.colors;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.45f);
        button.colors = colors;
    }

    // ────────────────────────────────── 다시 그리기

    private void Refresh()
    {
        if (root == null || !root.activeSelf)
            return;

        ItemActionMenu.Close();

        ShopKeeperTable.Keeper keeper = ShopKeeperTable.Of(kind);
        titleLabel.text = ShopTable.NameOf(kind);
        keeperName.text = keeper.Name;
        keeperLine.text = keeper.Line;
        KeeperColors(kind, out Color body, out Color head);
        keeperBody.color = body;
        keeperHead.color = head;

        int gold = PassiveManager.EnsureInstance().Gold;
        goldLabel.text = $"{gold:N0}엽전";

        Canvas.ForceUpdateCanvases();
        DrawTabs();
        DrawList(gold);
        DrawCounter();
        DrawTotals(gold);
        DrawBag();
    }

    /// <summary>【임시 도형】의 빛깔 — 가게마다 다르게(영감 · 빚쟁이 · 참봉). 아트 때 그림으로 바꾼다.</summary>
    private static void KeeperColors(ShopKind shop, out Color body, out Color head)
    {
        switch (shop)
        {
            case ShopKind.Smithy:
                body = new Color(0.30f, 0.27f, 0.30f, 1f);
                head = new Color(0.62f, 0.52f, 0.48f, 1f);
                break;
            case ShopKind.Apothecary:
                body = new Color(0.24f, 0.36f, 0.40f, 1f);
                head = new Color(0.80f, 0.74f, 0.66f, 1f);
                break;
            default:
                body = new Color(0.40f, 0.33f, 0.22f, 1f);
                head = new Color(0.72f, 0.62f, 0.50f, 1f);
                break;
        }
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

        UIFactory.ClearChildren(tabRow);

        // 종류가 하나뿐이어도 「구매」 탭 하나는 둔다 — 참고 그림의 맨 위 탭.
        bool split = ShopTabs.Shows(kinds);
        if (!split || shopTab > kinds.Count)
            shopTab = 0;

        int count = split ? kinds.Count + 1 : 1;
        float width = 1f / count;

        for (int i = 0; i < count; i++)
        {
            int index = i;
            string name = i == 0 ? (split ? "전체" : "구매") : ShopTabs.Name(kinds[i - 1]);

            // 참고 그림의 맨 위 「구매」 알약 — 고른 탭은 밝은 베이지 · 짙은 글씨.
            Button tab = UIFactory.CreateButton(tabRow, name,
                new Vector2(i * width + 0.005f, 0f), new Vector2((i + 1) * width - 0.005f, 1f),
                i == shopTab ? TabOn : TabOff,
                () =>
                {
                    shopTab = index;
                    Refresh();
                }, 20, 30);
            tab.GetComponentInChildren<Text>().color = i == shopTab ? TabInkOn : UIPalette.TextDim;
        }
    }

    /// <summary>
    /// 왼쪽 목록. 【품절도 자리를 지킨다】 — 어둡게 「구매 불가」와 이유. 값은 남은 엽전(담은 합계를 뺀 것)으로 못 사면 붉다.
    /// </summary>
    private void DrawList(int gold)
    {
        UIFactory.ClearChildren(list.Content);

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

        list.Content.sizeDelta = new Vector2(0f, row * (RowHeight + RowGap));
    }

    private void DrawRow(int index, ItemDefinition definition, ShopEntry entry, int budget)
    {
        int remaining = ShopManager.Of(kind).Remaining(entry.ItemId);
        int inCart = cart.CountOf(entry.ItemId);
        int price = TradeRules.BuyPrice(definition, entry, ShopManager.BuyDiscountPercent);
        bool soldOut = remaining <= 0;

        Image row = UIFactory.CreatePanel($"Row_{index}", list.Content, soldOut ? RowDim : RowColor,
            new Vector2(0f, 1f), new Vector2(1f, 1f), UIFactory.Radius);
        row.rectTransform.pivot = new Vector2(0.5f, 1f);
        row.rectTransform.offsetMin = new Vector2(4f, -(index * (RowHeight + RowGap) + RowHeight));
        row.rectTransform.offsetMax = new Vector2(-4f, -(index * (RowHeight + RowGap)));
        UIFactory.CreateOutline(row, RowEdge, UIFactory.Radius, 1);

        var button = row.gameObject.AddComponent<Button>();
        button.targetGraphic = row;
        RectTransform rowRect = row.rectTransform;
        button.onClick.AddListener(() => OpenRowMenu(rowRect, definition, entry));

        // 그림 칸 — 가방과 같은 칸(ItemCell). 아래 오른쪽에 「1」 대신 묶음이면 개수 없이.
        Image cell = ItemCell.Draw("Icon", row.transform, new Vector2(0.02f, 0.08f), new Vector2(0.02f, 0.92f),
            new ItemStack(definition), chosen: false, null);
        cell.rectTransform.sizeDelta = new Vector2(RowHeight - 18f, 0f);
        cell.rectTransform.pivot = new Vector2(0f, 0.5f);
        cell.raycastTarget = false;
        if (cell.TryGetComponent(out Button cellButton))
            cellButton.enabled = false;

        float left = RowHeight + 4f;

        Text name = UIFactory.CreateLabel(row.transform, definition.DisplayName, 24, FontStyle.Bold,
            new Vector2(0f, 0.64f), new Vector2(1f, 0.96f), TextAnchor.MiddleLeft, soldOut ? UIPalette.TextDim : Color.white);
        name.rectTransform.offsetMin = new Vector2(left, 0f);
        name.rectTransform.offsetMax = new Vector2(-96f, 0f);

        Image coin = UIFactory.CreatePanel("Coin", row.transform, Coin, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), 0);
        coin.sprite = UISprites.Of(UISprites.Glyph.Circle);
        coin.rectTransform.sizeDelta = new Vector2(18f, 18f);
        coin.rectTransform.anchoredPosition = new Vector2(left + 10f, 0f);
        coin.raycastTarget = false;

        Text cost = UIFactory.CreateLabel(row.transform, $"{price:N0}", 24, FontStyle.Bold,
            new Vector2(0f, 0.34f), new Vector2(1f, 0.66f), TextAnchor.MiddleLeft,
            price > budget ? UIPalette.Warning : Coin);
        cost.rectTransform.offsetMin = new Vector2(left + 26f, 0f);

        string third = soldOut
            ? "재고 없음 — 파밍이 끝나면 채워집니다"
            : ShopManager.IsDaily(kind, entry.ItemId) ? $"오늘의 물건 · 남은 재고 {remaining}" : $"남은 재고 {remaining}";

        Text stock = UIFactory.CreateLabel(row.transform, third, 18, FontStyle.Normal,
            new Vector2(0f, 0.04f), new Vector2(1f, 0.34f), TextAnchor.MiddleLeft,
            soldOut ? UIPalette.Warning : Limit);
        stock.rectTransform.offsetMin = new Vector2(left, 0f);

        if (soldOut)
        {
            // 참고 그림처럼 그림 칸 위에 「구매 불가」.
            Image veil = UIFactory.CreatePanel("Veil", cell.transform, new Color(0f, 0f, 0f, 0.62f), Vector2.zero, Vector2.one, UIFactory.Radius);
            veil.raycastTarget = false;
            UIFactory.CreateLabel(veil.transform, "구매\n불가", 20, FontStyle.Bold, Vector2.zero, Vector2.one,
                TextAnchor.MiddleCenter, Color.white);
        }

        if (inCart > 0)
        {
            Text badge = UIFactory.CreateBadge(row.transform, $"담음 {inCart}",
                new Vector2(1f, 0.6f), new Vector2(1f, 0.92f), 18, UIPalette.Gain);
            var pill = (RectTransform)badge.transform.parent;
            pill.offsetMin = new Vector2(-96f, 0f);
            pill.offsetMax = new Vector2(-8f, 0f);
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

        float w = 1f / CounterSlots;
        for (int s = 0; s < CounterSlots; s++)
        {
            int i = counterPage * CounterSlots + s;
            CartLine? line = i < lines.Count ? lines[i] : null;
            ItemDefinition d = line.HasValue && catalog != null ? catalog.Find(line.Value.ItemId) : null;

            var min = new Vector2(s * w + 0.01f, 0f);
            var max = new Vector2((s + 1) * w - 0.01f, 1f);

            if (d == null)
            {
                Image empty = UIFactory.CreatePanel($"Slot_{s}", counterSlots, new Color(0f, 0f, 0f, 0.28f), min, max, UIFactory.Radius);
                empty.raycastTarget = false;
                continue;
            }

            Image cell = ItemCell.Draw($"Slot_{s}", counterSlots, min, max, new ItemStack(d), chosen: false, null);
            UIFactory.CreateBadge(cell.transform, $"×{line.Value.Count}",
                new Vector2(0.40f, 0.04f), new Vector2(0.96f, 0.30f), 18, Color.white);

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
            Button dot = UIFactory.CreateButton(counterDots, string.Empty,
                new Vector2(p * dw + dw * 0.35f, 0.15f), new Vector2((p + 1) * dw - dw * 0.35f, 0.85f),
                p == counterPage ? Color.white : new Color(1f, 1f, 1f, 0.35f),
                () =>
                {
                    counterPage = page;
                    Refresh();
                }, 10, 6);
            dot.name = $"Page_{p}";
        }
    }

    /// <summary>합계 / 보유 · 막히면 붉은 이유 · 「구매」가 꺼진다.</summary>
    private void DrawTotals(int gold)
    {
        Inventory bag = PlayerInventory.EnsureInstance().Bag;
        List<CartItem> items = ShopManager.Resolve(kind, cart);
        int total = CartRules.Total(items, ShopManager.BuyDiscountPercent);

        totalLabel.text = $"{total:N0}";
        totalLabel.color = total > gold ? UIPalette.Warning : Coin;
        ownedLabel.text = $"{gold:N0}";

        clearButton.interactable = !cart.IsEmpty;

        if (cart.IsEmpty)
        {
            reasonLabel.text = string.Empty;
            buyButton.interactable = false;
            buyLabel.text = "구매";
            return;
        }

        TradeError error = CartRules.CanBuy(items, ShopManager.Of(kind).Remaining, gold, bag,
            ShopManager.BuyDiscountPercent, out int failed);

        string name = failed >= 0 && failed < items.Count ? items[failed].Definition?.DisplayName : null;
        reasonLabel.text = error == TradeError.None
            ? string.Empty
            : CartRules.Explain(error, total - gold, CartRules.SlotsNeeded(items, bag) - bag.FreeSlots, name);

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

    /// <summary>
    /// 오른쪽 내 가방 — 가방 칸 순서 그대로. 【팔 수 없는 것은 어둡게】 (사용자) — 눌러도 상세보기만.
    /// </summary>
    private void DrawBag()
    {
        UIFactory.ClearChildren(bagGrid.Content);

        Inventory bag = PlayerInventory.EnsureInstance().Bag;
        IReadOnlyList<ItemStack> stacks = bag.Stacks;
        float bonus = ShopManager.SellBonusPercent;

        bagTitle.text = $"내 가방 ({bag.UsedSlots}/{bag.SlotCapacity})";
        bagFoot.text = $"무게 {bag.TotalWeight:0.0} / {bag.WeightLimit:0.0} kg · 팔면 가치의 절반";

        int cells = Mathf.Max(bag.SlotCapacity, stacks.Count);
        UIFactory.SquareGridMetrics(bagGrid.Viewport, BagColumns, cells, 3,
            out int rows, out float height, out float padX, out float padY);
        bagGrid.Content.sizeDelta = new Vector2(0f, height);

        for (int i = 0; i < rows * BagColumns; i++)
        {
            UIFactory.GetCellAnchors(i, BagColumns, rows, padX, padY, out Vector2 min, out Vector2 max);
            ItemStack stack = i < stacks.Count ? stacks[i] : null;

            Image cell = ItemCell.Draw($"Bag_{i}", bagGrid.Content, min, max, stack, chosen: false, null);
            if (stack?.Definition == null)
                continue;

            bool sellable = TradeRules.CanSell(stack, bonus) == TradeError.None;
            if (!sellable)
                cell.gameObject.AddComponent<CanvasGroup>().alpha = 0.38f;

            if (!cell.TryGetComponent(out Button button))
                continue;

            RectTransform cellRect = cell.rectTransform;
            ItemStack captured = stack;
            button.onClick.AddListener(() => OpenBagMenu(cellRect, captured));
        }
    }

    private void OpenBagMenu(RectTransform cell, ItemStack stack)
    {
        float bonus = ShopManager.SellBonusPercent;
        TradeError error = TradeRules.CanSell(stack, bonus);
        var entries = new List<ItemActionMenu.Entry>();

        if (error == TradeError.None)
            entries.Add(new ItemActionMenu.Entry($"판매 +{TradeRules.SellPrice(stack, bonus):N0}엽전", UIPalette.Gain,
                () => AskSell(stack)));
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
