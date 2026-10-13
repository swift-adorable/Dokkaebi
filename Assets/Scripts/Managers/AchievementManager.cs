using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【업적】 (결정 2-118) — 1초마다 지금 상태로 판정하고, 새로 달성하면 화면 위에 한 줄 띄운다. 보상은 없다.
/// 보스 처치 수(StoryManager.ReportBossDefeated) · 번 엽전(PassiveManager.Gold가 늘 때 — 세이브 읽기는 빼고)은 여기 쌓는다.
/// </summary>
public class AchievementManager : MonoBehaviour
{
    private static AchievementState state;
    private static AchievementManager instance;
    private float next;

    public static AchievementState State => state ??= new AchievementState();

    public static void EnsureRunning()
    {
        if (instance != null)
            return;
        var go = new GameObject("AchievementManager");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AchievementManager>();
    }

    public static void RecordBossKill(string bossId) => State.AddBossKill(bossId);

    public static void RecordEarned(long amount) => State.AddEarned(amount);

    public static void Capture(SaveData data)
    {
        State.Capture(out data.achievements, out data.bossKillCounts, out data.earnedGold, out data.arrivedChapters);
    }

    public static void Restore(SaveData data)
    {
        State.Restore(data.achievements, data.bossKillCounts, data.earnedGold, data.arrivedChapters);
    }

    private void Update()
    {
        if (Time.unscaledTime < next)
            return;
        next = Time.unscaledTime + 1f;
        Check(announce: true);
    }

    /// <summary>지금 상태로 판정한다. 새로 달성한 것을 돌려준다.</summary>
    public static List<AchievementDefinition> Check(bool announce)
    {
        if (MapRuntime.Current != null)
            State.Arrive(MapRuntime.Current.Chapter);

        List<AchievementDefinition> fresh = State.Evaluate(BuildContext());
        if (announce)
            foreach (AchievementDefinition a in fresh)
                StoryDialogueUI.ShowBanner($"업적 — {a.Name}", 2.5f);
        return fresh;
    }

    private static AchievementContext BuildContext()
    {
        StoryProgress progress = StoryManager.Progress;
        return new AchievementContext
        {
            Arrived = State.HasArrived,
            Built = BuildingManager.IsOpen,
            BossKills = State.BossKills,
            Earned = State.Earned,
            Learned = id => PassiveManager.HasInstance && PassiveManager.Instance.State.IsLearned(id),
            WorkbenchStage = WorkbenchManager.State.Stage,
            CodexComplete = IsCodexComplete(progress),
            Ended = progress != null && progress.IsEnded
        };
    }

    /// <summary>세계관 도감 전부 + 기억의 조각 아홉 (사용자 2026-10-13).</summary>
    public static bool IsCodexComplete(StoryProgress progress)
    {
        if (progress == null || progress.PieceCount < StoryTable.Pieces.Count)
            return false;
        foreach (LoreEntry e in StoryManager.Lore)
            if (e.Kind == LoreKind.Codex && !LoreTable.IsUnlocked(e, progress))
                return false;
        return true;
    }
}
