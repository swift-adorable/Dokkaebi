using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 착용 장비의 옵션을 합산한 결과. MonoBehaviour 의존이 없는 순수 클래스다.
///
/// 옵션은 전부 가산 합산한다. 곱연산은 전투 공식의 고정된 항들뿐이다.
/// (docs/Blob_Combat_Baseline.md 3절)
/// </summary>
public class EquipmentModifiers
{
    private readonly Dictionary<EquipmentStatType, float> totals = new();

    /// <summary>합산에 참여한 장비가 준 상태이상 면역 목록.</summary>
    private readonly HashSet<StatusEffectType> immunities = new();

    public void Reset()
    {
        totals.Clear();
        immunities.Clear();
    }

    /// <summary>
    /// 장비 하나를 합산에 더한다.
    ///
    /// 내구도가 0이면 【방어 옵션만】 빠진다. 탐지·수집·적재 옵션은 계속 작동한다.
    /// 마모(33% 이하) 상태에서는 방어 옵션이 절반만 적용된다.
    /// (docs/Blob_Equipment_System.md 4절)
    /// </summary>
    public void Add(EquipmentDefinition definition, bool isBroken = false, bool isWorn = false)
    {
        if (definition == null)
            return;

        EquipmentStat[] stats = definition.Stats;

        for (int i = 0; i < stats.Length; i++)
        {
            EquipmentStatType type = stats[i].type;

            if (type == EquipmentStatType.None)
                continue;

            float value = stats[i].value;

            if (IsDefensive(type))
            {
                if (isBroken)
                    continue;

                if (isWorn)
                    value *= 0.5f;
            }

            totals.TryGetValue(type, out float current);
            totals[type] = current + value;
        }

        if (definition.Immunity != StatusEffectType.None)
            immunities.Add(definition.Immunity);
    }

    public float Get(EquipmentStatType type)
    {
        return totals.TryGetValue(type, out float value) ? value : 0f;
    }

    public bool IsImmuneTo(StatusEffectType status)
    {
        return status != StatusEffectType.None && immunities.Contains(status);
    }

    /// <summary>전투 공식에 넘길 방어 정보로 변환한다.</summary>
    public DefenceProfile ToDefenceProfile()
    {
        ElementalResistances resist = ElementalResistances.Default;

        // 내성 옵션은 배율에서 빼는 값이다. 0.12 = 내성 ×0.88
        resist.physical -= Get(EquipmentStatType.ResistPhysical);
        resist.fire -= Get(EquipmentStatType.ResistFire);
        resist.cold -= Get(EquipmentStatType.ResistCold);
        resist.lightning -= Get(EquipmentStatType.ResistLightning);
        resist.chaos -= Get(EquipmentStatType.ResistChaos);

        // 내성이 음수가 되면 피해가 회복으로 뒤집힌다. 0에서 막는다.
        resist.physical = Mathf.Max(0f, resist.physical);
        resist.fire = Mathf.Max(0f, resist.fire);
        resist.cold = Mathf.Max(0f, resist.cold);
        resist.lightning = Mathf.Max(0f, resist.lightning);
        resist.chaos = Mathf.Max(0f, resist.chaos);

        int head = Mathf.Clamp(Mathf.RoundToInt(Get(EquipmentStatType.HeadArmour)), 0, CombatConstants.MaxArmour);
        int body = Mathf.Clamp(Mathf.RoundToInt(Get(EquipmentStatType.BodyArmour)), 0, CombatConstants.MaxArmour);

        return DefenceProfile.Create(head, body, resist);
    }

    /// <summary>방어 옵션인지. 내구도 0이면 이것만 정지한다.</summary>
    private static bool IsDefensive(EquipmentStatType type)
    {
        switch (type)
        {
            case EquipmentStatType.HeadArmour:
            case EquipmentStatType.BodyArmour:
            case EquipmentStatType.ContainmentWard:
            case EquipmentStatType.ResistPhysical:
            case EquipmentStatType.ResistFire:
            case EquipmentStatType.ResistCold:
            case EquipmentStatType.ResistLightning:
            case EquipmentStatType.ResistChaos:
                return true;

            default:
                return false;
        }
    }
}
