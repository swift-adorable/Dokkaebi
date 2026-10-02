using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이야기 진행을 들고 있다. (로드맵 3단계 · docs/Dokkaebi_Story.md)
///
/// MonoBehaviour가 아니다 — 진행은 세이브에 속한다 (BuildingManager와 같은 방식).
/// 씬마다 무엇을 보여 줄지는 StoryDirector가 정한다.
///
/// 【파밍 중에 바뀐 진행도 파밍 경계에서만 저장된다.】 보스를 쓰러뜨리고 죽으면
/// 사망 저장에 담기고, 파밍 도중 앱을 끄면 그 파밍의 진행은 없던 일이 된다.
/// </summary>
public static class StoryManager
{
    /// <summary>본문 사본의 Resources 경로. 「Dokkaebi/Story/이야기 본문 가져오기」가 만든다.</summary>
    public const string ScriptResource = "Story/StoryScript";

    private static StoryProgress progress;
    private static Dictionary<string, StoryPassage> passages;
    private static string targetZone;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        progress = null;
        passages = null;
        targetZone = null;
        OnChanged = null;
    }

    public static StoryProgress Progress => progress ??= new StoryProgress();

    /// <summary>진행이 바뀌었을 때 — 보스 · 조각 · 방 · 밤.</summary>
    public static event Action OnChanged;

    /// <summary>본문을 나눈 토막들. 처음 읽을 때 Resources의 사본을 나눈다.</summary>
    public static Dictionary<string, StoryPassage> Passages
    {
        get
        {
            if (passages != null)
                return passages;

            var asset = Resources.Load<TextAsset>(ScriptResource);

            if (asset == null)
                GameLogger.Warning("[Story] 이야기 본문이 없습니다. 「Dokkaebi/Story/이야기 본문 가져오기」를 실행하십시오.");

            passages = StoryScriptParser.Parse(asset != null ? asset.text : string.Empty);
            return passages;
        }
    }

    public static StoryPassage Passage(string id)
        => id != null && Passages.TryGetValue(id, out StoryPassage p) ? p : null;

    /// <summary>
    /// 이번 파밍에 들어갈 구역. 출발 화면이 정한다. 정하지 않았으면
    /// 아직 끝내지 않은 가장 이른 열린 구역이다.
    /// </summary>
    public static string TargetZone
    {
        get
        {
            if (!string.IsNullOrEmpty(targetZone) && Progress.IsZoneOpen(targetZone))
                return targetZone;

            List<ZoneDefinition> open = Progress.OpenZones();

            foreach (ZoneDefinition z in open)
                if (!Progress.IsCleared(z.Id))
                    return z.Id;

            return open.Count > 0 ? open[open.Count - 1].Id : "0-1";
        }
        set => targetZone = value;
    }

    // ── 진행 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 이야기 보스를 쓰러뜨렸다. 처음이면 그 뒤의 이야기를 보여 주고,
    /// 사신패가 있으면 마지막 쪽에 적는다.
    /// </summary>
    public static void ReportBossDefeated(string bossId)
    {
        BossDefinition boss = StoryTable.Boss(bossId);

        if (boss == null || !Progress.Defeat(bossId))
            return;

        GameLogger.Log($"[Story] 보스 — {boss.Name}");
        OnChanged?.Invoke();

        StoryPassage passage = Passage(StoryTable.BossEvent(bossId));
        var pages = passage != null ? new List<string>(passage.Pages) : new List<string>();

        if (!string.IsNullOrEmpty(boss.Tablet))
            pages.Add($"{Josa.EulReul(boss.Tablet)} 얻었다. ({Progress.Tablets.Count}/{StoryTable.TabletsForGate})");

        if (pages.Count > 0)
            StoryDialogueUI.Show(boss.Name, pages, () => Progress.See(StoryTable.BossEvent(bossId)));
    }

    public static void ReportPiece(string pieceId)
    {
        if (!Progress.CollectPiece(pieceId))
            return;

        OnChanged?.Invoke();

        StoryPassage passage = Passage(StoryTable.PieceEvent(pieceId));

        if (passage != null)
            StoryDialogueUI.Show(passage.Title, passage.Pages, () => Progress.See(passage.Id));
    }

    public static void ReportNotice(string noticeId)
    {
        NoticeDefinition notice = StoryTable.Notice(noticeId);

        if (notice == null || !Progress.CollectNotice(noticeId))
            return;

        PassiveManager.EnsureInstance().Gold += StoryTable.NoticeReward;
        OnChanged?.Invoke();

        StoryDialogueUI.Show("방(榜)", new List<string>
        {
            notice.Text,
            $"엽전 {StoryTable.NoticeReward}닢을 주웠다."
        }, null);
    }

    /// <summary>
    /// 한 토막을 아직 보지 않았으면 보여 준다. 다 보면(건너뛰어도) 본 것으로 적는다.
    /// 볼 것이 없으면 바로 끝난 것으로 친다.
    /// </summary>
    public static void PlayOnce(string eventId, Action done)
    {
        StoryPassage passage = Passage(eventId);

        if (passage == null || Progress.HasSeen(eventId))
        {
            if (passage == null && !string.IsNullOrEmpty(eventId))
                Progress.See(eventId);   // 본문에 없는 토막(이야기 구역의 빈칸)도 지나간 것으로 친다

            done?.Invoke();
            return;
        }

        StoryDialogueUI.Show(passage.Title, passage.Pages, () =>
        {
            Progress.See(eventId);
            OnChanged?.Invoke();
            done?.Invoke();
        });
    }

    /// <summary>밤을 보여 주고 지난 것으로 적는다. 그 밤의 상인이 소굴에 온다.</summary>
    public static void PlayNight(int night, Action done)
    {
        PlayOnce(StoryTable.NightEvent(night), () =>
        {
            if (Progress.PassNight(night))
            {
                foreach (MerchantDefinition m in StoryTable.Merchants)
                    if (m.Night == night)
                        GameLogger.Log($"[Story] 상인이 왔다 — {m.Name}");

                OnChanged?.Invoke();
            }

            done?.Invoke();
        });
    }

    /// <summary>
    /// 가게를 지어 줬다 — 그 가게를 부탁한 상인의 보상을 창고에 넣는다.
    /// 창고가 가득 차면 가방으로, 그래도 안 들어가면 로그만 남긴다.
    /// </summary>
    public static void GrantBuildingReward(string buildingId)
    {
        MerchantDefinition merchant = StoryTable.MerchantFor(buildingId);

        if (merchant == null || merchant.Rewards.Length == 0)
            return;

        ItemCatalog catalog = ItemCatalog.Load();
        PlayerInventory inventory = PlayerInventory.EnsureInstance();

        foreach ((string itemId, int count) in merchant.Rewards)
        {
            ItemDefinition definition = catalog != null ? catalog.Find(itemId) : null;

            if (definition == null)
            {
                GameLogger.Warning($"[Story] {merchant.Name}의 보상 「{itemId}」이 카탈로그에 없습니다.");
                continue;
            }

            int left = count - inventory.Stash.TryAdd(definition, count);

            if (left > 0)
                left -= inventory.Bag.TryAdd(definition, left);

            if (left > 0)
                GameLogger.Warning($"[Story] 창고 · 가방이 가득 차 「{definition.DisplayName}」 {left}개를 받지 못했습니다.");
        }

        StoryDialogueUI.ShowBanner($"{Josa.IGa(merchant.Name)} 고맙다며 보상을 건넸다. (창고)");
    }

    // ── 세이브 ───────────────────────────────────────────────────────

    public static SavedStory Capture() => Progress.Capture();

    public static void Restore(SavedStory saved)
    {
        Progress.Restore(saved);
        OnChanged?.Invoke();
    }
}
