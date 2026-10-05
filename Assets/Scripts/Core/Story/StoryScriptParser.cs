using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>이야기 한 토막 — 화면에 한 쪽씩 넘기며 보여 준다.</summary>
public sealed class StoryPassage
{
    public readonly string Id;
    public readonly string Title;
    public readonly List<string> Pages;

    public StoryPassage(string id, string title, List<string> pages)
    {
        Id = id;
        Title = title;
        Pages = pages;
    }
}

/// <summary>
/// 【이야기 본문을 읽어 토막으로 나눈다.】 (로드맵 3단계 · 결정 2-47)
///
/// 본문(docs/Dokkaebi_Story_Script.txt)이 유일한 원본이다. 게임에 대사를 따로
/// 옮겨 적지 않는다 — 옮겨 적으면 본문을 고칠 때마다 두 곳이 어긋난다.
///
/// 【나누는 규칙】 본문의 짜임을 그대로 쓴다.
///   · 가로줄(────)이 단락을 가른다. 「부록 1」부터는 읽지 않는다
///   · 「프롤로그」와 「도깨비 터와 고목 아래 소굴」의 첫 구역 앞까지 → prologue
///   · 「N장 — …」의 첫 구역 앞까지는 그 장 첫 구역의 앞머리가 된다
///   · 「1-1 · 장터 어귀」 같은 줄이 구역을 연다
///   · 구역 안에서 「싸움 끝에」 · 「싸움이 끝났」으로 시작하는 문단이 보스를 가른다 —
///     그 앞은 enter_구역, 그 뒤는 그 구역 보스 순서대로 boss_보스
///   · 「【기억의 조각 · 하나】」 문단은 구역 글에서 빼고 read_piece_n으로 둔다
///   · 「소굴 — 첫 번째 밤」 … 「소굴 — 마지막 밤」 → night_1 … night_5
///   · 「(UI 튜토리얼 / …)」 「(환경 연출 / …)」 「(세계관 도감 / …)」 같은 괄호 문단은
///     【연출 지시】다. 이야기 장면에 넣지 않는다 (결정 2-54 — 정보는 맞는 곳에서 전한다)
/// 문단 하나가 한 쪽이다.
/// </summary>
public static class StoryScriptParser
{
    private const string Separator = "────";
    private const string AppendixMark = "부록 1";

    private static readonly Regex ZoneHeader = new(@"^(\d)-(\d) · ");
    private static readonly Regex ChapterHeader = new(@"^(\d)장 — ");
    private static readonly Regex PieceHeader = new(@"^【기억의 조각 · (.+)】$");

    /// <summary>연출 지시 문단 — 이야기 장면에는 넣지 않는다.</summary>
    private static readonly Regex DirectionNote =
        new(@"^\((?:UI 튜토리얼|팝업|시스템 설명|환경 연출|세계관 도감|세계관 팝업|기억 연출)(?: / [^)]*)?\)");

    /// <summary>문단이 연출 지시(괄호 표시)인가.</summary>
    public static bool IsDirectionNote(string block)
        => !string.IsNullOrEmpty(block) && DirectionNote.IsMatch(block);

    private static readonly string[] Numerals = { "하나", "둘", "셋", "넷", "다섯", "여섯", "일곱", "여덟", "아홉" };
    private static readonly string[] NightNames = { "첫 번째", "두 번째", "세 번째", "네 번째", "마지막" };

    public static Dictionary<string, StoryPassage> Parse(string text)
    {
        var result = new Dictionary<string, StoryPassage>();

        if (string.IsNullOrEmpty(text))
            return result;

        List<List<string>> sections = SplitSections(text.Replace("\r\n", "\n"));

        var prologue = new List<string>();
        string prologueTitle = "프롤로그";
        List<string> pendingChapterIntro = null;

        // 첫 단락은 문서 머리(제목 · 작성일 · 구조 설명)다.
        for (int s = 1; s < sections.Count; s++)
        {
            List<string> blocks = sections[s];

            if (blocks.Count == 0)
                continue;

            string title = FirstLine(blocks[0]);

            if (title.StartsWith(AppendixMark))
                break;

            if (title.StartsWith("소굴 — "))
            {
                int night = NightNumber(title);

                if (night > 0)
                    result[StoryTable.NightEvent(night)] =
                        new StoryPassage(StoryTable.NightEvent(night), title, blocks.GetRange(1, blocks.Count - 1));

                continue;
            }

            bool isChapter = ChapterHeader.IsMatch(title);
            bool isOpening = !isChapter && (title.StartsWith("프롤로그") || title.StartsWith("도깨비 터"));

            // 단락 머리를 첫 쪽으로 둔다 — 장 제목 · 「도깨비 터와 고목 아래 소굴」
            var intro = new List<string>();
            int i = isOpening && title.StartsWith("프롤로그") ? 1 : 0;

            for (; i < blocks.Count && !ZoneHeader.IsMatch(FirstLine(blocks[i])); i++)
                intro.Add(blocks[i]);

            if (isOpening)
                prologue.AddRange(intro);
            else if (isChapter)
                pendingChapterIntro = intro;

            // 구역들
            while (i < blocks.Count)
            {
                string header = FirstLine(blocks[i]);
                Match m = ZoneHeader.Match(header);
                string zoneId = $"{m.Groups[1].Value}-{m.Groups[2].Value}";
                i++;

                var zoneBlocks = new List<string>();

                for (; i < blocks.Count && !ZoneHeader.IsMatch(FirstLine(blocks[i])); i++)
                    zoneBlocks.Add(blocks[i]);

                AddZone(result, zoneId, header, zoneBlocks, pendingChapterIntro);
                pendingChapterIntro = null;
            }
        }

        if (prologue.Count > 0)
            result[StoryTable.PrologueEvent] = new StoryPassage(StoryTable.PrologueEvent, prologueTitle, prologue);

        return result;
    }

    private static void AddZone(Dictionary<string, StoryPassage> result, string zoneId, string header,
        List<string> blocks, List<string> chapterIntro)
    {
        List<BossDefinition> bosses = StoryTable.BossesIn(zoneId);

        var segments = new List<List<string>> { new() };

        if (chapterIntro != null)
            segments[0].AddRange(chapterIntro);

        segments[0].Add(header);

        foreach (string block in blocks)
        {
            Match piece = PieceHeader.Match(FirstLine(block));

            if (piece.Success)
            {
                int number = System.Array.IndexOf(Numerals, piece.Groups[1].Value) + 1;
                string body = block.Contains("\n") ? block.Substring(block.IndexOf('\n') + 1) : string.Empty;
                string id = StoryTable.PieceEvent($"piece_{number}");

                result[id] = new StoryPassage(id, $"기억의 조각 · {piece.Groups[1].Value}", new List<string> { body });
                continue;
            }

            if (block.StartsWith("싸움 끝에") || block.StartsWith("싸움이 끝났"))
                segments.Add(new List<string>());

            segments[segments.Count - 1].Add(block);
        }

        string enter = StoryTable.EnterEvent(zoneId);
        result[enter] = new StoryPassage(enter, header, segments[0]);

        for (int k = 1; k < segments.Count && k - 1 < bosses.Count; k++)
        {
            string id = StoryTable.BossEvent(bosses[k - 1].Id);
            result[id] = new StoryPassage(id, bosses[k - 1].Name, segments[k]);
        }
    }

    private static List<List<string>> SplitSections(string text)
    {
        var sections = new List<List<string>> { new() };
        var current = new List<string>();

        void Flush()
        {
            if (current.Count > 0)
            {
                string block = string.Join("\n", current).Trim();

                if (!IsDirectionNote(block))
                    sections[sections.Count - 1].Add(block);
            }

            current.Clear();
        }

        foreach (string raw in text.Split('\n'))
        {
            string line = raw.TrimEnd();

            if (line.StartsWith(Separator))
            {
                Flush();
                sections.Add(new List<string>());
                continue;
            }

            if (line.Length == 0)
            {
                Flush();
                continue;
            }

            current.Add(line);
        }

        Flush();
        return sections;
    }

    private static string FirstLine(string block)
    {
        int n = block.IndexOf('\n');
        return n < 0 ? block : block.Substring(0, n);
    }

    private static int NightNumber(string title)
    {
        for (int i = 0; i < NightNames.Length; i++)
            if (title.Contains(NightNames[i]))
                return i + 1;

        return 0;
    }
}
