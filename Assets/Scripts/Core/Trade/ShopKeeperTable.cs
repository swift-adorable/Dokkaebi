/// <summary>
/// 【가게 주인】 (결정 2-121) — 가게 화면 가운데 계산대 뒤에 서는 상인 · 이름표 · 말풍선 한 줄.
/// 【말풍선은 본문 대사를 그대로 빌린다】 (사용자 2026-10-13) — 새로 짓지 않는다. 출처 줄은 테스트가 본문에서 찾는다.
///   영감 — 본문 「잡화 가게」 장면(소굴 첫날) · 빚쟁이 — 대장간을 맡겠다는 말 · 참봉 — 약탕간 부탁
/// </summary>
public static class ShopKeeperTable
{
    public readonly struct Keeper
    {
        public readonly string Name;
        public readonly string Line;

        public Keeper(string name, string line)
        {
            Name = name;
            Line = line;
        }
    }

    private static readonly Keeper Elder = new("영감", "이것 하나는 내가 맡으마. 먹을 것이며 자잘한 살림살이 정도는 대 주마.");
    private static readonly Keeper Debtor = new("빚쟁이", "활이든 갑옷이든 내가 지켜 줄게.");
    private static readonly Keeper Chambong = new("참봉", "네가 주워 온 것으로 약도 달여 주마.");

    public static Keeper Of(ShopKind kind) => kind switch
    {
        ShopKind.Smithy => Debtor,
        ShopKind.Apothecary => Chambong,
        _ => Elder,
    };
}
