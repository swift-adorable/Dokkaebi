using UnityEngine;

/// <summary>
/// 【밤 시계】 (결정 2-59 ~ 2-63) — 다음 파밍의 달과 날씨 칸을 들고 있는다. 세이브에 담긴다(moon · weather · snowy).
///
///   · 판이 끝나면(철수 · 쓰러짐) 달이 한 칸 돌고 날씨를 다시 뽑는다 — 출발 화면이 다음 판을 미리 보여 준다
///   · 하룻밤 쉬기(출발 화면 버튼)도 같다 — 궂은 날 Ⅱ를 쉬어 넘긴다 (덕코프 침대 · 결정 2-61 ①)
///   · 날씨 칸은 하나 — 장마다 그 장의 계절로 읽는다(WeatherTable.For)
/// 이야기의 밤과는 따로 돈다 — 쉬어도 상인은 오지 않는다 (결정 2-63).
/// </summary>
public static class NightClock
{
    private static MoonPhase moon = MoonPhase.New;
    private static WeatherSlot slot = WeatherSlot.Clear;
    private static bool snowy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Reset();

    public static MoonPhase Moon => moon;
    public static WeatherSlot Slot => slot;
    public static bool Snowy => snowy;

    /// <summary>그 장의 다음 날씨.</summary>
    public static RaidWeather WeatherFor(int chapter, StoryProgress progress)
    {
        bool firstVisit = progress != null && !progress.HasSeen(StoryTable.EnterEvent("1-1"));
        return WeatherTable.For(chapter, slot, snowy, firstVisit);
    }

    /// <summary>한 밤이 지났다 — 판이 끝났거나 하룻밤 쉬었다.</summary>
    public static void PassNight(System.Random random = null)
    {
        random ??= new System.Random(System.Environment.TickCount);
        moon = MoonTable.Next(moon);
        slot = WeatherTable.Roll(random);
        snowy = random.Next(2) == 0;
        GameLogger.Log($"[Night] 다음 밤 — {MoonTable.Name(moon)} · 날씨 칸 {slot}");
    }

    public static void Capture(SaveData data)
    {
        data.moon = (int)moon;
        data.weather = (int)slot;
        data.snowy = snowy;
    }

    public static void Restore(SaveData data)
    {
        moon = (MoonPhase)Mathf.Clamp(data.moon, 0, MoonTable.Count - 1);
        slot = (WeatherSlot)Mathf.Clamp(data.weather, 0, (int)WeatherSlot.Bad2);
        snowy = data.snowy;
    }

    /// <summary>새 게임 — 삭 · 맑음.</summary>
    public static void Reset()
    {
        moon = MoonPhase.New;
        slot = WeatherSlot.Clear;
        snowy = false;
    }

    /// <summary>검증 도구 — 직접 정한다.</summary>
    public static void Set(MoonPhase m, WeatherSlot s, bool snow = false)
    {
        moon = m;
        slot = s;
        snowy = snow;
    }
}
