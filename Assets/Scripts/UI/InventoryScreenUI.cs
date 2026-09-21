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
    private const float WeightBarLeft = 0.075f;
    private const float WeightBarRight = 0.30f;
    private const float WeightBarBottom = FooterBottom + 0.022f;
    private const float WeightBarTop = FooterTop - 0.022f;

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

    /// <summary>끼울 수 있는 젬이 있을 때 가방 버튼에 붙는 점.</summary>
    private Image socketDot;

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

        hintLabel = UIFactory.CreateLabel(panel.transform, string.Empty, 27, FontStyle.Normal,
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
        image.sprite = UISprites.Rounded(UIFactory.RadiusLarge);
        image.type = Image.Type.Sliced;

        var button = toggleButton.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(Toggle);

        UIFactory.AddGlassSheen(image, UIFactory.RadiusLarge);
        UIFactory.CreateOutline(image, UIPalette.Rim, UIFactory.RadiusLarge, 2);

        toggleLabel = UIFactory.CreateLabel(toggleButton.transform, "가방", 35, FontStyle.Bold,
            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

        // 빈 소켓 알림 점. 버튼 좌상단 모서리에 걸친다.
        socketDot = UIFactory.CreatePanel("SocketDot", toggleButton.transform,
            UIPalette.TextAccent, new Vector2(-0.08f, 0.62f), new Vector2(0.30f, 1.16f),
            radius: 14);

        UIFactory.CreateLabel(socketDot.transform, "0", 24, FontStyle.Bold,
            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, new Color(0.08f, 0.08f, 0.1f));

        socketDot.gameObject.SetActive(false);
    }

    private void BuildTopBar()
    {
        // 크레딧 — 스크린샷의 좌상단 화폐 표시 자리.
        Image purse = UIFactory.CreateGlass("Credits", panel.transform, UIPalette.Header,
            new Vector2(0f, TopBarBottom), new Vector2(0.185f, 1f), UIFactory.RadiusLarge);

        creditLabel = UIFactory.CreateLabel(purse.transform, "₡ 0", 35, FontStyle.Bold,
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
                UIPalette.Inset, () => SelectTab((Tab)captured), 32, UIFactory.RadiusLarge);

            tabButtons.Add(button);
        }
    }

    private void BuildLeftColumn()
    {
        leftColumn = UIFactory.CreateRegion("Left", panel.transform,
            new Vector2(0f, ColumnBottom), new Vector2(LeftRight, ColumnTop));

        UIFactory.CreateGlass("Back", leftColumn, UIPalette.Panel,
            Vector2.zero, Vector2.one, UIFactory.RadiusLarge);

        UIFactory.CreateLabel(leftColumn, "장비", 30, FontStyle.Bold,
            new Vector2(0.035f, 0.92f), new Vector2(0.97f, 0.99f), TextAnchor.MiddleLeft,
            UIPalette.TextDim);

        equipmentGrid = UIFactory.CreateRegion("Equipment", leftColumn,
            new Vector2(0.03f, 0.63f), new Vector2(0.97f, 0.91f));

        bagTitleLabel = UIFactory.CreateLabel(leftColumn, "가방", 30, FontStyle.Bold,
            new Vector2(0.035f, 0.55f), new Vector2(0.97f, 0.62f), TextAnchor.MiddleLeft,
            UIPalette.TextDim);

        // 가방 격자 뒤에 한 단계 눌린 면을 깔아 깊이를 준다.
        UIFactory.CreatePanel("BagWell", leftColumn, UIPalette.Inset,
            new Vector2(0.025f, 0.015f), new Vector2(0.975f, 0.545f), UIFactory.Radius);

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
        // 소지 중량 — 좌하단. 막대 · 숫자를 한 카드 안에 담는다.
        UIFactory.CreateGlass("WeightCard", panel.transform, UIPalette.Panel,
            new Vector2(0f, FooterBottom), new Vector2(0.40f, FooterTop),
            UIFactory.RadiusLarge);

        UIFactory.CreateLabel(panel.transform, "소지 중량", 23, FontStyle.Normal,
            new Vector2(0.018f, FooterBottom), new Vector2(0.07f, FooterTop),
            TextAnchor.MiddleLeft, UIPalette.TextDim);

        UIFactory.CreatePanel("WeightTrack", panel.transform, UIPalette.Inset,
            new Vector2(WeightBarLeft, WeightBarBottom),
            new Vector2(WeightBarRight, WeightBarTop), radius: 6);

        weightFill = UIFactory.CreatePanel("WeightFill", panel.transform, UIPalette.Action,
            new Vector2(WeightBarLeft, WeightBarBottom),
            new Vector2(WeightBarLeft, WeightBarTop), radius: 6);

        weightLabel = UIFactory.CreateLabel(panel.transform, string.Empty, 24, FontStyle.Bold,
            new Vector2(0.305f, FooterBottom), new Vector2(0.395f, FooterTop),
            TextAnchor.MiddleRight, UIPalette.TextDim);

        // 퀵슬롯 1~8 — 하단 중앙.
        quickSlots = UIFactory.CreateRegion("QuickSlots", panel.transform,
            new Vector2(0.42f, FooterBottom), new Vector2(0.855f, FooterTop));

        UIFactory.CreateButton(panel.transform, "닫기",
            new Vector2(0.875f, FooterBottom), new Vector2(1f, FooterTop),
            UIPalette.Subtle, Close, 31, UIFactory.RadiusLarge);
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 검증 패널은 이 화면 뒤에 있다. 켜져 있으면 글자가 비쳐 보인다.
        PlaytestPanelUI.SetHiddenByScreen(true);
#endif
    }

    public void Close()
    {
        CloseDiscardPopup();

        selected = null;
        selectedSlot = null;

        panel.SetActive(false);

        if (GameManager.HasInstance)
            GameManager.Instance.CloseSkill();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        PlaytestPanelUI.SetHiddenByScreen(false);
#endif
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
                image.color = (Tab)i == tab ? UIPalette.Action : UIPalette.Inset;

            // 고른 탭만 글자를 밝힌다. 색만으로는 작은 화면에서 구분이 약하다.
            var label = tabButtons[i].GetComponentInChildren<Text>();

            if (label != null)
                label.color = (Tab)i == tab ? UIPalette.Text : UIPalette.TextDim;
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

        // 【글자로 두 줄을 쓰지 않는다.】
        // 「가방 / 빈 소켓 1」은 버튼 안에서 무슨 뜻인지 읽히지 않는다.
        // 끼울 수 있는 젬이 있다는 신호는 점 하나면 충분하다.
        toggleLabel.text = "가방";
        toggleLabel.color = UIPalette.Text;

        if (socketDot != null)
        {
            socketDot.gameObject.SetActive(canSocket);

            if (socketDot.transform.childCount > 0)
            {
                var count = socketDot.transform.GetChild(0).GetComponent<Text>();

                if (count != null)
                    count.text = free.ToString();
            }
        }
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

            Color kind = stack?.Definition != null
                ? UIPalette.ForItem(stack.Definition.Kind)
                : UIPalette.Slot;

            Color body = isSelectedSlot
                ? UIPalette.SlotSelected
                : canAccept
                    ? UIPalette.SlotEquippable
                    : stack == null
                        ? UIPalette.Inset
                        : UIPalette.Glassify(kind, 0.34f);

            Image cell = UIFactory.CreatePanel($"Equip_{slots[i]}", equipmentGrid,
                body, min, max);

            if (stack != null || isSelectedSlot || canAccept)
                UIFactory.AddGlassSheen(cell, UIFactory.Radius);

            // 낄 수 있는 자리는 테두리까지 밝혀 준다. 바탕색만 바꾸면
            // 어두운 화면에서 「조금 다른 회색」으로만 보인다.
            UIFactory.CreateOutline(cell,
                isSelectedSlot ? UIPalette.Brighten(UIPalette.SlotSelected, 0.22f)
                    : canAccept ? UIPalette.Brighten(UIPalette.SlotEquippable, 0.34f)
                    : stack == null ? UIPalette.EdgeSoft
                    : UIPalette.Brighten(kind),
                UIFactory.Radius,
                isSelectedSlot || canAccept ? 3 : 2);

            // 장비 슬롯도 누를 수 있어야 한다. 이것이 없던 동안에는
            // 에셋 82종을 만들어 놓고 게임에서 입을 방법이 없었다. (docs/Blob_Audit.md A1)
            var slotButton = cell.gameObject.AddComponent<Button>();
            slotButton.targetGraphic = cell;

            EquipmentSlot captured = slots[i];
            slotButton.onClick.AddListener(() => OnEquipSlotClicked(captured));

            // 부위 이름은 항상 작게 위에 남긴다 —
            // 끼고 나면 어느 자리였는지 알 수 없던 문제를 없앤다.
            UIFactory.CreateLabel(cell.transform, EquipmentSlotName(slots[i]), 18,
                FontStyle.Normal, new Vector2(0.07f, 0.58f), new Vector2(0.93f, 0.94f),
                TextAnchor.UpperLeft, UIPalette.TextDim);

            if (stack?.Definition == null)
                continue;

            // 장비 이름은 흰 글자 + 반투명 검정 띠. 종류 색이 밝은 칸에서도 읽힌다.
            Image strip = UIFactory.CreatePanel("NameStrip", cell.transform,
                UIPalette.NameStrip,
                new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.50f), radius: 6);

            strip.raycastTarget = false;

            UIFactory.CreateLabel(strip.transform, stack.Definition.DisplayName, 21,
                FontStyle.Bold, new Vector2(0.06f, 0f), new Vector2(0.94f, 1f),
                TextAnchor.MiddleLeft, Color.white);

            DrawDurabilityBar(cell.transform, stack);
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
            : Mathf.Max(bagStacks.Count, bag.SlotCapacity);

        // 부모(=뷰포트)의 실제 크기를 읽기 전에 레이아웃을 확정시킨다.
        Canvas.ForceUpdateCanvases();

        float viewWidth = bagViewport.rect.width;
        float viewHeight = bagViewport.rect.height;

        // 【칸을 정사각형으로 만든다.】
        // 가로를 열 수로 나눈 값이 한 칸의 변이다. 내용물 높이를
        // 「칸 변 × 줄 수」로 잡으면 세로도 같은 길이가 된다.
        // 예전에는 높이를 뷰포트 기준으로 잡아서 칸이 납작했다.
        float cellSize = viewWidth > 0f ? viewWidth / BagColumns : 0f;

        int needed = Mathf.CeilToInt(cells / (float)BagColumns);

        // 가방 탭은 화면을 채울 만큼은 그린다. 빈 칸이 곧 남은 자리라는 표시다.
        int fits = cellSize > 0f ? Mathf.CeilToInt(viewHeight / cellSize) : BagVisibleRows;

        int rows = Mathf.Max(1, tab == Tab.Socket ? needed : Mathf.Max(needed, fits));

        float contentHeight = cellSize * rows;

        bagGrid.sizeDelta = new Vector2(0f, contentHeight);

        // 여백은 부모 기준 정규화 값이다. 가로와 세로의 기준 길이가 다르므로
        // 같은 픽셀 간격을 내려면 각각 따로 환산해야 한다.
        float gap = cellSize * 0.055f;

        float paddingX = viewWidth > 0f ? gap / viewWidth : BagCellPadding;
        float paddingY = contentHeight > 0f ? gap / contentHeight : BagCellPadding;

        for (int i = 0; i < rows * BagColumns; i++)
        {
            UIFactory.GetCellAnchors(i, BagColumns, rows, paddingX, paddingY,
                out Vector2 min, out Vector2 max);

            ItemStack cellStack = i < bagStacks.Count ? bagStacks[i] : null;

            DrawItemCell($"Bag_{i}", bagGrid, min, max, cellStack);
        }
    }

    /// <summary>
    /// 아이템 한 칸. 가방·전리품이 같은 모양을 쓴다.
    ///
    /// 구성 — 바탕(종류 색을 눌러서) · 윤곽(종류 색을 밝혀서) ·
    ///        이름(아래 정렬) · 개수 배지(우하단) · 내구도 막대(맨 아래)
    ///
    /// 【개수 배지를 알약으로 까는 이유】
    /// 칸 배경색이 종류마다 달라서 글자만 얹으면 밝은 칸에서 숫자가 사라진다.
    /// 어두운 알약 위에 얹으면 어떤 칸에서도 같은 크기로 읽힌다.
    /// </summary>
    private void DrawItemCell(
        string name, Transform parent, Vector2 min, Vector2 max, ItemStack stack)
    {
        bool empty = stack?.Definition == null;
        bool chosen = !empty && stack == selected;

        Color kind = empty ? UIPalette.Slot : UIPalette.ForItem(stack.Definition.Kind);

        Color body = empty
            ? UIPalette.Inset
            : chosen
                ? UIPalette.SlotSelected
                : UIPalette.Glassify(kind, 0.34f);

        Image cell = UIFactory.CreatePanel(name, parent, body, min, max);

        // 빈 칸에는 광택을 얹지 않는다. 격자 전체가 번들거려 아이템이 묻힌다.
        if (!empty)
            UIFactory.AddGlassSheen(cell, UIFactory.Radius);

        UIFactory.CreateOutline(cell,
            empty ? UIPalette.EdgeSoft
                  : chosen ? UIPalette.Brighten(UIPalette.SlotSelected, 0.22f)
                           : UIPalette.Glassify(UIPalette.Brighten(kind, 0.34f), 0.70f),
            UIFactory.Radius,
            chosen ? 3 : 2);

        var button = cell.gameObject.AddComponent<Button>();
        button.targetGraphic = cell;
        button.interactable = !empty;

        ItemStack captured = stack;
        button.onClick.AddListener(() => SelectStack(captured));

        if (empty)
            return;

        // 종류 도형. 가운데에 크게 깔아 배경처럼 쓴다 —
        // 글자를 읽기 전에 「무기인가 재료인가」가 먼저 들어온다.
        // 실제 그림이 생기면 definition.Icon이 이 자리를 대신한다.
        Image glyph = UIFactory.CreatePanel("Glyph", cell.transform, UIPalette.GlyphTint,
            new Vector2(0.20f, 0.16f), new Vector2(0.80f, 0.76f), radius: 0);

        glyph.raycastTarget = false;
        glyph.preserveAspect = true;

        if (stack.Definition.Icon != null)
        {
            glyph.sprite = stack.Definition.Icon;
            glyph.color = Color.white;
        }
        else
        {
            glyph.sprite = UISprites.Of(UISprites.GlyphFor(stack.Definition.Kind));
        }

        // 이름은 좌상단. 반투명 검정 띠를 깔아 어떤 칸 색 위에서도 흰 글자가 읽히게 한다.
        Image strip = UIFactory.CreatePanel("NameStrip", cell.transform, UIPalette.NameStrip,
            new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.96f), radius: 6);

        strip.raycastTarget = false;

        Text nameLabel = UIFactory.CreateLabel(strip.transform, stack.Definition.DisplayName,
            19, FontStyle.Bold, new Vector2(0.06f, 0f), new Vector2(0.94f, 1f),
            TextAnchor.MiddleLeft, Color.white);

        nameLabel.horizontalOverflow = HorizontalWrapMode.Wrap;

        // 개수는 우하단. 겹칠 수 있는 물건에만 뜬다 —
        // 1개짜리에 「1」을 붙이면 잡음이다.
        if (stack.Count > 1)
        {
            UIFactory.CreateBadge(cell.transform, stack.Count.ToString(),
                new Vector2(0.56f, 0.06f), new Vector2(0.96f, 0.30f), 22,
                Color.white);
        }

        DrawDurabilityBar(cell.transform, stack);
    }

    /// <summary>내구도 막대. 없는 아이템에는 그리지 않는다.</summary>
    private static void DrawDurabilityBar(Transform cell, ItemStack stack)
    {
        if (stack.Definition == null || !stack.Definition.HasDurability)
            return;

        if (stack.MaxDurability <= 0)
            return;

        float ratio = Mathf.Clamp01(stack.Durability / (float)stack.MaxDurability);

        UIFactory.CreatePanel("DurTrack", cell, UIPalette.NameStrip,
            new Vector2(0.06f, 0.035f), new Vector2(0.50f, 0.085f), radius: 3);

        Color color = stack.IsBroken
            ? UIPalette.Warning
            : stack.IsWorn
                ? UIPalette.Cost
                : UIPalette.Gain;

        UIFactory.CreatePanel("DurFill", cell, color,
            new Vector2(0.06f, 0.035f),
            new Vector2(0.06f + (0.50f - 0.06f) * ratio, 0.085f), radius: 3);
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
                UIPalette.Inset, min, max);

            UIFactory.CreateOutline(cell, UIPalette.EdgeSoft, UIFactory.Radius, 2);

            UIFactory.CreateLabel(cell.transform, (i + 1).ToString(), 21, FontStyle.Normal,
                new Vector2(0.1f, 0.05f), new Vector2(0.88f, 0.4f),
                TextAnchor.LowerRight, UIPalette.TextDim);
        }
    }

    private void RefreshWeight()
    {
        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        float ratio = bag.WeightLimit <= 0f ? 0f : bag.TotalWeight / bag.WeightLimit;

        // 막대는 꽉 차면 멈추되 문구는 실제 값을 보여 준다. 무게는 한도를 넘을 수 있다.
        float clamped = Mathf.Clamp01(ratio);

        // 【좌표를 여기서 다시 적지 않는다.】
        // 예전에는 0.10~0.30을 하드코딩해 두었는데, 배치를 바꾸면서
        // 막대는 0.08~0.245로 옮겼고 이 줄만 남아 채움이 어긋났다.
        // 이제 상수에서 계산한다.
        weightFill.rectTransform.anchorMin = new Vector2(WeightBarLeft, WeightBarBottom);

        weightFill.rectTransform.anchorMax = new Vector2(
            WeightBarLeft + (WeightBarRight - WeightBarLeft) * clamped, WeightBarTop);

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
        UIFactory.CreateGlass("Back", rightPanel, UIPalette.Panel,
            Vector2.zero, Vector2.one, UIFactory.RadiusLarge);

        if (selected?.Definition == null)
        {
            UIFactory.CreateLabel(rightPanel, "칸을 눌러 무엇인지 확인하십시오.", 29,
                FontStyle.Normal, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter,
                UIPalette.TextDim);
            return;
        }

        ItemDefinition definition = selected.Definition;

        Color kind = UIPalette.ForItem(definition.Kind);

        // 머리 띠는 아이템 종류 색을 쓴다 — 어떤 부류인지 색으로 먼저 읽힌다.
        Image band = UIFactory.CreatePanel("Header", rightPanel,
            UIPalette.Glassify(kind, 0.50f),
            new Vector2(0f, 0.865f), new Vector2(1f, 1f), UIFactory.RadiusLarge);

        UIFactory.AddGlassSheen(band, UIFactory.RadiusLarge);

        // 아이콘 자리. 아트가 들어오면 definition.Icon이 채운다.
        Image icon = UIFactory.CreatePanel("Icon", rightPanel, UIPalette.Darken(kind, 0.34f),
            new Vector2(0.035f, 0.885f), new Vector2(0.115f, 0.98f), UIFactory.Radius);

        UIFactory.CreateOutline(icon, UIPalette.Brighten(kind), UIFactory.Radius, 2);

        if (definition.Icon != null)
        {
            icon.sprite = definition.Icon;
            icon.type = Image.Type.Simple;
            icon.color = Color.white;
            icon.preserveAspect = true;
        }

        UIFactory.CreateLabel(rightPanel, definition.DisplayName, 37, FontStyle.Bold,
            new Vector2(0.135f, 0.925f), new Vector2(0.96f, 0.99f), TextAnchor.LowerLeft,
            UIPalette.TextOnGlass);

        UIFactory.CreateLabel(rightPanel, ItemKindName(definition.Kind), 22, FontStyle.Normal,
            new Vector2(0.135f, 0.875f), new Vector2(0.60f, 0.925f), TextAnchor.UpperLeft,
            UIPalette.TextDim);

        // 무게·가치는 칩으로. 문장에 섞으면 눈이 숫자를 못 찾는다.
        DrawChip(0.035f, 0.795f, 0.24f,
            $"{definition.Weight * selected.Count:0.0} kg", UIPalette.TextAccent);

        DrawChip(0.255f, 0.795f, 0.24f,
            $"₡ {definition.BaseValue * selected.Count:N0}", UIPalette.TextAccent);

        if (selected.Count > 1)
            DrawChip(0.475f, 0.795f, 0.19f, $"×{selected.Count}", UIPalette.Text);

        UIFactory.CreateLabel(rightPanel, definition.Description, 26, FontStyle.Normal,
            new Vector2(0.04f, 0.63f), new Vector2(0.96f, 0.775f), TextAnchor.UpperLeft,
            UIPalette.TextDim);

        // 젬은 스킬 정보를 덧붙인다. 무엇을 하는 젬인지 모르면 끼울 판단이 안 선다.
        if (definition.IsSkillGem && definition.Skill != null)
            DrawGemInfo(definition.Skill);

        if (definition.IsSkillGem)
        {
            UIFactory.CreateButton(rightPanel, "젬 탭에서 장착",
                new Vector2(0.04f, ActionRowBottom), new Vector2(0.50f, ActionRowTop),
                UIPalette.Action, () => SelectTabKeepingSelection(Tab.Socket), 27);
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
                UIPalette.Action, () => UnequipSelectedSlot(slot), 28);

            return;
        }

        ItemStack target = selected;

        UIFactory.CreateButton(rightPanel, "버리기",
            new Vector2(0.54f, ActionRowBottom), new Vector2(0.96f, ActionRowTop),
            UIPalette.Subtle, () => OpenDiscardPopup(target), 28);
    }

    /// <summary>작은 알약 칩. 무게·가치처럼 짧은 수치를 담는다.</summary>
    private void DrawChip(float left, float bottom, float width, string text, Color color)
    {
        Image chip = UIFactory.CreatePanel($"Chip_{text}", rightPanel, UIPalette.Inset,
            new Vector2(left, bottom), new Vector2(left + width, bottom + 0.058f), radius: 8);

        UIFactory.CreateOutline(chip, UIPalette.EdgeSoft, 8, 1);

        UIFactory.CreateLabel(chip.transform, text, 24, FontStyle.Bold,
            new Vector2(0.08f, 0f), new Vector2(0.92f, 1f), TextAnchor.MiddleCenter, color);
    }

    /// <summary>
    /// 표 한 줄. 좌측에 이름, 우측에 값.
    ///
    /// 홀짝으로 바탕을 번갈아 까는 이유 — 값이 여러 줄이면
    /// 어느 값이 어느 이름의 것인지 눈이 놓친다.
    /// </summary>
    private void DrawStatRow(int index, float top, string label, string value, Color valueColor)
    {
        const float RowHeight = 0.052f;
        const float Gap = 0.006f;

        float bottom = top - (index + 1) * RowHeight - index * Gap;

        Image row = UIFactory.CreatePanel($"Row_{index}", rightPanel,
            index % 2 == 0 ? UIPalette.Row : UIPalette.RowAlt,
            new Vector2(0.04f, bottom), new Vector2(0.96f, bottom + RowHeight), radius: 6);

        UIFactory.CreateLabel(row.transform, label, 24, FontStyle.Normal,
            new Vector2(0.04f, 0f), new Vector2(0.60f, 1f), TextAnchor.MiddleLeft,
            UIPalette.TextDim);

        UIFactory.CreateLabel(row.transform, value, 24, FontStyle.Bold,
            new Vector2(0.60f, 0f), new Vector2(0.96f, 1f), TextAnchor.MiddleRight,
            valueColor);
    }

    private static string ItemKindName(ItemKind kind)
    {
        switch (kind)
        {
            case ItemKind.Weapon:     return "무기";
            case ItemKind.Armour:     return "방어구";
            case ItemKind.Backpack:   return "가방";
            case ItemKind.Imprint:    return "각인";
            case ItemKind.SkillGem:   return "젬";
            case ItemKind.Consumable: return "소모품";
            case ItemKind.Key:        return "열쇠";
            case ItemKind.Material:   return "재료";
            default:                  return kind.ToString();
        }
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

        var lines = new List<string>(5)
        {
            $"분류: {category}",
            $"요구 각성: Lv{skill.RequiredLevel}"
        };

        if (skill.Category == SkillCategory.Support && skill.RequiredTags != SkillTag.None)
            lines.Add($"요구 태그: {skill.RequiredTags.ToKoreanString()}");

        if (skill.Tags != SkillTag.None)
            lines.Add($"태그: {skill.Tags.ToKoreanString()}");

        if (!string.IsNullOrEmpty(skill.CostDescription))
            lines.Add($"대가: {skill.CostDescription}");

        for (int i = 0; i < lines.Count; i++)
        {
            int split = lines[i].IndexOf(':');

            string label = split > 0 ? lines[i].Substring(0, split) : lines[i];
            string value = split > 0 ? lines[i].Substring(split + 1).Trim() : string.Empty;

            DrawStatRow(i, 0.60f, label, value, UIPalette.Text);
        }
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
