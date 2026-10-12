using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 퀘스트 자리를 세우고 거둔다 (결정 2-108). 씬마다 StoryDirector 옆에 붙는다.
///   · 장 맵 — 이 장의 열린 구역에 있는 자리 중, 지금 나타날 자리(QuestSpotTable.IsAvailable)
///   · 소굴 — 영감 · 빚쟁이에게 건넬 자리
/// 0.5초마다 다시 본다 — 보스를 쓰러뜨리거나 앞의 자리를 하면 다음 자리가 나타난다.
/// </summary>
public class QuestSpotDirector : MonoBehaviour
{
    private const float PollInterval = 0.5f;

    private readonly Dictionary<string, QuestSpot> live = new();
    private float timer;

    private static Transform player;

    /// <summary>지키는 자리가 플레이어와의 거리를 잰다.</summary>
    public static Transform Player
    {
        get
        {
            if (player == null)
            {
                var movement = FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Exclude);
                player = movement != null ? movement.transform : null;
            }
            return player;
        }
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f)
            return;
        timer = PollInterval;

        if (StoryDialogueUI.IsShowing)
            return;

        StoryProgress progress = StoryManager.Progress;
        QuestTracker tracker = StoryManager.Quests;

        foreach (QuestSpotDefinition spot in QuestSpotTable.Spots)
        {
            bool here = IsHere(spot, progress);
            bool want = here && QuestSpotTable.IsAvailable(spot, progress, tracker);
            live.TryGetValue(spot.Id, out QuestSpot existing);

            if (want && existing == null)
                live[spot.Id] = QuestSpot.Create(spot, new Vector3(spot.X, 0f, spot.Z));
            else if (!want && existing != null && spot.Kind != QuestSpotKind.Defend)
            {
                Destroy(existing.gameObject);
                live.Remove(spot.Id);
            }
        }
    }

    /// <summary>이 씬에 서는 자리인가 — 소굴이면 소굴 자리, 장 맵이면 이 장의 열린 구역 자리.</summary>
    private static bool IsHere(QuestSpotDefinition spot, StoryProgress progress)
    {
        if (SceneFlow.InBunker)
            return spot.ZoneId == QuestSpotDefinition.BunkerZone;

        if (spot.ZoneId == QuestSpotDefinition.BunkerZone || ZoneMap.Current == null)
            return false;

        ZoneDefinition zone = StoryTable.Zone(spot.ZoneId);
        ChapterMap map = ZoneMap.Current.Map;

        return zone != null && map != null && zone.Chapter == map.Chapter && progress.IsZoneOpen(spot.ZoneId);
    }
}
