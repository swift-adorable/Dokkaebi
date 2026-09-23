using System;

/// <summary>
/// 조건부 드롭 3종. (docs/Blob_Hunting_System.md 6-3절)
///
/// 【빌드를 「가장 센 것」 하나로 수렴시키지 않는 가장 값싼 장치다.】
/// 치명타 빌드는 「온전한 신경절」을 못 얻고,
/// 화염 빌드는 「미연소 포자」를 못 얻는다.
/// 그래서 두 번째 빌드를 굴릴 이유가 생긴다.
///
/// ⚠️ 조건은 반드시 사전에 알려준다. 모르고 놓치게 만드는 건
/// 난이도가 아니라 정보 은폐다. (문서 6-3절)
/// </summary>
[Flags]
public enum ConditionalDrop
{
    None = 0,

    /// <summary>온전한 신경절 — 치명타가 아닌 공격으로 처치.</summary>
    IntactGanglion = 1 << 0,

    /// <summary>굳지 않은 수액 — 동결 상태에서 처치.</summary>
    UnsetSap = 1 << 1,

    /// <summary>미연소 포자 — 점화를 걸지 않고 처치.</summary>
    UnburntSpore = 1 << 2
}

/// <summary>
/// 처치 순간의 사정. 조건부 드롭이 이것만 본다.
/// </summary>
public struct KillContext
{
    /// <summary>마지막 타격이 치명타였는가.</summary>
    public bool killedByCritical;

    /// <summary>죽는 순간 동결 상태였는가.</summary>
    public bool frozenAtDeath;

    /// <summary>
    /// 【살아 있는 동안 한 번이라도】 점화된 적이 있는가.
    ///
    /// 죽는 순간만 보면 안 된다 — 불을 붙였다가 꺼진 뒤에 죽여도
    /// 「점화를 걸지 않고」가 되어 버린다. 조건이 거짓말이 된다.
    /// </summary>
    public bool everIgnited;
}

/// <summary>조건부 드롭 판정. 순수 클래스다.</summary>
public static class ConditionalDropTable
{
    public static ConditionalDrop Evaluate(in KillContext context)
    {
        ConditionalDrop result = ConditionalDrop.None;

        if (!context.killedByCritical)
            result |= ConditionalDrop.IntactGanglion;

        if (context.frozenAtDeath)
            result |= ConditionalDrop.UnsetSap;

        if (!context.everIgnited)
            result |= ConditionalDrop.UnburntSpore;

        return result;
    }

    /// <summary>
    /// 플레이어에게 미리 보여 줄 문구. 【사전 고지가 규칙이다.】
    /// </summary>
    public static string Describe(ConditionalDrop drop)
    {
        switch (drop)
        {
            case ConditionalDrop.IntactGanglion: return "온전한 신경절 — 치명타가 아닌 공격으로 처치";
            case ConditionalDrop.UnsetSap:       return "굳지 않은 수액 — 동결 상태에서 처치";
            case ConditionalDrop.UnburntSpore:   return "미연소 포자 — 점화를 걸지 않고 처치";
            default:                             return string.Empty;
        }
    }

    /// <summary>None을 제외한 3종 전부. 표시와 테스트가 쓴다.</summary>
    public static readonly ConditionalDrop[] All =
    {
        ConditionalDrop.IntactGanglion,
        ConditionalDrop.UnsetSap,
        ConditionalDrop.UnburntSpore
    };
}
