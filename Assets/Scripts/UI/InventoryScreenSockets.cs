using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 가방 화면의 「젬」 탭 — 소켓 배치.
///
/// 조작은 한 가지다 — 【누르면 상세가 뜨고, 그 안에서 한다.】
/// 왼쪽 목록의 젬을 누르면 「끼울 자리」가 버튼으로 나오고,
/// 꽂혀 있는 자리를 누르면 그 젬의 상세와 「빼기」가 나온다.
///
/// 【SkillSelectionUI(레벨업 3장 선택)를 대체한 화면이다.】
/// 레벨업은 선택창을 열지 않는다. 자리가 하나 열릴 뿐이다.
/// </summary>
public partial class InventoryScreenUI
{
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

    /// <summary>
    /// 소켓판을 주어진 칸에 그린다. 【장비 8칸과 같은 자리】다.
    ///
    /// 전에는 화면 오른쪽 절반을 혼자 썼다. 왼쪽에 젬 목록, 오른쪽에 소켓판이라
    /// 장비 화면(위 슬롯 · 아래 목록)과 구조가 달랐다. 이제 둘을 맞춘다 —
    /// 위가 끼우는 자리, 아래가 가진 것.
    /// </summary>
    private void DrawSocketPanel(RectTransform area)
    {
        // 칸 크기를 픽셀로 환산하려면 실제 크기가 확정되어 있어야 한다.
        Canvas.ForceUpdateCanvases();

        SkillManager manager = SkillManager.EnsureInstance();
        SocketedBuild build = manager.Build;
        SocketCapacity capacity = build.Capacity;

        equipTitleLabel.text = $"각성 Lv.{build.AwakeningLevel}";

        int next = SocketUnlockTable.NextUnlockLevel(build.AwakeningLevel);

        // 【고르는 중에는 안내가 우선이다.】 밝아진 칸만으로는
        // "지금 무엇을 기다리는 중인지"와 "어떻게 그만두는지"를 알 수 없다.
        topInfoLabel.text = placingGem != null
            ? $"「{placingGem.Definition.DisplayName}」 — 밝은 자리를 누르십시오 (다른 곳을 누르면 취소)"
            : next > 0
                ? $"다음 개방 Lv.{next} ({SocketUnlockTable.DescribeUnlock(next)})"
                : "전부 개방됨";

        // 핵심 2줄 + 발동·전령 1줄 = 세 줄. 칸 폭은 계산으로 낸다 —
        // 손으로 적으면 좌우 여백이 서로 달라진다.
        const int Columns = 4;
        const int Rows = 3;

        // 【세로 여백을 가로와 같게 만든다.】
        // 가로는 0.012, 세로는 0.05를 쓰고 있었다. 칸이 가로로 길어서
        // 픽셀로 환산하면 가로 9px · 세로 17px — 세로만 벌어져 보였다.
        // 가로 간격을 픽셀로 잰 뒤 그만큼을 세로에 환산한다.
        const float cellGap = 0.012f;

        float width = Mathf.Max(1f, area.rect.width);
        float height = Mathf.Max(1f, area.rect.height);

        float rowGap = cellGap * width / height;

        float cellWidth = (1f - cellGap * (Columns - 1)) / Columns;
        float rowHeight = (1f - rowGap * (Rows - 1)) / Rows;

        float Left(int column) => column * (cellWidth + cellGap);
        float Bottom(int row) => 1f - (row + 1) * rowHeight - row * rowGap;

        for (int c = 0; c < SocketedBuild.MaxCores; c++)
        {
            float y = Bottom(c);

            DrawSlot(area, new SlotRef(SlotKind.Core, c, 0),
                build.GetCore(c), c < capacity.CoreSlots, $"핵심 {c + 1}",
                new Vector2(Left(0), y), new Vector2(Left(0) + cellWidth, y + rowHeight));

            for (int i = 0; i < SocketedBuild.SocketsPerCore; i++)
            {
                DrawSlot(area, new SlotRef(SlotKind.Support, c, i),
                    build.GetSocket(c, i), i < capacity.SocketsIn(c), $"소켓 {i + 1}",
                    new Vector2(Left(i + 1), y),
                    new Vector2(Left(i + 1) + cellWidth, y + rowHeight));
            }
        }

        // 발동 2 + 전령 1 — 같은 격자의 0·1·3열을 쓴다.
        float last = Bottom(Rows - 1);

        for (int i = 0; i < SocketedBuild.MaxMetas; i++)
        {
            DrawSlot(area, new SlotRef(SlotKind.Meta, 0, i),
                build.GetMeta(i), i < capacity.MetaSlots, $"발동 {i + 1}",
                new Vector2(Left(i), last), new Vector2(Left(i) + cellWidth, last + rowHeight));
        }

        DrawSlot(area, new SlotRef(SlotKind.Herald, 0, 0),
            build.Herald, capacity.HeraldSlots > 0, "전령",
            new Vector2(Left(Columns - 1), last),
            new Vector2(Left(Columns - 1) + cellWidth, last + rowHeight));
    }

    private void DrawSlot(RectTransform area, SlotRef slot, SkillDefinition occupant,
                          bool unlocked, string emptyLabel, Vector2 min, Vector2 max)
    {
        SkillDefinition picked = PickedSkill();

        // 고른 젬을 여기에 끼울 수 있으면 테두리 색으로 알린다.
        // "어디에 들어가는지"를 눌러 보기 전에 알 수 있어야 한다.
        bool isCandidate = unlocked && picked != null && CanPlace(slot, picked);

        Color color = !unlocked ? UIPalette.SlotLocked
                    : isCandidate ? UIPalette.SlotSelected
                    : occupant != null ? UIPalette.SkillCategory[(int)occupant.Category]
                    : UIPalette.Slot;

        Image cell = UIFactory.CreatePanel(
            $"Slot_{slot.Kind}_{slot.CoreIndex}_{slot.Index}", area, color, min, max);

        UIFactory.CreateOutline(cell,
            isCandidate ? UIPalette.Brighten(UIPalette.SlotSelected, 0.22f)
                        : unlocked ? UIPalette.EdgeSoft : UIPalette.Edge,
            UIFactory.Radius, isCandidate ? 3 : 2);

        var button = cell.gameObject.AddComponent<Button>();
        button.targetGraphic = cell;
        button.interactable = unlocked;

        SlotRef captured = slot;
        RectTransform cellRect = cell.rectTransform;

        button.onClick.AddListener(() => OnSlotClicked(captured, cellRect));

        string text = !unlocked ? $"{emptyLabel}\n잠김"
                    : occupant != null ? occupant.DisplayName
                    : $"{emptyLabel}\n비어 있음";

        UIFactory.CreateLabel(cell.transform, text, 23, FontStyle.Bold,
            new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f), TextAnchor.MiddleCenter,
            unlocked ? UIPalette.Text : UIPalette.TextDim);
    }

    /// <summary>
    /// 상세에서 「장착」을 눌러 지금 자리를 고르는 중인 젬. 평소에는 null.
    ///
    /// selected와 따로 두는 이유 — selected는 상세가 닫히면 풀린다.
    /// 자리 고르기는 상세가 닫힌 **뒤에** 시작되므로 살아남는 표시가 필요하다.
    /// </summary>
    private ItemStack placingGem;

    /// <summary>
    /// 지금 소켓판이 밝혀 줘야 할 젬. 자리를 고르는 중이면 그것, 아니면 상세의 것.
    /// </summary>
    private SkillDefinition PickedSkill()
    {
        ItemStack source = placingGem ?? selected;

        return source?.Definition != null && source.Definition.IsSkillGem
            ? source.Definition.Skill
            : null;
    }

    /// <summary>
    /// 상세를 닫고 소켓판에서 자리를 고르게 한다. 「장착」 버튼이 부른다.
    ///
    /// 상세가 화면 가운데를 덮고 있어서, 밝아진 자리를 보려면 먼저 치워야 한다.
    /// </summary>
    private void BeginPlacingGem(ItemStack stack)
    {
        if (stack?.Definition == null || !stack.Definition.IsSkillGem)
            return;

        // CloseItemDetail이 placingGem을 비우므로 그 뒤에 세운다.
        CloseItemDetail();

        placingGem = stack;
        selected = stack;
        selectedSlot = null;

        // 소켓판이 보이는 탭이어야 밝혀 봐야 소용이 있다.
        tab = Tab.Socket;

        Refresh();
    }

    /// <summary>고르기를 그만둔다. 밝기가 꺼지고 평소 화면으로 돌아간다.</summary>
    private void CancelPlacingGem()
    {
        if (placingGem == null)
            return;

        placingGem = null;
        selected = null;
        selectedSlot = null;

        Refresh();
    }

    private bool CanPlace(SlotRef slot, SkillDefinition skill)
    {
        SocketedBuild build = SkillManager.EnsureInstance().Build;

        switch (slot.Kind)
        {
            case SlotKind.Core:
                return build.CanEquipCore(skill, slot.CoreIndex) == SocketError.None;

            case SlotKind.Support:
                return build.CanEquipSupport(skill, slot.CoreIndex, slot.Index) == SocketError.None;

            case SlotKind.Meta:
                return build.CanEquipMeta(skill, slot.Index) == SocketError.None;

            default:
                return build.CanEquipHerald(skill) == SocketError.None;
        }
    }

    /// <summary>
    /// 소켓 자리를 눌렀다. 꽂혀 있으면 그 젬의 상세를, 비어 있으면 알림만 띄운다.
    ///
    /// 【빈 자리를 눌러 끼우지 않는다.】
    /// 전에는 「왼쪽에서 고른 뒤 여기를 누른다」였는데, 고른 것이 없는 상태로
    /// 누르면 대신 젬이 빠졌다. 같은 자리를 같은 방식으로 눌러도 결과가
    /// 정반대라 실수로 빼는 일이 잦았다. 이제 넣고 빼는 일은 둘 다 상세에서 한다.
    /// </summary>
    private void OnSlotClicked(SlotRef slot, RectTransform cell)
    {
        // 자리를 고르는 중이면 그것이 먼저다. 밝은 칸이면 넣고, 아니면 그만둔다.
        if (placingGem != null)
        {
            SkillDefinition picked = PickedSkill();

            if (picked != null && CanPlace(slot, picked))
            {
                PlaceGem(slot);
                return;
            }

            CancelPlacingGem();
            ShowToast("끼우기를 그만두었습니다.");
            return;
        }

        SkillDefinition occupant = OccupantOf(slot);

        if (occupant == null)
        {
            ShowToast("빈 자리입니다. 아래에서 젬을 누르고 「장착」을 누르십시오.");
            return;
        }

        // 꽂혀 있는 젬도 가방 칸과 같은 규칙이다 — 빠른 메뉴가 먼저 뜬다.
        var entries = ItemActionMenu.ForEquippedItem(
            "빼기",
            remove: () => TakeGemOut(slot),
            detail: () => OpenSocketDetail(occupant, slot));

        ItemActionMenu.Open(cell, entries);
    }

    private static SkillDefinition OccupantOf(SlotRef slot)
    {
        SocketedBuild build = SkillManager.EnsureInstance().Build;

        switch (slot.Kind)
        {
            case SlotKind.Core:    return build.GetCore(slot.CoreIndex);
            case SlotKind.Support: return build.GetSocket(slot.CoreIndex, slot.Index);
            case SlotKind.Meta:    return build.GetMeta(slot.Index);
            default:               return build.Herald;
        }
    }

    /// <summary>상세의 「끼울 자리」 버튼. 고른 자리에 바로 들어간다.</summary>
    private void PlaceGem(SlotRef slot)
    {
        SkillManager manager = SkillManager.EnsureInstance();
        SkillDefinition picked = PickedSkill();

        if (picked == null)
            return;

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

        if (!equipped)
            return;

        CloseDetailAndRefresh();
    }

    /// <summary>상세의 「빼기」. 젬이 가방으로 돌아간다.</summary>
    private void TakeGemOut(SlotRef slot)
    {
        UnequipSlot(SkillManager.EnsureInstance(), slot);

        CloseDetailAndRefresh();
    }

    private void UnequipSlot(SkillManager manager, SlotRef slot)
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

        if (!removed)
            ShowToast("빼지 못했습니다. 가방에 자리가 있는지 확인하십시오.");
    }
}
