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

    /// <summary>
    /// 읽기만 하는 상세인가. 【남의 칸을 들여다볼 때】 켠다.
    ///
    /// 전리품·상점의 칸은 아직 내 물건이 아니다. 「버리기·장착·퀵슬롯」은
    /// 내 물건에만 뜻이 있으므로, 그 화면에서는 아래 줄을 통째로 비운다.
    /// 그래야 「상세를 보려면 먼저 주워야 하는가」라는 질문이 생기지 않는다.
    /// </summary>
    private bool detailReadOnly;

    /// <summary>읽기 전용일 때 「어디에 있는가」에 적을 말.</summary>
    private string detailWhere;

    private bool IsDetailOpen => detailPopup != null;

    // ── 상세 안의 세로 배치 ───────────────────────────────────────────
    // 【버튼 줄을 하나로 합쳤다.】
    // 전에는 「장착」 한 줄과 「버리기 · 닫기」 한 줄, 둘이 아래에 쌓여 있었다.
    // 세 버튼이 두 층으로 나뉘어 있으면 어느 것이 주된 일인지 알 수 없고,
    // 수치 표가 쓸 자리도 두 줄만큼 줄어든다.
    //
    // 이제 아래는 한 줄이다 — 【버리기(좁게) · 주된 일(넓게)】.
    // 닫는 일은 오른쪽 위 구석의 ✕가 맡는다. 창을 닫는 자리는 원래 거기다.

    private const float ActionRowBottom = 0.022f;
    private const float ActionRowTop = 0.108f;

    /// <summary>「버리기」가 차지하는 폭. 되돌릴 수 없는 일은 작게 둔다.</summary>
    private const float DiscardRight = 0.30f;

    /// <summary>퀵슬롯 줄. 소모품에만 나오므로 수치 표와 겹치지 않는다.</summary>
    private const float QuickRowBottom = 0.400f;
    private const float QuickRowTop = 0.480f;

    /// <summary>수치 표가 시작하는 높이. 아래 버튼들을 덮지 않는 선이다.</summary>
    private const float StatTop = 0.62f;

    // ── 열고 닫기 ─────────────────────────────────────────────────────

    /// <summary>
    /// 바깥(전리품·창고·상점 패널)에서 상세를 연다. 【읽기만 한다.】
    /// 그 칸의 물건은 아직 내 것이 아니므로 옮기지 않는다.
    /// </summary>
    /// <param name="where">「어디에 있는가」에 적을 말. 예: 전리품</param>
    public static void OpenReadOnlyDetail(ItemStack stack, string where)
    {
        InventoryScreenUI screen = EnsureInstance();

        if (screen == null || stack?.Definition == null)
            return;

        screen.CloseItemDetail();

        screen.detailStack = stack;
        screen.detailEquipSlot = null;
        screen.detailSocket = null;
        screen.detailSkill = null;
        screen.detailReadOnly = true;
        screen.detailWhere = where;

        screen.BuildDetailPopup();
    }

    /// <summary>
    /// 바깥에서 「장착」을 눌렀다. 들어갈 자리는 여기서 고른다 —
    /// 각인만 두 자리라 부르는 쪽이 알 필요가 없다.
    /// </summary>
    public static void EquipFromOutside(ItemStack stack)
    {
        if (instance == null || stack?.Definition is not EquipmentDefinition definition)
            return;

        EquipmentLoadout loadout = PlayerInventory.EnsureInstance().Loadout;

        instance.EquipFromDetail(stack, ResolveEquipSlot(loadout, definition));

        instance.Refresh();
    }

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

        detailReadOnly = false;
        detailWhere = null;

        // 상세가 닫히면 「자리 고르는 중」도 끝난다.
        // BeginPlacingGem은 이 함수를 먼저 부른 뒤에 다시 세운다.
        placingGem = null;
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

        // 【폭을 한 겹 줄인다.】 0.46 → 0.41 (약 10%).
        RectTransform box = UIFactory.CreateRegion("Box", frame,
            new Vector2(0.295f, 0.075f), new Vector2(0.705f, 0.945f));

        UIFactory.CreateGlass("Back", box, UIPalette.Panel,
            Vector2.zero, Vector2.one, UIFactory.RadiusLarge);

        // 상세 자체는 눌러도 닫히지 않아야 한다. 덮개의 클릭을 여기서 끊는다.
        var blocker = box.gameObject.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0f);

        UIFactory.CreateOutline(blocker, UIPalette.Rim, UIFactory.RadiusLarge, 2);

        // 【닫는 자리는 오른쪽 위 구석이다.】 아래 줄에 「닫기」를 두면
        // 그 줄이 「장착·버리기」와 섞여 어느 것이 주된 일인지 알 수 없다.
        // 구석의 ✕는 어느 화면에서나 같은 뜻이라 읽을 필요가 없다.
        Image close = UIFactory.CreatePanel("Close", box, UIPalette.Subtle,
            new Vector2(0.855f, 0.905f), new Vector2(0.975f, 0.985f), UIFactory.Radius);

        var closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = close;
        closeButton.onClick.AddListener(CloseDetailAndRefresh);

        UIFactory.CreateOutline(close, UIPalette.Rim, UIFactory.Radius, 2);

        UIFactory.CreateLabel(close.transform, "✕", 26, FontStyle.Bold,
            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, UIPalette.TextOnGlass);

        detailContent = UIFactory.Inset(
            UIFactory.CreateRegion("Content", box, Vector2.zero, Vector2.one),
            UIFactory.Gap);

        // 행 높이를 글자에 맞춰 늘리려면 이 면의 실제 크기를 알아야 한다.
        Canvas.ForceUpdateCanvases();

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

        string where = detailReadOnly
            ? $"{ItemKindName(definition.Kind)} · {detailWhere}"
            : detailEquipSlot.HasValue
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

        // 설명은 길이가 제각각이라 넘치면 굴린다. (UIFactory.CreateScrollText)
        UIFactory.CreateScrollText(detailContent, definition.Description, 25, FontStyle.Normal,
            new Vector2(0f, 0.64f), new Vector2(1f, 0.775f), UIPalette.TextDim);

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

        UIFactory.CreateScrollText(detailContent, detailSkill.Description, 25, FontStyle.Normal,
            new Vector2(0f, 0.68f), new Vector2(1f, 0.855f), UIPalette.TextDim);

        DrawGemInfo(detailSkill);

        SlotRef socket = detailSocket.Value;

        if (detailReadOnly)
            return;

        UIFactory.CreateButton(detailContent, "빼기",
            new Vector2(0f, ActionRowBottom), new Vector2(1f, ActionRowTop),
            UIPalette.Action, () => TakeGemOut(socket), 28);
    }

    // ── 행동 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 아래 한 줄. 【버리기(좁게) · 주된 일(넓게)】.
    ///
    /// 주된 일은 무엇을 눌렀느냐가 정한다 —
    ///   착용 중  → 장착 해제
    ///   젬       → 장착 (자리는 소켓판에서 고른다)
    ///   장비     → 장착 / 교체 착용
    ///   그 밖    → 없음 (버리기만 남는다)
    ///
    /// 읽기만 하는 상세(남의 칸)에는 아무 줄도 없다. 닫는 ✕만 있다.
    /// </summary>
    private void DrawDetailActions(ItemDefinition definition)
    {
        if (detailReadOnly)
            return;

        ItemStack target = detailStack;

        // 착용 중 — 벗는 것만 할 수 있다. 착용 중인 것은 가방에 없으니 버릴 수도 없다.
        if (detailEquipSlot.HasValue)
        {
            EquipmentSlot slot = detailEquipSlot.Value;

            UIFactory.CreateButton(detailContent, "장착 해제",
                new Vector2(0f, ActionRowBottom), new Vector2(1f, ActionRowTop),
                UIPalette.Action, () => UnequipFromSlot(slot), 28);

            return;
        }

        // 【버리기를 왼쪽에, 작게.】
        // 둘이 같은 크기로 나란히 있으면 손가락이 어느 쪽인지 덜 가린다.
        // 되돌릴 수 없는 일은 줄여 둔다.
        UIFactory.CreateButton(detailContent, "버리기",
            new Vector2(0f, ActionRowBottom), new Vector2(DiscardRight, ActionRowTop),
            UIPalette.Subtle, () => OpenDiscardPopup(target), 24);

        // 젬 — 「장착」 하나로 두고, 자리는 소켓판에서 고른다.
        if (definition.IsSkillGem && definition.Skill != null)
        {
            DrawGemEquipButton(definition.Skill, target);
            return;
        }

        if (definition.Kind == ItemKind.Consumable)
        {
            DrawQuickSlotRow(target);
            return;
        }

        if (definition is not EquipmentDefinition equipment)
            return;

        EquipmentLoadout loadout = PlayerInventory.EnsureInstance().Loadout;
        EquipmentSlot destination = ResolveEquipSlot(loadout, equipment);

        bool can = loadout.CanEquip(target, destination);

        Button equip = UIFactory.CreateButton(detailContent,
            loadout.Get(destination) != null ? "교체 착용" : "장착",
            new Vector2(DiscardRight + 0.03f, ActionRowBottom), new Vector2(1f, ActionRowTop),
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
    /// 퀵슬롯 8칸 — 【화면 하단 줄에 걸 자리】.
    ///
    /// 소모품에만 붙인다. 장비는 착용하는 것이고 젬은 끼우는 것이라
    /// 급할 때 한 번 누를 일이 없다. 여덟 칸을 다 보여 주는 이유 —
    /// 「몇 번에 걸까」는 손가락이 기억하는 것이라 유저가 직접 골라야 한다.
    /// 이미 걸린 칸을 다시 누르면 뺀다.
    /// </summary>
    private void DrawQuickSlotRow(ItemStack target)
    {
        QuickSlots quick = PlayerInventory.EnsureInstance().Quick;

        int current = quick.IndexOf(target);

        UIFactory.CreateLabel(detailContent,
            current == QuickSlots.None
                ? "퀵슬롯에 걸기"
                : $"퀵슬롯 {current + 1}번에 걸려 있습니다 — 같은 칸을 누르면 뺍니다",
            22, FontStyle.Bold,
            new Vector2(0f, QuickRowTop + 0.008f), new Vector2(1f, QuickRowTop + 0.062f),
            TextAnchor.MiddleLeft, UIPalette.TextDim);

        for (int i = 0; i < QuickSlots.Count; i++)
        {
            float left = i / (float)QuickSlots.Count + 0.005f;
            float right = (i + 1) / (float)QuickSlots.Count - 0.005f;

            int captured = i;

            Button slot = UIFactory.CreateButton(detailContent, (i + 1).ToString(),
                new Vector2(left, QuickRowBottom), new Vector2(right, QuickRowTop),
                current == i ? UIPalette.Action : UIPalette.Subtle,
                () => AssignQuickSlot(captured, target), 24);

            // 다른 것이 이미 걸린 칸은 색으로만 알린다 — 누르면 밀려난다.
            ItemStack occupant = quick.Get(i);

            if (occupant == null || ReferenceEquals(occupant, target))
                continue;

            var label = slot.GetComponentInChildren<Text>();

            if (label != null)
                label.color = UIPalette.TextAccent;
        }
    }

    private void AssignQuickSlot(int index, ItemStack target)
    {
        PlayerInventory.EnsureInstance().Quick.Assign(index, target);

        RefreshQuickSlots();

        // 상세는 열어 둔다 — 여덟 칸 중 한 번에 고르지 못할 수 있다.
        UIFactory.ClearChildren(detailContent);

        DrawItemDetail();
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
    /// 젬의 「장착」 버튼 하나.
    ///
    /// 【자리 목록을 상세 안에 깔지 않는 이유】
    /// 「핵심 1 / 1-소켓 2 / 발동 1」 같은 이름만 나열하면, 그 이름이
    /// 소켓판의 어느 칸인지 머릿속에서 맞춰 봐야 한다. 최대 열 칸이 넘어
    /// 상세의 절반을 먹기도 했다. 이제 누르면 상세가 닫히고 소켓판에서
    /// 들어갈 수 있는 칸이 직접 밝아진다 — 이름 대신 자리를 보고 고른다.
    ///
    /// 【그래도 자동으로 넣지는 않는다.】
    /// 보조 젬은 소켓 여섯 자리 중 어디에 꽂느냐가 곧 빌드다.
    /// 게임이 대신 정하면 유저가 고를 것이 사라진다.
    /// </summary>
    private void DrawGemEquipButton(SkillDefinition skill, ItemStack target)
    {
        var targets = new List<SlotRef>(8);

        CollectPlaceableSlots(skill, targets);

        bool any = targets.Count > 0;

        Button equip = UIFactory.CreateButton(detailContent, "장착",
            new Vector2(DiscardRight + 0.03f, ActionRowBottom), new Vector2(1f, ActionRowTop),
            UIPalette.Action, () => BeginPlacingGem(target), 28);

        equip.interactable = any;

        if (any)
            return;

        UIFactory.CreateLabel(detailContent, "지금 끼울 수 있는 자리가 없습니다.", 21,
            FontStyle.Normal,
            new Vector2(0f, ActionRowTop + 0.012f), new Vector2(1f, ActionRowTop + 0.078f),
            TextAnchor.MiddleCenter, UIPalette.TextDim);
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
