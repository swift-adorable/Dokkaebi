using System;
using System.Collections.Generic;

/// <summary>메인인가 서브인가. (결정 2-55 · 2-56 — 메인 2 · 서브 9)</summary>
public enum QuestKind
{
    Main = 0,
    Sub = 1
}

/// <summary>목표가 무엇으로 채워지는가. 전부 StoryProgress · 지은 건물에서 계산한다.</summary>
public enum QuestCondition
{
    /// <summary>이 이야기 사건을 봤다 (구역 들어가기 · 밤 · 보스 뒤 이야기).</summary>
    Seen = 0,

    /// <summary>이 보스를 쓰러뜨렸다.</summary>
    DefeatBoss = 1,

    /// <summary>이 구역을 끝냈다 (구역마다 끝내는 조건이 다르다 — ZoneClear).</summary>
    ClearZone = 2,

    /// <summary>이 건물을 지어 놓았다.</summary>
    Build = 3
}

/// <summary>퀘스트 목표 하나. MainQuest → Objective → UnlockZone (Progression 4절).</summary>
public sealed class QuestObjective
{
    public readonly string Text;
    public readonly QuestCondition Condition;

    /// <summary>사건 id · 보스 id · 구역 id · 건물 id — Condition에 따라.</summary>
    public readonly string Target;

    /// <summary>이 목표를 채우면 열리는 장. 0이면 없다. 실제로 여는 규칙은 StoryProgress에 있고, 둘이 같은지는 테스트가 본다.</summary>
    public readonly int UnlocksChapter;

    public QuestObjective(string text, QuestCondition condition, string target, int unlocksChapter = 0)
    {
        Text = text;
        Condition = condition;
        Target = target;
        UnlocksChapter = unlocksChapter;
    }
}

/// <summary>퀘스트 하나. 이름 · 문구는 본문의 「(퀘스트 …)」 문단과 같다.</summary>
public sealed class QuestDefinition
{
    public readonly string Id;
    public readonly QuestKind Kind;
    public readonly string Title;

    /// <summary>준 사람 — 영감 · 참봉 · 빚쟁이 · 길달 (상인 id).</summary>
    public readonly string GiverId;

    /// <summary>이 사건을 보면 퀘스트 창에 뜬다.</summary>
    public readonly string StartEvent;

    public readonly QuestObjective[] Objectives;

    /// <summary>보상 — 화면에 보이는 글. 실제 아이템은 상인 표(MerchantDefinition.Rewards)가 준다.</summary>
    public readonly string RewardText;

    public QuestDefinition(string id, QuestKind kind, string title, string giverId, string startEvent,
        QuestObjective[] objectives, string rewardText = "")
    {
        Id = id;
        Kind = kind;
        Title = title;
        GiverId = giverId;
        StartEvent = startEvent;
        Objectives = objectives ?? Array.Empty<QuestObjective>();
        RewardText = rewardText ?? string.Empty;
    }
}

/// <summary>
/// 【퀘스트 표 — Quest Data.】 메인 2 · 서브 9 (결정 2-55 · 2-56 · Story Lock 2-67).
/// 기준은 본문 docs/Dokkaebi_Story_Script.txt의 「(퀘스트 …)」 문단과 docs/Dokkaebi_Story.md 8-0절이다.
///
/// 【진행은 담지 않는다】 — 목표가 채워졌는지는 StoryProgress(보스 · 조각 · 본 사건 · 밤)와
/// 지은 건물에서 매번 계산한다(QuestTracker). 세이브에 퀘스트 칸을 따로 두지 않는다.
/// </summary>
public static class QuestTable
{
    public const string MainMyStory = "main_my_story";
    public const string MainLastStory = "main_last_story";

    // 결정 2-52의 건물 — 잡화 가게만 코드에 있다. 약탕간 · 대장간 · 장부방은 아직 없다 [코드 미반영].
    public const string Apothecary = "apothecary";
    public const string Smithy = "smithy";
    public const string LedgerRoom = "ledger_room";

    /// <summary>퀘스트가 가리키지만 아직 BuildingTable에 없는 건물 (결정 2-52 코드 반영 때 만든다).</summary>
    public static readonly string[] PlannedBuildings = { Apothecary, Smithy, LedgerRoom };

    private static QuestObjective See(string text, string eventId, int unlocks = 0)
        => new(text, QuestCondition.Seen, eventId, unlocks);

    private static QuestObjective Boss(string text, string bossId, int unlocks = 0)
        => new(text, QuestCondition.DefeatBoss, bossId, unlocks);

    private static QuestObjective Clear(string text, string zoneId, int unlocks = 0)
        => new(text, QuestCondition.ClearZone, zoneId, unlocks);

    private static QuestObjective Build(string text, string buildingId)
        => new(text, QuestCondition.Build, buildingId);

    private static string Enter(string zone) => StoryTable.EnterEvent(zone);
    private static string Night(int n) => StoryTable.NightEvent(n);

    private static readonly QuestDefinition[] quests =
    {
        // ── 메인 ─────────────────────────────────────────────────────
        new(MainMyStory, QuestKind.Main, "내 이야기를 찾는 길", StoryTable.Elder, Enter("0-2"), new[]
        {
            Boss("현무패 — 북쪽 젖은 장터길", "hyeonmu", 2),
            Boss("청룡패 — 동쪽 물레방아길", "cheongnyong", 3),
            Boss("주작패 — 남쪽 불씨길", "jujak", 4),
            Boss("백호패 — 서쪽 장승들길", "baekho", 5),
            See("돌문 열기 — 고목 뿌리 아래", Enter("5-1")),
            Clear("해태의 두루마리 확인 — 돌문 아래 길 끝, 궁궐 문", "5-3", 6)
        }, "사신패 넷 · 두루마리"),

        new(MainLastStory, QuestKind.Main, "마지막 이야기", StoryTable.Elder, Enter("5-3"), new[]
        {
            See("꽃밭으로 간다", Enter("6-1")),
            See("결계의 가장자리로 간다", Enter("6-2")),
            Boss("할머니의 넋을 보낸다", "gumiho")
        }),

        // ── 서브 — 상인 넷 (가게 세우기) ─────────────────────────────
        new("sub_elder_store", QuestKind.Sub, "영감의 잡화 가게", StoryTable.Elder, Enter("0-1"), new[]
        {
            Build("쇠붙이와 숯을 모아 와 고목 밑동의 좌판을 다시 세운다", BuildingTable.GeneralStore)
        }, "잡화 가게가 열린다 (잡화 · 음식 · 요리)"),

        new("sub_chambong_apothecary", QuestKind.Sub, "참봉의 약탕간", StoryTable.Chambong, Night(1), new[]
        {
            Build("쇠붙이 · 숯 · 새끼 뭉치를 모아 와 샘가에 약탕간을 세운다", Apothecary)
        }, "소환단 · 식혜 · 가공과 회복이 열린다"),

        new("sub_debtor_smithy", QuestKind.Sub, "빚쟁이의 대장간", StoryTable.Debtor, Night(1), new[]
        {
            Build("쇠붙이 · 숯 · 새끼 뭉치를 모아 와 대장간을 세운다", Smithy)
        }, "헌 지갑(가벼운 갑옷) · 무기와 방어구가 열린다"),

        new("sub_gildal_ledger", QuestKind.Sub, "길달의 장부방", StoryTable.Gildal, Night(2), new[]
        {
            Build("재료를 모아 와 장부방을 세운다", LedgerRoom)
        }, "화살 한 묶음 · 등록과 관리가 열린다"),

        // ── 서브 — 지역 사건 다섯 ────────────────────────────────────
        new("sub_bundle", QuestKind.Sub, "봇짐 되찾기", StoryTable.Elder, Enter("1-1"), new[]
        {
            Boss("봇짐을 훔쳐 간 시커먼 것을 쫓아 봇짐을 되찾는다", "yagwanggwi")
        }),

        new("sub_watermill", QuestKind.Sub, "멈춘 물레방아", StoryTable.Elder, Night(1), new[]
        {
            See("대숲 강가의 방앗간 늙은이에게 영감의 안부를 전한다", Enter("2-1")),
            Clear("언덕 너머에서 절구를 찾아온다", "2-2"),
            Boss("상류의 강철이를 쫓아 강물을 되돌린다", "gangcheori"),
            See("영감에게 물레방아 소식을 전한다", Night(2))
        }),

        new("sub_chongmyeongtang", QuestKind.Sub, "총명탕", StoryTable.Chambong, Night(2), new[]
        {
            Clear("약방골에서 총명탕 약재를 찾는다", "3-1"),
            Clear("불씨 골목의 꺼져 가는 아궁이에서 불씨를 모은다", "3-2"),
            See("달인 총명탕을 빚쟁이에게 건넨다", Night(3))
        }),

        // 「장승에 달라붙은 잡귀를 떼어 낸다」는 구역 목표다 — 구역 목표가 아직 없어 구역을 끝내면 채운다 [임시].
        new("sub_jangseung", QuestKind.Sub, "장승 지키기", StoryTable.Elder, Enter("4-1"), new[]
        {
            Clear("장승에 달라붙은 잡귀를 떼어 낸다", "4-1"),
            Boss("장승을 긁는 꺼먹살이를 막는다", "kkeomeoksari")
        }),

        // 「지방을 모아 화로에 태운다」도 구역 목표다 — 지금은 돌길(5-1)을 끝내면 채운다 [임시].
        new("sub_jibang", QuestKind.Sub, "지방 보내기", StoryTable.Elder, Enter("5-1"), new[]
        {
            Clear("돌길에 떠도는 지방을 모아 담장의 화로에 마저 태워 보낸다", "5-1")
        })
    };

    public static IReadOnlyList<QuestDefinition> Quests => quests;

    public static QuestDefinition Quest(string id)
    {
        foreach (QuestDefinition q in quests)
            if (q.Id == id)
                return q;

        return null;
    }

    public static IEnumerable<QuestDefinition> OfKind(QuestKind kind)
    {
        foreach (QuestDefinition q in quests)
            if (q.Kind == kind)
                yield return q;
    }
}
