/// <summary>
/// Skill의 대가(Cost) — v5 §7-1 확정 4종.
///
/// ※ Damage 항목을 의도적으로 두지 않는다. (마스터 프롬프트 10-3)
///    체력 10 / 짧은 TTK 환경에서 유저는 DPS 변화를 인지하지 못하므로
///    피해 수치 감소는 대가로서 기능하지 않는다.
///    기획 단계의 실수를 타입 수준에서 차단하기 위해 enum에 넣지 않는다.
/// </summary>
public enum CostType
{
    /// <summary>대가 없음. 기본값.</summary>
    None = 0,

    /// <summary>탄막 밀도 — 발사 간격 증가, 탄 수 감소. 작은 화면에서 즉시 체감된다.</summary>
    BarrageDensity = 1,

    /// <summary>유효 사거리 — 사거리 축소 → 접근 강요 → 위험 증가.</summary>
    EffectiveRange = 2,

    /// <summary>조작 제약 — 행동 자체를 제약한다 (이동 중에만, 연사 시 과열 등).</summary>
    ControlConstraint = 3,

    /// <summary>기능 배타 — "X를 이용하는 대신 X를 생성할 수 없다".</summary>
    FunctionalExclusion = 4
}
