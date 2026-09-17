using System;

/// <summary>
/// 피격자의 방어 정보. 플레이어와 적이 공용으로 쓴다.
///
/// 머리 방어도는 원거리 피격에, 몸통 방어도는 근접 피격에 적용된다.
/// 두 값을 합산하지 않는다. (docs/Blob_Equipment_System.md 1절)
/// </summary>
[Serializable]
public struct DefenceProfile
{
    /// <summary>원거리 · 투사체 피격에 적용되는 방어도.</summary>
    public int headArmour;

    /// <summary>근접 · 접촉 · 폭발 피격에 적용되는 방어도.</summary>
    public int bodyArmour;

    /// <summary>속성 내성 배율.</summary>
    public ElementalResistances resistances;

    /// <summary>방어도 0 / 내성 전부 1.0인 기본값.</summary>
    public static DefenceProfile None => new DefenceProfile
    {
        headArmour = 0,
        bodyArmour = 0,
        resistances = ElementalResistances.Default
    };

    public static DefenceProfile Create(int head, int body, ElementalResistances resist)
    {
        return new DefenceProfile { headArmour = head, bodyArmour = body, resistances = resist };
    }

    /// <summary>피격 유형에 해당하는 방어도.</summary>
    public int ArmourFor(HitKind kind)
    {
        return kind == HitKind.Melee ? bodyArmour : headArmour;
    }
}
