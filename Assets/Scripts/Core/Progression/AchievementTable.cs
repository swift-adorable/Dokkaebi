using System;
using System.Collections.Generic;

/// <summary>업적 갈래 — Progression_System 9절 「업적 6분류」.</summary>
public enum AchievementKind
{
    Arrive,     // 도달 — 각 장 첫 도착
    Build,      // 건설 — 상인의 가게 넷
    BossKill,   // 처치 — 보스별 1회 · 10회
    Earn,       // 축적 — 지금까지 번 엽전의 합
    Passive,    // 해금 — 패시브 갈래 끝 노드 (역행 제외)
    Workbench,  // 해금 — 작업대 Ⅳ
    Codex,      // 해금 — 도감 완성 (세계관 도감 + 기억의 조각 9)
    Ending      // 엔딩 — 넋을 보낸다
}

public sealed class AchievementDefinition
{
    public readonly string Id;
    public readonly string Name;
    public readonly string Description;
    public readonly AchievementKind Kind;
    public readonly string Target;   // 가게 id · 보스 id · 패시브 id
    public readonly long Amount;     // 장 번호 · 처치 수 · 엽전 · 작업대 단계

    public AchievementDefinition(string id, string name, string description, AchievementKind kind, string target = null, long amount = 0)
    {
        Id = id;
        Name = name;
        Description = description;
        Kind = kind;
        Target = target;
        Amount = amount;
    }
}

/// <summary>판단에 쓰는 지금 상태 — 매니저가 채운다. 순수 판정을 테스트하려고 분리했다.</summary>
public sealed class AchievementContext
{
    public Func<int, bool> Arrived = _ => false;
    public Func<string, bool> Built = _ => false;
    public Func<string, int> BossKills = _ => 0;
    public long Earned;
    public Func<string, bool> Learned = _ => false;
    public int WorkbenchStage;
    public bool CodexComplete;
    public bool Ended;
}

/// <summary>
/// 【업적】 (결정 2-118 · Progression_System 9절) — 이름은 새로 짓지 않고 내용을 그대로 적는다. 보상 없이 표시만.
/// 축적은 지금까지 번 엽전의 합(써도 줄지 않음) · 패시브 정점은 갈래마다 끝 노드(역행 제외) · 도감 완성은 세계관 도감 + 기억의 조각 9 (사용자 2026-10-13).
/// </summary>
public static class AchievementTable
{
    /// <summary>패시브 갈래의 끝 노드 — 적응 · 대사 · 회수 · 중개 (역행 제외 — 사용자).</summary>
    public static readonly (string id, string name, string branch)[] PassiveEnds =
    {
        ("adapt_slots_3", "가방 정리 3", "적응"),
        ("meta_rare", "감정안", "대사"),
        ("rec_safe_2", "이중 속주머니", "회수"),
        ("brok_slots_2", "단골 2", "중개"),
    };

    public static readonly long[] EarnSteps = { 10_000, 100_000, 1_000_000 };

    public static readonly string[] Shops =
    {
        BuildingTable.GeneralStore, BuildingTable.Apothecary, BuildingTable.Smithy, BuildingTable.LedgerRoom
    };

    private static List<AchievementDefinition> all;

    public static IReadOnlyList<AchievementDefinition> All => all ??= Build();

    private static List<AchievementDefinition> Build()
    {
        var list = new List<AchievementDefinition>();

        for (int ch = 1; ch <= 6; ch++)
            list.Add(new AchievementDefinition($"arrive_{ch}", $"{ch}장 첫 도착", $"{ch}장에 처음 들어간다.", AchievementKind.Arrive, amount: ch));

        foreach (string shop in Shops)
        {
            string name = BuildingTable.Find(shop)?.Name ?? shop;
            list.Add(new AchievementDefinition($"build_{shop}", $"{name} 짓기", $"소굴에 {name}을(를) 세운다.", AchievementKind.Build, shop));
        }

        foreach (BossDefinition boss in StoryTable.Bosses)
        {
            list.Add(new AchievementDefinition($"boss_{boss.Id}_1", $"{boss.Name} 처치", $"{boss.Name}을(를) 쓰러뜨린다.", AchievementKind.BossKill, boss.Id, 1));
            list.Add(new AchievementDefinition($"boss_{boss.Id}_10", $"{boss.Name} 처치 10회", $"{boss.Name}을(를) 열 번 쓰러뜨린다.", AchievementKind.BossKill, boss.Id, 10));
        }

        foreach (long step in EarnSteps)
            list.Add(new AchievementDefinition($"earn_{step}", $"엽전 {step:N0} 벌기", $"지금까지 번 엽전이 모두 {step:N0}이 된다(써도 줄지 않는다).", AchievementKind.Earn, amount: step));

        foreach ((string id, string name, string branch) in PassiveEnds)
            list.Add(new AchievementDefinition($"passive_{id}", $"{name} 배우기", $"{branch} 갈래의 끝 패시브를 배운다.", AchievementKind.Passive, id));

        list.Add(new AchievementDefinition("workbench_4", "작업대 Ⅳ", "작업대를 Ⅳ단계로 올린다.", AchievementKind.Workbench, amount: 4));
        list.Add(new AchievementDefinition("codex_complete", "도감 완성", "세계관 도감과 기억의 조각 아홉을 모두 모은다.", AchievementKind.Codex));
        list.Add(new AchievementDefinition("ending", "넋을 보낸다", "엔딩을 본다.", AchievementKind.Ending));

        return list;
    }

    public static bool IsMet(AchievementDefinition a, AchievementContext c)
    {
        if (a == null || c == null)
            return false;

        return a.Kind switch
        {
            AchievementKind.Arrive => c.Arrived((int)a.Amount),
            AchievementKind.Build => c.Built(a.Target),
            AchievementKind.BossKill => c.BossKills(a.Target) >= a.Amount,
            AchievementKind.Earn => c.Earned >= a.Amount,
            AchievementKind.Passive => c.Learned(a.Target),
            AchievementKind.Workbench => c.WorkbenchStage >= a.Amount,
            AchievementKind.Codex => c.CodexComplete,
            AchievementKind.Ending => c.Ended,
            _ => false
        };
    }
}

[Serializable]
public class SavedCount
{
    public string id;
    public int count;
}

/// <summary>업적 진행 — 달성한 것 · 보스 처치 수 · 번 엽전 · 처음 들어간 장 (세이브 판 23).</summary>
public sealed class AchievementState
{
    private readonly HashSet<string> unlocked = new();
    private readonly Dictionary<string, int> bossKills = new();
    private readonly HashSet<int> arrived = new();

    public long Earned { get; private set; }

    public IReadOnlyCollection<string> Unlocked => unlocked;
    public bool IsUnlocked(string id) => id != null && unlocked.Contains(id);
    public int BossKills(string bossId) => bossId != null && bossKills.TryGetValue(bossId, out int n) ? n : 0;
    public bool HasArrived(int chapter) => arrived.Contains(chapter);

    public void AddBossKill(string bossId)
    {
        if (string.IsNullOrEmpty(bossId))
            return;
        bossKills[bossId] = BossKills(bossId) + 1;
    }

    public void AddEarned(long amount)
    {
        if (amount > 0)
            Earned += amount;
    }

    public bool Arrive(int chapter) => chapter >= 1 && arrived.Add(chapter);

    /// <summary>새로 달성한 것을 돌려준다(이미 한 것은 빼고).</summary>
    public List<AchievementDefinition> Evaluate(AchievementContext c)
    {
        var fresh = new List<AchievementDefinition>();
        foreach (AchievementDefinition a in AchievementTable.All)
            if (!unlocked.Contains(a.Id) && AchievementTable.IsMet(a, c))
            {
                unlocked.Add(a.Id);
                fresh.Add(a);
            }
        return fresh;
    }

    public void Capture(out List<string> unlockedIds, out List<SavedCount> kills, out long earned, out List<int> chapters)
    {
        unlockedIds = new List<string>(unlocked);
        kills = new List<SavedCount>();
        foreach (KeyValuePair<string, int> pair in bossKills)
            kills.Add(new SavedCount { id = pair.Key, count = pair.Value });
        earned = Earned;
        chapters = new List<int>(arrived);
    }

    public void Restore(List<string> unlockedIds, List<SavedCount> kills, long earned, List<int> chapters)
    {
        unlocked.Clear();
        bossKills.Clear();
        arrived.Clear();
        Earned = Math.Max(0, earned);

        if (unlockedIds != null)
            foreach (string id in unlockedIds)
                if (!string.IsNullOrEmpty(id)) unlocked.Add(id);
        if (kills != null)
            foreach (SavedCount k in kills)
                if (k != null && !string.IsNullOrEmpty(k.id) && k.count > 0) bossKills[k.id] = k.count;
        if (chapters != null)
            foreach (int ch in chapters)
                if (ch >= 1) arrived.Add(ch);
    }

    public void Reset() => Restore(null, null, 0, null);
}
