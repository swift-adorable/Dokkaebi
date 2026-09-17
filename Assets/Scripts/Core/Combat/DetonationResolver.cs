using UnityEngine;

/// <summary>
/// 기폭(원소 작렬) 판정. 걸린 상태를 소모해 폭발로 바꾼다.
///
/// PoE2의 「원소 작렬」과 같은 구조다 — 파동 자체는 피해가 없고,
/// 소모된 상태이상이 해당 속성의 폭발을 일으킨다.
/// (docs/Blob_Skill_System.md 5-2절)
///
/// MonoBehaviour 의존이 없는 순수 클래스다.
/// </summary>
public readonly struct DetonationResult
{
    /// <summary>폭발이 일어나는지.</summary>
    public readonly bool Detonated;

    /// <summary>폭발 피해.</summary>
    public readonly int Damage;

    /// <summary>폭발 반경(m).</summary>
    public readonly float Radius;

    /// <summary>폭발 속성.</summary>
    public readonly DamageElement Element;

    /// <summary>주변 적에게 전이되는 상태. None이면 전이가 없다.</summary>
    public readonly StatusEffectType SpreadStatus;

    /// <summary>폭발이 바닥에 남기는 잔류물.</summary>
    public readonly GroundEffectType Ground;

    public DetonationResult(int damage, float radius, DamageElement element,
        StatusEffectType spread, GroundEffectType ground)
    {
        Detonated = damage > 0 || spread != StatusEffectType.None || ground != GroundEffectType.None;
        Damage = damage;
        Radius = radius;
        Element = element;
        SpreadStatus = spread;
        Ground = ground;
    }

    public static DetonationResult None => default;
}

public static class DetonationResolver
{
    /// <summary>기폭 기본 반경(m).</summary>
    public const float BaseRadius = 3f;

    /// <summary>상태 1중첩이 폭발로 바뀌는 비율. 기본 피해 대비.</summary>
    public const float DamagePerStack = 1.2f;

    /// <summary>
    /// 걸린 상태를 소모해 폭발 결과를 만든다.
    ///
    /// 상태가 없으면 아무 일도 일어나지 않는다. 이것이
    /// "원소 작렬은 단독으로 무의미하다"를 코드에서 보장한다.
    /// </summary>
    /// <param name="status">소모할 상태</param>
    /// <param name="stacks">중첩 수. 중독만 크게 쌓인다</param>
    /// <param name="baseDamage">기준 피해</param>
    /// <param name="radiusMultiplier">기폭 범위 보정 (짧은 퓨즈 -30% 등)</param>
    public static DetonationResult Resolve(
        StatusEffectType status, int stacks, int baseDamage, float radiusMultiplier = 1f)
    {
        if (status == StatusEffectType.None || baseDamage <= 0)
            return DetonationResult.None;

        StatusEffectSpec spec = StatusEffectTable.Get(status);

        if (spec.Duration <= 0f)
            return DetonationResult.None;

        int effectiveStacks = Mathf.Max(1, stacks);
        float radius = Mathf.Max(0.1f, BaseRadius * Mathf.Max(0.1f, radiusMultiplier));

        switch (status)
        {
            // 점화 → 화염 폭발 + 화염 잔류물
            case StatusEffectType.Ignite:
                return new DetonationResult(
                    Damage(baseDamage, effectiveStacks), radius, DamageElement.Fire,
                    StatusEffectType.None, GroundEffectType.FireZone);

            // 중독 → 독 구름 확산. 중첩이 크게 쌓이므로 피해보다 확산이 본체다.
            case StatusEffectType.Poison:
                return new DetonationResult(
                    Damage(baseDamage, effectiveStacks), radius, DamageElement.Chaos,
                    StatusEffectType.Poison, GroundEffectType.ToxicSwamp);

            // 동결 → 파편 폭발 + 인접 동결
            case StatusEffectType.Freeze:
                return new DetonationResult(
                    Damage(baseDamage, effectiveStacks), radius, DamageElement.Cold,
                    StatusEffectType.Freeze, GroundEffectType.None);

            // 감전 → 낙뢰 3회. 피해가 세 번 나뉘어 들어가므로 반경이 넓다.
            case StatusEffectType.Shock:
                return new DetonationResult(
                    Damage(baseDamage, effectiveStacks), radius * 1.5f, DamageElement.Lightning,
                    StatusEffectType.None, GroundEffectType.None);

            // 출혈 → 혈액 분출 + 혈액 잔류물
            case StatusEffectType.Bleed:
                return new DetonationResult(
                    Damage(baseDamage, effectiveStacks), radius, DamageElement.Physical,
                    StatusEffectType.None, GroundEffectType.BloodZone);

            // 응집은 기폭 대상이 아니다. 전이 범위를 넓히는 증폭 상태다.
            default:
                return DetonationResult.None;
        }
    }

    private static int Damage(int baseDamage, int stacks)
    {
        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * DamagePerStack * stacks));
    }
}
