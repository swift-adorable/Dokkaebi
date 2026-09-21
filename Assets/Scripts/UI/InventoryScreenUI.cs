using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 가방 화면 — 장비 · 가방 · 젬 소켓 · 패시브를 한 화면의 탭으로 묶는다.
/// 배치는 덕코프 스크린샷을 따르고, 【조작은 전부 터치】다.
///
/// 화면 구성
///   상단 중앙 : 탭 (가방 / 젬 / 패시브)
///   좌상단    : 크레딧
///   좌측      : 장비 8슬롯 · 가방 격자 (n/m)
///   우측      : 탭에 따라 — 아이템 상세 / 소켓 배치 / 패시브 트리
///   하단 중앙 : 퀵슬롯 1~8
///   하단 좌   : 소지 중량 막대
///
/// 【PC 스크린샷과 다른 점】 키 힌트(F/X/RMB/L/N)와 마우스 호버 툴팁이 없다.
/// 칸을 한 번 누르면 고르고, 상세는 우측 패널에 나타난다.
/// 모바일에는 호버가 없으므로 「누르면 고른다 · 고른 것의 상세는 정해진 자리에」가
/// 유일하게 성립하는 방식이다.
/// </summary>
public partial class InventoryScreenUI : MonoBehaviour
{
    private enum Tab { Bag = 0, Socket = 1, Passive = 2 }

    private static readonly string[] TabNames = { "가방", "젬", "패시브" };

    // ── 레이아웃 ──────────────────────────────────────────────────────
    // 값은 전부 【안전 영역 기준】의 0~1이다. 노치와 홈 인디케이터는
    // SafeAreaFitter가 이미 잘라 냈으므로 여기서는 화면 전체를 쓴다고 생각해도 된다.
    // 한 곳에 모아 두는 이유 — 흩어져 있으면 한 줄만 옮겨도 겹치는지 알 수 없다.

    private const float TopBarBottom = 0.90f;
    private const float ColumnTop = 0.875f;
    private const float ColumnBottom = 0.155f;
    private const float LeftRight = 0.495f;
    private const float RightLeft = 0.505f;
    private const float FooterTop = 0.125f;
    private const float FooterBottom = 0.025f;
    private const float HintBottom = 0.132f;
    private const float HintTop = 0.172f;

    private const int BagColumns = 6;

    /// <summary>
    /// 한 번에 보이는 행. 【칸 크기의 기준】이다.
    ///
    /// 용량이 늘면 칸이 작아지는 것이 아니라 내용물이 길어지고 스크롤이 생긴다.
    /// 6-N 이후 실사용 상한이 65칸(기본 20 + 장비 37 + 패시브 8)이라
    /// 고정 격자로는 29칸이 보이지도 눌리지도 않았다. (Blob_Audit.md F1)
    /// 모바일에서 터치 목표 크기를 지키는 유일한 방법이 스크롤이다.
    /// </summary>
    private const int BagVisibleRows = 6;

    /// <summary>칸 사이 여백. 부모 기준 정규화 값이다.</summary>
    private const float BagCellPadding = 0.006f;

    private static InventoryScreenUI instance;

    private GameObject panel;
    private GameObject toggleButton;
    private Text toggleLabel;

    private RectTransform leftColumn;
    private RectTransform equipmentGrid;
    private RectTransform bagViewport;
    private RectTransform bagGrid;
    private ScrollRect bagScroll;
    private RectTransform rightPanel;
    private RectTransform quickSlots;

    /// <summary>안전 영역 컨테이너. 화면 UI는 전부 이 아래에 붙는다.</summary>
    private RectTransform safeArea;

    /// <summary>
    /// 지금 고른 【착용 중인】 장비의 자리. 가방에서 고른 것(selected)과 구분한다.
    ///
    /// 나눠 두는 이유 — 해제는 이제 두 단계다. 슬롯을 누르면 고르기만 하고,
    /// 우측 행동 줄의 「해제」를 눌러야 실제로 벗는다.
    /// 예전에는 빈손으로 슬롯을 누르면 즉시 벗겨져서, 무엇을 끼웠는지
    /// 확인하려고 누른 것만으로 장비가 가방으로 돌아갔다.
    /// </summary>
    private EquipmentSlot? selectedSlot;

    private Text creditLabel;
    private Text bagTitleLabel;
    private Text weightLabel;
    private Image weightFill;
    private Text hintLabel;

    private readonly List<Button> tabButtons = new();
    private readonly List<ItemStack> bagStacks = new();

    private Tab tab = Tab.Bag;

    /// <summary>가방에서 고른 칸. 우측 패널과 소켓 장착이 이것을 본다.</summary>
    private ItemStack selected;

    public bool IsOpen => panel != null && panel.activeSelf;

    public static InventoryScreenUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<InventoryScreenUI>(FindObjectsInactive.Include);

        if (instance != null)
            return instance;

        Canvas canvas = UIFactory.CreateCanvas("InventoryScreenCanvas (Runtime)", 1000);

        instance = canvas.gameObject.AddComponent<InventoryScreenUI>();
        instance.Build(canvas.transform);

        return instance;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        if (SkillManager.HasInstance)
        {
            SkillManager.Instance.OnBuildChanged -= HandleExternalChange;
            SkillManager.Instance.OnGemGained -= HandleGemGained;
            SkillManager.Instance.OnEquipRejected -= HandleEquipRejected;
        }
    }

    private void Start()
    {
        SkillManager manager = SkillManager.EnsureInstance();

        manager.OnBuildChanged += HandleExternalChange;
        manager.OnGemGained += HandleGemGained;
        manager.OnEquipRejected += HandleEquipRejected;

        RefreshToggle();
    }

    private void HandleExternalChange()
    {
        RefreshToggle();

        if (IsOpen)
            Refresh();
    }

    private void HandleGemGained(SkillDefinition skill) => HandleExternalChange();

    private void HandleEquipRejected(SkillDefinition skill, SocketError error)
    {
        SetHint(SocketErrorText.Describe(error));
    }

    // ────────────────────────────────── 생성

    private void Build(Transform parent)
    {
        safeArea = UIFactory.CreateSafeArea(parent.GetComponent<Canvas>());

        BuildToggleButton(safeArea);

        panel = UIFactory.CreateRegion("Panel", safeArea, Vector2.zero, Vector2.one).gameObject;

        // 덮개는 패널 안에 둔다. 패널이 꺼지면 같이 꺼져야 한다.
        // 안전 영역 밖까지 덮으려고 앵커를 넉넉히 넘긴다.
        Image dim = UIFactory.CreatePanel("Dim", panel.transform, UIPalette.Dim,
            new Vector2(-0.2f, -0.2f), new Vector2(1.2f, 1.2f));

        dim.raycastTarget = true;

        BuildTopBar();
        BuildLeftColumn();

        rightPanel = UIFactory.CreateRegion("Right", panel.transform,
            new Vector2(RightLeft, ColumnBottom), new Vector2(1f, ColumnTop));

        BuildBottomBar();

        hintLabel = UIFactory.CreateLabel(panel.transform, string.Empty, 24, FontStyle.Normal,
            new Vector2(0f, HintBottom), new Vector2(1f, HintTop),
            TextAnchor.MiddleLeft, UIPalette.TextAccent);

        panel.SetActive(false);
    }

    /// <summary>항상 떠 있는 「가방」 버튼. 빈 소켓이 있으면 색으로 알린다.</summary>
    private void BuildToggleButton(Transform parent)
    {
        toggleButton = UIFactory.CreateChild("InventoryToggle", parent);

        var rect = toggleButton.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(220f, 96f);
        rect.anchoredPosition = new Vector2(-8f, -8f);

        var image = toggleButton.AddComponent<Image>();
        image.color = UIPalette.Header;

        var button = toggleButton.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(Toggle);

        toggleLabel = UIFactory.CreateLabel(toggleButton.transform, "가방", 34, FontStyle.Bold,
            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
    }

    private void BuildTopBar()
    {
        // 크레딧 — 스크린샷의 좌상단 화폐 표시 자리.
        Image purse = UIFactory.CreatePanel("Credits", panel.transform, UIPalette.Header,
            new Vector2(0f, TopBarBottom), new Vector2(0.185f, 1f));

        creditLabel = UIFactory.CreateLabel(purse.transform, "₡ 0", 32, FontStyle.Bold,
            new Vector2(0.06f, 0f), new Vector2(0.94f, 1f), TextAnchor.MiddleRight,
            UIPalette.TextAccent);

        // 탭 — 스크린샷의 상단 중앙 아이콘 줄. 아트 전이라 글자로 둔다.
        // 우상단은 「가방」 토글이 먹는다. 탭 줄의 중심을 그만큼 왼쪽으로 민다.
        float width = 0.115f;
        float gap = 0.01f;
        float total = TabNames.Length * width + (TabNames.Length - 1) * gap;
        float startX = 0.46f - total * 0.5f;

        tabButtons.Clear();

        for (int i = 0; i < TabNames.Length; i++)
        {
            float x = startX + i * (width + gap);
            int captured = i;

            Button button = UIFactory.CreateButton(panel.transform, TabNames[i],
                new Vector2(x, TopBarBottom), new Vector2(x + width, 1f),
                UIPalette.Subtle, () => SelectTab((Tab)captured), 30);

            tabButtons.Add(button);
        }
    }

    private void BuildLeftColumn()
    {
        leftColumn = UIFactory.CreateRegion("Left", panel.transform,
            new Vector2(0f, ColumnBottom), new Vector2(LeftRight, ColumnTop));

        UIFactory.CreatePanel("Back", leftColumn, UIPalette.Panel, Vector2.zero, Vector2.one);

        UIFactory.CreateLabel(leftColumn, "장비", 30, FontStyle.Bold,
            new Vector2(0.03f, 0.92f), new Vector2(0.97f, 0.99f), TextAnchor.MiddleLeft);

        equipmentGrid = UIFactory.CreateRegion("Equipment", leftColumn,
            new Vector2(0.03f, 0.63f), new Vector2(0.97f, 0.91f));

        bagTitleLabel = UIFactory.CreateLabel(leftColumn, "가방", 30, FontStyle.Bold,
            new Vector2(0.03f, 0.55f), new Vector2(0.97f, 0.62f), TextAnchor.MiddleLeft);

        bagViewport = UIFactory.CreateRegion("Bag", leftColumn,
            new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.54f));

        // 마스크가 없으면 스크롤한 칸이 위쪽 장비 영역을 덮는다.
        bagViewport.gameObject.AddComponent<RectMask2D>();

        // 드래그를 받으려면 레이캐스트 대상이 필요하다. 빈 칸 사이나
        // 마지막 줄 아래를 문질러도 스크롤되게 만드는 투명 판이다.
        var bagCatcher = bagViewport.gameObject.AddComponent<Image>();
        bagCatcher.color = new Color(0f, 0f, 0f, 0f);

        // 내용물은 위를 기준으로 자란다 — 첫 칸의 자리가 용량과 무관하게 같다.
        bagGrid = UIFactory.CreateRegion("BagContent", bagViewport,
            new Vector2(0f, 1f), new Vector2(1f, 1f));
        bagGrid.pivot = new Vector2(0.5f, 1f);
        bagGrid.sizeDelta = Vector2.zero;

        bagScroll = bagViewport.gameObject.AddComponent<ScrollRect>();
        bagScroll.viewport = bagViewport;
        bagScroll.content = bagGrid;
        bagScroll.horizontal = false;
        bagScroll.vertical = true;
        bagScroll.movementType = ScrollRect.MovementType.Elastic;
        bagScroll.elasticity = 0.1f;
        bagScroll.inertia = true;
        bagScroll.decelerationRate = 0.135f;
        bagScroll.scrollSensitivity = 40f;
    }

    private void BuildBottomBar()
    {
        // 소지 중량 막대 — 스크린샷의 좌하단.
        // 소지 중량 — 좌하단. 막대와 숫자를 한 줄에 둔다.
        UIFactory.CreateLabel(panel.transform, "소지 중량", 22, FontStyle.Normal,
            new Vector2(0f, FooterBottom), new Vector2(0.075f, FooterTop),
            TextAnchor.MiddleLeft, UIPalette.TextDim);

        UIFactory.CreatePanel("WeightTrack", panel.transform, UIPalette.Slot,
            new Vector2(0.08f, FooterBottom + 0.025f), new Vector2(0.245f, FooterTop - 0.025f));

        weightFill = UIFactory.CreatePanel("WeightFill", panel.transform, UIPalette.Action,
            new Vector2(0.08f, FooterBottom + 0.025f), new Vector2(0.08f, FooterTop - 0.025f));

        weightLabel = UIFactory.CreateLabel(panel.transform, string.Empty, 22, FontStyle.Bold,
            new Vector2(0.25f, FooterBottom), new Vector2(0.40f, FooterTop),
            TextAnchor.MiddleLeft, UIPalette.TextDim);

        // 퀵슬롯 1~8 — 하단 중앙.
        quickSlots = UIFactory.CreateRegion("QuickSlots", panel.transform,
            new Vector2(0.42f, FooterBottom), new Vector2(0.86f, FooterTop));

        UIFactory.CreateButton(panel.transform, "닫기",
            new Vector2(0.88f, FooterBottom), new Vector2(1f, FooterTop),
            UIPalette.Subtle, Close);
    }

    // ────────────────────────────────── 열고 닫기

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    public void Open(int tabIndex = -1)
    {
        if (tabIndex >= 0 && tabIndex < TabNames.Length)
            tab = (Tab)tabIndex;

        selected = null;
        selectedSlot = null;

        // 활성화가 먼저다 — 꺼진 상태에서는 뷰포트 높이를 읽을 수 없어
        // 스크롤 내용물의 높이가 0으로 잡힌다.
        panel.SetActive(true);

        Refresh();

        if (bagScroll != null)
            bagScroll.verticalNormalizedPosition = 1f;

        if (GameManager.HasInstance)
            GameManager.Instance.OpenSkill();
    }

    public void Close()
    {
        CloseDiscardPopup();

        selected = null;
        selectedSlot = null;

        panel.SetActive(false);

        if (GameManager.HasInstance)
            GameManager.Instance.CloseSkill();
    }

    private void SelectTab(Tab next)
    {
        tab = next;
        selected = null;
        selectedSlot = null;

        SetHint(string.Empty);
        Refresh();
    }

    // ────────────────────────────────── 다시 그리기

    private void Refresh()
    {
        RefreshTabs();
        RefreshCredits();

        // 패시브는 트리를 넓게 보여 줘야 하므로 좌측을 접는다. 스크린샷과 같다.
        bool showLeft = tab != Tab.Passive;

        leftColumn.gameObject.SetActive(showLeft);
        quickSlots.gameObject.SetActive(showLeft);

        rightPanel.anchorMin = new Vector2(showLeft ? RightLeft : 0f, ColumnBottom);

        if (showLeft)
        {
            RefreshEquipment();
            RefreshBag();
            RefreshQuickSlots();
            RefreshWeight();
        }

        UIFactory.ClearChildren(rightPanel);

        switch (tab)
        {
            case Tab.Socket:
                DrawSocketPanel();
                break;

            case Tab.Passive:
                DrawPassivePanel();
                break;

            default:
                DrawItemDetail();
                break;
        }

        RefreshToggle();
    }

    private void RefreshTabs()
    {
        for (int i = 0; i < tabButtons.Count; i++)
        {
            if (tabButtons[i] == null)
                continue;

            var image = tabButtons[i].targetGraphic as Image;

            if (image != null)
                image.color = (Tab)i == tab ? UIPalette.Action : UIPalette.Subtle;
        }
    }

    private void RefreshCredits()
    {
        int amount = PassiveManager.HasInstance ? PassiveManager.Instance.Credits : 0;

        creditLabel.text = $"₡ {amount:N0}";
    }

    private void RefreshToggle()
    {
        if (toggleLabel == null || !SkillManager.HasInstance)
            return;

        int free = SkillManager.Instance.Build.FreeSocketCount;
        int gems = SkillManager.Instance.GetGemsInBag().Count;

        bool canSocket = free > 0 && gems > 0;

        toggleLabel.text = canSocket ? $"가방\n빈 소켓 {free}" : "가방";
        toggleLabel.color = canSocket ? UIPalette.TextAccent : UIPalette.Text;
    }

    // ────────────────────────────────── 좌측 — 장비

    private void RefreshEquipment()
    {
        UIFactory.ClearChildren(equipmentGrid);

        EquipmentLoadout loadout = PlayerInventory.EnsureInstance().Loadout;

        var slots = (EquipmentSlot[])System.Enum.GetValues(typeof(EquipmentSlot));

        for (int i = 0; i < slots.Length; i++)
        {
            UIFactory.GetCellAnchors(i, 4, 2, 0.008f, out Vector2 min, out Vector2 max);

            ItemStack stack = loadout.Get(slots[i]);

            bool isSelectedSlot = selectedSlot == slots[i];

            // 【고른 장비가 들어갈 수 있는 자리를 밝힌다.】
            // 어느 칸에 끼워야 하는지 글로만 알려 주면 8칸을 하나씩 눌러 보게 된다.
            bool canAccept = selected != null
                             && selected.Definition is EquipmentDefinition
                             && loadout.CanEquip(selected, slots[i]);

            Color color = isSelectedSlot
                ? UIPalette.SlotSelected
                : canAccept
                    ? UIPalette.SlotEquippable
                    : stack == null
                        ? UIPalette.Slot
                        : UIPalette.ForItem(stack.Definition.Kind);

            Image cell = UIFactory.CreatePanel($"Equip_{slots[i]}", equipmentGrid,
                color, min, max);

            // 장비 슬롯도 누를 수 있어야 한다. 이것이 없던 동안에는
            // 에셋 82종을 만들어 놓고 게임에서 입을 방법이 없었다. (docs/Blob_Audit.md A1)
            var slotButton = cell.gameObject.AddComponent<Button>();
            slotButton.targetGraphic = cell;

            EquipmentSlot captured = slots[i];
            slotButton.onClick.AddListener(() => OnEquipSlotClicked(captured));

            string label = stack?.Definition != null
                ? stack.Definition.DisplayName
                : EquipmentSlotName(slots[i]);

            Color textColor = stack == null ? UIPalette.TextDim : UIPalette.Text;

            UIFactory.CreateLabel(cell.transform, label, 20,
                stack == null ? FontStyle.Normal : FontStyle.Bold,
                new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f),
                TextAnchor.MiddleCenter, textColor);
        }
    }

    private static string EquipmentSlotName(EquipmentSlot slot)
    {
        switch (slot)
        {
            case EquipmentSlot.Weapon:   return "무기";
            case EquipmentSlot.Head:     return "머리";
            case EquipmentSlot.Body:     return "신체";
            case EquipmentSlot.Face:     return "얼굴";
            case EquipmentSlot.Ears:     return "이어폰";
            case EquipmentSlot.Backpack: return "가방";
            case EquipmentSlot.ImprintA: return "각인 1";
            case EquipmentSlot.ImprintB: return "각인 2";
            default:                     return slot.ToString();
        }
    }

    // ────────────────────────────────── 좌측 — 가방

    private void RefreshBag()
    {
        UIFactory.ClearChildren(bagGrid);

        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        // 【탭에 맞는 것만 보여 준다.】
        // 젬 탭에서 방탄복을 고를 일이 없고, 가방 탭에서 젬을 눌러 봐야
        // 「젬 탭에서 장착」이라는 안내만 다시 나온다. 목록만 길어진다.
        bagStacks.Clear();

        int hidden = 0;

        foreach (ItemStack stack in bag.Stacks)
        {
            if (BelongsToTab(stack))
                bagStacks.Add(stack);
            else
                hidden++;
        }

        bagTitleLabel.text = tab == Tab.Socket
            ? $"젬 ({bagStacks.Count}개)"
            : $"가방 ({bag.UsedSlots}/{bag.SlotCapacity})"
              + (hidden > 0 ? $"    젬 {hidden}개는 젬 탭에" : string.Empty);

        // 【전부 그린다.】 잘라내면 그 칸의 물건은 보이지도 눌리지도 않는다.
        // 가방 탭은 실제 용량만큼(빈 칸이 곧 남은 자리다),
        // 젬 탭은 가진 젬만큼 그린다 — 젬 4개에 빈 칸 16개는 정보가 아니다.
        int cells = tab == Tab.Socket
            ? Mathf.Max(BagColumns, bagStacks.Count)
            : Mathf.Max(0, bag.SlotCapacity);

        int rows = Mathf.Max(BagVisibleRows,
            Mathf.CeilToInt(cells / (float)BagColumns));

        // 부모(=뷰포트)의 실제 높이를 읽기 전에 레이아웃을 확정시킨다.
        Canvas.ForceUpdateCanvases();

        float viewHeight = bagViewport.rect.height;

        bagGrid.sizeDelta = viewHeight > 0f
            ? new Vector2(0f, viewHeight * rows / BagVisibleRows)
            : Vector2.zero;

        // 세로 여백은 내용물이 길어진 만큼 줄여야 픽셀 간격이 그대로다.
        float paddingY = BagCellPadding * BagVisibleRows / rows;

        for (int i = 0; i < cells; i++)
        {
            UIFactory.GetCellAnchors(i, BagColumns, rows, BagCellPadding, paddingY,
                out Vector2 min, out Vector2 max);

            ItemStack stack = i < bagStacks.Count ? bagStacks[i] : null;

            Color color = stack == null
                ? UIPalette.Slot
                : stack == selected
                    ? UIPalette.SlotSelected
                    : UIPalette.ForItem(stack.Definition.Kind);

            Image cell = UIFactory.CreatePanel($"Bag_{i}", bagGrid, color, min, max);

            var button = cell.gameObject.AddComponent<Button>();
            button.targetGraphic = cell;
            button.interactable = stack != null;

            ItemStack captured = stack;
            button.onClick.AddListener(() => SelectStack(captured));

            if (stack == null)
                continue;

            UIFactory.CreateLabel(cell.transform, stack.Definition.DisplayName, 18,
                FontStyle.Bold, new Vector2(0.08f, 0.2f), new Vector2(0.92f, 0.92f),
                TextAnchor.UpperLeft);

            if (stack.Count > 1)
            {
                UIFactory.CreateLabel(cell.transform, stack.Count.ToString(), 20,
                    FontStyle.Bold, new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.26f),
                    TextAnchor.LowerRight, UIPalette.TextAccent);
            }
        }
    }

    private void SelectStack(ItemStack stack)
    {
        selected = selected == stack ? null : stack;

        // 가방에서 무언가를 고르면 착용 슬롯 선택은 풀린다. 둘은 배타다.
        selectedSlot = null;

        SetHint(DescribeSelection());

        Refresh();
    }

    /// <summary>이 칸이 지금 탭에 속하는가.</summary>
    private bool BelongsToTab(ItemStack stack)
    {
        if (stack?.Definition == null)
            return false;

        return tab == Tab.Socket
            ? stack.Definition.IsSkillGem
            : !stack.Definition.IsSkillGem;
    }

    private void RefreshQuickSlots()
    {
        UIFactory.ClearChildren(quickSlots);

        for (int i = 0; i < 8; i++)
        {
            UIFactory.GetCellAnchors(i, 8, 1, 0.006f, out Vector2 min, out Vector2 max);

            Image cell = UIFactory.CreatePanel($"Quick_{i}", quickSlots,
                UIPalette.Slot, min, max);

            UIFactory.CreateLabel(cell.transform, (i + 1).ToString(), 20, FontStyle.Normal,
                new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.35f),
                TextAnchor.LowerRight, UIPalette.TextDim);
        }
    }

    private void RefreshWeight()
    {
        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        float ratio = bag.WeightLimit <= 0f ? 0f : bag.TotalWeight / bag.WeightLimit;

        // 막대는 꽉 차면 멈추되 문구는 실제 값을 보여 준다. 무게는 한도를 넘을 수 있다.
        float clamped = Mathf.Clamp01(ratio);

        weightFill.rectTransform.anchorMax = new Vector2(
            0.10f + (0.30f - 0.10f) * clamped, 0.145f);

        weightFill.color = bag.IsOverweight ? UIPalette.Warning : UIPalette.Action;

        weightLabel.text = $"{bag.TotalWeight:0.0} / {bag.WeightLimit:0.0} kg"
            + (bag.IsOverweight ? $"  ({EncumbranceName(bag.Encumbrance)})" : string.Empty);

        weightLabel.color = bag.IsOverweight ? UIPalette.Warning : UIPalette.TextDim;
    }

    private static string EncumbranceName(EncumbranceLevel level)
    {
        switch (level)
        {
            case EncumbranceLevel.Heavy:      return "과중량";
            case EncumbranceLevel.Overloaded: return "심한 과중량";
            case EncumbranceLevel.Immobile:   return "움직일 수 없음";
            default:                          return string.Empty;
        }
    }

    // ────────────────────────────────── 우측 — 아이템 상세 (가방 탭)

    private void DrawItemDetail()
    {
        UIFactory.CreatePanel("Back", rightPanel, UIPalette.Panel, Vector2.zero, Vector2.one);

        if (selected?.Definition == null)
        {
            UIFactory.CreateLabel(rightPanel, "칸을 눌러 무엇인지 확인하십시오.", 28,
                FontStyle.Normal, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter,
                UIPalette.TextDim);
            return;
        }

        ItemDefinition definition = selected.Definition;

        UIFactory.CreatePanel("Header", rightPanel, UIPalette.Header,
            new Vector2(0f, 0.88f), new Vector2(1f, 1f));

        UIFactory.CreateLabel(rightPanel, definition.DisplayName, 36, FontStyle.Bold,
            new Vector2(0.04f, 0.88f), new Vector2(0.96f, 1f), TextAnchor.MiddleLeft);

        UIFactory.CreateLabel(rightPanel,
            $"{definition.Weight * selected.Count:0.0} kg    가치 {definition.BaseValue * selected.Count:N0}",
            26, FontStyle.Normal,
            new Vector2(0.04f, 0.80f), new Vector2(0.96f, 0.87f), TextAnchor.MiddleLeft,
            UIPalette.TextAccent);

        UIFactory.CreateLabel(rightPanel, definition.Description, 26, FontStyle.Normal,
            new Vector2(0.04f, 0.52f), new Vector2(0.96f, 0.78f), TextAnchor.UpperLeft,
            UIPalette.TextDim);

        // 젬은 스킬 정보를 덧붙인다. 무엇을 하는 젬인지 모르면 끼울 판단이 안 선다.
        if (definition.IsSkillGem && definition.Skill != null)
            DrawGemInfo(definition.Skill);

        if (definition.IsSkillGem)
        {
            UIFactory.CreateButton(rightPanel, "젬 탭에서 장착",
                new Vector2(0.04f, ActionRowBottom), new Vector2(0.50f, ActionRowTop),
                UIPalette.Action, () => SelectTabKeepingSelection(Tab.Socket));
        }
        else if (definition is EquipmentDefinition equipment)
        {
            DrawEquipmentInfo(equipment);
        }

        DrawDiscardButtons();
    }

    // ────────────────────────────────── 버리기

    /// <summary>상세 패널 맨 아래 행동 줄. 모든 아이템이 같은 높이를 쓴다.</summary>
    private const float ActionRowBottom = 0.03f;
    private const float ActionRowTop = 0.11f;

    /// <summary>
    /// 버리기는 개수를 고르는 팝업을 연다. (InventoryScreenDiscard.cs)
    /// 「1개 / 전부」 두 버튼으로는 15개 중 7개를 버릴 수 없다 —
    /// 과중량은 "몇 kg만 덜어내면 되는가"의 문제다.
    /// </summary>
    private void DrawDiscardButtons()
    {
        if (selected == null || selected.IsEmpty)
            return;

        // 착용 중인 것은 가방에 없다. 버릴 수 없고, 대신 여기서 벗는다.
        if (selectedSlot.HasValue)
        {
            EquipmentSlot slot = selectedSlot.Value;

            UIFactory.CreateButton(rightPanel, "해제",
                new Vector2(0.54f, ActionRowBottom), new Vector2(0.96f, ActionRowTop),
                UIPalette.Action, () => UnequipSelectedSlot(slot), 26);

            return;
        }

        ItemStack target = selected;

        UIFactory.CreateButton(rightPanel, "버리기",
            new Vector2(0.54f, ActionRowBottom), new Vector2(0.96f, ActionRowTop),
            UIPalette.Subtle, () => OpenDiscardPopup(target), 26);
    }

    /// <summary>고른 것에 맞춰 다음에 무엇을 하라고 알려준다.</summary>
    private string DescribeSelection()
    {
        if (selected?.Definition == null)
            return string.Empty;

        string name = selected.Definition.DisplayName;

        if (tab == Tab.Socket)
            return $"「{name}」 — 끼울 자리를 누르십시오.";

        if (selectedSlot.HasValue)
            return $"「{name}」 착용 중 — 오른쪽 아래 「해제」로 벗습니다.";

        return selected.Definition is EquipmentDefinition
            ? $"「{name}」 — 초록색으로 밝아진 자리를 누르십시오."
            : string.Empty;
    }

    private void SelectTabKeepingSelection(Tab next)
    {
        ItemStack keep = selected;

        tab = next;
        selected = keep;

        SetHint(keep?.Definition != null
            ? $"「{keep.Definition.DisplayName}」 — 끼울 자리를 누르십시오."
            : string.Empty);

        Refresh();
    }

    private void DrawGemInfo(SkillDefinition skill)
    {
        string category = skill.Category switch
        {
            SkillCategory.Core => "핵심",
            SkillCategory.Support => "보조",
            SkillCategory.Meta => "발동",
            _ => "유지형"
        };

        var lines = new List<string>(4)
        {
            $"{category}    요구 각성 Lv{skill.RequiredLevel}"
        };

        if (skill.Category == SkillCategory.Support && skill.RequiredTags != SkillTag.None)
            lines.Add($"요구 태그: {skill.RequiredTags.ToKoreanString()}");

        if (skill.Tags != SkillTag.None)
            lines.Add($"태그: {skill.Tags.ToKoreanString()}");

        if (!string.IsNullOrEmpty(skill.CostDescription))
            lines.Add($"대가: {skill.CostDescription}");

        UIFactory.CreateLabel(rightPanel, string.Join("\n", lines), 24, FontStyle.Normal,
            new Vector2(0.04f, 0.13f), new Vector2(0.96f, 0.50f), TextAnchor.UpperLeft,
            UIPalette.Text);
    }

    private void SetHint(string text)
    {
        if (hintLabel != null)
            hintLabel.text = text;
    }

    /// <summary>
    /// 가방 화면을 열어 한 줄을 띄운다. 검증 도구가 결과를 보이려고 쓴다.
    ///
    /// 【왜 필요한가】 Playtest 메뉴는 Debug.Log로만 결과를 말했다.
    /// 에디터 Console 창을 찾아 띄워 두지 않으면 "아무 일도 안 일어난 것"과
    /// 구분되지 않는다. 결과는 결과가 보이는 곳에 있어야 한다.
    /// </summary>
    public static void ShowBagWithMessage(string message)
    {
        InventoryScreenUI screen = EnsureInstance();

        if (screen == null)
            return;

        if (!screen.IsOpen)
            screen.Open((int)Tab.Bag);
        else
            screen.Refresh();

        screen.SetHint(message);
    }

    /// <summary>열려 있을 때만 다시 그린다. 가방을 건드린 쪽이 부른다.</summary>
    public static void RefreshIfOpen()
    {
        if (instance != null && instance.IsOpen)
            instance.Refresh();
    }
}
