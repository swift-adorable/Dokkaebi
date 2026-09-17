/// <summary>
/// 인자를 소켓에 끼우지 못한 이유. UI가 그대로 문구로 바꿔 보여준다.
///
/// bool 하나로 돌려주면 "왜 안 되는지"를 유저에게 말해 줄 수 없다.
/// 파밍으로 획득 방식이 바뀌면서 "주웠는데 못 끼운다"가 흔한 상황이 되었으므로
/// 이유를 반드시 구분해야 한다.
/// </summary>
public enum SocketError
{
    /// <summary>끼울 수 있다.</summary>
    None = 0,

    /// <summary>인자가 없다.</summary>
    NullGem,

    /// <summary>그 자리에 들어갈 분류가 아니다. (Support를 Core 자리에 등)</summary>
    WrongCategory,

    /// <summary>존재하지 않는 자리다.</summary>
    NoSuchSlot,

    /// <summary>아직 각성 레벨이 열지 않은 자리다.</summary>
    SlotLocked,

    /// <summary>인자의 요구 레벨이 각성 레벨보다 높다. 주울 수는 있으나 끼울 수 없다.</summary>
    LevelTooHigh,

    /// <summary>그 Core에 아직 아무것도 끼워지지 않아 소켓이 작동하지 않는다.</summary>
    NoCore,

    /// <summary>Core가 이 Support의 요구 태그를 만족하지 않는다. (태그 게이팅)</summary>
    TagMismatch,

    /// <summary>같은 인자가 이미 그 자리 무리 안에 있다.</summary>
    Duplicate
}

/// <summary>SocketError를 유저에게 보여 줄 한국어 문구로 바꾼다.</summary>
public static class SocketErrorText
{
    public static string Describe(SocketError error)
    {
        switch (error)
        {
            case SocketError.None:          return string.Empty;
            case SocketError.NullGem:       return "인자가 없습니다.";
            case SocketError.WrongCategory: return "이 자리에 들어갈 분류가 아닙니다.";
            case SocketError.NoSuchSlot:    return "없는 자리입니다.";
            case SocketError.SlotLocked:    return "아직 열리지 않은 자리입니다.";
            case SocketError.LevelTooHigh:  return "각성 레벨이 부족합니다.";
            case SocketError.NoCore:        return "핵심 스킬을 먼저 끼워야 합니다.";
            case SocketError.TagMismatch:   return "핵심 스킬이 요구 태그를 만족하지 않습니다.";
            case SocketError.Duplicate:     return "같은 인자가 이미 끼워져 있습니다.";
            default:                        return "끼울 수 없습니다.";
        }
    }
}
