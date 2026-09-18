using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 가방 화면의 「인자」 탭 — 소켓 배치.
///
/// 조작은 한 가지다 — 왼쪽 가방에서 인자를 누르고, 오른쪽 자리를 누른다.
/// 고른 것이 없는 상태로 자리를 누르면 그 자리가 비고 인자는 가방으로 돌아간다.
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

    private void DrawSocketPanel()
    {
        UIFactory.CreatePanel("Back", rightPanel, UIPalette.Panel, Vector2.zero, Vector2.one);

        SkillManager manager = SkillManager.EnsureInstance();
        SocketedBuild build = manager.Build;
        SocketCapacity capacity = build.Capacity;

        UIFactory.CreatePanel("Header", rightPanel, UIPalette.Header,
            new Vector2(0f, 0.90f), new Vector2(1f, 1f));

        int next = SocketUnlockTable.NextUnlockLevel(build.AwakeningLevel);

        string nextText = next > 0
            ? $"다음 개방 Lv.{next} ({SocketUnlockTable.DescribeUnlock(next)})"
            : "전부 개방됨";

        UIFactory.CreateLabel(rightPanel,
            $"각성 Lv.{build.AwakeningLevel}", 32, FontStyle.Bold,
            new Vector2(0.03f, 0.90f), new Vector2(0.45f, 1f), TextAnchor.MiddleLeft);

        UIFactory.CreateLabel(rightPanel, nextText, 24, FontStyle.Normal,
            new Vector2(0.45f, 0.90f), new Vector2(0.97f, 1f), TextAnchor.MiddleRight,
            UIPalette.TextDim);

        // 핵심 2줄 — 각 줄이 [핵심][소켓 1][소켓 2][소켓 3]이다.
        float rowHeight = 0.135f;
        float gap = 0.03f;
        float y = 0.88f;

        for (int c = 0; c < SocketedBuild.MaxCores; c++)
        {
            y -= rowHeight;

            DrawSlot(new SlotRef(SlotKind.Core, c, 0),
                build.GetCore(c), c < capacity.CoreSlots, $"핵심 {c + 1}",
                new Vector2(0.03f, y), new Vector2(0.30f, y + rowHeight));

            for (int s = 0; s < SocketedBuild.SocketsPerCore; s++)
            {
                float x0 = 0.32f + s * 0.225f;

                DrawSlot(new SlotRef(SlotKind.Support, c, s),
                    build.GetSocket(c, s), s < capacity.SocketsIn(c), $"소켓 {s + 1}",
                    new Vector2(x0, y), new Vector2(x0 + 0.205f, y + rowHeight));
            }

            y -= gap;
        }

        // 발동 2 + 전령 1
        y -= rowHeight;

        for (int i = 0; i < SocketedBuild.MaxMetas; i++)
        {
            float x0 = 0.03f + i * 0.30f;

            DrawSlot(new SlotRef(SlotKind.Meta, 0, i),
                build.GetMeta(i), i < capacity.MetaSlots, $"발동 {i + 1}",
                new Vector2(x0, y), new Vector2(x0 + 0.28f, y + rowHeight));
        }

        DrawSlot(new SlotRef(SlotKind.Herald, 0, 0),
            build.Herald, capacity.HeraldSlots > 0, "전령",
            new Vector2(0.63f, y), new Vector2(0.91f, y + rowHeight));

        UIFactory.CreateLabel(rightPanel,
            "가방에서 인자를 고른 뒤 자리를 누르십시오. 끼워진 자리를 그냥 누르면 빠집니다.",
            22, FontStyle.Normal,
            new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.10f),
            TextAnchor.MiddleLeft, UIPalette.TextDim);
    }

    private void DrawSlot(SlotRef slot, SkillDefinition occupant, bool unlocked,
                          string emptyLabel, Vector2 min, Vector2 max)
    {
        SkillDefinition picked = PickedSkill();

        // 고른 인자를 여기에 끼울 수 있으면 테두리 색으로 알린다.
        // "어디에 들어가는지"를 눌러 보기 전에 알 수 있어야 한다.
        bool isCandidate = unlocked && picked != null && CanPlace(slot, picked);

        Color color = !unlocked ? UIPalette.SlotLocked
                    : isCandidate ? UIPalette.SlotSelected
                    : occupant != null ? UIPalette.SkillCategory[(int)occupant.Category]
                    : UIPalette.Slot;

        Image cell = UIFactory.CreatePanel(
            $"Slot_{slot.Kind}_{slot.CoreIndex}_{slot.Index}", rightPanel, color, min, max);

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

    /// <summary>가방에서 고른 것이 인자면 그 스킬. 아니면 null.</summary>
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

    private void OnSlotClicked(SlotRef slot)
    {
        SkillManager manager = SkillManager.EnsureInstance();
        SkillDefinition picked = PickedSkill();

        // 고른 인자가 없으면 그 자리를 비운다.
        if (picked == null)
        {
            UnequipSlot(manager, slot);
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
            selected = null;
        }

        Refresh();
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

        if (removed)
            SetHint("인자를 가방으로 되돌렸습니다.");

        Refresh();
    }
}
