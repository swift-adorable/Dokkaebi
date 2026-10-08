using UnityEngine;

/// <summary>
/// 길달의 장부방 (결정 2-97) — 장 지도 사기 · 처치 기록. 세이브 판 19.
/// 처치는 장부방이 없어도 센다 — 장부방은 【보는 곳】이다.
/// </summary>
public static class LedgerManager
{
    private static KillRecord kills;

    public static KillRecord Kills => kills ??= new KillRecord();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => kills = null;

    /// <summary>장부방을 지어 놓았는가.</summary>
    public static bool Built => BuildingManager.IsOpen(BuildingTable.LedgerRoom);

    public static int Gold => PassiveManager.HasInstance ? PassiveManager.Instance.Gold : 0;

    /// <summary>내가 쓰러뜨린 적 하나 (EnemyController가 부른다). 저장은 판이 끝날 때 함께.</summary>
    public static void RecordKill(EnemyArchetype archetype, EnemyRarity rarity) => Kills.Add(archetype, rarity);

    public static LedgerError CanBuyMap(int chapter)
    {
        StoryProgress progress = StoryManager.Progress;
        return LedgerTable.CanBuyMap(Built, progress != null && progress.IsChapterOpen(chapter),
            MapMemory.Fog(chapter), Gold, chapter);
    }

    /// <summary>그 장의 지도를 산다 — 안개가 다 걷힌다.</summary>
    public static LedgerError BuyMap(int chapter)
    {
        LedgerError error = CanBuyMap(chapter);
        if (error != LedgerError.None)
            return error;

        PassiveManager.Instance.Gold -= LedgerTable.MapPrice(chapter);
        MapMemory.Fog(chapter).RevealAll();
        ExchangeWindowUI.RefreshIfOpen();
        SaveManager.Commit("장 지도 사기");
        return LedgerError.None;
    }

    // ── 세이브 ───────────────────────────────────────────────────────

    public static void Capture(SaveData data) => data.kills = Kills.Capture();

    public static void Restore(SaveData data) => Kills.Restore(data.kills);

    public static void Reset() => Kills.Reset();
}
