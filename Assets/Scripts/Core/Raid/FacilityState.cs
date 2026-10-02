/// <summary>
/// 밤 상태 3종 — 환경 조건부 스폰. (docs/Dokkaebi_Hunting_System.md 5절)
///
/// 【장 수를 늘리지 않고 조우 다양성을 3배로 만드는 가장 싼 축이다.】
/// 맵 제작이 가장 비싼 자산이라, 같은 맵에 상태만 갈아 끼운다.
///
/// 【상태는 양날이어야 한다.】(문서 5절)
/// 그믐이 시야를 줄이는 대신 순라귀를 멈추듯, 손해와 기회가 같이 와야
/// "위험하지만 갈 만하다"가 된다. 손해만 있으면 그 상태를 피해 다니게 된다.
/// </summary>
public enum FacilityState
{
    /// <summary>평시. 아무 일도 없다.</summary>
    Normal = 0,

    /// <summary>그믐 — 시야 대폭 감소. 대신 순라귀가 멈춘다. 무주귀가 추가로 나온다.</summary>
    Blackout = 1,

    /// <summary>큰물 — 이동 감소, 감전 피해 2배. 수귀가 추가로 나온다.</summary>
    Flooded = 2,

    /// <summary>독안개 — 지속 화학 피해. 왕지네가 강해지고 추가로 나온다.</summary>
    Decontamination = 3
}
