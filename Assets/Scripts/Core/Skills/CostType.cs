/// <summary>
/// Skill의 대가(Cost) — Skill System v8 §6-1 확정 7종.
///
/// ※ Damage 항목을 의도적으로 두지 않는다.
///    체력 10 / 짧은 TTK 환경에서 유저는 DPS 변화를 인지하지 못하므로
///    직접 피해 수치 감소는 대가로서 기능하지 않는다.
///    기획 단계의 실수를 타입 수준에서 차단하기 위해 enum에 넣지 않는다.
///
/// ※ v7에서 4종 → 7종으로 확장했다.
///    기존 4종으로 분류 가능한 Support가 35종 중 13종뿐이었고, 나머지의 대가
///    (탄속·지속시간·즉시성)가 타입 밖에 있어 CostType이 실제 데이터를
///    설명하지 못했다. PoE2도 대가의 종류를 제한하지 않는다.
///    제한해야 할 것은 종류가 아니라 "유저가 체감하지 못하는 대가"뿐이다.
/// </summary>
public enum CostType
{
    /// <summary>대가 없음. 기본값.</summary>
    None = 0,

    /// <summary>탄막 밀도 — 발사 간격 증가, 탄 수 감소. 작은 화면에서 즉시 체감된다.</summary>
    BarrageDensity = 1,

    /// <summary>유효 사거리 — 사거리·범위 축소 → 접근 강요 → 위험 증가.</summary>
    EffectiveRange = 2,

    /// <summary>조작 제약 — 행동 자체를 제약한다 (이동 중에만, 연사 시 과열 등).</summary>
    ControlConstraint = 3,

    /// <summary>기능 배타 — "X를 이용하는 대신 X를 생성할 수 없다".</summary>
    FunctionalExclusion = 4,

    /// <summary>
    /// 투사체 속도 — 탄속 증감.
    /// poe2db에 「투사체 가속」「투사체 감속」이 각각 독립 젬으로 실존한다. [확인됨]
    /// </summary>
    ProjectileSpeed = 5,

    /// <summary>
    /// 지속시간 — 상태이상·잔류물의 지속시간 증감.
    /// poe2db에 「지속시간 연장」「지속시간 압축」이 실존한다. [확인됨]
    /// 총 피해량을 줄이지만 유저가 "짧아졌다"로 체감하므로 별도 통화로 인정한다.
    /// </summary>
    Duration = 6,

    /// <summary>
    /// 즉시성 — 발동이 지연되거나, 즉발이 지속으로 전환된다.
    /// poe2db 「긴 퓨즈」「느린 효력」이 이 유형이다. [확인됨]
    /// </summary>
    Immediacy = 7
}
