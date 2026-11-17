using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【가게 · 시설 화면의 한 틀】 (결정 2-123) — 사용자 참고 그림(가게) 배치를 가게 · 창고 · 약탕 · 부뚜막 · 작업대 · 장부방 · 건물 짓기가 같이 쓴다.
/// 화면마다 같은 자리 · 같은 비율이라 한 번 배우면 어디서든 읽힌다.
///
///   ┌──────────────────────────────── 위 띠: ‹ 이름 ─── ● 엽전 ─── ✕ ───────────────────────────────┐
///   │ 왼쪽(34%): 탭 + 목록           │ 가운데(32%): 말풍선 · 이름표 · 주인 · 계산대 · 합계 · 알림 │ 오른쪽(30%): 내 가방 │
///   └───────────────────────────────────────────────────────────────────────────────────────────────┘
///   · 세 칸 사이 간격은 같다(Gutter) · 위아래 끝도 같다(Edge)
///   · 뒤는 흐린 게임 화면(ScreenBlur), 떠 있는 동안 게임이 멈춘다(결정 2-92), HUD는 감춘다
///   · 주인이 없는 시설(부뚜막 · 작업대)은 그 자리에 시설 도형 + 이름표만 — 말풍선은 없다
///   · 말풍선은 본문 대사 그대로(KeeperTable) — 새로 짓지 않는다
/// </summary>
public sealed class StationFrame
{
    // ── 비율 (안전 영역 0 ~ 1) ─────────────────────────────────────────
    public const float Edge = 0.012f;
    public const float Gutter = 0.012f;
    public const float TopBarBottom = 0.905f;
    public const float BodyTop = 0.892f;
    public const float BodyBottom = 0.03f;

    public const float LeftEnd = Edge + 0.34f;                     // 0.352
    public const float CenterStart = LeftEnd + Gutter;             // 0.364
    public const float RightStart = 1f - Edge - 0.30f;             // 0.688
    public const float CenterEnd = RightStart - Gutter;            // 0.676

    /// <summary>가운데 무대의 가로/세로 — 16:9(1920×1080 기준)에서의 가운데 칸 모양.</summary>
    public const float StageAspect = (CenterEnd - CenterStart) * UIFactory.ReferenceWidth / ((BodyTop - BodyBottom) * UIFactory.ReferenceHeight);

    // ── 빛깔 [임시 — 아트 때] ─────────────────────────────────────────
    public static readonly Color Backdrop = new(0.05f, 0.05f, 0.07f, 0.98f);
    public static readonly Color BlurTint = new(0.30f, 0.30f, 0.34f, 1f);
    public static readonly Color PanelColor = new(0.05f, 0.05f, 0.07f, 0.55f);
    public static readonly Color RowColor = new(0.13f, 0.13f, 0.16f, 0.94f);
    public static readonly Color RowDim = new(0.09f, 0.09f, 0.11f, 0.94f);
    public static readonly Color RowEdge = new(1f, 1f, 1f, 0.07f);
    public static readonly Color TabOn = new(0.80f, 0.73f, 0.60f, 1f);
    public static readonly Color TabOff = new(0.18f, 0.17f, 0.17f, 0.92f);
    public static readonly Color TabInkOn = new(0.20f, 0.16f, 0.12f, 1f);
    public static readonly Color Coin = new(0.95f, 0.76f, 0.32f, 1f);
    public static readonly Color Limit = new(0.95f, 0.82f, 0.36f, 1f);
    public static readonly Color NameTag = new(1f, 0.84f, 0.30f, 1f);
    public static readonly Color Accent = new(0.58f, 0.46f, 0.92f, 1f);
    public static readonly Color Wood = new(0.36f, 0.24f, 0.15f, 1f);
    public static readonly Color WoodDark = new(0.24f, 0.15f, 0.09f, 1f);
    public static readonly Color PillDark = new(0.44f, 0.38f, 0.33f, 0.95f);
    public static readonly Color PillAction = new(0.40f, 0.68f, 0.72f, 0.98f);
    private static readonly Color Cloth1 = new(0.62f, 0.46f, 0.30f, 1f);
    private static readonly Color Cloth2 = new(0.50f, 0.36f, 0.24f, 1f);

    public const int PillRadius = 30;
    public const float RowHeight = 86f * UIFactory.RowScale;   // 참고 그림의 줄 높이 86(1080 기준)에 글자 배율을 곱한다 (결정 2-134)
    public const float RowGap = 8f;
    private const int FallbackBagColumns = 6;     // 칸 크기는 UIFactory.ItemCellSize — 열 수는 폭에서 정한다

    // ── 자리 ──────────────────────────────────────────────────────────
    public GameObject Root { get; private set; }
    public RectTransform Safe { get; private set; }

    /// <summary>왼쪽 칸 — 탭(TabRow) 아래 목록(List). 화면이 쓰던 상자 대신 여기를 쓴다.</summary>
    public RectTransform Left { get; private set; }
    public RectTransform TabRow { get; private set; }
    public UIFactory.ScrollList List { get; private set; }

    /// <summary>가운데 칸 — 주인 아래 계산대(Counter) · 그 아래 합계 칸(TotalsBox) · 맨 아래 알림 한 줄(Status).</summary>
    public RectTransform Center { get; private set; }
    public RectTransform Counter { get; private set; }
    public RectTransform TotalsBox { get; private set; }
    public Text Status { get; private set; }

    /// <summary>계산대 앞판 가운데 글 — 담는 칸이 없는 시설은 여기에 지금 사정을 적는다.</summary>
    public Text CounterNote { get; private set; }

    /// <summary>오른쪽 칸 — 내 가방 · 아래 단추 줄(Buttons).</summary>
    public RectTransform Right { get; private set; }
    public RectTransform Buttons { get; private set; }

    public Text Title { get; private set; }
    public Text Gold { get; private set; }

    private RawImage blurImage;
    private RenderTexture blurTexture;
    private Text keeperName;
    private Text keeperLine;
    private GameObject bubble;
    private Image keeperBody;
    private Image keeperHead;
    private GameObject canopy;
    private GameObject prop;
    private Text bagTitle;
    private Text bagFoot;
    private UIFactory.ScrollList bagGrid;

    // ── 여러 개 고르기 (사용자 2026-11-17 「상점 · 창고 다중 선택해서 판매 혹은 창고에 넣기」) ──
    // 화면이 EnableMultiSelect 로 켜면 내 가방 위에 「여러 개 선택」 단추가 생긴다. 고르는 동안 칸을 누르면
    // 상세 메뉴 대신 고름 · 풂이 바뀌고, 아래에 「n개 팔기 +엽전」 같은 한 번에 처리하는 단추가 뜬다.
    private Button selectToggle;
    private Text selectToggleLabel;
    private Button batchButton;
    private Text batchLabel;
    private bool selecting;
    private readonly List<ItemStack> selected = new();
    private Func<List<ItemStack>, string> batchDescribe;
    private Action<List<ItemStack>> batchApply;
    private Func<ItemStack, bool> lastUsable;
    private Action<RectTransform, ItemStack> lastTap;
    private string lastFoot;
    private readonly Action onClose;

    // ────────────────────────────────── 만들기

    private StationFrame(Action onClose) => this.onClose = onClose;

    /// <summary>캔버스 위에 틀을 세운다. 처음에는 꺼져 있다 — Show로 연다.</summary>
    public static StationFrame Build(Canvas canvas, Action onClose)
    {
        var f = new StationFrame(onClose);

        Image dim = UIFactory.CreatePanel("Dim", canvas.transform, Backdrop, Vector2.zero, Vector2.one, 0);
        dim.raycastTarget = true;
        f.Root = dim.gameObject;
        GamePause.Register(f.Root);   // 떠 있는 동안 게임이 멈춘다 (결정 2-92)
        f.Root.AddComponent<HudGuard>();

        f.blurImage = UIFactory.CreateRegion("Blur", f.Root.transform, Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
        f.blurImage.color = BlurTint;
        f.blurImage.raycastTarget = false;
        f.blurImage.enabled = false;

        f.Safe = UIFactory.CreateRegion("SafeArea", f.Root.transform, Vector2.zero, Vector2.one);
        f.Safe.gameObject.AddComponent<SafeAreaFitter>();

        f.BuildTopBar();
        f.BuildLeft();
        f.BuildCenter();
        f.BuildRight();

        f.Root.SetActive(false);
        return f;
    }

    /// <summary>글자가 칸보다 길면 min 크기까지 줄여 칸 안에 넣는다 (결정 2-124) — 좁은 4:3 화면에서 넘치지 않게.</summary>
    public static Text Shrink(Text label, int min)
    {
        label.resizeTextMaxSize = label.fontSize;
        label.resizeTextMinSize = Mathf.Min(min, label.fontSize);
        label.resizeTextForBestFit = true;
        label.verticalOverflow = VerticalWrapMode.Truncate;   // 넘침 허용이면 줄이지 않는다
        return label;
    }

    private static Text Inked(Text label)
    {
        label.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
        return label;
    }

    private void BuildTopBar()
    {
        RectTransform bar = UIFactory.CreateRegion("Top", Safe, new Vector2(Edge, TopBarBottom), new Vector2(1f - Edge, 0.99f));

        Button back = UIFactory.CreateButton(bar, "‹", new Vector2(0f, 0f), new Vector2(0f, 1f), new Color(0f, 0f, 0f, 0.001f),
            () => onClose?.Invoke(), 56);
        Inked(back.GetComponentInChildren<Text>());
        var backRect = (RectTransform)back.transform;
        backRect.pivot = new Vector2(0f, 0.5f);
        backRect.sizeDelta = new Vector2(72f, 0f);

        Title = Inked(UIFactory.CreateLabel(bar, "", 36, FontStyle.Bold,
            new Vector2(0f, 0f), new Vector2(0.4f, 1f), TextAnchor.MiddleLeft, Color.white));
        Title.rectTransform.offsetMin = new Vector2(80f, 0f);

        // 엽전 — 가운데에 동그라미 + 숫자를 한 묶음으로 (숫자 길이가 바뀌어도 가운데에 선다).
        RectTransform gold = UIFactory.CreateRegion("Gold", bar, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f));
        gold.sizeDelta = new Vector2(280f, 0f);
        var layout = gold.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 10f;
        // 너비는 그룹이 직접 잰다 — 글자 칸의 ContentSizeFitter에 맡기면 처음 열 때 옛 너비로 놓여 동그라미가 떨어진다.
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;

        Image coin = UIFactory.CreatePanel("Coin", gold, Coin, Vector2.zero, Vector2.zero, 0);
        coin.sprite = UISprites.Of(UISprites.Glyph.Circle);
        coin.rectTransform.sizeDelta = new Vector2(26f, 26f);
        coin.raycastTarget = false;
        coin.gameObject.AddComponent<LayoutElement>().preferredWidth = 26f;

        Gold = Inked(UIFactory.CreateLabel(gold, "", 30, FontStyle.Bold, Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft, Color.white));
        Gold.horizontalOverflow = HorizontalWrapMode.Overflow;
        Gold.rectTransform.sizeDelta = new Vector2(200f, 40f);

        Button close = UIFactory.CreateButton(bar, "✕", new Vector2(1f, 0f), new Vector2(1f, 1f), new Color(0f, 0f, 0f, 0.001f),
            () => onClose?.Invoke(), 40);
        Inked(close.GetComponentInChildren<Text>());
        var closeRect = (RectTransform)close.transform;
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.sizeDelta = new Vector2(72f, 0f);
    }

    private void BuildLeft()
    {
        Image back = UIFactory.CreatePanel("Left", Safe, PanelColor, new Vector2(Edge, BodyBottom), new Vector2(LeftEnd, BodyTop),
            UIFactory.RadiusLarge);
        Left = back.rectTransform;

        TabRow = UIFactory.CreateRegion("Tabs", Left, new Vector2(0.025f, 0.925f), new Vector2(0.975f, 0.985f));
        List = UIFactory.CreateScrollList("List", Left, new Vector2(0.015f, 0.01f), new Vector2(0.985f, 0.915f), 6f);
    }

    private void BuildCenter()
    {
        Center = UIFactory.CreateRegion("Center", Safe, new Vector2(CenterStart, BodyBottom), new Vector2(CenterEnd, BodyTop));

        // 무대 — 16:9에서의 가운데 칸 모양을 그대로 지킨다 (결정 2-124). 4:3 태블릿에서 계산대 · 주인이
        // 위아래로 늘어나거나, 20:9 휴대폰에서 옆으로 퍼지지 않도록 비율을 고정하고 남는 자리는 비워 둔다.
        RectTransform stage = UIFactory.CreateRegion("Stage", Center, Vector2.zero, Vector2.one);
        stage.pivot = new Vector2(0.5f, 0.4f);
        var fit = stage.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fit.aspectRatio = StageAspect;

        // 차양 · 기둥 【임시 도형】 — 상인 뒤 노점.
        RectTransform shade = UIFactory.CreateRegion("Canopy", stage, new Vector2(0.10f, 0.585f), new Vector2(0.90f, 0.665f));
        const int stripes = 9;
        for (int i = 0; i < stripes; i++)
            UIFactory.CreatePanel($"Stripe_{i}", shade, i % 2 == 0 ? Cloth1 : Cloth2,
                new Vector2(i / (float)stripes, 0f), new Vector2((i + 1) / (float)stripes, 1f), 0).raycastTarget = false;
        UIFactory.CreatePanel("Valance", shade, WoodDark, new Vector2(0f, -0.14f), new Vector2(1f, 0f), 0).raycastTarget = false;
        foreach (float x in new[] { 0.115f, 0.86f })
            UIFactory.CreatePanel("Post", stage, WoodDark, new Vector2(x, 0.43f), new Vector2(x + 0.025f, 0.585f), 0).raycastTarget = false;
        canopy = shade.gameObject;

        // 주인 【임시 도형】 — 몸 · 머리. 계산대가 아랫몸을 가린다.
        keeperBody = UIFactory.CreatePanel("KeeperBody", stage, Color.gray, new Vector2(0.34f, 0.40f), new Vector2(0.66f, 0.62f), 40);
        keeperHead = UIFactory.CreatePanel("KeeperHead", stage, Color.white, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), 0);
        keeperHead.sprite = UISprites.Of(UISprites.Glyph.Circle);
        keeperHead.rectTransform.sizeDelta = new Vector2(112f, 112f);
        keeperHead.rectTransform.anchoredPosition = new Vector2(0f, 36f);

        // 시설 도형 (주인이 없을 때) — 단 위에 놓인 것.
        Image block = UIFactory.CreatePanel("Prop", stage, WoodDark, new Vector2(0.22f, 0.44f), new Vector2(0.78f, 0.62f), 12);
        UIFactory.CreateOutline(block, Wood, 12, 3);
        prop = block.gameObject;

        // 이름표 — 머리 위 노란 글씨. 말풍선 — 이름표 위 흰 판 · 검은 테 · 아래 꼬리.
        keeperName = Inked(UIFactory.CreateLabel(stage, "", 30, FontStyle.Bold,
            new Vector2(0.1f, 0.725f), new Vector2(0.9f, 0.775f), TextAnchor.MiddleCenter, NameTag));

        Image b = UIFactory.CreatePanel("Bubble", stage, new Color(0.98f, 0.97f, 0.94f, 0.97f),
            new Vector2(0.08f, 0.795f), new Vector2(0.92f, 0.975f), 14);
        UIFactory.CreateOutline(b, new Color(0.15f, 0.13f, 0.12f, 0.9f), 14, 2);
        Image tail = UIFactory.CreatePanel("Tail", b.transform, b.color, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), 0);
        tail.sprite = UISprites.Of(UISprites.Glyph.Triangle);
        tail.rectTransform.sizeDelta = new Vector2(24f, 16f);
        tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        tail.rectTransform.anchoredPosition = new Vector2(0f, -6f);
        keeperLine = UIFactory.CreateLabel(b.transform, "", 23, FontStyle.Normal,
            new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.92f), TextAnchor.MiddleCenter, new Color(0.12f, 0.11f, 0.10f, 1f));
        keeperLine.lineSpacing = 1.1f;
        // 긴 대사(건설 · 영감)도 판 안에 들어가게 줄인다 — 판을 넓히고 높여서(결정 2-135)
        // 세 줄 대사가 다른 글보다 눈에 띄게 작아지던 것(16까지 줄었다)을 막는다.
        FitTextBlock.Apply(keeperLine, UIFactory.Scaled(17));
        bubble = b.gameObject;

        // 계산대 — 윗판 · 앞판.
        UIFactory.CreatePanel("CounterTop", stage, Wood, new Vector2(0f, 0.405f), new Vector2(1f, 0.44f), 6).raycastTarget = false;
        Image front = UIFactory.CreatePanel("Counter", stage, WoodDark, new Vector2(0.03f, 0.205f), new Vector2(0.97f, 0.41f), 6);
        UIFactory.CreateOutline(front, Wood, 6, 3);
        Counter = front.rectTransform;

        CounterNote = UIFactory.CreateLabel(Counter, "", 22, FontStyle.Bold, new Vector2(0.06f, 0.1f), new Vector2(0.94f, 0.9f),
            TextAnchor.MiddleCenter, new Color(1f, 0.93f, 0.80f, 1f));
        CounterNote.supportRichText = true;

        Image totals = UIFactory.CreatePanel("Totals", stage, new Color(0f, 0f, 0f, 0.78f),
            new Vector2(0.22f, 0.075f), new Vector2(0.78f, 0.185f), 10);
        UIFactory.CreateOutline(totals, Accent, 10, 2);
        TotalsBox = totals.rectTransform;

        Status = Inked(UIFactory.CreateLabel(Center, "", 20, FontStyle.Bold,
            new Vector2(0f, 0f), new Vector2(1f, 0.065f), TextAnchor.MiddleCenter, UIPalette.TextAccent));
    }

    private void BuildRight()
    {
        Right = UIFactory.CreateRegion("Right", Safe, new Vector2(RightStart, BodyBottom), new Vector2(1f - Edge, BodyTop));

        // 참고 그림의 보관함처럼 — 큰 판 없이 위에 보라 테 이름표 하나, 아래는 칸만.
        Image chip = UIFactory.CreatePanel("BagChip", Right, new Color(0.04f, 0.04f, 0.07f, 0.85f),
            new Vector2(0.38f, 0.93f), new Vector2(1f, 1f), 12);
        UIFactory.CreateOutline(chip, Accent, 12, 3);
        bagTitle = Inked(UIFactory.CreateLabel(chip.transform, "내 가방", 24, FontStyle.Bold,
            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, Color.white));

        bagGrid = UIFactory.CreateScrollList("Bag", Right, new Vector2(0f, 0.17f), new Vector2(1f, 0.915f), 6f);

        bagFoot = UIFactory.CreateLabel(Right, "", 18, FontStyle.Normal,
            new Vector2(0.01f, 0.115f), new Vector2(1f, 0.165f), TextAnchor.MiddleLeft, UIPalette.TextDim);

        selectToggle = Pill(Right, "여러 개 선택", new Vector2(0f, 0.93f), new Vector2(0.36f, 1f), PillDark, ToggleSelecting, 20);
        selectToggleLabel = selectToggle.GetComponentInChildren<Text>();
        selectToggle.gameObject.SetActive(false);

        batchButton = Pill(Right, "", new Vector2(0f, 0.107f), new Vector2(1f, 0.168f), PillAction, ApplyBatch, 22);
        batchLabel = batchButton.GetComponentInChildren<Text>();
        batchButton.gameObject.SetActive(false);

        Buttons = UIFactory.CreateRegion("Buttons", Right, new Vector2(0f, 0f), new Vector2(1f, 0.095f));
    }

    // ────────────────────────────────── 열고 닫기

    public bool IsOpen => Root != null && Root.activeSelf;

    /// <summary>이번 프레임 끝에 뒤 화면을 찍어 흐리게 깔고 연다. after는 연 뒤에(다시 그리기).</summary>
    public IEnumerator Show(Action after)
    {
        HudGuard.Hide(true);

        yield return ScreenBlur.Capture(shot =>
        {
            ScreenBlur.Release(ref blurTexture);
            blurTexture = shot;
            blurImage.texture = shot;
            blurImage.enabled = shot != null;
        });

        if (Root == null)
        {
            HudGuard.Hide(false);
            yield break;
        }

        Root.SetActive(true);
        after?.Invoke();
    }

    /// <summary>닫는다 — 흐린 뒤 화면을 놓아준다. HUD는 HudGuard가 되돌린다.</summary>
    public void Hide()
    {
        if (selecting)
        {
            selecting = false;
            selected.Clear();
            selectToggleLabel.text = "여러 개 선택";
            selectToggle.image.color = PillDark;
            batchButton.gameObject.SetActive(false);
            bagFoot.gameObject.SetActive(true);
        }
        ItemActionMenu.Close();
        if (Root != null)
            Root.SetActive(false);

        blurImage.texture = null;
        blurImage.enabled = false;
        ScreenBlur.Release(ref blurTexture);
    }

    // ────────────────────────────────── 채우기

    /// <summary>주인 — 이름표 · 말풍선(본문 대사) · 도형 빛깔. 주인이 없으면 시설 도형과 이름표만.</summary>
    public void SetKeeper(KeeperTable.Keeper keeper)
    {
        bool person = keeper.IsPerson;
        keeperName.text = keeper.Name;
        keeperLine.text = keeper.Line ?? string.Empty;
        bubble.SetActive(person && !string.IsNullOrEmpty(keeper.Line));
        keeperBody.gameObject.SetActive(person);
        keeperHead.gameObject.SetActive(person);
        canopy.SetActive(person);
        prop.SetActive(!person);
        keeperBody.color = keeper.Body;
        keeperHead.color = keeper.Head;
    }

    public void SetGold(int gold) => Gold.text = $"{gold:N0}엽전";

    /// <summary>계산대를 담는 칸으로 쓰지 않는 시설 — 앞판에 지금 사정 한 줄, 합계 칸은 감춘다.</summary>
    public void UseCounterAsNote(bool note)
    {
        CounterNote.gameObject.SetActive(note);
        TotalsBox.gameObject.SetActive(!note);
    }

    /// <summary>알약 탭 — 고른 것은 베이지 · 짙은 글씨. 탭이 하나뿐이어도 그린다(참고 그림의 맨 위 「구매」).</summary>
    public void DrawTabs(IReadOnlyList<string> names, int current, Action<int> pick)
    {
        UIFactory.ClearChildren(TabRow);
        if (names == null || names.Count == 0)
            return;

        float width = 1f / names.Count;
        for (int i = 0; i < names.Count; i++)
        {
            int index = i;
            Button tab = UIFactory.CreateButton(TabRow, names[i],
                new Vector2(i * width + 0.006f, 0f), new Vector2((i + 1) * width - 0.006f, 1f),
                i == current ? TabOn : TabOff, () => pick?.Invoke(index), 20, PillRadius);
            tab.GetComponentInChildren<Text>().color = i == current ? TabInkOn : UIPalette.TextDim;
        }
    }

    /// <summary>알약 단추 (오른쪽 아래 줄 · 계산대 안 등).</summary>
    public static Button Pill(Transform parent, string text, Vector2 min, Vector2 max, Color color, UnityEngine.Events.UnityAction action,
                              int fontSize = 26)
    {
        Button button = UIFactory.CreateButton(parent, text, min, max, color, action, fontSize, PillRadius);
        ColorBlock colors = button.colors;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.45f);
        button.colors = colors;
        return button;
    }

    /// <summary>목록 한 줄의 바탕 — 같은 높이 · 같은 간격 · 얇은 테. index번째 자리.</summary>
    public Image Row(int index, bool dim, float height = RowHeight)
    {
        Image row = UIFactory.CreatePanel($"Row_{index}", List.Content, dim ? RowDim : RowColor,
            new Vector2(0f, 1f), new Vector2(1f, 1f), UIFactory.Radius);
        row.rectTransform.pivot = new Vector2(0.5f, 1f);
        row.rectTransform.offsetMin = new Vector2(4f, -(index * (height + RowGap) + height));
        row.rectTransform.offsetMax = new Vector2(-4f, -(index * (height + RowGap)));
        UIFactory.CreateOutline(row, RowEdge, UIFactory.Radius, 1);
        return row;
    }

    /// <summary>
    /// 내 가방에서 여러 개를 골라 한 번에 처리하게 한다 — 화면을 만들 때 한 번 부른다.
    /// describe: 고른 것들 → 단추 글 (예: 「3개 팔기 +120엽전」) · apply: 고른 것들을 처리한다(끝나면 화면이 다시 그린다).
    /// 고를 수 있는 칸은 DrawBag 의 usable 이 true 인 칸뿐이다.
    /// </summary>
    public void EnableMultiSelect(Func<List<ItemStack>, string> describe, Action<List<ItemStack>> apply)
    {
        batchDescribe = describe;
        batchApply = apply;
        selectToggle.gameObject.SetActive(true);
        SetSelecting(false);
    }

    private void ToggleSelecting() => SetSelecting(!selecting);

    private void SetSelecting(bool on)
    {
        selecting = on;
        selected.Clear();
        ItemActionMenu.Close();
        selectToggleLabel.text = on ? "선택 끝" : "여러 개 선택";
        selectToggle.image.color = on ? Accent : PillDark;
        RedrawBag();
    }

    private void RedrawBag()
    {
        if (IsOpen && (lastTap != null || lastUsable != null))
            DrawBag(lastUsable, lastTap, lastFoot);
    }

    private void ToggleChosen(ItemStack stack)
    {
        if (!selected.Remove(stack))
            selected.Add(stack);
        RedrawBag();
    }

    private void ApplyBatch()
    {
        if (selected.Count == 0 || batchApply == null)
            return;
        var chosen = new List<ItemStack>(selected);
        selecting = false;
        selected.Clear();
        selectToggleLabel.text = "여러 개 선택";
        selectToggle.image.color = PillDark;
        batchApply(chosen);   // 화면이 처리한 뒤 다시 그린다(DrawBag)
    }

    /// <summary>
    /// 오른쪽 내 가방 — 가방 칸 순서 그대로. usable이 false인 칸은 어둡게. 칸을 누르면 onTap(칸, 물건).
    /// </summary>
    public void DrawBag(Func<ItemStack, bool> usable, Action<RectTransform, ItemStack> onTap, string foot = null)
    {
        UIFactory.ClearChildren(bagGrid.Content);
        lastUsable = usable;
        lastTap = onTap;
        lastFoot = foot;

        Inventory bag = PlayerInventory.EnsureInstance().Bag;
        IReadOnlyList<ItemStack> stacks = bag.Stacks;

        // 팔거나 옮겨서 가방에서 빠진 것은 고른 목록에서도 뺀다.
        selected.RemoveAll(s => s == null || s.IsEmpty || !Contains(stacks, s));

        bagTitle.text = $"내 가방 ({bag.UsedSlots}/{bag.SlotCapacity})";
        bagFoot.text = foot ?? $"무게 {bag.TotalWeight:0.0} / {bag.WeightLimit:0.0} kg";

        int cells = Mathf.Max(bag.SlotCapacity, stacks.Count);
        int BagColumns = UIFactory.ItemColumns(bagGrid.Viewport, FallbackBagColumns);
        UIFactory.SquareGridMetrics(bagGrid.Viewport, BagColumns, cells, 3,
            out int rows, out float height, out float padX, out float padY);
        bagGrid.Content.sizeDelta = new Vector2(0f, height);

        for (int i = 0; i < rows * BagColumns; i++)
        {
            UIFactory.GetCellAnchors(i, BagColumns, rows, padX, padY, out Vector2 min, out Vector2 max);
            ItemStack stack = i < stacks.Count ? stacks[i] : null;

            bool chosen = selecting && stack != null && selected.Contains(stack);
            Image cell = ItemCell.Draw($"Bag_{i}", bagGrid.Content, min, max, stack, chosen, null);
            if (stack?.Definition == null)
                continue;

            bool canUse = usable == null || usable(stack);
            if (!canUse)
                cell.gameObject.AddComponent<CanvasGroup>().alpha = 0.38f;

            if (!cell.TryGetComponent(out Button button))
                continue;

            RectTransform cellRect = cell.rectTransform;
            ItemStack captured = stack;
            if (selecting)
            {
                if (canUse)
                    button.onClick.AddListener(() => ToggleChosen(captured));
            }
            else if (onTap != null)
                button.onClick.AddListener(() => onTap(cellRect, captured));
        }

        // 고르는 중이면 아래 무게 줄 자리에 「n개 팔기」 단추를 띄운다.
        bool showBatch = selecting && selected.Count > 0 && batchDescribe != null;
        batchButton.gameObject.SetActive(showBatch);
        bagFoot.gameObject.SetActive(!showBatch);
        if (showBatch)
            batchLabel.text = batchDescribe(new List<ItemStack>(selected));
        else if (selecting)
            bagFoot.text = "팔거나 넣을 칸을 누르세요 — 다시 누르면 풀립니다";
    }

    private static bool Contains(IReadOnlyList<ItemStack> stacks, ItemStack stack)
    {
        for (int i = 0; i < stacks.Count; i++)
            if (stacks[i] == stack)
                return true;
        return false;
    }

    /// <summary>가방 칸을 눌렀을 때 기본 — 상세보기 하나.</summary>
    public static void DetailOnly(RectTransform cell, ItemStack stack)
        => ItemActionMenu.Open(cell, new List<ItemActionMenu.Entry>
        {
            new("상세보기", UIPalette.Subtle, () => InventoryScreenUI.OpenReadOnlyDetail(stack, "내 가방")),
        });

    /// <summary>
    /// 틀이 떠 있는 동안 HUD(생존 · 퀘스트 · 화면 버튼)를 감추고, 꺼지거나 지워지면 되돌린다.
    /// 여러 틀이 겹쳐도 하나라도 떠 있으면 감춘 채로 둔다.
    /// </summary>
    private sealed class HudGuard : MonoBehaviour
    {
        private static int open;

        public static void Hide(bool hidden)
        {
            SurvivalHudUI.SetHiddenByScreen(hidden);
            QuestHudUI.SetHiddenByScreen(hidden);
            InventoryScreenUI.SetHudSuppressed(hidden);
        }

        private void OnEnable()
        {
            open++;
            Hide(true);
        }

        private void OnDisable()
        {
            open = Mathf.Max(0, open - 1);
            if (open == 0)
                Hide(false);
        }
    }
}
