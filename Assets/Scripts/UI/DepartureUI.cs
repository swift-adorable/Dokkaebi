using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 파밍 출발 — 어느 구역으로 갈지 고른다. (로드맵 3단계 · Progression 3절)
///
/// 【덕코프식이다】 — 장 하나가 몇 번이고 다시 들어가는 맵이다. 열린 구역은
/// 끝낸 뒤에도 언제든 고를 수 있다. 「오늘 어디를 털지 고른다」가 된다.
/// 닫힌 장은 왜 닫혔는지 적는다 — 5장은 사신패 수를 보인다.
///
/// 【오늘 밤을 미리 보여 준다】 (결정 2-59 ~ 2-64 · 2-91) — 맨 위에 달, 장 머리마다 계절 · 날씨.
/// 들어가서 알게 하지 않는다(Hunting 5절). 궂은 날이 싫으면 「하룻밤 쉬기」로 넘긴다 —
/// 달이 한 칸 돌고 날씨를 다시 뽑는다(덕코프 침대). 이야기의 밤과는 따로 돈다.
/// </summary>
public class DepartureUI : MonoBehaviour
{
    private static DepartureUI instance;

    private GameObject panel;
    private UIFactory.ScrollList list;
    private Text nightLabel;

    private const float RowHeight = 92f;
    private const float RowGap = 10f;

    public static void Open()
    {
        if (instance == null)
        {
            Canvas canvas = UIFactory.CreateCanvas("DepartureCanvas (Runtime)", 1300);
            instance = canvas.gameObject.AddComponent<DepartureUI>();
            instance.Build(canvas);
        }

        instance.Fill();
        instance.panel.SetActive(true);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build(Canvas canvas)
    {
        RectTransform safe = UIFactory.CreateSafeArea(canvas);

        Image shade = UIFactory.CreatePanel("DepartureShade", safe, new Color(0f, 0f, 0f, 0.55f),
            Vector2.zero, Vector2.one, radius: 0);
        shade.raycastTarget = true;
        panel = shade.gameObject;

        Image box = UIFactory.CreateGlass("DepartureBox", shade.transform, UIPalette.Panel,
            new Vector2(0.22f, 0.08f), new Vector2(0.78f, 0.92f), UIFactory.RadiusLarge);

        UIFactory.CreateLabel(box.transform, "어디로 갈까", 36, FontStyle.Bold,
            new Vector2(0.05f, 0.89f), new Vector2(0.95f, 0.98f), TextAnchor.MiddleCenter, UIPalette.TextOnGlass);

        nightLabel = UIFactory.CreateLabel(box.transform, string.Empty, 20, FontStyle.Normal,
            new Vector2(0.05f, 0.83f), new Vector2(0.95f, 0.89f), TextAnchor.MiddleCenter, UIPalette.TextOnGlass);

        list = UIFactory.CreateScrollList("DepartureList", box.transform,
            new Vector2(0.05f, 0.14f), new Vector2(0.95f, 0.82f));

        UIFactory.CreateButton(box.transform, "하룻밤 쉬기",
            new Vector2(0.08f, 0.03f), new Vector2(0.46f, 0.11f), UIPalette.Subtle, Rest, 26);

        UIFactory.CreateButton(box.transform, "닫기",
            new Vector2(0.54f, 0.03f), new Vector2(0.92f, 0.11f), UIPalette.Subtle, Close, 26);

        panel.SetActive(false);
    }

    private void Fill()
    {
        UIFactory.ClearChildren(list.Content);

        StoryProgress progress = StoryManager.Progress;
        float y = 0f;

        MoonPhase moon = NightClock.Moon;
        nightLabel.text = $"오늘 밤 · {MoonTable.Name(moon)} — {MoonTable.Describe(moon)}";

        foreach (ChapterDefinition chapter in StoryTable.Chapters)
        {
            AddHeader(chapter, progress, ref y);

            if (!progress.IsChapterOpen(chapter.Number))
                continue;

            foreach (ZoneDefinition zone in chapter.Zones)
            {
                if (!progress.IsZoneOpen(zone.Id))
                    continue;

                string label = progress.IsCleared(zone.Id) ? $"{zone.Title}  ·  끝냄" : zone.Title;
                string id = zone.Id;

                Button row = UIFactory.CreateButton(list.Content, label,
                    Vector2.zero, Vector2.zero, progress.IsCleared(zone.Id) ? UIPalette.Subtle : UIPalette.Action,
                    () => Depart(id), 28);

                Place(row.GetComponent<RectTransform>(), ref y);
            }
        }

        list.Content.sizeDelta = new Vector2(0f, y);
    }

    private void AddHeader(ChapterDefinition chapter, StoryProgress progress, ref float y)
    {
        string text = chapter.Title;

        if (!progress.IsChapterOpen(chapter.Number))
            text += chapter.Number == 5
                ? $"  —  사신패 {progress.Tablets.Count}/{StoryTable.TabletsForGate}"
                : "  —  아직 길이 열리지 않았다";

        Text label = UIFactory.CreateLabel(list.Content, text, 26, FontStyle.Bold,
            Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft,
            progress.IsChapterOpen(chapter.Number) ? UIPalette.TextAccent : UIPalette.TextDim);

        Place(label.rectTransform, ref y);

        if (!progress.IsChapterOpen(chapter.Number))
            return;

        // 계절 · 날씨 한 줄 — 무엇이 아프고 무엇으로 막는지까지.
        RaidWeather weather = NightClock.WeatherFor(chapter.Number, progress);
        string detail = weather.Describe();
        string line = $"{WeatherTable.SeasonName(weather.Season)} · {weather.Name}"
                      + (string.IsNullOrEmpty(detail) ? string.Empty : $"  —  {detail}");

        Text sub = UIFactory.CreateLabel(list.Content, line, 20, FontStyle.Normal,
            Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft,
            weather.Hazard != WeatherHazard.None || weather.IsMonsoon ? UIPalette.Cost : UIPalette.TextDim);

        Place(sub.rectTransform, ref y, WeatherRowHeight);
    }

    private const float WeatherRowHeight = 40f;

    private static void Place(RectTransform rect, ref float y, float height = RowHeight)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(8f, -(y + height));
        rect.offsetMax = new Vector2(-8f, -y);
        y += height + RowGap;
    }

    /// <summary>하룻밤 쉰다 — 달이 한 칸 돌고 날씨를 다시 뽑는다. 이야기의 밤은 지나지 않는다 (결정 2-63).</summary>
    private void Rest()
    {
        NightClock.PassNight();
        SaveManager.Commit("하룻밤 쉬기");
        Fill();
    }

    private void Depart(string zoneId)
    {
        StoryManager.TargetZone = zoneId;
        Close();
        SceneFlow.Depart();
    }

    private void Close() => panel.SetActive(false);
}
