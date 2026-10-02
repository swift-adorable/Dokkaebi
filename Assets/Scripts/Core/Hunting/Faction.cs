/// <summary>
/// 진영 5종. (docs/Dokkaebi_Hunting_System.md 4절)
///
/// 【적 수를 늘리지 않고 전투 다양성을 키우는 가장 싼 방법이다.】
/// 어부지리 · 뒤통수 · 3장 이후 난전의 정체성이 여기서 나온다.
///
/// Team과 다른 축이다 — Team은 「총알이 누구를 때리는가」이고,
/// Faction은 「적끼리 서로를 때리는가」다. 적은 전부 Team.Enemy이면서
/// 서로 다른 Faction일 수 있다.
/// </summary>
public enum Faction
{
    /// <summary>떠돌이(옛 야생) — 잡귀 · 수귀 · 왕지네.</summary>
    Wild = 0,

    /// <summary>부리던 것(옛 시설) — 절굿공이귀 · 순라귀.</summary>
    Facility = 1,

    /// <summary>살(옛 실험체) — 번개귀 · 침귀.</summary>
    Subject = 2,

    /// <summary>헛것(옛 정착) — 허깨비 · 무주귀. 나머지 네 진영 전부와 적대한다.</summary>
    Settled = 3,

    /// <summary>우호 — 구역 내 수집기(상인) · 중립 개체. 플레이어를 공격하지 않는다.</summary>
    Friendly = 4
}

/// <summary>
/// 진영 적대 판정.
///
/// ⚠️ 진영 판정은 프레임마다 돌리지 않는다. 어그로가 바뀔 때만 부른다.
/// (문서 4절 경고) 그래서 이 클래스는 상태를 갖지 않고 순수 함수만 둔다.
/// </summary>
public static class FactionTable
{
    public const int Count = 5;

    /// <summary>
    /// 두 진영이 서로 적대하는가.
    ///
    /// 【기본 규칙 — 소속이 다르면 적대한다.】 (문서 4절 "다른 소속끼리 서로 싸운다")
    ///
    /// 예외 둘:
    ///   · 우호는 누구와도 먼저 싸우지 않는다. 적대만 있으면 구역이
    ///     사격장이 되므로 우호를 반드시 둔다. (문서 4절)
    ///   · 같은 소속끼리는 싸우지 않는다.
    ///
    /// 【정착의 「모든 생명체와 적대」는 해결됐다. (2026-09-22)】
    /// 덕코프는 「모든 것과 적대」를 【전용 진영】(그 종 혼자만 있는 진영)으로
    /// 표현한다. [확인됨 — research/duckov_적.md 1절]
    /// 허깨비와 무주귀는 한 진영에 묶여 있으므로 전용 진영이 아니다.
    /// 문서 4절의 문장을 「나머지 네 진영 전부와 적대」로 고쳤다.
    /// </summary>
    public static bool IsHostile(Faction a, Faction b)
    {
        if (a == b)
            return false;

        if (a == Faction.Friendly || b == Faction.Friendly)
            return false;

        return true;
    }

    /// <summary>플레이어를 공격하는 진영인가. 우호만 아니다.</summary>
    public static bool IsHostileToPlayer(Faction faction)
    {
        return faction != Faction.Friendly;
    }
}
