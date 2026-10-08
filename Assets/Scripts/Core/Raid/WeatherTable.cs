using UnityEngine;

/// <summary>날씨 칸 (결정 2-60 — 덕코프 그대로: 맑음 · 흐림 · 비/눈 · 궂은 날 Ⅰ · Ⅱ).</summary>
public enum WeatherSlot
{
    Clear = 0,
    Cloudy = 1,
    Precipitation = 2,
    Bad1 = 3,
    Bad2 = 4,
}

/// <summary>궂은 날이 무엇으로 아프게 하는가 — 계절마다 (결정 2-60).</summary>
public enum WeatherHazard
{
    None = 0,
    /// <summary>겨울 한파 — 추위가 쌓인다 · 막이 = 방한.</summary>
    Cold = 1,
    /// <summary>봄 흙비 — 몇 초마다 피해 · 막이 = 막이.</summary>
    Dust = 2,
    /// <summary>여름 폭염 · 독안개 — 수분이 빨리 준다 · 막이 = 서늘함.</summary>
    Heat = 3,
    /// <summary>가을 짙은 안개 — 내 시야가 준다 · 막이 = 밝히기.</summary>
    Fog = 4,
}

/// <summary>막이 넷 (결정 2-63 — 기능만 확정, 이름 · 그림은 Naming · 아트 때).</summary>
public enum ProtectionKind
{
    None = 0,
    Warmth = 1,
    Shield = 2,
    Cool = 3,
    Light = 4,
}

/// <summary>
/// 이번 판의 날씨 — 장의 계절 + 날씨 칸 (결정 2-59 · 2-60 · 2-61 · 2-64).
/// 수치는 추천안 v3 [임시값 — 결정 2-61] (docs/research/Dokkaebi_날씨_제안_2026-10-06.md).
/// MonoBehaviour 의존 없음 — EditMode 테스트 대상.
/// </summary>
public readonly struct RaidWeather
{
    public readonly int Chapter;
    public readonly Season Season;
    public readonly WeatherSlot Slot;

    /// <summary>겨울의 비 · 눈 칸이 눈인가 (아니면 비).</summary>
    public readonly bool Snow;

    public RaidWeather(int chapter, Season season, WeatherSlot slot, bool snow)
    {
        Chapter = chapter;
        Season = season;
        Slot = slot;
        Snow = snow;
    }

    public static RaidWeather Calm => new(0, Season.Spring, WeatherSlot.Clear, false);

    /// <summary>궂은 날 단계 (Ⅰ = 1 · Ⅱ = 2). 궂은 날이 아니면 0.</summary>
    public int Severity => Slot == WeatherSlot.Bad1 ? 1 : Slot == WeatherSlot.Bad2 ? 2 : 0;

    /// <summary>6장 꽃비 — 화면만, 피해 없음 (결정 2-64).</summary>
    public bool IsFlowerRain => Severity > 0 && Chapter == 6;

    public WeatherHazard Hazard
    {
        get
        {
            if (Severity == 0 || IsFlowerRain)
                return WeatherHazard.None;

            return Season switch
            {
                Season.Winter => WeatherHazard.Cold,
                Season.Spring => WeatherHazard.Dust,
                Season.Summer => WeatherHazard.Heat,
                Season.Autumn => WeatherHazard.Fog,
                _ => WeatherHazard.None,
            };
        }
    }

    /// <summary>이 계절의 막이.</summary>
    public ProtectionKind Protection => WeatherTable.ProtectionFor(Season);

    /// <summary>여름 비 칸 = 장마비 (큰물을 합쳤다 — 결정 2-64): 이동 ×0.75 · 감전 2배.</summary>
    public bool IsMonsoon => Slot == WeatherSlot.Precipitation && Season == Season.Summer;

    /// <summary>여름 궂은 날 Ⅱ = 독안개 (결정 2-64): 폭염 Ⅱ 수치 + 왕지네가 더 나오고 강해진다.</summary>
    public bool IsMiasma => Slot == WeatherSlot.Bad2 && Season == Season.Summer;

    /// <summary>막이가 모자란 단계 (0이면 아프지 않다).</summary>
    public int Deficit(int protectionLevel)
        => Hazard == WeatherHazard.None ? 0 : Mathf.Max(0, Severity - Mathf.Max(0, protectionLevel));

    public string Name
    {
        get
        {
            ChapterData c = ZoneDataTable.Chapter(Chapter);

            return Slot switch
            {
                WeatherSlot.Clear => "맑음",
                WeatherSlot.Cloudy => "흐림",
                WeatherSlot.Precipitation => IsMonsoon ? "장마비" : Snow ? "눈" : "비",
                WeatherSlot.Bad1 => c != null && !string.IsNullOrEmpty(c.BadWeather1) ? c.BadWeather1 : "궂은 날 Ⅰ",
                WeatherSlot.Bad2 => c != null && !string.IsNullOrEmpty(c.BadWeather2) ? c.BadWeather2 : "궂은 날 Ⅱ",
                _ => "?",
            };
        }
    }

    /// <summary>출발 화면 · HUD에 적을 한 줄. 비어 있으면 아무 일도 없다.</summary>
    public string Describe()
    {
        if (IsMonsoon)
            return "모두 느려진다 · 감전 피해 2배";

        if (IsFlowerRain)
            return "꽃잎이 흩날린다 (해는 없다)";

        string protection = WeatherTable.ProtectionName(Protection);

        return Hazard switch
        {
            WeatherHazard.Cold => $"추위가 쌓인다 — {protection} {Severity}이면 괜찮다",
            WeatherHazard.Dust => $"흙비에 다친다 — {protection} {Severity}이면 괜찮다",
            WeatherHazard.Heat => IsMiasma
                ? $"수분이 크게 준다 · 왕지네가 몰려 있다 — {protection} {Severity}"
                : $"수분이 빨리 준다 — {protection} {Severity}이면 괜찮다",
            WeatherHazard.Fog => $"앞이 잘 안 보인다 · 적도 늦게 알아챈다 — {protection} {Severity}",
            _ => string.Empty,
        };
    }
}

/// <summary>
/// 【날씨 표】 (결정 2-59 ~ 2-64 · [임시값]) — 확률 · 궂은 날 피해 · 막이.
/// </summary>
public static class WeatherTable
{
    // ── 확률 (결정 2-61 ②) ───────────────────────────────────────────
    public const int ClearWeight = 40;
    public const int CloudyWeight = 25;
    public const int PrecipitationWeight = 20;
    public const int Bad1Weight = 10;
    public const int Bad2Weight = 5;

    // ── 한파 — 덕코프 추위 중첩 (결정 2-61 ②) ───────────────────────
    /// <summary>방한이 1단계 모자라면 15분에 100중첩.</summary>
    public const float ColdStacksPerDeficitPerSecond = 100f / (15f * 60f);
    public const int ColdMaxStacks = 100;
    public const float ColdMovePerStack = 0.0025f;
    public const float ColdFireIntervalPerStack = 0.003f;
    public const float ColdEnergyDrainPerStack = 0.013f;
    /// <summary>100중첩(동상)이면 이만큼마다 피해 1.</summary>
    public const float FrostbiteInterval = 1.5f;
    /// <summary>화로 · 모닥불 곁에서 녹는 속도 (초당 중첩).</summary>
    public const float WarmthMeltPerSecond = 5f;

    /// <summary>
    /// 추위 중첩 한 걸음. 한파에 방한이 모자라면 쌓이고, 불 곁이거나 쌓이지 않는 동안에는 녹는다.
    /// </summary>
    public static float ColdStep(float stacks, WeatherHazard hazard, int deficit, bool nearWarmth, float dt)
    {
        bool freezing = hazard == WeatherHazard.Cold && deficit > 0;

        if (freezing)
            stacks += deficit * ColdStacksPerDeficitPerSecond * dt;

        if (nearWarmth || !freezing)
            stacks -= WarmthMeltPerSecond * dt;

        return Mathf.Clamp(stacks, 0f, ColdMaxStacks);
    }

    // ── 흙비 ─────────────────────────────────────────────────────────
    public static float DustInterval(int deficit) => deficit switch
    {
        1 => 18f,
        >= 2 => 2.5f,
        _ => float.PositiveInfinity,
    };

    // ── 폭염 · 독안개 — 수분 소모 배율 ──────────────────────────────
    public static float WaterDrainScale(int deficit) => deficit switch
    {
        1 => 1.6f,
        >= 2 => 2.4f,
        _ => 1f,
    };

    // ── 짙은 안개 — 내 시야 (적 반응은 안개가 있으면 ×1.5 — 양날) ───────
    /// <summary>안개가 없을 때의 「내 시야」 반경 (m) — 덕코프 기본 시야 18m.</summary>
    public const float BaseSight = 18f;
    public const float FogEnemyReactionScale = 1.5f;

    public static float PlayerSightScale(int deficit) => deficit switch
    {
        1 => 0.7f,
        >= 2 => 0.45f,
        _ => 1f,
    };

    // ── 장마비 · 독안개 ──────────────────────────────────────────────
    public const float MonsoonMoveScale = 0.75f;
    public const float MonsoonLightningScale = 2f;
    public const float MiasmaEmpowerScale = 1.3f;

    public static ProtectionKind ProtectionFor(Season season) => season switch
    {
        Season.Winter => ProtectionKind.Warmth,
        Season.Spring => ProtectionKind.Shield,
        Season.Summer => ProtectionKind.Cool,
        Season.Autumn => ProtectionKind.Light,
        _ => ProtectionKind.None,
    };

    public static string ProtectionName(ProtectionKind kind) => kind switch
    {
        ProtectionKind.Warmth => "방한",
        ProtectionKind.Shield => "막이",
        ProtectionKind.Cool => "서늘함",
        ProtectionKind.Light => "밝히기",
        _ => string.Empty,
    };

    public static string SeasonName(Season season) => season switch
    {
        Season.Winter => "겨울",
        Season.Spring => "봄",
        Season.Summer => "여름",
        Season.Autumn => "가을",
        _ => string.Empty,
    };

    /// <summary>날씨 칸을 뽑는다 (확률 40 · 25 · 20 · 10 · 5).</summary>
    public static WeatherSlot Roll(System.Random random)
    {
        int total = ClearWeight + CloudyWeight + PrecipitationWeight + Bad1Weight + Bad2Weight;
        int roll = random.Next(total);

        if ((roll -= ClearWeight) < 0) return WeatherSlot.Clear;
        if ((roll -= CloudyWeight) < 0) return WeatherSlot.Cloudy;
        if ((roll -= PrecipitationWeight) < 0) return WeatherSlot.Precipitation;
        if ((roll -= Bad1Weight) < 0) return WeatherSlot.Bad1;
        return WeatherSlot.Bad2;
    }

    /// <summary>
    /// 그 장의 이번 날씨. 0장(튜토리얼)은 늘 맑음, 1장 첫 방문은 비 (결정 2-63).
    /// 날씨 칸은 장과 무관하게 하나를 뽑아 두고 장의 계절로 읽는다.
    /// </summary>
    public static RaidWeather For(int chapter, WeatherSlot slot, bool snow, bool firstVisit)
    {
        ChapterData c = ZoneDataTable.Chapter(chapter);
        Season season = c != null ? c.Season : Season.Spring;

        if (chapter <= 0)
            return new RaidWeather(chapter, season, WeatherSlot.Clear, false);

        if (chapter == 1 && firstVisit)
            return new RaidWeather(chapter, season, WeatherSlot.Precipitation, false);

        return new RaidWeather(chapter, season, slot, snow && season == Season.Winter);
    }
}
