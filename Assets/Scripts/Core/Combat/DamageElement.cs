/// <summary>
/// 피해 속성 5종. 장비의 내성과 몬스터 속성이 이 축으로 계산된다.
///
/// 스킬 문서의 부여 Core 이름과 1:1로 대응한다.
///   화염 → Fire / 서리 → Cold / 뇌전 → Lightning / 역병 → Chaos / 열상 → Physical
/// (docs/Blob_Combat_Baseline.md 7절)
/// </summary>
public enum DamageElement
{
    Physical = 0,
    Fire = 1,
    Cold = 2,
    Lightning = 3,
    Chaos = 4
}

/// <summary>
/// 피격 유형. 어느 쪽 방어도를 쓸지 결정한다.
///
/// Blob은 조준이 자동에 가까워 부위 판정이 없다. 대신 피해 유형으로 나눈다.
/// (docs/Blob_Equipment_System.md 1절)
/// </summary>
public enum HitKind
{
    /// <summary>원거리 · 투사체 → 머리 방어도를 적용한다.</summary>
    Ranged = 0,

    /// <summary>근접 · 접촉 · 폭발 → 몸통 방어도를 적용한다.</summary>
    Melee = 1
}
