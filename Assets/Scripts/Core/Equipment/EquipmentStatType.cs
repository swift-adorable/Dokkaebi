/// <summary>
/// 장비 옵션 항목. (docs/Dokkaebi_Equipment_System.md 4절)
///
/// enum + 값 쌍으로 두는 이유 — 합산·표시·세이브가 전부 단순해진다.
/// 필드로 나열하면 옵션 하나를 추가할 때마다 세 곳을 고쳐야 한다.
/// </summary>
public enum EquipmentStatType
{
    None = 0,

    // ── 방어 ──────────────────────────────────────────────────────────
    /// <summary>머리 방어도 — 원거리 · 투사체 피격에 적용.</summary>
    HeadArmour = 1,

    /// <summary>몸통 방어도 — 근접 · 접촉 · 폭발 피격에 적용.</summary>
    BodyArmour = 2,

    /// <summary>방어 관통 — 적의 방어도를 뚫는다. Pierce(관통)와 다른 개념이다.</summary>
    ArmourPenetration = 3,

    /// <summary>격리 방호 — 누적 수치 한계치. 1 이상이면 1단계, 2 이상이면 2단계 차단.</summary>
    ContainmentWard = 4,

    // 속성 저항 배율에 더해지는 값. 음수가 저항 강화다. (0.12 = 저항 ×0.88)
    ResistPhysical = 10,
    ResistFire = 11,
    ResistCold = 12,
    ResistLightning = 13,
    ResistChaos = 14,

    // ── 생존 ──────────────────────────────────────────────────────────
    MaxHealth = 20,

    /// <summary>초당 체력 회복. 【음수면 체력 지속 감소다】 — 각인 Ⅲ의 대가로 쓴다.</summary>
    HealthRegen = 21,

    InvulnerableTime = 22,

    /// <summary>받는 회복량 증가율. −1.0이면 회복량 0. (각인 「먹성 Ⅲ」)</summary>
    HealingReceived = 23,

    // ── 기동 ──────────────────────────────────────────────────────────
    MoveAbility = 30,
    DashCooldown = 31,

    // ── 탐지 ──────────────────────────────────────────────────────────
    ViewDistance = 40,
    ViewAngle = 41,
    NightVision = 42,
    DetectDistance = 43,
    DetectedDistance = 44,

    // ── 청각 ──────────────────────────────────────────────────────────
    Hearing = 50,
    SoundLocate = 51,
    MoveSoundRange = 52,

    // ── 수집 ──────────────────────────────────────────────────────────
    XpAbsorbRange = 60,
    XpAbsorbAmount = 61,
    RareDropRate = 62,

    // ── 칸 ──────────────────────────────────────────────────────────
    MaxCarryWeight = 70,
    SlotCapacity = 71,

    // ── 공격 ──────────────────────────────────────────────────────────
    // 무기가 정한 기본값에 곱해지는 보정이다. 전부 가산 합산한다.
    // (docs/Dokkaebi_Combat_Baseline.md 2절 — "모든 증가는 가산 합산")
    //
    // ※ 이 축은 각인과 무기 부착물만 건드린다.
    //   방어구가 피해를 올리면 「무기 = 화력 / 방어구 = 생존」 경계가 무너진다.

    /// <summary>기본 피해 증가율. 0.2 = +20%</summary>
    DamageIncrease = 80,

    /// <summary>유효 사거리 증가율. 음수면 줄어든다.</summary>
    WeaponRangeIncrease = 81,

    /// <summary>발사 간격 증가율. 【양수면 느려진다】.</summary>
    FireIntervalIncrease = 82,

    /// <summary>치명타 확률(절대값). 0.15 = 15%</summary>
    CriticalChance = 83,

    /// <summary>치명타 배율에 더해지는 값. 0.5면 배율 1.5 → 2.0</summary>
    CriticalMultiplier = 84,

    /// <summary>상태이상 위력 증가율. 직접 피해와 분리된 축이다.</summary>
    AilmentPower = 85,

    // ── 막이 — 궂은 날 (결정 2-63 · 2-91) ────────────────────────────────
    // 덕코프 방한 · 폭풍 방어처럼 장비 한 부위 +0.5 ~ +1. 합을 반올림한 단계가 궂은 날 Ⅰ · Ⅱ를 막는다.

    /// <summary>방한 — 겨울 한파.</summary>
    ProtectWarmth = 90,

    /// <summary>막이 — 봄 흙비.</summary>
    ProtectShield = 91,

    /// <summary>서늘함 — 여름 폭염 · 독안개.</summary>
    ProtectCool = 92,

    /// <summary>밝히기 — 가을 짙은 안개.</summary>
    ProtectLight = 93
}

/// <summary>
/// 옵션의 부호 의미. 【대부분은 양수가 이득이지만 예외가 있다.】
///
/// 이 클래스가 필요한 이유 —
/// 「발사 간격 +150%」와 「대시 쿨다운 +99초」는 양수인데 페널티다.
/// 부호만 보고 대가를 판정하면 각인 「중장 Ⅲ」이 순수 증가로 통과한다.
/// 실제로 EquipmentAssetTests가 그 버그를 잡았다.
///
/// 규약을 enum 이름에 맡기지 않고 여기에 명시한 이유는,
/// 옵션을 추가할 때 "이건 어느 쪽이지"를 반드시 한 번 생각하게 만들기 위함이다.
/// </summary>
public static class EquipmentStatMeta
{
    /// <summary>값이 작을수록 이득인 옵션인지.</summary>
    public static bool IsLowerBetter(EquipmentStatType type)
    {
        switch (type)
        {
            // 쿨다운 · 간격 — 짧아야 좋다
            case EquipmentStatType.DashCooldown:
            case EquipmentStatType.FireIntervalIncrease:

            // 발각 · 소음 — 작아야 좋다
            case EquipmentStatType.DetectedDistance:
            case EquipmentStatType.MoveSoundRange:
                return true;

            default:
                return false;
        }
    }

    /// <summary>이 옵션이 대가(페널티)인지.</summary>
    public static bool IsDrawback(EquipmentStat stat)
    {
        if (stat.type == EquipmentStatType.None || stat.value == 0f)
            return false;

        return IsLowerBetter(stat.type) ? stat.value > 0f : stat.value < 0f;
    }

    /// <summary>이 옵션이 이득인지.</summary>
    public static bool IsGain(EquipmentStat stat)
    {
        if (stat.type == EquipmentStatType.None || stat.value == 0f)
            return false;

        return IsLowerBetter(stat.type) ? stat.value < 0f : stat.value > 0f;
    }
}
