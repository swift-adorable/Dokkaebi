using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>도감 카드인가, 한 번 뜨는 설명 창인가.</summary>
public enum LoreKind
{
    Codex = 0,   // (세계관 도감 / …)
    Popup = 1,   // (세계관 팝업 / …)
    Tutorial = 2, // (UI 튜토리얼 / …) — 튜토리얼 안내가 본문 글을 그대로 쓴다 (결정 2-107)
    Memory = 3    // (기억 연출) — 5-3에서 아홉 조각의 장면이 겹친다 (결정 2-111)
}

/// <summary>본문 괄호 줄 하나 — 「(세계관 도감 / 넋불) 사람의 넋은 …」.</summary>
public sealed class LoreEntry
{
    public readonly LoreKind Kind;
    public readonly string Title;
    public readonly string Body;

    /// <summary>이 이야기 토막을 보면 열린다(도감) · 그 뒤에 뜬다(팝업).</summary>
    public readonly string UnlockEvent;

    public LoreEntry(LoreKind kind, string title, string body, string unlockEvent)
    {
        Kind = kind;
        Title = title;
        Body = body;
        UnlockEvent = unlockEvent;
    }

    /// <summary>팝업을 이미 보였는지 적는 사건 id.</summary>
    public string SeenEvent => $"lore_{Title}";
}

/// <summary>
/// 【세계관 도감 · 팝업】 (결정 2-106) — 본문의 「(세계관 도감 / …)」 7줄 · 「(세계관 팝업 / …)」 1줄을 그대로 읽는다.
/// 글을 게임에 옮겨 적지 않는다 — 본문이 유일한 원본이다(StoryScriptParser와 같다).
///
///   · 열리는 때 = 그 줄이 들어 있는 이야기 토막을 본 뒤 (프롤로그 · enter_구역 · boss_보스 · night_n · arrive_den)
///   · 「5-3 이후에 열린다」가 붙은 줄은 5-3을 본 뒤에 열린다 — 그 문장(지은이 메모)은 화면에 보이지 않는다
/// </summary>
public static class LoreTable
{
    private static readonly Regex Note = new(@"^\((세계관 도감|세계관 팝업|UI 튜토리얼) / ([^)]+)\)\s*(.+)$");
    private static readonly Regex MemoryNote = new(@"^\(기억 연출\)\s*(.+)$");
    private static readonly Regex OpensAfter = new(@"\s*(\d)-(\d) 이후에 열린다.*$");
    private static readonly Regex ZoneHeader = new(@"^(\d)-(\d) · ");
    private static readonly Regex ChapterHeader = new(@"^(\d)장 — ");

    public static List<LoreEntry> Parse(string text)
    {
        var result = new List<LoreEntry>();

        if (string.IsNullOrEmpty(text))
            return result;

        string current = StoryTable.PrologueEvent;
        string zone = null;
        int bossIndex = 0;
        var pending = new List<(LoreKind kind, string title, string body, string after)>();

        void Add(LoreKind kind, string title, string body, string after, string at)
            => result.Add(new LoreEntry(kind, title, body, after ?? at));

        foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            string line = raw.Trim();

            if (line.StartsWith("부록 1"))
                break;

            if (ChapterHeader.IsMatch(line))
            {
                current = null;   // 장 머리글 — 그 장 첫 구역에 들어갈 때 함께 본다
                continue;
            }

            Match zm = ZoneHeader.Match(line);
            if (zm.Success)
            {
                zone = $"{zm.Groups[1].Value}-{zm.Groups[2].Value}";
                bossIndex = 0;
                current = StoryTable.EnterEvent(zone);

                foreach (var p in pending)
                    Add(p.kind, p.title, p.body, p.after, current);
                pending.Clear();
                continue;
            }

            if (StoryScriptParser.IsArrivalHeader(line))
            {
                current = ChapterZeroTable.ArrivalEvent;
                continue;
            }

            if (line.StartsWith("소굴 — "))
            {
                int night = StoryScriptParser.NightNumber(line);
                if (night > 0)
                    current = StoryTable.NightEvent(night);
                continue;
            }

            if (zone != null && (line.StartsWith("싸움 끝에") || line.StartsWith("싸움이 끝났")))
            {
                List<BossDefinition> bosses = StoryTable.BossesIn(zone);
                if (bossIndex < bosses.Count)
                    current = StoryTable.BossEvent(bosses[bossIndex].Id);
                bossIndex++;
                continue;
            }

            Match mm = MemoryNote.Match(line);
            if (mm.Success)
            {
                if (current == null)
                    pending.Add((LoreKind.Memory, "기억", mm.Groups[1].Value.Trim(), null));
                else
                    Add(LoreKind.Memory, "기억", mm.Groups[1].Value.Trim(), null, current);
                continue;
            }

            Match m = Note.Match(line);
            if (!m.Success)
                continue;

            LoreKind kind = m.Groups[1].Value switch
            {
                "세계관 팝업" => LoreKind.Popup,
                "UI 튜토리얼" => LoreKind.Tutorial,
                _ => LoreKind.Codex
            };
            string title = m.Groups[2].Value.Trim();
            string body = m.Groups[3].Value.Trim();
            string after = null;

            Match om = OpensAfter.Match(body);
            if (om.Success)
            {
                after = StoryTable.EnterEvent($"{om.Groups[1].Value}-{om.Groups[2].Value}");
                body = body.Substring(0, om.Index).Trim();
            }

            if (current == null)
                pending.Add((kind, title, body, after));
            else
                Add(kind, title, body, after, current);
        }

        return result;
    }

    /// <summary>기억 연출 글을 장면 조각으로 나눈다 — 「 · 」 「 / 」 「 — 」로 끊는다.</summary>
    public static List<string> MontageFrames(string body)
    {
        var frames = new List<string>();
        if (string.IsNullOrEmpty(body))
            return frames;

        foreach (string part in Regex.Split(body, @"\s*(?:·|/|—)\s*"))
        {
            string t = part.Trim();
            if (t.Length > 0 && t != "이어서")
                frames.Add(t);
        }
        return frames;
    }

    /// <summary>그 갈래 · 이름의 본문 글. 없으면 빈 글.</summary>
    public static string BodyOf(IReadOnlyList<LoreEntry> lore, LoreKind kind, string title)
    {
        if (lore != null)
            foreach (LoreEntry e in lore)
                if (e.Kind == kind && e.Title == title)
                    return e.Body;
        return string.Empty;
    }

    /// <summary>열렸는가 — 그 토막을 봤다.</summary>
    public static bool IsUnlocked(LoreEntry entry, StoryProgress progress)
        => entry != null && progress != null && progress.HasSeen(entry.UnlockEvent);
}
