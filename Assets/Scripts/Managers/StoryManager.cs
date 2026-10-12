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
    private static List<LoreEntry> lore;
    private static string targetZone;

    /// <summary>창고 · 가방이 가득해 아직 못 건넨 상인 보상 (결정 2-101 · 세이브 판 21).</summary>
    private static readonly List<SavedItem> pendingRewards = new();

    /// <summary>가방에만 건네는 상인(참봉)이 맡아 둔 보상 (결정 2-104 · 판 22).</summary>
    private static readonly List<SavedItem> pendingBagRewards = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        progress = null;
        passages = null;
        lore = null;
        targetZone = null;
        OnChanged = null;
        pendingRewards.Clear();
        pendingBagRewards.Clear();
    }

    public static StoryProgress Progress => progress ??= new StoryProgress();

    /// <summary>퀘스트 진행 — 이야기 진행과 지은 건물에서 계산한다 (QuestTable · 결정 2-55 · 2-56).</summary>
    public static QuestTracker Quests => new QuestTracker(Progress, BuildingManager.IsOpen);

    /// <summary>진행이 바뀌었을 때 — 보스 · 조각 · 밤.</summary>
    public static event Action OnChanged;

    /// <summary>진행이 바뀌었다고 알린다 — 퀘스트 자리를 했을 때 (결정 2-108).</summary>
    public static void NotifyChanged() => OnChanged?.Invoke();

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

    /// <summary>본문의 세계관 도감 · 팝업 줄 (결정 2-106).</summary>
    public static IReadOnlyList<LoreEntry> Lore
    {
        get
        {
            if (lore != null)
                return lore;

            var asset = Resources.Load<TextAsset>(ScriptResource);
            lore = LoreTable.Parse(asset != null ? asset.text : string.Empty);
            return lore;
        }
    }

    /// <summary>
    /// 이야기 토막을 본 것으로 적고, 그 토막에 붙은 세계관 팝업이 있으면 한 번 띄운다 (결정 2-106).
    /// done은 팝업까지 닫힌 뒤에 부른다.
    /// </summary>
    private static void MarkSeen(string eventId, Action done = null)
    {
        Progress.See(eventId);
        ShowPopups(eventId, done);
    }

    private static void PlayMemories(string eventId, Action done)
    {
        foreach (LoreEntry entry in Lore)
        {
            if (entry.Kind == LoreKind.Memory && entry.UnlockEvent == eventId)
            {
                MemoryMontageUI.Play(LoreTable.MontageFrames(entry.Body), done);
                return;
            }
        }

        done?.Invoke();
    }

    private static void ShowPopups(string eventId, Action done)
    {
        foreach (LoreEntry entry in Lore)
        {
            if (entry.Kind != LoreKind.Popup || entry.UnlockEvent != eventId || Progress.HasSeen(entry.SeenEvent))
                continue;

            Progress.See(entry.SeenEvent);
            StoryDialogueUI.Show(entry.Title, new List<string> { entry.Body }, () => ShowPopups(eventId, done));
            return;
        }

        done?.Invoke();
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
            StoryDialogueUI.Show(boss.Name, pages, () => MarkSeen(StoryTable.BossEvent(bossId)));
    }

    public static void ReportPiece(string pieceId)
    {
        if (!Progress.CollectPiece(pieceId))
            return;

        OnChanged?.Invoke();

        StoryPassage passage = Passage(StoryTable.PieceEvent(pieceId));

        if (passage != null)
            StoryDialogueUI.Show(passage.Title, passage.Pages, () => MarkSeen(passage.Id));
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

        // 프롤로그는 뒤를 가린다 — 지금은 어두운 바탕, 나중에 프롤로그 그림 (결정 2-82).
        // 5-3 두루마리 · 넋 보내기도 뒤를 가린다 (결정 2-111).
        bool backdrop = eventId == StoryTable.PrologueEvent || eventId == StoryTable.EnterEvent("5-3")
                        || eventId == StoryTable.SendSoulEvent;

        StoryDialogueUI.Show(passage.Title, passage.Pages, () =>
        {
            MarkSeen(eventId, () =>
            {
                // 기억 연출 — 그 토막 안에 있던 「(기억 연출)」 글을 장면 조각으로 겹쳐 띄운다 (결정 2-111).
                PlayMemories(eventId, () =>
                {
                    OnChanged?.Invoke();
                    done?.Invoke();
                });
            });
        }, backdrop);
    }

    /// <summary>새 게임 — 이야기 진행 · 고른 구역을 비운다 (디버그 「세이브 지우기」).</summary>
    public static void ResetForNewGame()
    {
        progress = null;
        targetZone = null;
        pendingRewards.Clear();
        pendingBagRewards.Clear();
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
    /// 창고가 가득 차면 가방으로, 그래도 안 들어가면 【상인이 맡아 둔다】 (결정 2-101) —
    /// 벙커에서 아무 자리나 쓰면 다시 건넨다(DeliverPendingRewards). 세이브에 남는다.
    /// </summary>
    public static void GrantBuildingReward(string buildingId)
    {
        MerchantDefinition merchant = StoryTable.MerchantFor(buildingId);

        if (merchant == null || merchant.Rewards.Length == 0)
            return;

        bool kept = false;
        bool toBag = StoryTable.RewardsToBag(merchant.Id);

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

            int left;

            if (toBag)
            {
                // 참봉은 봇짐(가방)에 넣어 준다 — 본문 346 (결정 2-104).
                left = count - inventory.Bag.TryAdd(definition, count);
            }
            else
            {
                left = count - inventory.Stash.TryAdd(definition, count);

                if (left > 0)
                    left -= inventory.Bag.TryAdd(definition, left);
            }

            if (left > 0)
            {
                (toBag ? pendingBagRewards : pendingRewards).Add(new SavedItem { id = itemId, count = left });
                kept = true;
            }
        }

        if (toBag)
            inventory.RefreshCapacity();

        string place = toBag ? "가방" : "창고";
        StoryDialogueUI.ShowBanner(kept
            ? $"{Josa.IGa(merchant.Name)} 보상을 건네려다 — {place}가 가득하구나, 자리가 나면 주마."
            : $"{Josa.IGa(merchant.Name)} 고맙다며 보상을 건넸다. ({place})");
    }

    /// <summary>맡아 둔 보상이 있는가.</summary>
    public static bool HasPendingRewards => pendingRewards.Count > 0 || pendingBagRewards.Count > 0;

    /// <summary>
    /// 맡아 둔 상인 보상을 건넨다 (결정 2-101) — 벙커 자리를 쓸 때마다 부른다. 들어간 만큼만 빼고 나머지는 계속 맡는다.
    /// 건넨 것이 있으면 true.
    /// </summary>
    public static bool DeliverPendingRewards()
    {
        if (!HasPendingRewards || !SceneFlow.InBunker)
            return false;

        ItemCatalog catalog = ItemCatalog.Load();
        PlayerInventory inventory = PlayerInventory.EnsureInstance();

        bool anyStash = Deliver(pendingRewards, catalog, inventory, bagOnly: false);
        bool anyBag = Deliver(pendingBagRewards, catalog, inventory, bagOnly: true);
        bool any = anyStash || anyBag;

        if (any)
        {
            StoryDialogueUI.ShowBanner(!HasPendingRewards
                ? $"상인이 맡아 두었던 보상을 건넸다. ({(anyBag ? (anyStash ? "창고 · 가방" : "가방") : "창고")})"
                : "상인이 맡아 두었던 보상 일부를 건넸다 — 나머지는 자리가 나면.");
            inventory.RefreshCapacity();
            SaveManager.Commit("맡아 둔 보상");
        }

        return any;
    }

    /// <summary>맡아 둔 줄을 들어가는 만큼 건넨다. bagOnly면 가방에만 (참봉 · 결정 2-104).</summary>
    private static bool Deliver(List<SavedItem> rows, ItemCatalog catalog, PlayerInventory inventory, bool bagOnly)
    {
        bool any = false;

        for (int i = rows.Count - 1; i >= 0; i--)
        {
            SavedItem row = rows[i];
            ItemDefinition definition = catalog != null ? catalog.Find(row.id) : null;

            if (definition == null)
            {
                rows.RemoveAt(i);
                continue;
            }

            int given = bagOnly ? 0 : inventory.Stash.TryAdd(definition, row.count);
            if (given < row.count)
                given += inventory.Bag.TryAdd(definition, row.count - given);

            if (given <= 0)
                continue;

            any = true;
            row.count -= given;
            if (row.count <= 0)
                rows.RemoveAt(i);
        }

        return any;
    }

    // ── 세이브 ───────────────────────────────────────────────────────

    public static SavedStory Capture() => Progress.Capture();

    /// <summary>맡아 둔 상인 보상 (결정 2-101 · 판 21).</summary>
    public static List<SavedItem> CapturePendingRewards() => Copy(pendingRewards);

    /// <summary>가방에만 건넬 맡아 둔 보상 (결정 2-104 · 판 22).</summary>
    public static List<SavedItem> CapturePendingBagRewards() => Copy(pendingBagRewards);

    public static void RestorePendingRewards(List<SavedItem> saved) => Fill(pendingRewards, saved);

    public static void RestorePendingBagRewards(List<SavedItem> saved) => Fill(pendingBagRewards, saved);

    private static List<SavedItem> Copy(List<SavedItem> rows)
    {
        var list = new List<SavedItem>();
        foreach (SavedItem row in rows)
            list.Add(new SavedItem { id = row.id, count = row.count });
        return list;
    }

    private static void Fill(List<SavedItem> into, List<SavedItem> saved)
    {
        into.Clear();
        if (saved == null)
            return;

        foreach (SavedItem row in saved)
            if (row != null && !row.IsEmpty)
                into.Add(new SavedItem { id = row.id, count = row.count });
    }

    public static void Restore(SavedStory saved)
    {
        Progress.Restore(saved);
        OnChanged?.Invoke();
    }
}
