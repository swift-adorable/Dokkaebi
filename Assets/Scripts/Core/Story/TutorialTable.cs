using System.Collections.Generic;

/// <summary>튜토리얼 한 단계가 끝났는지 보는 조건 (결정 2-85).</summary>
public enum TutorialCheck
{
    BundleOpened,
    WeaponInHand,      // 가방 · 착용 어디든 무기가 있다 (봇짐에서 주웠다)
    WeaponEquipped,
    Kills,             // 이 단계 동안 쓰러뜨린 수 ≥ Count
    CorpseLooted,
    Arrived,           // 0-1 뒤 처음 소굴 — 「고목 아래」를 봤다 (철수든 쓰러짐이든)
    StashUsed,
    Entered02,
    GiftDropped,       // 0-2 큰 요괴를 쓰러뜨려 구슬이 떨어졌다
    CoreGemOwned,      // 핵심 구슬이 가방이나 소켓에
    CoreSocketed,
    Extracted02,
}

/// <summary>단계가 어디서 하는 일인가 — 안내에 「구역에서 · 소굴에서」를 붙인다.</summary>
public enum TutorialPlace { Raid, Bunker }

public readonly struct TutorialStep
{
    public readonly string Id;
    public readonly string Text;
    public readonly string Hint;
    public readonly TutorialCheck Check;
    public readonly int Count;
    /// <summary>이 체크가 이미 참이면 이 단계는 건너뛴다 — 예: 쓰러져 소굴에 와 버렸다(0-1 단계들).</summary>
    public readonly TutorialCheck? SkipWhen;
    public readonly TutorialPlace Place;

    public TutorialStep(string id, TutorialPlace place, string text, string hint, TutorialCheck check,
        int count = 1, TutorialCheck? skipWhen = null)
    {
        Id = id;
        Place = place;
        Text = text;
        Hint = hint;
        Check = check;
        Count = count;
        SkipWhen = skipWhen;
    }
}

/// <summary>지금 상태 — 런타임(TutorialDirector)이 모아 넘긴다. 순수 판정이 쓴다.</summary>
public struct TutorialFacts
{
    public bool BundleOpened, WeaponInHand, WeaponEquipped, CorpseLooted, Arrived, StashUsed,
        Entered02, GiftDropped, CoreGemOwned, CoreSocketed, Extracted02;

    /// <summary>지금 단계가 시작된 뒤 쓰러뜨린 수.</summary>
    public int KillsThisStep;

    public bool Has(TutorialCheck check, int count) => check switch
    {
        TutorialCheck.BundleOpened => BundleOpened,
        TutorialCheck.WeaponInHand => WeaponInHand || WeaponEquipped,
        TutorialCheck.WeaponEquipped => WeaponEquipped,
        TutorialCheck.Kills => KillsThisStep >= count,
        TutorialCheck.CorpseLooted => CorpseLooted,
        TutorialCheck.Arrived => Arrived,
        TutorialCheck.StashUsed => StashUsed,
        TutorialCheck.Entered02 => Entered02,
        TutorialCheck.GiftDropped => GiftDropped,
        TutorialCheck.CoreGemOwned => CoreGemOwned || CoreSocketed,
        TutorialCheck.CoreSocketed => CoreSocketed,
        TutorialCheck.Extracted02 => Extracted02,
        _ => false,
    };
}

/// <summary>
/// 【임시】 0장 튜토리얼 가이드 (결정 2-85) — 본문 0장의 (UI 튜토리얼 / …) 자리를 순서대로 한 줄 안내로.
/// 화면(튜토리얼 창 · 손가락 표시)은 레이어 · 아트 때. 끝난 단계는 이야기 진행(seen)에 「tut_…」로 적혀 세이브에 남는다.
///
///   0-1   봇짐 열기 → 무기 줍기 → 장착 → 잡귀 셋 → 시체 뒤지기 → 길목에서 철수(쓰러져도 소굴로 온다)
///   소굴  창고에 맡기기 → 0-2로 출발
///   0-2   큰 요괴 → 구슬 줍기 → 구슬 지니기 → 길목에서 철수
/// 다 끝나면 「tut_done」 — 그 뒤부터 주운 장비가 빈 자리에 바로 들어간다 (2-84).
/// </summary>
public static class TutorialTable
{
    public const string DoneEvent = "tut_done";
    public const string Extracted02Event = "tut_extracted_0-2";

    private static readonly TutorialStep[] steps =
    {
        new("tut_bundle", TutorialPlace.Raid, "길가의 젖은 봇짐을 열어라",
            "봇짐 가까이 가서 「젖은 봇짐」을 누른다", TutorialCheck.BundleOpened, skipWhen: TutorialCheck.Arrived),
        new("tut_take", TutorialPlace.Raid, "봇짐에서 환목궁을 주워라",
            "전리품 창 — 칸을 눌러 「줍기」, 또는 「전부 줍기」", TutorialCheck.WeaponInHand, skipWhen: TutorialCheck.Arrived),
        new("tut_equip", TutorialPlace.Raid, "환목궁을 무기 칸에 걸어라",
            "가방 화면(장비) → 환목궁 → 「장착」", TutorialCheck.WeaponEquipped, skipWhen: TutorialCheck.Arrived),
        new("tut_kill", TutorialPlace.Raid, "잡귀를 쓰러뜨려라",
            "조준해 쏜다 · 붉은 빛이 번뜩이면 대시로 피한다", TutorialCheck.Kills, 3, TutorialCheck.Arrived),
        new("tut_loot", TutorialPlace.Raid, "쓰러진 잡귀를 뒤져라",
            "시체 가까이 「파밍」 — 엽전 · 정기는 저절로 들어온다", TutorialCheck.CorpseLooted, skipWhen: TutorialCheck.Arrived),
        new("tut_extract1", TutorialPlace.Raid, "길목에서 고목 아래로 돌아가라",
            "화면 가장자리 화살표 → 초록 기둥의 원 안에서 5초", TutorialCheck.Arrived),
        new("tut_stash", TutorialPlace.Bunker, "아까운 것은 창고에 맡겨라",
            "창고 앞 「창고」 → 가방 물건 → 「창고에 넣기」 · 창고에 둔 것은 쓰러져도 남는다",
            TutorialCheck.StashUsed, skipWhen: TutorialCheck.Entered02),
        new("tut_depart", TutorialPlace.Bunker, "고목 뒤 수풀(0-2)로 나가라",
            "「파밍 출발」 → 0-2 고목 뒤 수풀", TutorialCheck.Entered02),
        new("tut_gift", TutorialPlace.Raid, "큰 요괴를 쓰러뜨려라",
            "잠시 뒤 큰 요괴가 나타난다", TutorialCheck.GiftDropped, skipWhen: TutorialCheck.Extracted02),
        new("tut_gem", TutorialPlace.Raid, "큰 요괴의 시체에서 구슬을 주워라",
            "시체 「파밍」 → 「핵심 구슬」 줍기", TutorialCheck.CoreGemOwned, skipWhen: TutorialCheck.Extracted02),
        new("tut_socket", TutorialPlace.Raid, "구슬을 몸에 지녀라",
            "가방 화면 「스킬」 → 구슬 → 「장착」 · 밝아진 핵심 칸을 누른다", TutorialCheck.CoreSocketed,
            skipWhen: TutorialCheck.Extracted02),
        new("tut_extract2", TutorialPlace.Raid, "길목에서 철수해 고목 아래로",
            "초록 기둥의 원 안에서 5초 — 철수해야 주운 것이 남는다", TutorialCheck.Extracted02),
    };

    public static IReadOnlyList<TutorialStep> Steps => steps;

    public static bool IsDone(StoryProgress p) => p.HasSeen(DoneEvent);

    /// <summary>
    /// 끝난 단계를 적고(건너뛸 단계도) 지금 단계의 번호를 돌려준다. 다 끝났으면 −1 · tut_done을 적는다.
    /// 한 번에 여러 단계가 끝날 수 있다(이미 해 둔 일). 새로 끝난 단계 수를 completed로 돌려준다.
    /// </summary>
    public static int Evaluate(StoryProgress p, in TutorialFacts facts, out int completed)
    {
        completed = 0;

        if (IsDone(p))
            return -1;

        for (int i = 0; i < steps.Length; i++)
        {
            TutorialStep step = steps[i];

            if (p.HasSeen(step.Id))
                continue;

            bool skip = step.SkipWhen.HasValue && facts.Has(step.SkipWhen.Value, 1);

            if (skip || facts.Has(step.Check, step.Count))
            {
                p.See(step.Id);
                if (!skip)
                    completed++;
                continue;
            }

            return i;
        }

        p.See(DoneEvent);
        return -1;
    }

    /// <summary>튜토리얼을 다 끝낸 것으로 (검증 패널 · 옛 세이브).</summary>
    public static void CompleteAll(StoryProgress p)
    {
        foreach (TutorialStep step in steps)
            p.See(step.Id);

        p.See(DoneEvent);
    }
}
