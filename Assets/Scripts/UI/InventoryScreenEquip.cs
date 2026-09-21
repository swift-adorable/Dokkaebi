using UnityEngine.UI;
using UnityEngine;

/// <summary>
/// 가방 화면의 장비 8슬롯 조작. (로드맵 6-H)
///
/// 조작 규칙은 젬 탭과 같다 —
/// 【왼쪽 가방에서 장비를 누르고, 왼쪽 위 슬롯을 누른다.】
/// 고른 것이 없는 상태로 낀 슬롯을 누르면 벗어서 가방으로 돌아간다.
///
/// 규칙을 두 탭에서 같게 두는 이유 — 모바일에서 조작 방식이 화면마다 다르면
/// 유저가 매번 다시 배워야 한다.
/// </summary>
public partial class InventoryScreenUI
{
    /// <summary>
    /// 장비 슬롯을 눌렀을 때.
    ///
    /// 벗은 장비를 가방에 넣지 못하면 착용 자체를 취소한다 —
    /// 여기서 반만 처리하면 장비가 사라진다.
    /// </summary>
    private void OnEquipSlotClicked(EquipmentSlot slot)
    {
        PlayerInventory inventory = PlayerInventory.EnsureInstance();
        EquipmentLoadout loadout = inventory.Loadout;
        Inventory bag = inventory.Bag;

        // 【빈손으로 슬롯을 누르면 고르기만 한다.】
        // 예전에는 즉시 벗겨졌다. 무엇을 끼웠는지 확인하려고 누른 것만으로
        // 장비가 가방으로 돌아가 버렸고, 가방이 차 있으면 그마저 실패했다.
        // 이제 해제는 우측 행동 줄의 「해제」 한 번을 더 받는다.
        if (selected == null || selectedSlot.HasValue)
        {
            SelectEquippedSlot(slot);
            return;
        }

        ItemStack picked = selected;

        if (picked.Definition is not EquipmentDefinition definition)
        {
            SetHint("장비가 아닙니다.");
            return;
        }

        if (!loadout.CanEquip(picked, slot))
        {
            SetHint(DescribeEquipFailure(loadout, definition, slot));
            return;
        }

        ItemStack previous = loadout.Get(slot);

        // 착용하려는 것은 가방에서 빠지고 벗은 것이 들어오므로 칸 수는 그대로다.
        // 다만 가방에 없던 것을 끼우려는 경우를 대비해 먼저 뺀다.
        if (!bag.RemoveStack(picked))
        {
            SetHint("가방에 없는 장비입니다.");
            return;
        }

        if (!loadout.TryEquip(picked, slot, out ItemStack displaced))
        {
            // 실패하면 원상복구한다. 가방에서 뺐는데 착용이 안 되면 사라진다.
            bag.TryAddStack(picked);
            SetHint("착용하지 못했습니다.");
            return;
        }

        if (displaced != null && !bag.TryAddStack(displaced))
        {
            // 벗은 것을 넣을 자리가 없다 — 전부 되돌린다.
            loadout.TryEquip(displaced, slot, out _);
            bag.TryAddStack(picked);

            SetHint("가방이 가득 차 벗은 장비를 넣을 수 없습니다.");
            return;
        }

        selected = null;
        selectedSlot = null;

        SetHint(previous == null
            ? $"「{definition.DisplayName}」 착용"
            : $"「{definition.DisplayName}」(으)로 교체");

        AfterLoadoutChanged(inventory);
    }

    /// <summary>착용 중인 장비를 고른다. 우측에 상세와 「해제」가 뜬다.</summary>
    private void SelectEquippedSlot(EquipmentSlot slot)
    {
        ItemStack current = PlayerInventory.EnsureInstance().Loadout.Get(slot);

        if (current == null)
        {
            selectedSlot = null;
            selected = null;

            SetHint("빈 자리입니다. 가방에서 장비를 먼저 고르십시오.");

            Refresh();
            return;
        }

        // 같은 자리를 다시 누르면 선택을 푼다.
        bool same = selectedSlot == slot;

        selectedSlot = same ? null : slot;
        selected = same ? null : current;

        SetHint(DescribeSelection());

        Refresh();
    }

    /// <summary>행동 줄의 「해제」. 여기까지 와야 실제로 벗는다.</summary>
    private void UnequipSelectedSlot(EquipmentSlot slot)
    {
        PlayerInventory inventory = PlayerInventory.EnsureInstance();

        selected = null;
        selectedSlot = null;

        UnequipSlot(inventory.Loadout, inventory.Bag, slot);
    }

    private void UnequipSlot(EquipmentLoadout loadout, Inventory bag, EquipmentSlot slot)
    {
        ItemStack current = loadout.Get(slot);

        if (current == null)
        {
            SetHint("가방에서 장비를 고른 뒤 자리를 누르십시오.");
            return;
        }

        if (bag.FreeSlots <= 0 && !bag.CanAdd(current.Definition))
        {
            SetHint("가방이 가득 차 벗을 수 없습니다.");
            return;
        }

        loadout.Unequip(slot);

        if (!bag.TryAddStack(current))
        {
            // 넣지 못하면 다시 입힌다. 벗은 채로 사라지게 두지 않는다.
            loadout.TryEquip(current, slot, out _);
            SetHint("가방이 가득 차 벗을 수 없습니다.");
            return;
        }

        SetHint($"「{current.Definition.DisplayName}」 벗음");

        AfterLoadoutChanged(PlayerInventory.EnsureInstance());
    }

    /// <summary>착용이 바뀌면 적재 한도와 실제 성능을 즉시 다시 계산한다.</summary>
    private void AfterLoadoutChanged(PlayerInventory inventory)
    {
        inventory.RefreshCapacity();

        // 가방을 바꾸면 적재 한도가 달라지고, 무기를 바꾸면 사격 성능이 달라진다.
        // 다음 주기를 기다리면 유저가 화면을 닫을 때까지 반영되지 않는다.
        PlayerLoadout binder = Object.FindAnyObjectByType<PlayerLoadout>();

        if (binder != null)
            binder.Refresh();

        Refresh();
    }

    /// <summary>왜 못 끼웠는지 한 줄로 알려준다. 이유를 모르면 유저는 버그로 받아들인다.</summary>
    private static string DescribeEquipFailure(
        EquipmentLoadout loadout, EquipmentDefinition definition, EquipmentSlot slot)
    {
        bool imprintSlot = slot == EquipmentSlot.ImprintA || slot == EquipmentSlot.ImprintB;
        bool imprintItem = definition.Slot == EquipmentSlot.ImprintA
                           || definition.Slot == EquipmentSlot.ImprintB;

        if (imprintSlot != imprintItem || (!imprintSlot && definition.Slot != slot))
            return $"「{EquipmentSlotName(slot)}」 자리에 넣을 수 없는 장비입니다.";

        // 여기까지 왔으면 각인 중복이다 — 같은 계열 + 같은 단계.
        EquipmentSlot other = slot == EquipmentSlot.ImprintA
            ? EquipmentSlot.ImprintB
            : EquipmentSlot.ImprintA;

        EquipmentDefinition opposite = loadout.GetDefinition(other);

        return opposite != null
            ? $"「{opposite.DisplayName}」와(과) 같은 계열·단계는 함께 낄 수 없습니다."
            : "장착할 수 없습니다.";
    }

    /// <summary>
    /// 장비 상세. 옵션을 이득과 대가로 나눠 보여준다.
    ///
    /// 부호를 그대로 보여주지 않는 이유 — 「발사 간격 +150%」는 양수인데 페널티다.
    /// 판정은 EquipmentStatMeta 한 곳에 있다.
    /// </summary>
    private void DrawEquipmentInfo(EquipmentDefinition equipment)
    {
        var gains = new System.Collections.Generic.List<string>(4);
        var costs = new System.Collections.Generic.List<string>(4);

        foreach (EquipmentStat stat in equipment.Stats)
        {
            if (stat.type == EquipmentStatType.None || stat.value == 0f)
                continue;

            string line = $"{EquipmentStatName(stat.type)} {stat.value:+0.##;-0.##}";

            if (EquipmentStatMeta.IsDrawback(stat))
                costs.Add(line);
            else
                gains.Add(line);
        }

        if (equipment.Immunity != StatusEffectType.None)
            gains.Add($"{StatusName(equipment.Immunity)} 면역");

        if (equipment.ExtraImmunities != null)
        {
            foreach (StatusEffectType extra in equipment.ExtraImmunities)
            {
                if (extra != StatusEffectType.None)
                    gains.Add($"{StatusName(extra)} 면역");
            }
        }

        // 표로 깐다. 줄글로 나열하면 어느 값이 이득이고 대가인지 한눈에 안 들어온다.
        int row = 0;

        foreach (string line in gains)
            DrawStatLine(ref row, line, UIPalette.Gain);

        foreach (string line in costs)
            DrawStatLine(ref row, line, UIPalette.Cost);

        if (row == 0)
        {
            UIFactory.CreateLabel(rightContent, "옵션이 없습니다.", 24, FontStyle.Normal,
                new Vector2(0f, 0.50f), new Vector2(1f, 0.58f),
                TextAnchor.UpperLeft, UIPalette.TextDim);
        }
    }

    /// <summary>「이름 +값」 한 줄을 이름과 값으로 갈라 표에 넣는다.</summary>
    private void DrawStatLine(ref int index, string line, Color valueColor)
    {
        // 행동 줄을 덮지 않도록 여기서 멈춘다. 넘치면 잘라 내는 편이
        // 버튼 위에 글자가 겹치는 것보다 낫다.
        if (index >= 8)
            return;

        int split = line.LastIndexOf(' ');

        string label = split > 0 ? line.Substring(0, split) : line;
        string value = split > 0 ? line.Substring(split + 1) : string.Empty;

        DrawStatRow(index, 0.60f, label, value, valueColor);

        index++;
    }

    private static string EquipmentStatName(EquipmentStatType type)
    {
        switch (type)
        {
            case EquipmentStatType.HeadArmour:           return "머리 방어도";
            case EquipmentStatType.BodyArmour:           return "몸통 방어도";
            case EquipmentStatType.ArmourPenetration:    return "방어 관통";
            case EquipmentStatType.ContainmentWard:      return "격리 방호";
            case EquipmentStatType.ResistPhysical:       return "물리 내성";
            case EquipmentStatType.ResistFire:           return "화염 내성";
            case EquipmentStatType.ResistCold:           return "냉기 내성";
            case EquipmentStatType.ResistLightning:      return "번개 내성";
            case EquipmentStatType.ResistChaos:          return "카오스 내성";
            case EquipmentStatType.MaxHealth:            return "최대 체력";
            case EquipmentStatType.HealthRegen:          return "체력 재생";
            case EquipmentStatType.HealingReceived:      return "받는 회복량";
            case EquipmentStatType.MoveAbility:          return "이동";
            case EquipmentStatType.DashCooldown:         return "대시 쿨타임";
            case EquipmentStatType.ViewDistance:         return "시야 거리";
            case EquipmentStatType.ViewAngle:            return "시야 각도";
            case EquipmentStatType.DetectDistance:       return "감지 거리";
            case EquipmentStatType.DetectedDistance:     return "발각 거리";
            case EquipmentStatType.Hearing:              return "청력";
            case EquipmentStatType.SoundLocate:          return "소리 위치";
            case EquipmentStatType.XpAbsorbRange:        return "흡수 범위";
            case EquipmentStatType.XpAbsorbAmount:       return "경험치 획득";
            case EquipmentStatType.RareDropRate:         return "희귀 드랍";
            case EquipmentStatType.MaxCarryWeight:       return "소지 중량";
            case EquipmentStatType.SlotCapacity:         return "적재 칸";
            case EquipmentStatType.DamageIncrease:       return "피해";
            case EquipmentStatType.WeaponRangeIncrease:  return "사거리";
            case EquipmentStatType.FireIntervalIncrease: return "발사 간격";
            case EquipmentStatType.CriticalChance:       return "치명타 확률";
            case EquipmentStatType.CriticalMultiplier:   return "치명타 배율";
            case EquipmentStatType.AilmentPower:         return "상태이상 위력";
            default:                                     return type.ToString();
        }
    }

    private static string StatusName(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Ignite:  return "점화";
            case StatusEffectType.Poison:  return "중독";
            case StatusEffectType.Freeze:  return "동결";
            case StatusEffectType.Chill:   return "냉각";
            case StatusEffectType.Paralyze: return "마비";
            case StatusEffectType.Corrode: return "부식";
            case StatusEffectType.Shock:   return "감전";
            case StatusEffectType.Bleed:   return "출혈";
            case StatusEffectType.Congeal: return "응집";
            default:                       return type.ToString();
        }
    }
}
