using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 무엇을 누르든 뜨는 상세 — 그 안에서 장착 · 해제 · 빼기 · 버리기를 한다.
///
/// 【두 단계였던 것을 한 자리로 모은다.】
/// 전에는 「가방에서 고른다 → 초록으로 밝아진 자리를 누른다」였다.
/// 규칙 자체는 일관됐지만, 두 단계라는 사실을 화면 맨 아래 한 줄로만
/// 알려 줘서 그 줄을 매번 읽어야 했다. 착용 중인 장비를 확인하려고
/// 눌렀을 뿐인데 무엇이 고쳐졌는지도 알기 어려웠다.
///
/// 이제 가방의 아이템 · 착용 중인 장비 · 소켓에 꽂힌 젬 — 무엇을 누르든
/// 같은 모양의 상세가 화면 가운데에 뜨고, 할 수 있는 일이 버튼으로 나온다.
/// 젬은 「끼울 수 있는 자리」가 버튼으로 나열되므로 어디에 들어가는지
/// 눌러 보기 전에 알 수 있다.
///
/// 【판매는 아직 없다.】 상인·상점이 9단계라 붙일 곳이 없다.
/// </summary>
public partial class InventoryScreenUI
{
    private GameObject detailPopup;

    /// <summary>상세 위젯이 그려지는 면. 열려 있지 않으면 null이다.</summary>
    private RectTransform detailContent;

    private ItemStack detailStack;

    /// <summary>착용 중인 것을 눌렀다면 그 자리. 가방의 것이면 null.</summary>
    private EquipmentSlot? detailEquipSlot;

    /// <summary>소켓에 꽂힌 젬을 눌렀다면 그 자리와 스킬.</summary>
    private SlotRef? detailSocket;
    private SkillDefinition detailSkill;

    private bool IsDetailOpen => detailPopup != null;

    // ── 상세 안의 세로 배치 ───────────────────────────────────────────
    // 아래에서부터 닫기 줄 · 행동 줄 · (젬이면) 자리 버튼 · 수치 표 · 설명.

    private const float DetailCloseBottom = 0.020f;
    private const float DetailCloseTop = 0.098f;
    private const float ActionRowBottom = 0.116f;
    private const float ActionRowTop = 0.194f;
    private const float SlotGridBottom = 0.116f;
    private const float SlotGridTop = 0.300f;

    /// <summary>수치 표가 시작하는 높이. 아래 버튼들을 덮지 않는 선이다.</summary>
    private const float StatTop = 0.62f;

    // ── 열고 닫기 ─────────────────────────────────────────────────────

    /// <summary>가방의 아이템, 또는 착용 중인 장비의 상세를 연다.</summary>
    private void OpenItemDetail(ItemStack stack, EquipmentSlot? equipSlot = null)
    {
        if (stack?.Definition == null)
            return;

        CloseItemDetail();

        detailStack = stack;
        detailEquipSlot = equipSlot;
        detailSocket = null;
        detailSkill = null;

        // 뒤쪽 칸도 골라진 것으로 보이게 둔다. 닫으면 같이 풀린다.
        selected = stack;
        selectedSlot = equipSlot;

        BuildDetailPopup();
    }

    /// <summary>소켓에 꽂힌 젬의 상세를 연다. 가방에 없으므로 아이템이 아니다.</summary>
    private void OpenSocketDetail(SkillDefinition skill, SlotRef socket)
    {
        if (skill == null)
            return;

        CloseItemDetail();

        detailStack = null;
        detailEquipSlot = null;
        detailSocket = socket;
        detailSkill = skill;

        BuildDetailPopup();
    }

    private void CloseItemDetail()
    {
        if (detailPopup != null)
            Destroy(detailPopup);

        detailPopup = null;
        detailContent = null;
        detailStack = null;
        detailEquipSlot = null;
        detailSocket = null;
        detailSkill = null;
    }

    /// <summary>닫고 화면을 다시 그린다. 행동 버튼들이 쓴다.</summary>
    private void CloseDetailAndRefresh()
    {
        CloseItemDetail();

        selected = null;
        selectedSlot = null;

        Refresh();
    }

    // ── 틀 ────────────────────────────────────────────────────────────

    private void BuildDetailPopup()
    {
        // 덮개. 상세 밖을 누르면 닫힌다 — 모바일에서 가장 기대되는 동작이다.
        // 패널은 바깥 여백만큼 안으로 들어와 있으므로 넉넉히 넘겨 덮는다.
        Image shade = UIFactory.CreatePanel("DetailShade", panel.transform,
            UIPalette.Dim, new Vector2(-0.2f, -0.2f), new Vector2(1.2f, 1.2f));

        shade.raycastTarget = true;

        var shadeButton = shade.gameObject.AddComponent<Button>();
        shadeButton.targetGraphic = shade;
        shadeButton.transition = Selectable.Transition.None;
        shadeButton.onClick.AddListener(CloseDetailAndRefresh);

        detailPopup = shade.gameObject;

        // 덮개를 화면 끝까지 넘겼으므로 패널 크기로 되돌린 뒤 자리를 잡는다.
        RectTransform frame = UIFactory.CreateRegion("Frame", detailPopup.transform,
            new Vector2(1f / 7f, 1f / 7f), new Vector2(6f / 7f, 6f / 7f));

        RectTransform box = UIFactory.CreateRegion("Box", frame,
            new Vector2(0.27f, 0.075f), new Vector2(0.73f, 0.945f));

        UIFactory.CreateGlass("Back", box, UIPalette.Panel,
            Vector2.zero, Vector2.one, UIFactory.RadiusLarge);

        // 상세 자체는 눌러도 닫히지 않아야 한다. 덮개의 클릭을 여기서 끊는다.
        var blocker = box.gameObject.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0f);

        UIFactory.CreateOutline(blocker, UIPalette.Rim, UIFactory.RadiusLarge, 2);

        detailContent = UIFactory.Inset(
            UIFactory.CreateRegion("Content", box, Vector2.zero, Vector2.one),
            UIFactory.Gap);

        if (detailSkill != null)
            DrawSocketedGemDetail();
        else
            DrawItemDetail();
    }

    // ── 가방의 아이템 · 착용 중인 장비 ────────────────────────────────

    private void DrawItemDetail()
    {
        ItemDefinition definition = detailStack.Definition;

        Color kind = UIPalette.ForItem(definition.Kind);

        // 아이콘 자리. 아트가 들어오면 definition.Icon이 채운다.
        Image icon = UIFactory.CreatePanel("Icon", detailContent, UIPalette.Darken(kind, 0.34f),
            new Vector2(0f, 0.885f), new Vector2(0.14f, 0.99f), UIFactory.Radius);

        UIFactory.CreateOutline(icon, UIPalette.Brighten(kind), UIFactory.Radius, 2);

        if (definition.Icon != null)
        {
            icon.sprite = definition.Icon;
            icon.type = Image.Type.Simple;
            icon.color = Color.white;
            icon.preserveAspect = true;
        }

        Text title = UIFactory.CreateLabel(detailContent, definition.DisplayName, 34,
            FontStyle.Bold, new Vector2(0.18f, 0.925f), new Vector2(1f, 0.99f),
            TextAnchor.LowerLeft, UIPalette.TextOnGlass);

        FitName(title, 34);

        string where = detailEquipSlot.HasValue
            ? $"{ItemKindName(definition.Kind)} · 착용 중 ({EquipmentSlotName(detailEquipSlot.Value)})"
            : ItemKindName(definition.Kind);

        UIFactory.CreateLabel(detailContent, where, 22, FontStyle.Normal,
            new Vector2(0.18f, 0.875f), new Vector2(1f, 0.925f), TextAnchor.UpperLeft,
            UIPalette.TextDim);

        // 무게·가치는 칩으로. 문장에 섞으면 눈이 숫자를 못 찾는다.
        const float ChipWidth = 0.245f;
        const float ChipGap = 0.020f;

        DrawChip(0f, 0.795f, ChipWidth,
            $"{definition.Weight * detailStack.Count:0.0} kg", UIPalette.TextAccent);

        DrawChip(ChipWidth + ChipGap, 0.795f, ChipWidth,
            $"₡ {definition.BaseValue * detailStack.Count:N0}", UIPalette.TextAccent);

        if (detailStack.Count > 1)
        {
            DrawChip((ChipWidth + ChipGap) * 2f, 0.795f, ChipWidth * 0.7f,
                $"×{detailStack.Count}", UIPalette.Text);
        }

        UIFactory.CreateLabel(detailContent, definition.Description, 25, FontStyle.Normal,
            new Vector2(0f, 0.64f), new Vector2(1f, 0.775f), TextAnchor.UpperLeft,
            UIPalette.TextDim);

        if (definition.IsSkillGem && definition.Skill != null)
            DrawGemInfo(definition.Skill);
        else if (definition is EquipmentDefinition equipment)
            DrawEquipmentInfo(equipment);

        DrawDetailActions(definition);
    }

    /// <summary>소켓에 꽂힌 젬 — 가방에 없으므로 무게·가치가 없다.</summary>
    private void DrawSocketedGemDetail()
    {
        Text title = UIFactory.CreateLabel(detailContent, detailSkill.DisplayName, 34,
            FontStyle.Bold, new Vector2(0f, 0.925f), new Vector2(1f, 0.99f),
            TextAnchor.LowerLeft, UIPalette.TextOnGlass);

        FitName(title, 34);

        UIFactory.CreateLabel(detailContent,
            $"젬 · 꽂혀 있음 ({SlotName(detailSocket.Value)})", 22, FontStyle.Normal,
            new Vector2(0f, 0.875f), new Vector2(1f, 0.925f), TextAnchor.UpperLeft,
            UIPalette.TextDim);

        UIFactory.CreateLabel(detailContent, detailSkill.Description, 25, FontStyle.Normal,
            new Vector2(0f, 0.68f), new Vector2(1f, 0.855f), TextAnchor.UpperLeft,
            UIPalette.TextDim);

        DrawGemInfo(detailSkill);

        SlotRef socket = detailSocket.Value;

        UIFactory.CreateButton(detailContent, "빼기",
            new Vector2(0f, ActionRowBottom), new Vector2(1f, ActionRowTop),
            UIPalette.Action, () => TakeGemOut(socket), 28);

        UIFactory.CreateButton(detailContent, "닫기",
            new Vector2(0f, DetailCloseBottom), new Vector2(1f, DetailCloseTop),
            UIPalette.Subtle, CloseDetailAndRefresh, 26);
    }

    // ── 행동 ──────────────────────────────────────────────────────────

    private void DrawDetailActions(ItemDefinition definition)
    {
        ItemStack target = detailStack;

        bool inBag = !detailEquipSlot.HasValue;

        // 아래 줄 — 닫기, 그리고 가방에 있는 것만 버릴 수 있다.
        if (inBag)
        {
            UIFactory.CreateButton(detailContent, "닫기",
                new Vector2(0f, DetailCloseBottom), new Vector2(0.49f, DetailCloseTop),
                UIPalette.Subtle, CloseDetailAndRefresh, 26);

            UIFactory.CreateButton(detailContent, "버리기",
                new Vector2(0.51f, DetailCloseBottom), new Vector2(1f, DetailCloseTop),
                UIPalette.Subtle, () => OpenDiscardPopup(target), 26);
        }
        else
        {
            UIFactory.CreateButton(detailContent, "닫기",
                new Vector2(0f, DetailCloseBottom), new Vector2(1f, DetailCloseTop),
                UIPalette.Subtle, CloseDetailAndRefresh, 26);
        }

        // 착용 중 — 벗는 것만 할 수 있다.
        if (detailEquipSlot.HasValue)
        {
            EquipmentSlot slot = detailEquipSlot.Value;

            UIFactory.CreateButton(detailContent, "장착 해제",
                new Vector2(0f, ActionRowBottom), new Vector2(1f, ActionRowTop),
                UIPalette.Action, () => UnequipFromSlot(slot), 28);

            return;
        }

        // 젬 — 들어갈 수 있는 자리를 버튼으로 나열한다.
        if (definition.IsSkillGem && definition.Skill != null)
        {
            DrawGemSlotButtons(definition.Skill);
            return;
        }

        if (definition is not EquipmentDefinition equipment)
            return;

        EquipmentLoadout loadout = PlayerInventory.EnsureInstance().Loadout;
        EquipmentSlot destination = ResolveEquipSlot(loadout, equipment);

        bool can = loadout.CanEquip(target, destination);

        Button equip = UIFactory.CreateButton(detailContent,
            loadout.Get(destination) != null ? "교체 착용" : "장착",
            new Vector2(0f, ActionRowBottom), new Vector2(1f, ActionRowTop),
            UIPalette.Action, () => EquipFromDetail(target, destination), 28);

        equip.interactable = can;

        if (!can)
        {
            UIFactory.CreateLabel(detailContent,
                DescribeEquipFailure(loadout, equipment, destination), 21, FontStyle.Normal,
                new Vector2(0f, ActionRowTop + 0.012f), new Vector2(1f, ActionRowTop + 0.078f),
                TextAnchor.MiddleCenter, UIPalette.Warning);
        }
    }

    /// <summary>
    /// 이 장비가 들어갈 자리. 각인만 두 자리라 비어 있는 쪽을 먼저 고른다.
    /// 둘 다 차 있으면 1번을 돌려준다 — 그쪽으로 교체된다.
    /// </summary>
    private static EquipmentSlot ResolveEquipSlot(
        EquipmentLoadout loadout, EquipmentDefinition definition)
    {
        bool imprint = definition.Slot == EquipmentSlot.ImprintA
                       || definition.Slot == EquipmentSlot.ImprintB;

        if (!imprint)
            return definition.Slot;

        return loadout.Get(EquipmentSlot.ImprintA) == null
            ? EquipmentSlot.ImprintA
            : loadout.Get(EquipmentSlot.ImprintB) == null
                ? EquipmentSlot.ImprintB
                : EquipmentSlot.ImprintA;
    }

    /// <summary>
    /// 이 젬이 들어갈 수 있는 자리를 전부 버튼으로 깐다.
    ///
    /// 【「끼우기」 한 버튼으로 알아서 넣지 않는 이유】
    /// 보조 젬은 소켓 여섯 자리 중 어디에 꽂느냐가 곧 빌드다.
    /// 1번 핵심에 붙일지 2번에 붙일지를 게임이 대신 정하면
    /// 유저가 고를 것이 사라진다.
    /// </summary>
    private void DrawGemSlotButtons(SkillDefinition skill)
    {
        var targets = new List<SlotRef>(8);

        CollectPlaceableSlots(skill, targets);

        if (targets.Count == 0)
        {
            UIFactory.CreateLabel(detailContent, "지금 끼울 수 있는 자리가 없습니다.", 23,
                FontStyle.Normal, new Vector2(0f, ActionRowBottom), new Vector2(1f, ActionRowTop),
                TextAnchor.MiddleCenter, UIPalette.TextDim);
            return;
        }

        UIFactory.CreateLabel(detailContent, "끼울 자리", 22, FontStyle.Bold,
            new Vector2(0f, SlotGridTop + 0.008f), new Vector2(1f, SlotGridTop + 0.062f),
            TextAnchor.MiddleLeft, UIPalette.TextDim);

        const int Columns = 4;

        int rows = Mathf.CeilToInt(targets.Count / (float)Columns);

        float height = (SlotGridTop - SlotGridBottom) / rows;

        for (int i = 0; i < targets.Count; i++)
        {
            int column = i % Columns;
            int row = i / Columns;

            float left = column / (float)Columns + 0.006f;
            float right = (column + 1) / (float)Columns - 0.006f;

            float bottom = SlotGridTop - (row + 1) * height + 0.008f;
            float top = SlotGridTop - row * height - 0.008f;

            SlotRef captured = targets[i];

            UIFactory.CreateButton(detailContent, SlotName(captured),
                new Vector2(left, bottom), new Vector2(right, top),
                UIPalette.Action, () => PlaceGem(captured), 23);
        }
    }

    private void CollectPlaceableSlots(SkillDefinition skill, List<SlotRef> into)
    {
        SocketedBuild build = SkillManager.EnsureInstance().Build;
        SocketCapacity capacity = build.Capacity;

        for (int c = 0; c < SocketedBuild.MaxCores; c++)
        {
            if (c < capacity.CoreSlots && CanPlace(new SlotRef(SlotKind.Core, c, 0), skill))
                into.Add(new SlotRef(SlotKind.Core, c, 0));

            for (int i = 0; i < SocketedBuild.SocketsPerCore; i++)
            {
                var slot = new SlotRef(SlotKind.Support, c, i);

                if (i < capacity.SocketsIn(c) && CanPlace(slot, skill))
                    into.Add(slot);
            }
        }

        for (int i = 0; i < SocketedBuild.MaxMetas; i++)
        {
            var slot = new SlotRef(SlotKind.Meta, 0, i);

            if (i < capacity.MetaSlots && CanPlace(slot, skill))
                into.Add(slot);
        }

        var herald = new SlotRef(SlotKind.Herald, 0, 0);

        if (capacity.HeraldSlots > 0 && CanPlace(herald, skill))
            into.Add(herald);
    }

    private static string SlotName(SlotRef slot)
    {
        switch (slot.Kind)
        {
            case SlotKind.Core:    return $"핵심 {slot.CoreIndex + 1}";
            case SlotKind.Support: return $"{slot.CoreIndex + 1}-소켓 {slot.Index + 1}";
            case SlotKind.Meta:    return $"발동 {slot.Index + 1}";
            default:               return "전령";
        }
    }
}
