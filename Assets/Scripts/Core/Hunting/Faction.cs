/// <summary>
/// 진영 5종. (docs/Blob_Hunting_System.md 4절)
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
    /// <summary>야생 — 스캐브 · 잠복체 · 화공체.</summary>
    Wild = 0,

    /// <summary>시설 — 압착기 · 보안기.</summary>
    Facility = 1,

    /// <summary>실험체 — 자전체 · 검체.</summary>
    Subject = 2,

    /// <summary>정착 — 정착체 · 데이터체.</summary>
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
    /// 【불확실】 문서는 정착을 "모든 생명체와 적대"라고 적었다.
    /// 그 문장을 글자 그대로 읽으면 정착체와 데이터체도 서로 싸운다는 뜻이 되는데,
    /// 둘은 같은 소속으로 묶여 있어 모순이다. 여기서는 【같은 소속 제외】로
    /// 구현했다. 원문에 없는 전제를 만들지 않으려고 이 주석을 남긴다.
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
