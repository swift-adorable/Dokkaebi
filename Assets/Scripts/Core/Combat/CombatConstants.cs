/// <summary>
/// 전투 수치의 단일 기준. (docs/Blob_Combat_Baseline.md 2절)
///
/// 이 값들을 바꾸면 스킬·장비·사냥이 전부 따라 움직인다.
/// 다른 곳에서 같은 의미의 상수를 새로 정의하지 않는다.
/// </summary>
public static class CombatConstants
{
    /// <summary>플레이어 기본 최대 체력.</summary>
    public const int PlayerBaseHealth = 100;

    /// <summary>플레이어 최대 체력 상한. (기본 100 + 계정 트리 30 + 장비 50)</summary>
    public const int PlayerHealthCap = 180;

    /// <summary>피격 후 무적 시간(초).</summary>
    public const float InvulnerableDuration = 0.6f;

    /// <summary>티어 1 무기의 기본 피해. 모든 피해 계산의 원점이다.</summary>
    public const int BaseWeaponDamage = 10;

    /// <summary>티어 1 무기의 발사 간격(초). 기본 DPS 25.</summary>
    public const float BaseFireInterval = 0.4f;

    /// <summary>티어 1 무기의 유효 사거리(m).</summary>
    public const float BaseEffectiveRange = 10f;

    /// <summary>
    /// 유효 사거리의 절반을 넘었을 때 곱해지는 배율. (계단식 감쇠)
    ///
    /// 이 한 항이 무기·부착물 선택과 교전 거리를 전부 지배한다.
    /// ※ TBD — 모바일에서 0.5가 과한지 검증 필요. 0.6까지 완화 여지.
    /// </summary>
    public const float FarRangeMultiplier = 0.5f;

    /// <summary>
    /// 방어도 공식의 상수항. 최종 배율 = K / (max(방어도 − 방어 관통, 0) + K)
    ///
    /// 피해량이 식에 들어가지 않으므로 저피해·다탄 빌드가 구조적으로 죽지 않는다.
    /// 관통이 방어도 이상이면 배율이 1로 고정되어 초과 관통에 보상이 없다.
    /// </summary>
    public const float ArmourConstant = 2f;

    /// <summary>방어도·방어 관통의 상한. (장비 티어 6 기준)</summary>
    public const int MaxArmour = 7;
}
