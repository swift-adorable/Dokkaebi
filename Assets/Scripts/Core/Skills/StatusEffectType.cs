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

    /// <summary>동결 — 【임계 상태】 냉각이 최대 중첩에 도달하면 전이한다. 행동 불능.</summary>
    Freeze = 3,

    /// <summary>감전 — 뇌전이 부여. 받는 모든 피해 +20%. 소비 시 낙뢰 3회.</summary>
    Shock = 4,

    /// <summary>출혈 — 열상이 부여. 이동 중인 적에게 피해 배증. 소비 시 혈액 분출.</summary>
    Bleed = 5,

    /// <summary>응집 — 중력 붕괴가 부여. 상태 전이 범위 2배.</summary>
    Congeal = 6,

    /// <summary>냉각 — 서리가 부여. 이동·공격 속도 감소. 최대 중첩에서 동결로 전이한다.</summary>
    Chill = 7,

    // ── 임계 상태 ─────────────────────────────────────────────────────
    // 중첩이 차면 질적으로 다른 것이 된다. 직접 부여되지 않고 전이로만 생긴다.
    // 전이 시 원본 중첩을 전부 소모하므로 끝나면 처음부터 다시 쌓아야 한다 —
    // 무한 제압을 막는 장치다. (docs/Blob_Combat_Baseline.md 「상태이상」)

    /// <summary>마비 — 【임계 상태】 감전이 최대 중첩에 도달하면 전이한다. 행동 불능.</summary>
    Paralyze = 8,

    /// <summary>부식 — 【임계 상태】 중독이 최대 중첩에 도달하면 전이한다. 방어도·회복량 절반.</summary>
    Corrode = 9
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
    GravityWell = 5,

    /// <summary>
    /// 【속성은 꽂힌 핵심 젬이 정한다.】 보조 젬이 쓰는 값이다.
    ///
    /// 「마름쇠」는 잔류물을 남기는 보조 젬인데 FireZone으로 고정돼 있었다.
    /// 서리 핵심 젬에 꽂아도 불바다가 생긴다는 뜻이고, 이는
    /// 「무기 = 기본값 / 핵심 젬 = 속성 / 보조 젬 = 궤도」라는 전투 3층
    /// 원칙을 보조 젬이 깨는 것이다. (Master_Prompt 기획 확정 현황)
    ///
    /// 실제 종류는 GroundEffectTable.Resolve가 핵심 젬의 부여 상태로 정한다.
    /// </summary>
    FromCoreAilment = 6
}
