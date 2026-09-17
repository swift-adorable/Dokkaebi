/// <summary>
/// Skill 대분류 — Skill System v8 §13 확정. 총 53종.
/// Core 8 / Support 35 / Meta 5 / Persistent 5
/// </summary>
public enum SkillCategory
{
    /// <summary>핵심 스킬. 소켓 3개 보유, 동시 보유 상한 2개 (2번째는 Lv7 개방).</summary>
    Core = 0,

    /// <summary>보조 스킬. 반드시 Core 소켓에 장착된다. 전역 버프가 아니다.</summary>
    Support = 1,

    /// <summary>발동 스킬. 에너지 축적 후 자동 발동. 동시 장착 2개. 확률 발동 금지.</summary>
    Meta = 2,

    /// <summary>
    /// 유지형 스킬 — 전령(Herald) 5종. 조건부 처치 시 연쇄.
    /// 동시 장착 1개. Nucleus 같은 자원 시스템은 쓰지 않는다. (v8 §8-1)
    /// </summary>
    Persistent = 3
}

/// <summary>
/// Core의 역할 계열 — Skill System v8 §5.
/// 부여(무엇을 남기는가) → 기폭(무엇을 소모하는가)
///
/// ※ "전달 계열"은 없다. "어떻게 닿는가"는 Core가 아니라 소켓의 Support가
///    담당한다. PoE2에서 투사체 행동은 전부 보조 젬이기 때문이다.
/// </summary>
public enum CoreFamily
{
    /// <summary>Core가 아닌 정의의 기본값.</summary>
    None = 0,

    /// <summary>
    /// 부여 계열 — 화염 / 역병 / 서리 / 뇌전 / 열상.
    /// 적에게 상태를 남기는 것이 본체이며 피해는 부수 효과다.
    /// 전부 `투사체` 태그를 가지므로 투사체 Support가 붙을 곳이 항상 존재한다.
    /// </summary>
    Ailment = 1,

    /// <summary>
    /// 기폭 계열 — 원소 작렬 / 중력 붕괴 / 충격파.
    /// `투사체` 태그가 없다. 파동·잔류물·플레이어 중심 효과다.
    /// </summary>
    Detonation = 2
}
