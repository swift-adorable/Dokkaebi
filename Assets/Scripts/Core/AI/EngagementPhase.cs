/// <summary>
/// 적이 지금 무엇을 하고 있는가. 「사람처럼 싸운다」를 상태로 쪼갠 것.
///
/// 이전 구조에는 이것이 없었다. 적은 언제나 플레이어를 향해 직선으로 달렸고,
/// 사거리에 닿으면 멈춰서 때렸다. 그래서 여럿이 나오면 한 점으로 뭉쳤다.
/// </summary>
public enum EngagementPhase
{
    /// <summary>대상이 없거나 너무 멀다.</summary>
    Idle = 0,

    /// <summary>사거리 밖이다. 붙는다.</summary>
    Advance = 1,

    /// <summary>적정 거리다. 옆으로 돌며 쏜다.</summary>
    Hold = 2,

    /// <summary>너무 붙었다. 쏘면서 물러난다.</summary>
    Back = 3,

    /// <summary>지금은 내 차례가 아니다. 사거리 밖을 돌며 기회를 본다.</summary>
    Reposition = 4,

    /// <summary>재장전 중이다. 쏘지 않고 거리를 벌린다.</summary>
    Reload = 5
}
