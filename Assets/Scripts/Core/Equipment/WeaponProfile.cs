using UnityEngine;

/// <summary>
/// 무기 기본값에 장비·각인 보정을 얹은 최종 사격 성능.
///
/// MonoBehaviour 의존이 없는 순수 구조체다. 무기를 들고 각인을 끼웠을 때
/// 실제로 어떤 숫자가 나오는지를 테스트로 검증할 수 있어야 한다.
/// (docs/Blob_Combat_Baseline.md 2절 — 모든 증가는 가산 합산)
/// </summary>
public readonly struct WeaponProfile
{
    /// <summary>보정이 적용된 기본 피해.</summary>
    public readonly float Damage;

    /// <summary>보정이 적용된 발사 간격(초).</summary>
    public readonly float FireInterval;

    /// <summary>보정이 적용된 유효 사거리(m).</summary>
    public readonly float EffectiveRange;

    /// <summary>방어 관통. 무기·부착물·각인의 합이다.</summary>
    public readonly int ArmourPenetration;

    /// <summary>치명타 확률(0~1).</summary>
    public readonly float CriticalChance;

    /// <summary>치명타 배율.</summary>
    public readonly float CriticalMultiplier;

    /// <summary>상태이상 위력 배수. 직접 피해와 분리된 축이다.</summary>
    public readonly float AilmentPower;

    private WeaponProfile(
        float damage, float fireInterval, float effectiveRange,
        int armourPenetration, float criticalChance,
        float criticalMultiplier, float ailmentPower)
    {
        Damage = damage;
        FireInterval = fireInterval;
        EffectiveRange = effectiveRange;
        ArmourPenetration = armourPenetration;
        CriticalChance = criticalChance;
        CriticalMultiplier = criticalMultiplier;
        AilmentPower = ailmentPower;
    }

    /// <summary>초당 피해. 치명타를 기대값으로 포함한다.</summary>
    public float ExpectedDps
    {
        get
        {
            float critFactor = 1f + CriticalChance * (CriticalMultiplier - 1f);
            return Damage / Mathf.Max(0.01f, FireInterval) * critFactor;
        }
    }

    /// <summary>
    /// 무기가 없을 때. 맨몸 상태를 0으로 두지 않는 이유 —
    /// 무기를 잃어도 조작이 죽지 않아야 시체 회수를 시도할 수 있다.
    /// </summary>
    public static WeaponProfile Unarmed => new(
        CombatConstants.BaseWeaponDamage * 0.5f,
        CombatConstants.BaseFireInterval,
        CombatConstants.BaseEffectiveRange * 0.6f,
        0, 0f, CombatConstants.BaseCriticalMultiplier, 1f);

    /// <summary>무기와 장비 보정을 합쳐 최종 성능을 만든다.</summary>
    public static WeaponProfile Create(WeaponDefinition weapon, EquipmentModifiers modifiers)
    {
        if (weapon == null)
            return modifiers == null ? Unarmed : Apply(Unarmed, modifiers);

        var baseProfile = new WeaponProfile(
            weapon.BaseDamage,
            weapon.FireInterval,
            weapon.EffectiveRange,
            0, 0f, CombatConstants.BaseCriticalMultiplier, 1f);

        return modifiers == null ? baseProfile : Apply(baseProfile, modifiers);
    }

    private static WeaponProfile Apply(WeaponProfile b, EquipmentModifiers m)
    {
        // 증가율은 전부 가산이다. 음수 보정(각인의 대가)도 같은 경로로 들어온다.
        float damage = b.Damage * Mathf.Max(0f, 1f + m.Get(EquipmentStatType.DamageIncrease));
        float range = b.EffectiveRange * Mathf.Max(0.1f, 1f + m.Get(EquipmentStatType.WeaponRangeIncrease));

        // 발사 간격은 양수 보정이 곧 페널티다. 0 이하로 떨어지지 않게 막는다.
        float interval = b.FireInterval * Mathf.Max(0.05f, 1f + m.Get(EquipmentStatType.FireIntervalIncrease));

        int penetration = Mathf.Clamp(
            Mathf.RoundToInt(m.Get(EquipmentStatType.ArmourPenetration)),
            0, CombatConstants.MaxArmour);

        float critChance = Mathf.Clamp(
            CombatConstants.BaseCriticalChance + m.Get(EquipmentStatType.CriticalChance),
            0f, CombatConstants.MaxCriticalChance);
        float critMult = Mathf.Max(1f, b.CriticalMultiplier + m.Get(EquipmentStatType.CriticalMultiplier));
        float ailment = Mathf.Max(0f, 1f + m.Get(EquipmentStatType.AilmentPower));

        return new WeaponProfile(damage, interval, range, penetration, critChance, critMult, ailment);
    }
}
