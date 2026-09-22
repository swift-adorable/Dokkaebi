using UnityEngine.UI;
using UnityEngine;

/// <summary>
/// 가방 화면의 장비 8슬롯 조작. (로드맵 6-H)
///
/// 조작 규칙은 스킬 탭과 같다 — 【누르면 상세가 뜨고, 그 안에서 한다.】
/// 가방의 장비를 누르면 「장착」, 착용 중인 것을 누르면 「장착 해제」가 나온다.
///
/// 규칙을 세 탭에서 같게 두는 이유 — 모바일에서 조작 방식이 화면마다 다르면
/// 유저가 매번 다시 배워야 한다.
/// </summary>
public partial class InventoryScreenUI
{
    /// <summary>
    /// 장비 슬롯을 눌렀다. 【상세를 연다.】
    ///
    /// 전에는 여기서 「고른 것이 있으면 입히고, 없으면 고른다」를 모두 처리했다.
    /// 같은 자리를 눌러도 손에 무엇이 들렸느냐에 따라 다른 일이 일어나서,
    /// 무엇을 끼웠는지 확인하려고 누른 것만으로 장비가 바뀌기도 했다.
    /// 이제 누르면 상세가 뜨고, 실제 동작은 그 안의 버튼이 한다.
    /// </summary>
    private void OnEquipSlotClicked(EquipmentSlot slot)
    {
        ItemStack current = PlayerInventory.EnsureInstance().Loadout.Get(slot);

        if (current?.Definition == null)
        {
            ShowToast($"「{EquipmentSlotName(slot)}」 자리가 비어 있습니다. 가방에서 장비를 누르십시오.");
            return;
        }

        OpenItemDetail(current, slot);
    }

    /// <summary>
    /// 상세의 「장착」. 벗은 장비를 가방에 넣지 못하면 착용 자체를 취소한다 —
    /// 여기서 반만 처리하면 장비가 사라진다.
    /// </summary>
    private void EquipFromDetail(ItemStack picked, EquipmentSlot slot)
    {
        PlayerInventory inventory = PlayerInventory.EnsureInstance();
        EquipmentLoadout loadout = inventory.Loadout;
        Inventory bag = inventory.Bag;

        if (picked?.Definition is not EquipmentDefinition definition)
        {
            ShowToast("장비가 아닙니다.");
            return;
        }

        if (!loadout.CanEquip(picked, slot))
        {
            ShowToast(DescribeEquipFailure(loadout, definition, slot));
            return;
        }

        // 착용하려는 것은 가방에서 빠지고 벗은 것이 들어오므로 칸 수는 그대로다.
        // 다만 가방에 없던 것을 끼우려는 경우를 대비해 먼저 뺀다.
        if (!bag.RemoveStack(picked))
        {
            ShowToast("가방에 없는 장비입니다.");
            return;
        }

        if (!loadout.TryEquip(picked, slot, out ItemStack displaced))
        {
            // 실패하면 원상복구한다. 가방에서 뺐는데 착용이 안 되면 사라진다.
            bag.TryAddStack(picked);
            ShowToast("착용하지 못했습니다.");
            return;
        }

        if (displaced != null && !bag.TryAddStack(displaced))
        {
            // 벗은 것을 넣을 자리가 없다 — 전부 되돌린다.
            loadout.TryEquip(displaced, slot, out _);
            bag.TryAddStack(picked);

            ShowToast("가방이 가득 차 벗은 장비를 넣을 수 없습니다.");
            return;
        }

        CloseItemDetail();

        selected = null;
        selectedSlot = null;

        AfterLoadoutChanged(inventory);
    }

    /// <summary>상세의 「장착 해제」.</summary>
    private void UnequipFromSlot(EquipmentSlot slot)
    {
        PlayerInventory inventory = PlayerInventory.EnsureInstance();
        EquipmentLoadout loadout = inventory.Loadout;
        Inventory bag = inventory.Bag;

        ItemStack current = loadout.Get(slot);

        if (current == null)
            return;

        if (bag.FreeSlots <= 0 && !bag.CanAdd(current.Definition))
        {
            ShowToast("가방이 가득 차 벗을 수 없습니다.");
            return;
        }

        loadout.Unequip(slot);

        if (!bag.TryAddStack(current))
        {
            // 넣지 못하면 다시 입힌다. 벗은 채로 사라지게 두지 않는다.
            loadout.TryEquip(current, slot, out _);
            ShowToast("가방이 가득 차 벗을 수 없습니다.");
            return;
        }

        CloseItemDetail();

        selected = null;
        selectedSlot = null;

        AfterLoadoutChanged(inventory);
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
        BeginStatRows();

        int row = 0;

        foreach (string line in gains)
            DrawStatLine(ref row, line, UIPalette.Gain);

        foreach (string line in costs)
            DrawStatLine(ref row, line, UIPalette.Cost);

        if (row == 0)
        {
            UIFactory.CreateLabel(detailContent, "옵션이 없습니다.", 24, FontStyle.Normal,
                new Vector2(0f, StatTop - 0.07f), new Vector2(1f, StatTop),
                TextAnchor.UpperLeft, UIPalette.TextDim);
        }
    }

    /// <summary>
    /// 「이름 +값」 한 줄을 이름과 값으로 갈라 표에 넣는다.
    ///
    /// 아래 버튼을 덮기 시작하면 DrawStatRow가 알아서 멈춘다 —
    /// 행 높이가 글자에 따라 달라져서 「몇 줄까지」로는 셀 수 없다.
    /// </summary>
    private void DrawStatLine(ref int index, string line, Color valueColor)
    {
        int split = line.LastIndexOf(' ');

        string label = split > 0 ? line.Substring(0, split) : line;
        string value = split > 0 ? line.Substring(split + 1) : string.Empty;

        DrawStatRow(label, value, valueColor);

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
            case EquipmentStatType.ResistPhysical:       return "물리 저항";
            case EquipmentStatType.ResistFire:           return "화염 저항";
            case EquipmentStatType.ResistCold:           return "냉기 저항";
            case EquipmentStatType.ResistLightning:      return "번개 저항";
            case EquipmentStatType.ResistChaos:          return "카오스 저항";
            case EquipmentStatType.MaxHealth:            return "최대 체력";
            case EquipmentStatType.HealthRegen:          return "체력 재생";
            case EquipmentStatType.HealingReceived:      return "받는 회복량";
            case EquipmentStatType.MoveAbility:          return "이동";
            case EquipmentStatType.DashCooldown:         return "대시 쿨다운";
            case EquipmentStatType.ViewDistance:         return "시야 거리";
            case EquipmentStatType.ViewAngle:            return "시야 각도";
            case EquipmentStatType.DetectDistance:       return "감지 거리";
            case EquipmentStatType.DetectedDistance:     return "발각 거리";
            case EquipmentStatType.Hearing:              return "청력";
            case EquipmentStatType.SoundLocate:          return "소리 위치";
            case EquipmentStatType.XpAbsorbRange:        return "흡수 범위";
            case EquipmentStatType.XpAbsorbAmount:       return "경험치 획득";
            case EquipmentStatType.RareDropRate:         return "희귀 드롭";
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
