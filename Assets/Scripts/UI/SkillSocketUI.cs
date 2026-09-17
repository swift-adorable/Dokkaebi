using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인자 소켓 화면 — 주운 인자를 직접 끼우는 곳. SkillSelectionUI를 대체한다.
/// (docs/Blob_Skill_System.md 11·12절)
///
/// 【더 이상 레벨업 때 3장이 뜨지 않는다.】
/// 레벨업은 자리를 열 뿐이고, 무엇을 끼울지는 여기서 유저가 정한다.
///
/// 화면 구성
///   왼쪽 : 핵심 2 × 소켓 3 · 발동 2 · 전령 1 — 지금 열린 자리와 끼워진 인자
///   오른쪽 : 가방에 든 인자 목록
///   조작 : 가방의 인자를 누르고 → 자리를 누르면 끼워진다.
///          끼워진 자리를 그냥 누르면 빠져 가방으로 돌아간다.
///
/// 조이스틱 UI와 같은 이유로 런타임에 코드로 생성한다. (마스터 프롬프트 5-4)
/// </summary>
public class SkillSocketUI : MonoBehaviour
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    /// <summary>자리의 종류.</summary>
    private enum SlotKind { Core, Support, Meta, Herald }

    private readonly struct SlotRef
    {
        public readonly SlotKind Kind;
        public readonly int CoreIndex;
        public readonly int Index;

        public SlotRef(SlotKind kind, int coreIndex, int index)
        {
            Kind = kind;
            CoreIndex = coreIndex;
            Index = index;
        }
    }

    private static readonly Color[] CategoryColors =
    {
        new Color(0.72f, 0.26f, 0.22f, 0.96f),   // Core       — 붉은색
        new Color(0.20f, 0.45f, 0.80f, 0.96f),   // Support    — 푸른색
        new Color(0.60f, 0.42f, 0.14f, 0.96f),   // Meta       — 황금색
        new Color(0.26f, 0.52f, 0.34f, 0.96f)    // Persistent — 초록색
    };

    private static readonly Color EmptyColor  = new Color(0.16f, 0.17f, 0.20f, 0.95f);
    private static readonly Color LockedColor = new Color(0.10f, 0.10f, 0.11f, 0.85f);
    private static readonly Color PickedColor = new Color(0.95f, 0.80f, 0.25f, 1f);

    private static readonly string[] CategoryNames = { "핵심", "보조", "발동", "유지형" };

    private SkillManager manager;
    private Font uiFont;

    private GameObject panel;
    private GameObject toggleButton;
    private Text toggleLabel;
    private Text statusLabel;
    private Text hintLabel;
    private RectTransform buildRoot;
    private RectTransform bagRoot;

    private SkillDefinition picked;

    private readonly List<GameObject> spawned = new();
    private readonly List<ItemStack> bagGems = new();

    /// <summary>패널이 열려 있는지.</summary>
    public bool IsOpen => panel != null && panel.activeSelf;

    /// <summary>소켓 UI를 런타임에 생성한다.</summary>
    public static SkillSocketUI Create()
    {
        var canvasObject = new GameObject("SkillSocketCanvas (Runtime)");
        canvasObject.SetActive(false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        var ui = canvasObject.AddComponent<SkillSocketUI>();
        ui.BuildStatic(canvasObject.transform);

        canvasObject.SetActive(true);

        return ui;
    }

    // ────────────────────────────────── 생성

    private void BuildStatic(Transform parent)
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        BuildToggleButton(parent);
        BuildPanel(parent);
    }

    /// <summary>
    /// 항상 떠 있는 「인자」 버튼. 빈 소켓 수를 같이 보여준다.
    /// 「주웠는데 끼울 데가 없다 / 끼울 데가 생겼다」를 화면에서 바로 읽게 하는 것이 목적이다.
    /// </summary>
    private void BuildToggleButton(Transform parent)
    {
        toggleButton = CreateChild("SocketToggle", parent);

        var rect = toggleButton.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(240f, 110f);
        rect.anchoredPosition = new Vector2(-40f, -40f);

        var image = toggleButton.AddComponent<Image>();
        image.color = new Color(0.20f, 0.22f, 0.28f, 0.92f);

        var button = toggleButton.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(Toggle);

        toggleLabel = CreateLabel(toggleButton.transform, "인자", 34, FontStyle.Bold,
            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
    }

    private void BuildPanel(Transform parent)
    {
        panel = CreateChild("Panel", parent);

        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var background = panel.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.86f);
        background.raycastTarget = true;

        CreateLabel(panel.transform, "인자", 54, FontStyle.Bold,
            new Vector2(0f, 0.91f), new Vector2(1f, 0.98f), TextAnchor.MiddleCenter);

        statusLabel = CreateLabel(panel.transform, string.Empty, 28, FontStyle.Normal,
            new Vector2(0.04f, 0.86f), new Vector2(0.96f, 0.91f), TextAnchor.MiddleCenter);

        if (statusLabel != null)
            statusLabel.color = new Color(0.78f, 0.82f, 0.88f, 1f);

        CreateLabel(panel.transform, "빌드", 32, FontStyle.Bold,
            new Vector2(0.04f, 0.79f), new Vector2(0.50f, 0.85f), TextAnchor.MiddleLeft);

        CreateLabel(panel.transform, "가방", 32, FontStyle.Bold,
            new Vector2(0.54f, 0.79f), new Vector2(0.96f, 0.85f), TextAnchor.MiddleLeft);

        buildRoot = CreateRegion("BuildRoot", panel.transform,
            new Vector2(0.04f, 0.10f), new Vector2(0.50f, 0.79f));

        bagRoot = CreateRegion("BagRoot", panel.transform,
            new Vector2(0.54f, 0.10f), new Vector2(0.96f, 0.79f));

        hintLabel = CreateLabel(panel.transform, string.Empty, 28, FontStyle.Normal,
            new Vector2(0.04f, 0.04f), new Vector2(0.78f, 0.10f), TextAnchor.MiddleLeft);

        if (hintLabel != null)
            hintLabel.color = new Color(1f, 0.92f, 0.78f, 1f);

        CreateButton(panel.transform, "닫기", new Vector2(0.80f, 0.035f), new Vector2(0.96f, 0.095f),
            new Color(0.34f, 0.24f, 0.24f, 0.95f), Close);

        panel.SetActive(false);
    }

    private RectTransform CreateRegion(string name, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject region = CreateChild(name, parent);

        var rect = region.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return rect;
    }

    // ────────────────────────────────── 수명

    private void Start()
    {
        manager = SkillManager.EnsureInstance();

        manager.OnBuildChanged += HandleChanged;
        manager.OnGemGained += HandleGemGained;
        manager.OnSocketsOpened += HandleSocketsOpened;
        manager.OnEquipRejected += HandleEquipRejected;

        RefreshToggle();
    }

    private void OnDestroy()
    {
        if (manager == null)
            return;

        manager.OnBuildChanged -= HandleChanged;
        manager.OnGemGained -= HandleGemGained;
        manager.OnSocketsOpened -= HandleSocketsOpened;
        manager.OnEquipRejected -= HandleEquipRejected;
    }

    private void HandleChanged()
    {
        RefreshToggle();

        if (IsOpen)
            Refresh();
    }

    private void HandleGemGained(SkillDefinition skill)
    {
        RefreshToggle();

        if (IsOpen)
            Refresh();
    }

    private void HandleSocketsOpened(int level, string opened)
    {
        RefreshToggle();

        if (IsOpen)
            Refresh();
    }

    private void HandleEquipRejected(SkillDefinition skill, SocketError error)
    {
        SetHint(SocketErrorText.Describe(error));
    }

    // ────────────────────────────────── 열고 닫기

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    public void Open()
    {
        picked = null;

        Refresh();

        panel.SetActive(true);

        if (GameManager.HasInstance)
            GameManager.Instance.OpenSkill();
    }

    public void Close()
    {
        picked = null;

        panel.SetActive(false);

        if (GameManager.HasInstance)
            GameManager.Instance.CloseSkill();
    }

    /// <summary>「인자」 버튼에 빈 소켓 수를 표시한다.</summary>
    private void RefreshToggle()
    {
        if (toggleLabel == null || manager == null)
            return;

        int free = manager.Build.FreeSocketCount;
        int inBag = manager.GetGemsInBag(bagGems).Count;

        toggleLabel.text = free > 0 && inBag > 0
            ? $"인자\n빈 소켓 {free}"
            : $"인자\n가방 {inBag}";

        toggleLabel.color = free > 0 && inBag > 0 ? PickedColor : Color.white;
    }

    // ────────────────────────────────── 다시 그리기

    private void Refresh()
    {
        ClearSpawned();

        RefreshStatus();
        BuildBuildColumn();
        BuildBagColumn();
    }

    private void RefreshStatus()
    {
        if (statusLabel == null || manager == null)
            return;

        SocketedBuild build = manager.Build;
        SocketCapacity capacity = build.Capacity;

        int next = SocketUnlockTable.NextUnlockLevel(build.AwakeningLevel);

        string nextText = next > 0
            ? $"    다음 개방 Lv.{next} ({SocketUnlockTable.DescribeUnlock(next)})"
            : "    전부 개방됨";

        statusLabel.text =
            $"각성 Lv.{build.AwakeningLevel}    " +
            $"핵심 {capacity.CoreSlots}/{SocketedBuild.MaxCores}    " +
            $"소켓 {capacity.TotalSockets}/{SocketedBuild.MaxCores * SocketedBuild.SocketsPerCore}    " +
            $"발동 {capacity.MetaSlots}/{SocketedBuild.MaxMetas}    " +
            $"전령 {capacity.HeraldSlots}/{SocketedBuild.MaxHeralds}" +
            nextText;
    }

    /// <summary>왼쪽 — 핵심·소켓·발동·전령.</summary>
    private void BuildBuildColumn()
    {
        SocketedBuild build = manager.Build;
        SocketCapacity capacity = build.Capacity;

        // 위에서부터: [핵심1][소켓1 소켓2 소켓3] / [핵심2][…] / [발동1 발동2] / [전령]
        float rowHeight = 0.12f;
        float gap = 0.025f;
        float y = 1f;

        for (int c = 0; c < SocketedBuild.MaxCores; c++)
        {
            y -= rowHeight;

            CreateSlot(buildRoot, new SlotRef(SlotKind.Core, c, 0),
                build.GetCore(c), c < capacity.CoreSlots,
                $"핵심 {c + 1}", new Vector2(0f, y), new Vector2(0.30f, y + rowHeight));

            for (int s = 0; s < SocketedBuild.SocketsPerCore; s++)
            {
                float x0 = 0.32f + s * 0.23f;

                CreateSlot(buildRoot, new SlotRef(SlotKind.Support, c, s),
                    build.GetSocket(c, s), s < capacity.SocketsIn(c),
                    $"소켓 {s + 1}", new Vector2(x0, y), new Vector2(x0 + 0.21f, y + rowHeight));
            }

            y -= gap;
        }

        y -= rowHeight;

        for (int i = 0; i < SocketedBuild.MaxMetas; i++)
        {
            float x0 = i * 0.36f;

            CreateSlot(buildRoot, new SlotRef(SlotKind.Meta, 0, i),
                build.GetMeta(i), i < capacity.MetaSlots,
                $"발동 {i + 1}", new Vector2(x0, y), new Vector2(x0 + 0.34f, y + rowHeight));
        }

        y -= rowHeight + gap;

        CreateSlot(buildRoot, new SlotRef(SlotKind.Herald, 0, 0),
            build.Herald, capacity.HeraldSlots > 0,
            "전령", new Vector2(0f, y), new Vector2(0.34f, y + rowHeight));
    }

    /// <summary>오른쪽 — 가방에 든 인자.</summary>
    private void BuildBagColumn()
    {
        manager.GetGemsInBag(bagGems);

        if (bagGems.Count == 0)
        {
            CreateLabel(bagRoot, "가방에 인자가 없습니다.\n적을 흡수하면 나옵니다.", 30,
                FontStyle.Normal, new Vector2(0f, 0.8f), new Vector2(1f, 1f),
                TextAnchor.UpperLeft);
            return;
        }

        const int columns = 2;
        const int rows = 7;

        float cellWidth = 1f / columns;
        float cellHeight = 1f / rows;

        int shown = Mathf.Min(bagGems.Count, columns * rows);

        for (int i = 0; i < shown; i++)
        {
            SkillDefinition skill = bagGems[i].Definition.Skill;

            int column = i % columns;
            int row = i / columns;

            var min = new Vector2(column * cellWidth + 0.01f, 1f - (row + 1) * cellHeight + 0.008f);
            var max = new Vector2((column + 1) * cellWidth - 0.01f, 1f - row * cellHeight - 0.008f);

            CreateGemButton(skill, min, max);
        }

        if (bagGems.Count > shown)
        {
            CreateLabel(bagRoot, $"… 외 {bagGems.Count - shown}개", 26, FontStyle.Normal,
                new Vector2(0f, 0f), new Vector2(1f, 0.04f), TextAnchor.MiddleRight);
        }
    }

    // ────────────────────────────────── 버튼 생성

    private void CreateSlot(RectTransform parent, SlotRef slot, SkillDefinition occupant,
                            bool unlocked, string emptyLabel, Vector2 min, Vector2 max)
    {
        GameObject slotObject = CreateChild($"Slot_{slot.Kind}_{slot.CoreIndex}_{slot.Index}", parent);
        spawned.Add(slotObject);

        var rect = slotObject.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = slotObject.AddComponent<Image>();

        image.color = !unlocked ? LockedColor
                    : occupant != null ? GetCategoryColor(occupant.Category)
                    : EmptyColor;

        var button = slotObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.interactable = unlocked;

        SlotRef captured = slot;
        button.onClick.AddListener(() => OnSlotClicked(captured));

        string text = !unlocked ? $"{emptyLabel}\n잠김"
                    : occupant != null ? occupant.DisplayName
                    : $"{emptyLabel}\n비어 있음";

        CreateLabel(slotObject.transform, text, 26, FontStyle.Bold,
            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
    }

    private void CreateGemButton(SkillDefinition skill, Vector2 min, Vector2 max)
    {
        GameObject gemObject = CreateChild($"Gem_{skill.Id}", bagRoot);
        spawned.Add(gemObject);

        var rect = gemObject.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = gemObject.AddComponent<Image>();
        image.color = picked == skill ? PickedColor : GetCategoryColor(skill.Category);

        var button = gemObject.AddComponent<Button>();
        button.targetGraphic = image;

        SkillDefinition captured = skill;
        button.onClick.AddListener(() => OnGemClicked(captured));

        // 각성 레벨 미달이면 주울 수는 있어도 끼울 수 없다. 그 사실을 카드에 적어 둔다.
        bool tooHigh = skill.RequiredLevel > manager.Build.AwakeningLevel;

        string header = $"{GetCategoryName(skill.Category)}  Lv{skill.RequiredLevel}" +
                        (tooHigh ? "  (레벨 부족)" : string.Empty);

        CreateLabel(gemObject.transform, header, 22, FontStyle.Normal,
            new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.95f), TextAnchor.UpperLeft);

        CreateLabel(gemObject.transform, skill.DisplayName, 30, FontStyle.Bold,
            new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.64f), TextAnchor.MiddleLeft);

        if (skill.Category == SkillCategory.Support && skill.RequiredTags != SkillTag.None)
        {
            CreateLabel(gemObject.transform, $"요구: {skill.RequiredTags.ToKoreanString()}", 22,
                FontStyle.Normal, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.30f),
                TextAnchor.LowerLeft);
        }
    }

    // ────────────────────────────────── 조작

    private void OnGemClicked(SkillDefinition skill)
    {
        // 같은 것을 다시 누르면 선택 해제.
        picked = picked == skill ? null : skill;

        SetHint(picked == null
            ? string.Empty
            : $"「{picked.DisplayName}」 — 끼울 자리를 누르십시오.");

        Refresh();
    }

    private void OnSlotClicked(SlotRef slot)
    {
        // 고른 인자가 없으면 그 자리를 비운다.
        if (picked == null)
        {
            Unequip(slot);
            return;
        }

        bool equipped;

        switch (slot.Kind)
        {
            case SlotKind.Core:
                equipped = manager.TryEquipCore(picked, slot.CoreIndex);
                break;

            case SlotKind.Support:
                equipped = manager.TryEquipSupport(picked, slot.CoreIndex, slot.Index);
                break;

            case SlotKind.Meta:
                equipped = manager.TryEquipMeta(picked, slot.Index);
                break;

            default:
                equipped = manager.TryEquipHerald(picked);
                break;
        }

        if (equipped)
        {
            SetHint($"「{picked.DisplayName}」 장착");
            picked = null;
        }

        Refresh();
    }

    private void Unequip(SlotRef slot)
    {
        bool removed;

        switch (slot.Kind)
        {
            case SlotKind.Core:
                removed = manager.TryUnequipCore(slot.CoreIndex);
                break;

            case SlotKind.Support:
                removed = manager.TryUnequipSupport(slot.CoreIndex, slot.Index);
                break;

            case SlotKind.Meta:
                removed = manager.TryUnequipMeta(slot.Index);
                break;

            default:
                removed = manager.TryUnequipHerald();
                break;
        }

        if (removed)
            SetHint("인자를 가방으로 되돌렸습니다.");

        Refresh();
    }

    private void SetHint(string text)
    {
        if (hintLabel != null)
            hintLabel.text = text;
    }

    // ────────────────────────────────── 생성 헬퍼

    private void CreateButton(Transform parent, string text, Vector2 min, Vector2 max,
                              Color color, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CreateChild($"Button_{text}", parent);

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = buttonObject.AddComponent<Image>();
        image.color = color;

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        CreateLabel(buttonObject.transform, text, 32, FontStyle.Bold,
            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
    }

    private Text CreateLabel(
        Transform parent, string text, int fontSize, FontStyle style,
        Vector2 anchorMin, Vector2 anchorMax, TextAnchor alignment)
    {
        if (uiFont == null)
            return null;

        GameObject labelObject = CreateChild("Label", parent);

        var rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var label = labelObject.AddComponent<Text>();
        label.font = uiFont;
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = Color.white;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;

        return label;
    }

    private static GameObject CreateChild(string name, Transform parent)
    {
        var child = new GameObject(name);
        child.AddComponent<RectTransform>();
        child.transform.SetParent(parent, false);

        return child;
    }

    private static Color GetCategoryColor(SkillCategory category)
    {
        return CategoryColors[Mathf.Clamp((int)category, 0, CategoryColors.Length - 1)];
    }

    private static string GetCategoryName(SkillCategory category)
    {
        return CategoryNames[Mathf.Clamp((int)category, 0, CategoryNames.Length - 1)];
    }

    private void ClearSpawned()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null)
                Destroy(spawned[i]);
        }

        spawned.Clear();

        // 레이블만 있는 안내 문구도 함께 지운다. (가방이 비었을 때 생성된 것)
        for (int i = bagRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = bagRoot.GetChild(i).gameObject;

            if (child.name == "Label")
                Destroy(child);
        }
    }
}
