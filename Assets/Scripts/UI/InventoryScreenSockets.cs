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

    /// <summary>머리글 줄의 아래 끝. 회색 띠는 깔지 않고 글자만 얹는다.</summary>
    private const float SocketHeaderBottom = 0.885f;

    private void DrawSocketPanel()
    {
        SkillManager manager = SkillManager.EnsureInstance();
        SocketedBuild build = manager.Build;
        SocketCapacity capacity = build.Capacity;

        int next = SocketUnlockTable.NextUnlockLevel(build.AwakeningLevel);

        string nextText = next > 0
            ? $"다음 개방 Lv.{next} ({SocketUnlockTable.DescribeUnlock(next)})"
            : "전부 개방됨";

        UIFactory.CreateLabel(rightContent,
            $"각성 Lv.{build.AwakeningLevel}", 32, FontStyle.Bold,
            new Vector2(0f, SocketHeaderBottom), new Vector2(0.45f, 1f),
            TextAnchor.MiddleLeft, UIPalette.TextOnGlass);

        UIFactory.CreateLabel(rightContent, nextText, 24, FontStyle.Normal,
            new Vector2(0.45f, SocketHeaderBottom), Vector2.one,
            TextAnchor.MiddleRight, UIPalette.TextDim);

        // 핵심 2줄 — 각 줄이 [핵심][소켓 1][소켓 2][소켓 3]이다.
        //
        // 【칸 폭을 손으로 적지 않는다.】
        // 0.03에서 시작해 0.32, 0.225 간격… 처럼 적어 두면 좌우 여백이
        // 서로 달라진다. 쓸 수 있는 폭을 열 수로 나눠서 계산한다.
        // 좌우 바깥 여백은 rightContent가 이미 들여 놨으므로 0~1을 다 쓴다.
        const int Columns = 4;

        float rowHeight = 0.135f;
        float rowGap = 0.028f;
        float cellGap = 0.014f;

        float cellWidth = (1f - cellGap * (Columns - 1)) / Columns;

        float Left(int column) => column * (cellWidth + cellGap);

        float y = SocketHeaderBottom - rowGap;

        for (int c = 0; c < SocketedBuild.MaxCores; c++)
        {
            y -= rowHeight;

            DrawSlot(new SlotRef(SlotKind.Core, c, 0),
                build.GetCore(c), c < capacity.CoreSlots, $"핵심 {c + 1}",
                new Vector2(Left(0), y), new Vector2(Left(0) + cellWidth, y + rowHeight));

            for (int s = 0; s < SocketedBuild.SocketsPerCore; s++)
            {
                DrawSlot(new SlotRef(SlotKind.Support, c, s),
                    build.GetSocket(c, s), s < capacity.SocketsIn(c), $"소켓 {s + 1}",
                    new Vector2(Left(s + 1), y),
                    new Vector2(Left(s + 1) + cellWidth, y + rowHeight));
            }

            y -= rowGap;
        }

        // 발동 2 + 전령 1 — 같은 격자의 0·1·3열을 쓴다.
        y -= rowHeight;

        for (int i = 0; i < SocketedBuild.MaxMetas; i++)
        {
            DrawSlot(new SlotRef(SlotKind.Meta, 0, i),
                build.GetMeta(i), i < capacity.MetaSlots, $"발동 {i + 1}",
                new Vector2(Left(i), y), new Vector2(Left(i) + cellWidth, y + rowHeight));
        }

        DrawSlot(new SlotRef(SlotKind.Herald, 0, 0),
            build.Herald, capacity.HeraldSlots > 0, "전령",
            new Vector2(Left(Columns - 1), y),
            new Vector2(Left(Columns - 1) + cellWidth, y + rowHeight));
    }

    private void DrawSlot(SlotRef slot, SkillDefinition occupant, bool unlocked,
                          string emptyLabel, Vector2 min, Vector2 max)
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
            $"Slot_{slot.Kind}_{slot.CoreIndex}_{slot.Index}", rightContent, color, min, max);

        UIFactory.CreateOutline(cell,
            isCandidate ? UIPalette.Brighten(UIPalette.SlotSelected, 0.22f)
                        : unlocked ? UIPalette.EdgeSoft : UIPalette.Edge,
            UIFactory.Radius, isCandidate ? 3 : 2);

        var button = cell.gameObject.AddComponent<Button>();
        button.targetGraphic = cell;
        button.interactable = unlocked;

        SlotRef captured = slot;
        button.onClick.AddListener(() => OnSlotClicked(captured));

        string text = !unlocked ? $"{emptyLabel}\n잠김"
                    : occupant != null ? occupant.DisplayName
                    : $"{emptyLabel}\n비어 있음";

        UIFactory.CreateLabel(cell.transform, text, 24, FontStyle.Bold,
            new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f), TextAnchor.MiddleCenter,
            unlocked ? UIPalette.Text : UIPalette.TextDim);
    }

    /// <summary>지금 상세가 열려 있는 것이 젬이면 그 스킬. 아니면 null.</summary>
    private SkillDefinition PickedSkill()
    {
        return selected?.Definition != null && selected.Definition.IsSkillGem
            ? selected.Definition.Skill
            : null;
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
    private void OnSlotClicked(SlotRef slot)
    {
        SkillDefinition occupant = OccupantOf(slot);

        if (occupant != null)
        {
            OpenSocketDetail(occupant, slot);
            return;
        }

        ShowToast("빈 자리입니다. 왼쪽에서 젬을 누른 뒤 「끼울 자리」에서 고르십시오.");
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
