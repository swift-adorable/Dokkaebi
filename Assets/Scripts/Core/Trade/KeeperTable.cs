using UnityEngine;

/// <summary>어느 시설 화면인가 — 가게 셋 밖의 것 (결정 2-123).</summary>
public enum Station
{
    Stash = 0,
    Building = 1,
    Ledger = 2,
    Brew = 3,
    Hearth = 4,
    Workbench = 5,
}

/// <summary>
/// 【가게 · 시설의 주인】 (결정 2-121 · 2-123) — 화면 가운데 계산대 뒤에 서는 이 · 이름표 · 말풍선 한 줄.
/// 【말풍선은 본문 대사를 그대로 빌린다】 (사용자 2026-10-13) — 새로 짓지 않는다. 테스트가 본문에서 찾는다.
///   영감 — 잡화 가게(좌판을 맡겠다는 말) · 창고(창고를 보여 줄 때) · 건물 짓기(설계도를 펼칠 때)
///   빚쟁이 — 대장간을 맡겠다는 말 · 참봉 — 약탕간 부탁(가게 · 약탕 같이) · 길달 — 장부방 부탁
///   부뚜막 · 작업대 — 주인이 없다. 이름표와 시설 도형만.
/// 도형 빛깔은 【임시】 — 아트 때 그림으로 바꾼다.
/// </summary>
public static class KeeperTable
{
    public readonly struct Keeper
    {
        public readonly string Name;
        public readonly string Line;
        public readonly bool IsPerson;
        public readonly Color Body;
        public readonly Color Head;

        public Keeper(string name, string line, bool person, Color body, Color head)
        {
            Name = name;
            Line = line;
            IsPerson = person;
            Body = body;
            Head = head;
        }
    }

    private static readonly Color ElderBody = new(0.40f, 0.33f, 0.22f), ElderHead = new(0.72f, 0.62f, 0.50f);
    private static readonly Color DebtorBody = new(0.30f, 0.27f, 0.30f), DebtorHead = new(0.62f, 0.52f, 0.48f);
    private static readonly Color ChambongBody = new(0.24f, 0.36f, 0.40f), ChambongHead = new(0.80f, 0.74f, 0.66f);
    private static readonly Color GildalBody = new(0.18f, 0.18f, 0.22f), GildalHead = new(0.78f, 0.70f, 0.62f);

    public const string ElderShopLine = "이것 하나는 내가 맡으마. 먹을 것이며 자잘한 살림살이 정도는 대 주마.";
    public const string DebtorLine = "활이든 갑옷이든 내가 지켜 줄게.";
    public const string ChambongLine = "네가 주워 온 것으로 약도 달여 주마.";
    public const string ElderStashLine = "아까운 건 여기 두고 다니거라. 여기 둔 것은 잃어버리지 않는다.";
    public const string ElderBuildLine = "도깨비는 원래 하룻밤 사이에 다리도 놓고 집도 짓는다. 재료는 네가 모아 오너라. 짓는 건 내가 하마.";
    public const string GildalLine = "셈은 정확히 하는 편이다.";

    public static Keeper OfShop(ShopKind kind) => kind switch
    {
        ShopKind.Smithy => new Keeper("빚쟁이", DebtorLine, true, DebtorBody, DebtorHead),
        ShopKind.Apothecary => new Keeper("참봉", ChambongLine, true, ChambongBody, ChambongHead),
        _ => new Keeper("영감", ElderShopLine, true, ElderBody, ElderHead),
    };

    public static Keeper Of(Station station) => station switch
    {
        Station.Stash => new Keeper("영감", ElderStashLine, true, ElderBody, ElderHead),
        Station.Building => new Keeper("영감", ElderBuildLine, true, ElderBody, ElderHead),
        Station.Ledger => new Keeper("길달", GildalLine, true, GildalBody, GildalHead),
        Station.Brew => new Keeper("참봉", ChambongLine, true, ChambongBody, ChambongHead),
        Station.Hearth => new Keeper(CookingTable.HearthName, null, false, default, default),
        _ => new Keeper("작업대", null, false, default, default),
    };
}
