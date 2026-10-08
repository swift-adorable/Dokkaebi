using System;
using System.Collections.Generic;

public enum LedgerError
{
    None = 0,
    /// <summary>장부방을 짓지 않았다.</summary>
    NoLedger = 1,
    /// <summary>아직 열리지 않은 장이다.</summary>
    ChapterClosed = 2,
    /// <summary>그 장에는 지도가 없다.</summary>
    NoMap = 3,
    /// <summary>이미 다 밝혀진 지도다.</summary>
    AlreadyRevealed = 4,
    NotEnoughGold = 5,
}

/// <summary>
/// 길달의 장부방 (결정 2-97) — 【장 지도 사기】와 【처치 기록】. 값은 전부 [임시].
///
///   · 장 지도 — 엽전으로 사면 그 장의 안개가 다 걷힌다 (사용자 결정 2026-10-08). 철수 지점은 여전히 찾아야 한다.
///   · 처치 기록 — 【내가 쓰러뜨린】 적만 센다(결정 2-93의 killedByPlayer). 많이 잡을수록 그 갈래를 더 안다.
///     1마리 이름 · 마릿수 / 5 요구하는 답 / 15 저항 / 30 체력 · 피해.
/// </summary>
public static class LedgerTable
{
    public const string MapsTab = "장 지도";
    public const string KillsTab = "처치 기록";
    public const string NoticesTab = "모은 방";

    /// <summary>장 지도 값 — 0장 100, n장 200 × n.</summary>
    public static int MapPrice(int chapter) => chapter <= 0 ? 100 : 200 * chapter;

    public static LedgerError CanBuyMap(bool built, bool chapterOpen, FogGrid fog, int gold, int chapter)
    {
        if (!built)
            return LedgerError.NoLedger;

        if (!chapterOpen)
            return LedgerError.ChapterClosed;

        if (fog == null)
            return LedgerError.NoMap;

        if (IsFullyRevealed(fog))
            return LedgerError.AlreadyRevealed;

        return gold < MapPrice(chapter) ? LedgerError.NotEnoughGold : LedgerError.None;
    }

    public static bool IsFullyRevealed(FogGrid fog) => fog != null && fog.RevealedCount >= fog.Width * fog.Height;

    public static string Explain(LedgerError error) => error switch
    {
        LedgerError.NoLedger => "장부방을 먼저 지어야 한다.",
        LedgerError.ChapterClosed => "아직 열리지 않은 장이다.",
        LedgerError.NoMap => "그 장에는 지도가 없다.",
        LedgerError.AlreadyRevealed => "이미 다 밝혀진 지도다.",
        LedgerError.NotEnoughGold => "엽전이 모자란다.",
        _ => string.Empty,
    };

    // ── 처치 기록 ────────────────────────────────────────────────────

    /// <summary>알게 되는 단계의 문턱 — 이름 · 답 · 저항 · 체력.</summary>
    public static readonly int[] KillThresholds = { 1, 5, 15, 30 };

    /// <summary>이만큼 잡았으면 몇 단계를 아는가 (0 ~ 4).</summary>
    public static int InfoLevel(int kills)
    {
        int level = 0;
        foreach (int t in KillThresholds)
            if (kills >= t) level++;
        return level;
    }

    /// <summary>다음 단계까지 남은 수. 다 알면 0.</summary>
    public static int ToNextLevel(int kills)
    {
        foreach (int t in KillThresholds)
            if (kills < t) return t - kills;
        return 0;
    }

    public static string AnswerName(EnemyAnswer answer) => answer switch
    {
        EnemyAnswer.Timing => "타이밍 — 예비동작을 보고 피한다",
        EnemyAnswer.Penetration => "방어 관통 — 장비로 방어를 뚫는다",
        EnemyAnswer.Angle => "각도 — 시야 바깥으로 돈다",
        EnemyAnswer.Initiative => "선제 — 먼저 찾아야 한다",
        EnemyAnswer.Distance => "거리 — 장판 밖으로",
        EnemyAnswer.Cover => "엄폐 — 사선을 끊는다",
        EnemyAnswer.Element => "속성 — 구슬로 속성을 바꾼다",
        EnemyAnswer.Dodge => "회피 — 탄을 피한다",
        _ => string.Empty,
    };

    public static string RarityName(EnemyRarity rarity) => rarity switch
    {
        EnemyRarity.Magic => "마법",
        EnemyRarity.Rare => "희귀",
        EnemyRarity.Unique => "고유",
        _ => "일반",
    };
}

/// <summary>갈래 × 등급별 처치 수 (결정 2-97). MonoBehaviour 의존이 없다 — EditMode 테스트 대상.</summary>
public sealed class KillRecord
{
    private readonly Dictionary<(EnemyArchetype, EnemyRarity), int> counts = new();

    public void Add(EnemyArchetype archetype, EnemyRarity rarity, int count = 1)
    {
        if (count <= 0)
            return;

        counts.TryGetValue((archetype, rarity), out int have);
        counts[(archetype, rarity)] = have + count;
    }

    public int Count(EnemyArchetype archetype, EnemyRarity rarity)
        => counts.TryGetValue((archetype, rarity), out int n) ? n : 0;

    public int Count(EnemyArchetype archetype)
    {
        int total = 0;
        foreach (KeyValuePair<(EnemyArchetype, EnemyRarity), int> pair in counts)
            if (pair.Key.Item1 == archetype) total += pair.Value;
        return total;
    }

    public int Total
    {
        get
        {
            int total = 0;
            foreach (int n in counts.Values) total += n;
            return total;
        }
    }

    public List<SavedKill> Capture()
    {
        var list = new List<SavedKill>();
        foreach (KeyValuePair<(EnemyArchetype, EnemyRarity), int> pair in counts)
            list.Add(new SavedKill { archetype = (int)pair.Key.Item1, rarity = (int)pair.Key.Item2, count = pair.Value });
        return list;
    }

    public void Restore(List<SavedKill> saved)
    {
        counts.Clear();

        if (saved == null)
            return;

        foreach (SavedKill k in saved)
        {
            if (k == null || k.count <= 0 || !Enum.IsDefined(typeof(EnemyArchetype), k.archetype)
                || !Enum.IsDefined(typeof(EnemyRarity), k.rarity))
                continue;

            Add((EnemyArchetype)k.archetype, (EnemyRarity)k.rarity, k.count);
        }
    }

    public void Reset() => counts.Clear();
}
