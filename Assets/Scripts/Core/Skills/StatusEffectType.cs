/// <summary>
/// 적에게 걸리는 상태 — v5 §5-1.
///
/// 모든 Skill은 '생성하는 상태'와 '소비하는 상태'를 명시한다.
/// 조합 가능 여부는 이 매칭으로 자동 결정되며, 개별 조합 규칙을 코드에 쓰지 않는다.
/// (마스터 프롬프트 10-2 [2])
/// </summary>
public enum StatusEffectType
{
    None = 0,

    /// <summary>점화 — 화염이 부여. 초당 화염 피해. 소비 시 화염 폭발 + 화염 지대.</summary>
    Ignite = 1,

    /// <summary>중독 — 역병이 부여. 중첩형 지속 피해(최대 10). 소비 시 독 구름 확산.</summary>
    Poison = 2,

    /// <summary>동결 — 서리가 부여. 행동 불능. 소비 시 파편 폭발 + 인접 동결.</summary>
    Freeze = 3,

    /// <summary>감전 — 뇌전이 부여. 받는 모든 피해 +20%. 소비 시 낙뢰 3회.</summary>
    Shock = 4,

    /// <summary>출혈 — 열상이 부여. 이동 중인 적에게 피해 배증. 소비 시 혈액 분출.</summary>
    Bleed = 5,

    /// <summary>응집 — 중력 붕괴가 부여. 상태 전이 범위 2배.</summary>
    Congeal = 6
}

/// <summary>바닥에 남는 지형 상태 — v5 §5-2.</summary>
public enum GroundEffectType
{
    None = 0,

    /// <summary>화염 지대 — 점화된 적 사망 시 생성. 진입 시 점화.</summary>
    FireZone = 1,

    /// <summary>독성 늪 — 중독된 적 사망 시 생성. 이동속도 감소 + 중독 중첩.</summary>
    ToxicSwamp = 2,

    /// <summary>서리 장판 — 동결된 적 사망 시 생성. 이동속도 감소, 냉기 누적 가속.</summary>
    FrostField = 3,

    /// <summary>혈액 지대 — 출혈 적 사망 시 생성. 진입 시 출혈.</summary>
    BloodZone = 4,

    /// <summary>중력 우물 — 중력 붕괴가 생성. 흡입 + 응집 부여.</summary>
    GravityWell = 5
}
