using System;

/// <summary>
/// 한 번의 피해를 계산하는 데 필요한 공격 측 정보.
///
/// 무기가 기본값을, 스킬이 속성과 증가율을 채운다.
/// (docs/Blob_Combat_Baseline.md 3절)
/// </summary>
[Serializable]
public struct DamageRequest
{
    /// <summary>무기가 정하는 기본 피해.</summary>
    public int baseDamage;

    /// <summary>
    /// 모든 "증가"의 합. 0.25 = +25%.
    ///
    /// 가산 합산만 쓴다. PoE의 "증가 / 더 증가" 2단 구조를 도입하지 않는다.
    /// 모바일에서 유저가 곱연산 폭발을 예측할 수 없고 밸런싱 비용이 급증한다.
    /// </summary>
    public float increasedPercent;

    /// <summary>피해 속성.</summary>
    public DamageElement element;

    /// <summary>피격 유형. 어느 쪽 방어도를 쓸지 결정한다.</summary>
    public HitKind hitKind;

    /// <summary>무기·장비가 제공하는 방어 관통 레벨. (Pierce와 다른 개념이다)</summary>
    public int armourPenetration;

    /// <summary>발사 지점에서 대상까지의 거리(m). 음수면 사거리 보정을 적용하지 않는다.</summary>
    public float distance;

    /// <summary>무기의 유효 사거리(m). 0 이하면 사거리 보정을 적용하지 않는다.</summary>
    public float effectiveRange;

    /// <summary>치명타 발동 여부.</summary>
    public bool isCritical;

    /// <summary>치명타 배율. isCritical이 true일 때만 곱해진다.</summary>
    public float criticalMultiplier;

    /// <summary>
    /// true면 방어도 보정을 건너뛴다. 상태이상 피해가 이 경로를 쓴다.
    ///
    /// "단단한 적을 스킬로 우회한다"를 성립시키는 유일한 장치다. (3-3절)
    /// </summary>
    public bool bypassArmour;

    /// <summary>무기 기본값만으로 만드는 기본 요청.</summary>
    public static DamageRequest FromWeapon(int damage, float range, DamageElement element = DamageElement.Physical)
    {
        return new DamageRequest
        {
            baseDamage = damage,
            increasedPercent = 0f,
            element = element,
            hitKind = HitKind.Ranged,
            armourPenetration = 0,
            distance = -1f,
            effectiveRange = range,
            isCritical = false,
            criticalMultiplier = 1f,
            bypassArmour = false
        };
    }
}
