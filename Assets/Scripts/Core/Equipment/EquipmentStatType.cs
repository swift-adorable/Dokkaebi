/// <summary>
/// 장비 옵션 항목. (docs/Blob_Equipment_System.md 4절)
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

    /// <summary>격리 방호 — 누적 수치 게이트. 1 이상이면 1단계, 2 이상이면 2단계 차단.</summary>
    ContainmentWard = 4,

    // 속성 내성 배율에 더해지는 값. 음수가 내성 강화다. (0.12 = 내성 ×0.88)
    ResistPhysical = 10,
    ResistFire = 11,
    ResistCold = 12,
    ResistLightning = 13,
    ResistChaos = 14,

    // ── 생존 ──────────────────────────────────────────────────────────
    MaxHealth = 20,
    HealthRegen = 21,
    InvulnerableTime = 22,

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

    // ── 적재 ──────────────────────────────────────────────────────────
    MaxCarryWeight = 70,
    SlotCapacity = 71
}
