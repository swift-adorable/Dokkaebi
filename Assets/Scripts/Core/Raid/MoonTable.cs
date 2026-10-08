/// <summary>달 여섯 (결정 2-60 · 2-61 · 2-62). 판마다 한 칸 돈다 — 삭 → 초승 → 상현 → 보름 → 하현 → 그믐 → 삭.</summary>
public enum MoonPhase
{
    /// <summary>삭 — 달 없는 밤. 옛 「그믐」 밤 상태의 자리.</summary>
    New = 0,
    WaxingCrescent = 1,
    FirstQuarter = 2,
    Full = 3,
    LastQuarter = 4,
    WaningCrescent = 5,
}

/// <summary>
/// 【달 표】 (결정 2-61 · 2-62 C안 · [임시값]) — 밝기(적 시야)와 양 끝만 나오는 것.
///
///   · 삭      ×0.45 · 무주귀가 더 나온다 · 순라귀가 멈춘다
///   · 초승 · 상현 ×0.7 · ×0.85 · 정기 +10% (차는 달)
///   · 보름    ×1.25(적이 멀리 본다) · 물가 수귀 ×2 · 잡귀 −20%
///   · 하현 · 그믐 ×0.85 · ×0.7 · 엽전 · 재료 +10% (기우는 달)
/// 추가 적은 장의 기본 적 풀에 있을 때만 (결정 2-63). MonoBehaviour 의존 없음 — EditMode 테스트 대상.
/// </summary>
public static class MoonTable
{
    public const int Count = 6;

    /// <summary>「더 나온다」 = 그 유형이 뽑힐 비중 배율 [임시값].</summary>
    public const float ExtraSpawnWeight = 2f;

    public const float GainScale = 1.1f;

    public static MoonPhase Next(MoonPhase moon) => (MoonPhase)(((int)moon + 1) % Count);

    public static string Name(MoonPhase moon) => moon switch
    {
        MoonPhase.New => "삭",
        MoonPhase.WaxingCrescent => "초승달",
        MoonPhase.FirstQuarter => "상현달",
        MoonPhase.Full => "보름달",
        MoonPhase.LastQuarter => "하현달",
        MoonPhase.WaningCrescent => "그믐달",
        _ => "?",
    };

    /// <summary>적 시야 배율 (밝기).</summary>
    public static float EnemyVisionScale(MoonPhase moon) => moon switch
    {
        MoonPhase.New => 0.45f,
        MoonPhase.WaxingCrescent => 0.7f,
        MoonPhase.FirstQuarter => 0.85f,
        MoonPhase.Full => 1.25f,
        MoonPhase.LastQuarter => 0.85f,
        MoonPhase.WaningCrescent => 0.7f,
        _ => 1f,
    };

    /// <summary>순라귀가 멈추는가 (삭).</summary>
    public static bool SentryAsleep(MoonPhase moon) => moon == MoonPhase.New;

    /// <summary>정기(경험치) 배율 — 차는 달.</summary>
    public static float XpScale(MoonPhase moon)
        => moon is MoonPhase.WaxingCrescent or MoonPhase.FirstQuarter ? GainScale : 1f;

    /// <summary>엽전 · 재료 배율 — 기우는 달.</summary>
    public static float LootScale(MoonPhase moon)
        => moon is MoonPhase.LastQuarter or MoonPhase.WaningCrescent ? GainScale : 1f;

    /// <summary>이 유형이 뽑힐 비중 배율. waterside = 물가 구역(보름 수귀).</summary>
    public static float SpawnWeight(MoonPhase moon, EnemyArchetype archetype, bool waterside)
    {
        switch (moon)
        {
            case MoonPhase.New:
                return archetype == EnemyArchetype.Wraith ? ExtraSpawnWeight : 1f;

            case MoonPhase.Full:
                if (archetype == EnemyArchetype.Lurker && waterside)
                    return ExtraSpawnWeight;
                return archetype == EnemyArchetype.Scav ? 0.8f : 1f;

            default:
                return 1f;
        }
    }

    /// <summary>출발 화면 · HUD에 적을 한 줄.</summary>
    public static string Describe(MoonPhase moon) => moon switch
    {
        MoonPhase.New => "적이 잘 못 본다 · 무주귀가 더 나온다 · 순라귀가 멈춘다",
        MoonPhase.WaxingCrescent => "적이 덜 본다 · 정기 +10%",
        MoonPhase.FirstQuarter => "적이 조금 덜 본다 · 정기 +10%",
        MoonPhase.Full => "적이 멀리 본다 · 물가에 수귀가 많다 · 잡귀가 적다",
        MoonPhase.LastQuarter => "적이 조금 덜 본다 · 엽전 · 재료 +10%",
        MoonPhase.WaningCrescent => "적이 덜 본다 · 엽전 · 재료 +10%",
        _ => string.Empty,
    };
}
