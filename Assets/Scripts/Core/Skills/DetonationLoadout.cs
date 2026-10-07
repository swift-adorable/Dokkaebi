using UnityEngine;

/// <summary>
/// 기폭 핵심 젬 하나와 그 소켓 보조 젬이 정한 값 (Audit A5 · Skill_System 5절).
/// 탄 쪽 보정(WeaponModifiers)과 따로 모은다 — 기폭은 투사체가 아니다.
/// 수치는 SkillZoneTable [임시값].
/// </summary>
public readonly struct DetonationLoadout
{
    public readonly SkillDefinition Core;

    /// <summary>기폭 피해 증가율(가산) — 긴 퓨즈 +35% · 연쇄 기폭 +20%.</summary>
    public readonly float DamageIncrease;

    /// <summary>기폭 범위 배율 — 짧은 퓨즈 ×0.7.</summary>
    public readonly float RadiusMultiplier;

    /// <summary>잔류물 · 우물 반경 배율 — 유지되는 대지 ×0.75 · 잔류물 효력 ×1.6.</summary>
    public readonly float ZoneRadiusMultiplier;

    /// <summary>잔류물 · 우물 지속 배율 — 유지되는 대지 ×2 · 지속시간 연장/압축.</summary>
    public readonly float DurationMultiplier;

    /// <summary>기폭 지연(초).</summary>
    public readonly float Fuse;

    public readonly bool ConsumesAllStacks;
    public readonly int Chains;
    public readonly float Cooldown;

    private DetonationLoadout(SkillDefinition core, float damage, float radius, float zone, float duration,
                              float fuse, bool consumeAll, int chains, float cooldown)
    {
        Core = core;
        DamageIncrease = damage;
        RadiusMultiplier = radius;
        ZoneRadiusMultiplier = zone;
        DurationMultiplier = duration;
        Fuse = fuse;
        ConsumesAllStacks = consumeAll;
        Chains = chains;
        Cooldown = cooldown;
    }

    public bool IsValid => Core != null;
    public string CoreId => Core != null ? Core.Id : string.Empty;

    public static DetonationLoadout For(SkillDefinition core)
        => new(core, core != null ? core.DamageIncrease : 0f, 1f, 1f, 1f,
               SkillZoneTable.DefaultFuse, false, 0, SkillZoneTable.DetonationCooldown);

    /// <summary>보조 젬 하나를 더한다 — 증가율은 더하고 배율은 곱한다(5-A).</summary>
    public DetonationLoadout With(SkillDefinition support)
    {
        if (support == null)
            return this;

        return new DetonationLoadout(
            Core,
            DamageIncrease + support.DamageIncrease,
            RadiusMultiplier * support.RangeMultiplier,
            ZoneRadiusMultiplier * support.ZoneRadiusMultiplier,
            DurationMultiplier * support.AilmentDurationMultiplier,
            support.FuseSeconds >= 0f ? support.FuseSeconds : Fuse,
            ConsumesAllStacks || support.ConsumesAllStacks,
            Chains + support.ChainDetonations,
            Cooldown + support.DetonationCooldownAdd);
    }

    /// <summary>폭발 반경(m).</summary>
    public float Radius(float baseRadius) => Mathf.Max(0.1f, baseRadius * RadiusMultiplier);
}
