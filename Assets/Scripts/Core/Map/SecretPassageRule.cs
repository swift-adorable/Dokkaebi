/// <summary>
/// 【역행 비밀 통로 규칙】 (결정 2-52 · 2-88) — 장마다 이전 장으로 이어지는 통로 하나.
///
///   · 입구는 그 장 맵의 Secret 자리, 나가는 곳은 이전 장 맵의 SecretExit 자리 (ZoneMapTable)
///   · 열쇠 없음 — 이스터 에그처럼 찾는다. 표시도 없다(가까이 가면 버튼만 뜬다)
///   · 「다음 장을 진행한 뒤에만」 = 입구가 있는 구역을 끝낸 뒤에만 나타난다
///     (1-3 현무 · 2-3 강철이 · 3-1 조각 · 4-1 두두리 · 5-1 산군 · 6-1 삼족오)
///   · 처음 지나가면 패시브 역행 계열이 보인다
///   · 지나가면 들고 있는 그대로 이전 장 맵의 그 구역으로 넘어간다 — 파밍 출발처럼 저장하고 넘어간다
/// </summary>
public static class SecretPassageRule
{
    /// <summary>입구에 이만큼 다가가면 버튼이 뜬다 (m).</summary>
    public const float Radius = 1.6f;

    public const string SaveReason = "비밀 통로";

    /// <summary>그 장의 통로 입구. 0장 · 맵이 없는 장은 null.</summary>
    public static MapAnchor? EntranceOf(int chapter) => ZoneMapTable.FirstOf(chapter, MapAnchorKind.Secret);

    /// <summary>그 장의 통로가 닿는 곳 — 이전 장의 SecretExit. 없으면 null.</summary>
    public static MapAnchor? ExitOf(int chapter)
        => chapter <= 0 ? null : ZoneMapTable.FirstOf(chapter - 1, MapAnchorKind.SecretExit);

    /// <summary>입구가 나타나는가 — 입구가 있는 구역을 끝냈다.</summary>
    public static bool IsOpen(StoryProgress progress, int chapter)
    {
        MapAnchor? entrance = EntranceOf(chapter);
        return progress != null && entrance.HasValue && ExitOf(chapter).HasValue
               && progress.IsCleared(entrance.Value.ZoneId);
    }

    /// <summary>버튼에 적을 말 — 입구 이름의 앞부분(「—」 앞).</summary>
    public static string PromptOf(MapAnchor entrance)
    {
        string label = entrance.Label ?? "샛길";
        int cut = label.IndexOf('—');
        return (cut > 0 ? label.Substring(0, cut) : label).Trim();
    }
}
