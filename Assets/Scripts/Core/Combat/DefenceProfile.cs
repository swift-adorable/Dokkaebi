using System;

/// <summary>
/// 피격자의 방어 정보. 플레이어와 적이 공용으로 쓴다.
///
/// 머리 방어도는 원거리 피격에, 몸통 방어도는 근접 피격에 적용된다.
/// 두 값을 합산하지 않는다. (docs/Dokkaebi_Equipment_System.md 1절)
/// </summary>
[Serializable]
public struct DefenceProfile
{
    /// <summary>
    /// 원거리 · 투사체 피격에 적용되는 방어도.
    ///
    /// 실수인 이유 — 점화가 중에 방어도를 1.0 깎고, 부식이 절반으로 만들고,
    /// 각인 「경화」가 0.2를 더한다. 정수로 두면 이 셋을 표현할 수 없다.
    /// 공식 자체가 2/(차이+2) 실수 연산이라 정수로 둘 이유도 없었다.
    /// </summary>
    public float headArmour;

    /// <summary>근접 · 접촉 · 폭발 피격에 적용되는 방어도.</summary>
    public float bodyArmour;

    /// <summary>속성 저항 배율.</summary>
    public ElementalResistances resistances;

    /// <summary>방어도 0 / 저항 전부 1.0인 기본값.</summary>
    public static DefenceProfile None => new DefenceProfile
    {
        headArmour = 0f,
        bodyArmour = 0f,
        resistances = ElementalResistances.Default
    };

    public static DefenceProfile Create(float head, float body, ElementalResistances resist)
    {
        return new DefenceProfile { headArmour = head, bodyArmour = body, resistances = resist };
    }

    /// <summary>피격 유형에 해당하는 방어도.</summary>
    public float ArmourFor(HitKind kind)
    {
        return kind == HitKind.Melee ? bodyArmour : headArmour;
    }
}
