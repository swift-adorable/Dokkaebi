/// <summary>
/// Skill 대분류 — v5 §13 확정. 총 60종.
/// Core 12 / Support 28 / Meta 7 / Persistent 13
/// </summary>
public enum SkillCategory
{
    /// <summary>핵심 스킬. 소켓 3개 보유, 동시 보유 상한 2개.</summary>
    Core = 0,

    /// <summary>보조 스킬. 반드시 Core 소켓에 장착된다. 전역 버프가 아니다.</summary>
    Support = 1,

    /// <summary>발동 스킬. 에너지 축적 후 발동. 확률 발동 금지.</summary>
    Meta = 2,

    /// <summary>유지형 스킬. Nucleus를 점유한다.</summary>
    Persistent = 3
}

/// <summary>
/// Core의 역할 계열 — v5 §6.
/// 전달(어떻게 닿는가) → 부여(무엇을 남기는가) → 기폭(무엇을 소모하는가)
/// </summary>
public enum CoreFamily
{
    /// <summary>Core가 아닌 정의의 기본값.</summary>
    None = 0,

    /// <summary>전달 계열 — 튕겨 쏘기 / 관통 / 분열 / 추적. 상태를 만들지 않는다.</summary>
    Delivery = 1,

    /// <summary>
    /// 부여 계열 — 화염 / 역병 / 서리 / 뇌전 / 열상.
    /// 적에게 상태를 남기는 것이 본체이며 피해는 부수 효과다.
    /// ※ 2026-09-17: "적재 계열"에서 개명했다. 적재(Loadout) 슬롯과 단어가
    ///    겹쳐 문서·코드 양쪽에서 혼동을 일으켰다.
    /// </summary>
    Ailment = 2,

    /// <summary>기폭 계열 — 원소 작렬 / 중력 붕괴 / 충격파.</summary>
    Detonation = 3
}

/// <summary>발동 스킬(Meta)의 두 갈래 — v5 §8.</summary>
public enum MetaTriggerKind
{
    /// <summary>Meta가 아닌 정의의 기본값.</summary>
    None = 0,

    /// <summary>자동 발동형 — 에너지 100% 도달 즉시 발동하고 0으로 초기화된다. 4종.</summary>
    Automatic = 1,

    /// <summary>기원형 — 에너지를 보유한 채 대기하며 유저가 버튼을 눌러야 발동한다. 동시 1개만 장착. 3종.</summary>
    Invocation = 2
}
