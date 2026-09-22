using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 주고받는 창 — 【왼쪽이 내 가방, 오른쪽이 상대】.
///
/// 시체·상자·캐비닛을 파밍할 때, 창고에 맡길 때, 상인과 거래할 때가
/// 전부 같은 일이다. 「내 것과 저쪽 것을 나란히 놓고 옮긴다.」
/// 창마다 다른 틀을 만들면 유저는 같은 일을 세 번 배워야 하고,
/// 우리는 같은 버그를 세 번 고쳐야 한다.
///
/// 【이전 전리품 창(LootWindowUI)에서 바뀐 것】
///   · 오른쪽에 상대만 띄우던 것을 두 칸으로 나눴다. 예전에는 「내 가방에
///     자리가 있나」를 보려고 창을 닫았다 열었다 해야 했다.
///   · 칸 아래에 설명 카드를 붙이던 것을 상세 팝업으로 바꿨다.
///     가방 화면과 조작이 같아진다 — 【무엇을 누르든 상세가 뜬다.】
///   · 칸의 생김새를 ItemCell에 맡겨 가방 화면과 한 벌로 맞췄다.
///
/// 【상점은 아직 이 창을 쓰지 않는다.】 파는 값·재고·통화 흐름이
/// 8단계 설계에 걸려 있다. 틀은 여기 있으므로 그때 Mode 하나를 더한다.
/// </summary>
public class ExchangeWindowUI : MonoBehaviour
{
    /// <summary>
    /// 이 창이 무엇을 하는 중인가. 방향과 버튼 이름만 달라진다.
    /// </summary>
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

    private static ExchangeWindowUI instance;

    private GameObject panel;
    private Text titleLabel;
    private Text footerLabel;
    private Text bagTitle;
    private Text otherTitle;
    private Button takeAllButton;

    private UIFactory.ScrollList bagList;
    private UIFactory.ScrollList otherList;

    private Mode mode = Mode.Loot;
    private CorpseController source;
    private LootContainer other;
    private string otherName = "전리품";

    // 상세 팝업
    private GameObject detailPopup;
    private ItemStack detailStack;
    private bool detailFromBag;

    public bool IsOpen => panel != null && panel.activeSelf;

    public static ExchangeWindowUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<ExchangeWindowUI>(FindObjectsInactive.Include);

        if (instance != null)
            return instance;

        Canvas canvas = UIFactory.CreateCanvas("ExchangeWindowCanvas (Runtime)", 1100);

        instance = canvas.gameObject.AddComponent<ExchangeWindowUI>();
        instance.Build(canvas.transform);

        return instance;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    // ────────────────────────────────── 생성

    private void Build(Transform parent)
    {
        // 화면 UI는 안전 영역 안에만 둔다. 노치와 홈 인디케이터를 피한다.
        RectTransform safe = UIFactory.CreateSafeArea(parent.GetComponent<Canvas>());

        panel = UIFactory.CreateRegion("Panel", safe, Vector2.zero, Vector2.one).gameObject;

        // 바깥을 눌러도 닫힌다. 모바일에서 닫기 버튼만 두면 답답하다.
        Image dim = UIFactory.CreatePanel("Dim", panel.transform, UIPalette.Dim,
            Vector2.zero, Vector2.one, radius: 0);

        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.targetGraphic = dim;
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(Close);

        Image window = UIFactory.CreateGlass("Window", panel.transform, UIPalette.Panel,
            new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.93f), UIFactory.RadiusLarge);

        // 창 안을 눌러도 닫히지 않아야 한다. 덮개의 클릭을 여기서 끊는다.
        window.raycastTarget = true;

        var box = window.rectTransform;

        BuildHeader(box);
        BuildPanes(box);
        BuildFooter(box);

        panel.SetActive(false);
    }

    private void BuildHeader(RectTransform box)
    {
        UIFactory.CreatePanel("Header", box, UIPalette.Header,
            new Vector2(0f, 0.90f), new Vector2(1f, 1f), UIFactory.RadiusLarge);

        titleLabel = UIFactory.CreateLabel(box, "전리품", 34, FontStyle.Bold,
            new Vector2(0.03f, 0.90f), new Vector2(0.80f, 1f),
            TextAnchor.MiddleLeft, UIPalette.TextOnGlass);

        UIFactory.CreateButton(box, "닫기",
            new Vector2(0.84f, 0.915f), new Vector2(0.975f, 0.985f),
            UIPalette.Subtle, Close, 26);
    }

    /// <summary>두 칸. 왼쪽이 내 가방, 오른쪽이 상대다. 순서를 뒤집지 않는다.</summary>
    private void BuildPanes(RectTransform box)
    {
        const float LeftFrom = 0.02f, LeftTo = 0.49f;
        const float RightFrom = 0.51f, RightTo = 0.98f;
        const float TitleBottom = 0.815f, TitleTop = 0.885f;
        const float GridBottom = 0.16f, GridTop = 0.805f;

        bagTitle = UIFactory.CreateLabel(box, "가방", 26, FontStyle.Bold,
            new Vector2(LeftFrom, TitleBottom), new Vector2(LeftTo, TitleTop),
            TextAnchor.MiddleLeft, UIPalette.TextDim);

        bagList = UIFactory.CreateScrollList("BagGrid", box,
            new Vector2(LeftFrom, GridBottom), new Vector2(LeftTo, GridTop));

        otherTitle = UIFactory.CreateLabel(box, "전리품", 26, FontStyle.Bold,
            new Vector2(RightFrom, TitleBottom), new Vector2(RightTo, TitleTop),
            TextAnchor.MiddleLeft, UIPalette.TextDim);

        otherList = UIFactory.CreateScrollList("OtherGrid", box,
            new Vector2(RightFrom, GridBottom), new Vector2(RightTo, GridTop));
    }

    private void BuildFooter(RectTransform box)
    {
        takeAllButton = UIFactory.CreateButton(box, "전부 줍기",
            new Vector2(0.02f, 0.035f), new Vector2(0.26f, 0.135f),
            UIPalette.Action, TakeAll, 27);

        // 무게는 「지금」과 「다 들면」을 나란히 둔다. 추출 판단의 근거다.
        footerLabel = UIFactory.CreateLabel(box, string.Empty, 24, FontStyle.Normal,
            new Vector2(0.29f, 0.035f), new Vector2(0.98f, 0.135f),
            TextAnchor.MiddleRight, UIPalette.TextDim);
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

    /// <summary>
    /// 임의의 컨테이너를 연다. 창고가 8단계에 이 길로 들어온다.
    /// </summary>
    public void Open(LootContainer container, string label, Mode windowMode)
    {
        if (container == null)
            return;

        other = container;
        otherName = string.IsNullOrEmpty(label) ? "상대" : label;
        mode = windowMode;

        CloseDetail();

        panel.SetActive(true);

        Refresh();

        if (GameManager.HasInstance)
            GameManager.Instance.OpenSkill();

        // 오른쪽 위 세 버튼이 이 창 위로 떠오른다. 열려 있는 동안 감춘다.
        InventoryScreenUI.SetHudSuppressed(true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        PlaytestPanelUI.SetHiddenByScreen(true);
#endif
    }

    public void Close()
    {
        CloseDetail();

        panel.SetActive(false);

        // 다 집었으면 시체를 정리한다. 남아 있으면 그대로 두어
        // 나중에 돌아와 마저 집을 수 있게 한다.
        if (mode == Mode.Loot && source != null && other != null && other.IsEmpty)
            DespawnSource();

        source = null;
        other = null;

        if (GameManager.HasInstance)
            GameManager.Instance.CloseSkill();

        InventoryScreenUI.SetHudSuppressed(false);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        PlaytestPanelUI.SetHiddenByScreen(false);
#endif
    }

    private void DespawnSource()
    {
        var absorber = FindAnyObjectByType<PlayerAbsorber>(FindObjectsInactive.Include);

        if (absorber != null)
            absorber.Despawn(source);
    }

    // ────────────────────────────────── 다시 그리기

    private void Refresh()
    {
        if (other == null)
            return;

        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        titleLabel.text = otherName;

        // 젬은 적재에 잡히지 않아 격자에 나오지 않는다(2-31). 그래서 개수만 따로 적는다 —
        // 적지 않으면 전리품에서 젬을 주웠을 때 왼쪽에 아무 변화가 없어
        // "주워지긴 한 건가"를 알 수 없다.
        int gems = CountGems(bag);

        bagTitle.text = gems > 0
            ? $"가방 ({bag.UsedSlots}/{bag.SlotCapacity})    젬 {gems}"
            : $"가방 ({bag.UsedSlots}/{bag.SlotCapacity})";

        otherTitle.text = $"{otherName} ({other.UsedSlots}/{other.Capacity})";

        takeAllButton.gameObject.SetActive(mode == Mode.Loot || mode == Mode.Stash);

        var label = takeAllButton.GetComponentInChildren<Text>();

        if (label != null)
            label.text = mode == Mode.Stash ? "전부 꺼내기" : "전부 줍기";

        takeAllButton.interactable = !other.IsEmpty;

        // 칸 크기를 픽셀로 환산하려면 실제 크기가 확정되어 있어야 한다.
        Canvas.ForceUpdateCanvases();

        DrawBagGrid(bag);
        DrawOtherGrid();
        RefreshFooter(bag);
    }

    private static int CountGems(Inventory bag)
    {
        int count = 0;

        for (int i = 0; i < bag.Stacks.Count; i++)
        {
            if (bag.Stacks[i]?.Definition != null && !bag.Stacks[i].Definition.IsCargo)
                count += bag.Stacks[i].Count;
        }

        return count;
    }

    private void DrawBagGrid(Inventory bag)
    {
        UIFactory.ClearChildren(bagList.Content);

        // 【젬은 여기 보이지 않는다.】 젬은 적재에 잡히지 않으므로(2-31)
        // 「가방에 자리가 있나」를 재는 이 격자에 섞이면 칸 수가 어긋나 보인다.
        // 주운 젬은 스킬 화면에서 확인한다.
        var stacks = new System.Collections.Generic.List<ItemStack>(bag.Stacks.Count);

        for (int i = 0; i < bag.Stacks.Count; i++)
        {
            if (bag.Stacks[i]?.Definition != null && bag.Stacks[i].Definition.IsCargo)
                stacks.Add(bag.Stacks[i]);
        }

        int cells = Mathf.Max(stacks.Count, bag.SlotCapacity);

        UIFactory.SquareGridMetrics(bagList.Viewport, Columns, cells, MinRows,
            out int rows, out float height, out float padX, out float padY);

        bagList.Content.sizeDelta = new Vector2(0f, height);

        for (int i = 0; i < rows * Columns; i++)
        {
            UIFactory.GetCellAnchors(i, Columns, rows, padX, padY,
                out Vector2 min, out Vector2 max);

            ItemStack stack = i < stacks.Count ? stacks[i] : null;
            ItemStack captured = stack;

            ItemCell.Draw($"Bag_{i}", bagList.Content, min, max, stack, chosen: false,
                () => OpenDetail(captured, fromBag: true));
        }
    }

    private void DrawOtherGrid()
    {
        UIFactory.ClearChildren(otherList.Content);

        int cells = Mathf.Max(0, other.Capacity);

        UIFactory.SquareGridMetrics(otherList.Viewport, Columns, cells, MinRows,
            out int rows, out float height, out float padX, out float padY);

        otherList.Content.sizeDelta = new Vector2(0f, height);

        // 【용량만큼 전부 그린다.】 잘라내면 그 칸의 물건은 보이지도 눌리지도 않는다.
        for (int i = 0; i < rows * Columns; i++)
        {
            UIFactory.GetCellAnchors(i, Columns, rows, padX, padY,
                out Vector2 min, out Vector2 max);

            ItemStack stack = i < cells ? other.Get(i) : null;
            ItemStack captured = stack;

            ItemCell.Draw($"Other_{i}", otherList.Content, min, max, stack, chosen: false,
                () => OpenDetail(captured, fromBag: false));
        }
    }

    /// <summary>다 들었을 때의 무게를 미리 보여 준다. 추출 판단의 근거다.</summary>
    private void RefreshFooter(Inventory bag)
    {
        float now = bag.TotalWeight;
        float after = now + other.TotalWeight;

        bool willOverload = after > bag.WeightLimit;

        footerLabel.text =
            $"소지 {now:0.0} / {bag.WeightLimit:0.0} kg"
            + $"    다 들면 {after:0.0} kg"
            + (willOverload ? " (과중량)" : string.Empty)
            + $"    빈 칸 {bag.FreeSlots}";

        footerLabel.color = willOverload ? UIPalette.Warning : UIPalette.TextDim;
    }

    // ────────────────────────────────── 상세 팝업

    /// <summary>
    /// 【무엇을 누르든 상세가 뜬다.】 가방 화면과 같은 규칙이다.
    ///
    /// 예전에는 칸 아래 설명 카드에 띄우고 「줍기」 버튼은 창 아래에 따로 뒀다.
    /// 무엇을 고른 상태인지 두 곳을 번갈아 봐야 알 수 있었다.
    /// </summary>
    private void OpenDetail(ItemStack stack, bool fromBag)
    {
        if (stack?.Definition == null)
            return;

        CloseDetail();

        detailStack = stack;
        detailFromBag = fromBag;

        BuildDetail();
    }

    private void CloseDetail()
    {
        if (detailPopup != null)
            Destroy(detailPopup);

        detailPopup = null;
        detailStack = null;
    }

    private void BuildDetail()
    {
        ItemDefinition definition = detailStack.Definition;

        Image shade = UIFactory.CreatePanel("DetailShade", panel.transform, UIPalette.Dim,
            Vector2.zero, Vector2.one, radius: 0);

        var shadeButton = shade.gameObject.AddComponent<Button>();
        shadeButton.targetGraphic = shade;
        shadeButton.transition = Selectable.Transition.None;
        shadeButton.onClick.AddListener(CloseDetail);

        detailPopup = shade.gameObject;

        RectTransform box = UIFactory.CreateRegion("Box", detailPopup.transform,
            new Vector2(0.30f, 0.20f), new Vector2(0.70f, 0.80f));

        UIFactory.CreateGlass("Back", box, UIPalette.Panel,
            Vector2.zero, Vector2.one, UIFactory.RadiusLarge);

        // 상세 자체는 눌러도 닫히지 않아야 한다.
        var blocker = box.gameObject.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0f);

        UIFactory.CreateOutline(blocker, UIPalette.Rim, UIFactory.RadiusLarge, 2);

        RectTransform content = UIFactory.Inset(
            UIFactory.CreateRegion("Content", box, Vector2.zero, Vector2.one),
            UIFactory.Gap);

        Color kind = UIPalette.ForItem(definition.Kind);

        Image icon = UIFactory.CreatePanel("Icon", content, UIPalette.Darken(kind, 0.34f),
            new Vector2(0f, 0.865f), new Vector2(0.17f, 0.99f), UIFactory.Radius);

        UIFactory.CreateOutline(icon, UIPalette.Brighten(kind), UIFactory.Radius, 2);

        if (definition.Icon != null)
        {
            icon.sprite = definition.Icon;
            icon.type = Image.Type.Simple;
            icon.color = Color.white;
            icon.preserveAspect = true;
        }
        else
        {
            icon.sprite = UISprites.Of(UISprites.GlyphFor(definition.Kind));
            icon.type = Image.Type.Simple;
        }

        UIFactory.CreateLabel(content, definition.DisplayName, 32, FontStyle.Bold,
            new Vector2(0.21f, 0.925f), new Vector2(1f, 0.99f),
            TextAnchor.LowerLeft, UIPalette.TextOnGlass);

        UIFactory.CreateLabel(content,
            detailFromBag ? "가방에 있음" : $"{otherName}에 있음", 21, FontStyle.Normal,
            new Vector2(0.21f, 0.865f), new Vector2(1f, 0.925f),
            TextAnchor.UpperLeft, UIPalette.TextDim);

        // 무게·가치는 칩으로. 문장에 섞으면 눈이 숫자를 못 찾는다.
        DrawChip(content, 0f, 0.775f, 0.30f,
            $"{definition.Weight * detailStack.Count:0.0} kg");

        DrawChip(content, 0.33f, 0.775f, 0.30f,
            $"₡ {definition.BaseValue * detailStack.Count:N0}");

        if (detailStack.Count > 1)
            DrawChip(content, 0.66f, 0.775f, 0.22f, $"×{detailStack.Count}");

        UIFactory.CreateScrollText(content, definition.Description, 24, FontStyle.Normal,
            new Vector2(0f, 0.30f), new Vector2(1f, 0.755f), UIPalette.TextDim);

        BuildDetailActions(content);
    }

    private static void DrawChip(RectTransform content, float left, float bottom,
                                 float width, string text)
    {
        Image chip = UIFactory.CreatePanel($"Chip_{text}", content, UIPalette.Badge,
            new Vector2(left, bottom), new Vector2(left + width, bottom + 0.07f),
            UIFactory.Radius);

        UIFactory.CreateLabel(chip.transform, text, 23, FontStyle.Bold,
            new Vector2(0.08f, 0f), new Vector2(0.92f, 1f), TextAnchor.MiddleCenter,
            UIPalette.TextAccent);
    }

    private void BuildDetailActions(RectTransform content)
    {
        UIFactory.CreateButton(content, "닫기",
            new Vector2(0f, 0.02f), new Vector2(1f, 0.11f),
            UIPalette.Subtle, CloseDetail, 26);

        string action = ActionName();

        if (string.IsNullOrEmpty(action))
        {
            // Loot 모드에서 가방 칸을 눌렀을 때. 시체에 물건을 넣는 조작은 없다.
            UIFactory.CreateLabel(content, "여기서는 옮길 수 없습니다.", 21, FontStyle.Normal,
                new Vector2(0f, 0.14f), new Vector2(1f, 0.23f),
                TextAnchor.MiddleCenter, UIPalette.TextDim);
            return;
        }

        ItemStack target = detailStack;
        bool fromBag = detailFromBag;

        Button move = UIFactory.CreateButton(content, action,
            new Vector2(0f, 0.14f), new Vector2(1f, 0.25f),
            UIPalette.Action, () => Move(target, fromBag), 28);

        bool can = fromBag
            ? !other.IsFull
            : PlayerInventory.EnsureInstance().Bag.CanAdd(
                  target.Definition, target.Count);

        move.interactable = can;

        if (can)
            return;

        UIFactory.CreateLabel(content,
            fromBag ? $"{otherName}에 자리가 없습니다." : "가방에 자리가 없습니다.",
            21, FontStyle.Normal,
            new Vector2(0f, 0.255f), new Vector2(1f, 0.30f),
            TextAnchor.MiddleCenter, UIPalette.Warning);
    }

    /// <summary>이 칸에서 할 수 있는 일의 이름. 없으면 빈 문자열.</summary>
    private string ActionName()
    {
        if (!detailFromBag)
            return mode == Mode.Stash ? "꺼내기" : "줍기";

        return mode == Mode.Stash ? "넣기" : string.Empty;
    }

    // ────────────────────────────────── 옮기기

    private void Move(ItemStack stack, bool fromBag)
    {
        if (stack == null || other == null)
            return;

        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        bool moved = fromBag
            ? other.TryPutFrom(bag, stack)
            : TakeFromOther(stack, bag);

        if (!moved)
        {
            // 옮기지 못하면 아이템은 있던 자리에 그대로 남는다. 사라지지 않는다.
            GameLogger.Log("[ExchangeWindowUI] 자리가 없어 옮기지 못했습니다.");
            return;
        }

        PlayerInventory.Instance.RefreshCapacity();

        CloseDetail();

        Refresh();
    }

    private bool TakeFromOther(ItemStack stack, Inventory bag)
    {
        for (int i = 0; i < other.Capacity; i++)
        {
            if (!ReferenceEquals(other.Get(i), stack))
                continue;

            return other.TryTakeTo(i, bag);
        }

        return false;
    }

    private void TakeAll()
    {
        if (other == null)
            return;

        int moved = other.TakeAllTo(PlayerInventory.EnsureInstance().Bag);

        PlayerInventory.Instance.RefreshCapacity();

        GameLogger.Log($"[ExchangeWindowUI] {moved}칸 회수");

        CloseDetail();

        // 다 집었으면 창을 닫는다. 빈 창을 보고 있을 이유가 없다.
        if (mode == Mode.Loot && other.IsEmpty)
        {
            Close();
            return;
        }

        Refresh();
    }
}
