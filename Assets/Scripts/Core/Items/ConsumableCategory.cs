/// <summary>
/// 소모품 4분류 + 음료·음식. (docs/Blob_Consumable_System.md 1절)
///
/// 【분류가 있어야 하는 이유】 같은 분류는 덮어쓴다는 규칙(6절)과
/// 「만능 회복약을 만들지 않는다」(7절)를 검사하려면 무엇이 무엇인지
/// 데이터가 알고 있어야 한다. 이 축이 없어서 감사 D4로 남아 있었다.
/// </summary>
public enum ConsumableCategory
{
    /// <summary>회복 — 체력을 되돌린다.</summary>
    Restore = 0,

    /// <summary>해제 — 걸린 상태이상 하나를 푼다.</summary>
    Cure = 1,

    /// <summary>
    /// 음료 · 음식 — 수분과 에너지를 채운다.
    ///
    /// 【회복과 같은 자리에 두지 않는다.】 음식은 체력을 채우지 않는다.
    /// 둘을 한 분류로 묶으면 「배고프면 회복약을 먹으면 되지」가 되어
    /// 생존 두 축이 체력에 흡수된다. (결정 2-32)
    /// </summary>
    Sustenance = 2,

    /// <summary>강화 — 일시적으로 세진다. 반드시 대가가 붙는다.</summary>
    Boost = 3,

    /// <summary>방호 — 환경·속성을 버틴다. 장비를 대체하지 못한다.</summary>
    Ward = 4
}
