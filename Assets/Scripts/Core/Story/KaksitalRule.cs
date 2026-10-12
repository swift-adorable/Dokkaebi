/// <summary>
/// 【각시탈】 (결정 2-103 · 본문 「각시탈은 구미호가 가까이 있다는 것만 알린다(얼굴의 정체는 가리지 못한다)」).
///
///   · 얻는 때 — 3-2 두억시니를 쓰러뜨린 뒤 새색시 장면에서 줍는다(본문). 그래서 「두억시니를 쓰러뜨렸다」가 곧 각시탈을 가졌다는 뜻이다.
///     가방에 들지 않는다 — 6-2까지 품에 있으므로 쓰러져도 잃지 않는다. 세이브에 따로 적지 않는다.
///   · 알림 — 구미호가 NoticeRange 안에 있으면 화면 위에 탈 표시가 떨리며 나타난다. 글은 없다(본문에 없는 문장을 쓰지 않는다).
///   · 본문에서 길달이 탈을 받아 보지만 6-2에서 탈은 어린 도깨비 품속에 있다 — 내내 가진 것으로 친다.
/// 수치는 [임시값].
/// </summary>
public static class KaksitalRule
{
    /// <summary>각시탈을 줍는 장면 바로 앞의 보스 — 3-2 두억시니.</summary>
    public const string FoundAfterBossId = "dueoksini";

    /// <summary>각시탈이 알리는 것.</summary>
    public const string GumihoId = "gumiho";

    /// <summary>이 거리(m) 안에 구미호가 있으면 알린다.</summary>
    public const float NoticeRange = 30f;

    /// <summary>지금 판에 나와 있는 구미호. 없으면 null. StoryRaidDirector가 넣는다.</summary>
    public static Health Gumiho;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Gumiho = null;

    /// <summary>각시탈을 가졌는가.</summary>
    public static bool Has(StoryProgress progress)
        => progress != null && progress.HasDefeated(FoundAfterBossId);

    /// <summary>알릴 때인가 — 탈을 가졌고, 살아 있는 구미호가 거리 안에 있다.</summary>
    public static bool ShouldNotice(bool hasMask, bool gumihoAlive, float distance)
        => hasMask && gumihoAlive && distance <= NoticeRange;
}
