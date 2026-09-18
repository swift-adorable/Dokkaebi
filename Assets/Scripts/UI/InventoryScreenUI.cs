using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 가방 화면 — 장비 · 가방 · 인자 소켓 · 패시브를 한 화면의 탭으로 묶는다.
/// 배치는 덕코프 스크린샷을 따르고, 【조작은 전부 터치】다.
///
/// 화면 구성
///   상단 중앙 : 탭 (가방 / 인자 / 패시브)
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

    private static readonly string[] TabNames = { "가방", "인자", "패시브" };

    private const int BagColumns = 6;
    private const int BagRows = 6;

    private static InventoryScreenUI instance;

    private GameObject panel;
    private GameObject toggleButton;
    private Text toggleLabel;

    private RectTransform leftColumn;
    private RectTransform equipmentGrid;
    private RectTransform bagGrid;
    private RectTransform rightPanel;
    private RectTransform quickSlots;

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
        BuildToggleButton(parent);

        panel = UIFactory.CreateRegion("Panel", parent, Vector2.zero, Vector2.one).gameObject;

        UIFactory.CreatePanel("Dim", panel.transform, UIPalette.Dim, Vector2.zero, Vector2.one);

        BuildTopBar();
        BuildLeftColumn();

        rightPanel = UIFactory.CreateRegion("Right", panel.transform,
            new Vector2(0.53f, 0.17f), new Vector2(0.985f, 0.87f));

        BuildBottomBar();

        hintLabel = UIFactory.CreateLabel(panel.transform, string.Empty, 24, FontStyle.Normal,
            new Vector2(0.015f, 0.005f), new Vector2(0.52f, 0.045f),
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
        rect.sizeDelta = new Vector2(240f, 110f);
        rect.anchoredPosition = new Vector2(-40f, -40f);

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
            new Vector2(0.015f, 0.905f), new Vector2(0.20f, 0.975f));

        creditLabel = UIFactory.CreateLabel(purse.transform, "₡ 0", 32, FontStyle.Bold,
            new Vector2(0.06f, 0f), new Vector2(0.94f, 1f), TextAnchor.MiddleRight,
            UIPalette.TextAccent);

        // 탭 — 스크린샷의 상단 중앙 아이콘 줄. 아트 전이라 글자로 둔다.
        float width = 0.10f;
        float gap = 0.008f;
        float total = TabNames.Length * width + (TabNames.Length - 1) * gap;
        float startX = 0.5f - total * 0.5f;

        tabButtons.Clear();

        for (int i = 0; i < TabNames.Length; i++)
        {
            float x = startX + i * (width + gap);
            int captured = i;

            Button button = UIFactory.CreateButton(panel.transform, TabNames[i],
                new Vector2(x, 0.905f), new Vector2(x + width, 0.975f),
                UIPalette.Subtle, () => SelectTab((Tab)captured), 30);

            tabButtons.Add(button);
        }
    }

    private void BuildLeftColumn()
    {
        leftColumn = UIFactory.CreateRegion("Left", panel.transform,
            new Vector2(0.015f, 0.17f), new Vector2(0.50f, 0.87f));

        UIFactory.CreatePanel("Back", leftColumn, UIPalette.Panel, Vector2.zero, Vector2.one);

        UIFactory.CreateLabel(leftColumn, "장비", 30, FontStyle.Bold,
            new Vector2(0.03f, 0.92f), new Vector2(0.97f, 0.99f), TextAnchor.MiddleLeft);

        equipmentGrid = UIFactory.CreateRegion("Equipment", leftColumn,
            new Vector2(0.03f, 0.63f), new Vector2(0.97f, 0.91f));

        bagTitleLabel = UIFactory.CreateLabel(leftColumn, "가방", 30, FontStyle.Bold,
            new Vector2(0.03f, 0.55f), new Vector2(0.97f, 0.62f), TextAnchor.MiddleLeft);

        bagGrid = UIFactory.CreateRegion("Bag", leftColumn,
            new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.54f));
    }

    private void BuildBottomBar()
    {
        // 소지 중량 막대 — 스크린샷의 좌하단.
        UIFactory.CreateLabel(panel.transform, "소지 중량", 24, FontStyle.Normal,
            new Vector2(0.015f, 0.10f), new Vector2(0.10f, 0.155f),
            TextAnchor.MiddleLeft, UIPalette.TextDim);

        UIFactory.CreatePanel("WeightTrack", panel.transform, UIPalette.Slot,
            new Vector2(0.10f, 0.11f), new Vector2(0.30f, 0.145f));

        weightFill = UIFactory.CreatePanel("WeightFill", panel.transform, UIPalette.Action,
            new Vector2(0.10f, 0.11f), new Vector2(0.10f, 0.145f));

        weightLabel = UIFactory.CreateLabel(panel.transform, string.Empty, 24, FontStyle.Bold,
            new Vector2(0.31f, 0.10f), new Vector2(0.50f, 0.155f),
            TextAnchor.MiddleLeft, UIPalette.TextDim);

        // 퀵슬롯 — 스크린샷의 하단 중앙 1~8.
        quickSlots = UIFactory.CreateRegion("QuickSlots", panel.transform,
            new Vector2(0.33f, 0.05f), new Vector2(0.70f, 0.145f));

        UIFactory.CreateButton(panel.transform, "닫기",
            new Vector2(0.88f, 0.05f), new Vector2(0.985f, 0.14f),
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

        Refresh();

        panel.SetActive(true);

        if (GameManager.HasInstance)
            GameManager.Instance.OpenSkill();
    }

    public void Close()
    {
        selected = null;

        panel.SetActive(false);

        if (GameManager.HasInstance)
            GameManager.Instance.CloseSkill();
    }

    private void SelectTab(Tab next)
    {
        tab = next;
        selected = null;

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

        rightPanel.anchorMin = new Vector2(showLeft ? 0.53f : 0.015f, 0.17f);

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

            Color color = stack == null
                ? UIPalette.Slot
                : UIPalette.ForItem(stack.Definition.Kind);

            Image cell = UIFactory.CreatePanel($"Equip_{slots[i]}", equipmentGrid,
                color, min, max);

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

        bagStacks.Clear();
        bagStacks.AddRange(bag.Stacks);

        bagTitleLabel.text = $"가방 ({bag.UsedSlots}/{bag.SlotCapacity})";

        int cells = Mathf.Min(bag.SlotCapacity, BagColumns * BagRows);

        for (int i = 0; i < cells; i++)
        {
            UIFactory.GetCellAnchors(i, BagColumns, BagRows, 0.006f,
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

        SetHint(selected == null
            ? string.Empty
            : tab == Tab.Socket
                ? $"「{selected.Definition.DisplayName}」 — 끼울 자리를 누르십시오."
                : string.Empty);

        Refresh();
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

        // 인자는 스킬 정보를 덧붙인다. 무엇을 하는 인자인지 모르면 끼울 판단이 안 선다.
        if (definition.IsSkillGem && definition.Skill != null)
            DrawGemInfo(definition.Skill);

        if (definition.IsSkillGem)
        {
            UIFactory.CreateButton(rightPanel, "인자 탭에서 장착",
                new Vector2(0.04f, 0.04f), new Vector2(0.50f, 0.12f),
                UIPalette.Action, () => SelectTabKeepingSelection(Tab.Socket));
        }
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
            new Vector2(0.04f, 0.16f), new Vector2(0.96f, 0.50f), TextAnchor.UpperLeft,
            UIPalette.Text);
    }

    private void SetHint(string text)
    {
        if (hintLabel != null)
            hintLabel.text = text;
    }
}
