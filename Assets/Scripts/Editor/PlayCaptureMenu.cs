using UnityEditor;
using UnityEngine;

/// <summary>
/// 【개발용】 플레이 중 화면 확인 — 게임 화면을 Temp/screens/에 찍고, 전체 지도를 열고 닫는다.
/// 에디터를 직접 보지 못할 때(원격 작업) 화면을 확인하려고 둔다. Temp는 git에 들어가지 않는다.
/// </summary>
public static class PlayCaptureMenu
{
    private const string Folder = "Temp/screens";

    [MenuItem("Dokkaebi/Debug/게임 화면 찍기")]
    public static void Capture()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[Capture] 플레이 중에만 찍는다.");
            return;
        }

        System.IO.Directory.CreateDirectory(Folder);
        string path = $"{Folder}/game_{System.DateTime.Now:HHmmss}.png";
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[Capture] {path}");
    }

    /// <summary>
    /// 덮는 창(난이도 고르기 등 정렬 1500 이상)을 잠시 끄고 켠다 — 고르지 않은 채로 뒤 화면을 보려고.
    /// 아무것도 고르지 않으니 세이브는 바뀌지 않는다.
    /// </summary>
    [MenuItem("Dokkaebi/Debug/덮는 창 잠시 끄고 켜기")]
    public static void ToggleOverlays()
    {
        if (!EditorApplication.isPlaying)
            return;

        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            if (c.isRootCanvas && c.sortingOrder >= 1500)
                c.enabled = !c.enabled;
    }

    [MenuItem("Dokkaebi/Debug/전체 지도 열고 닫기")]
    public static void ToggleWorldMap()
    {
        if (EditorApplication.isPlaying)
            WorldMapUI.Toggle();
    }

    [MenuItem("Dokkaebi/Debug/전체 지도 한 칸 확대")]
    public static void ZoomWorldMap()
    {
        if (EditorApplication.isPlaying)
            WorldMapUI.DebugZoomIn();
    }

    [MenuItem("Dokkaebi/Debug/작업대 화면 열기")]
    public static void OpenWorkbench()
    {
        if (EditorApplication.isPlaying)
            WorkbenchUI.Open();
    }

    [MenuItem("Dokkaebi/Debug/약탕 화면 열기 (샘가 채움)")]
    public static void OpenApothecary()
    {
        if (!EditorApplication.isPlaying)
            return;

        // 샘가가 비었으면 다섯 밤어치를 모아 둔다 — 약탕간이 없으면 모이지 않으니 있는 것처럼 센다.
        if (ApothecaryManager.Springside.IsEmpty)
            for (int i = 0; i < 5; i++)
                ApothecaryManager.Springside.AccrueNight(true,
                    ApothecaryTable.ChapterPool(c => StoryManager.Progress.IsChapterOpen(c) || c <= 2), new System.Random(i));

        LedgerUI.Close();
        ApothecaryUI.OpenSpringside();
    }

    [MenuItem("Dokkaebi/Debug/장부방 화면 열기 (처치 기록)")]
    public static void OpenLedger()
    {
        if (!EditorApplication.isPlaying)
            return;

        if (LedgerManager.Kills.Total == 0)
        {
            LedgerManager.RecordKill(EnemyArchetype.Scav, EnemyRarity.Normal);
            for (int i = 0; i < 6; i++) LedgerManager.RecordKill(EnemyArchetype.Crusher, EnemyRarity.Normal);
            for (int i = 0; i < 31; i++) LedgerManager.RecordKill(EnemyArchetype.Dynamo, i % 5 == 0 ? EnemyRarity.Magic : EnemyRarity.Normal);
        }

        ApothecaryUI.Close();
        LedgerUI.OpenKills();
    }

    [MenuItem("Dokkaebi/Debug/출발 화면 열기")]
    public static void OpenDeparture()
    {
        if (EditorApplication.isPlaying)
            DepartureUI.Open();
    }

    /// <summary>날씨 칸을 한 칸 넘기고 지금 판에 바로 건다 (결정 2-91 · 검증용).</summary>
    [MenuItem("Dokkaebi/Debug/날씨 다음 칸")]
    public static void NextWeather()
    {
        WeatherSlot next = (WeatherSlot)(((int)NightClock.Slot + 1) % 5);
        NightClock.Set(NightClock.Moon, next, !NightClock.Snowy);
        ApplyNight();
    }

    /// <summary>달을 한 칸 돌리고 지금 판에 바로 건다 (검증용).</summary>
    [MenuItem("Dokkaebi/Debug/달 다음 칸")]
    public static void NextMoon()
    {
        NightClock.Set(MoonTable.Next(NightClock.Moon), NightClock.Slot, NightClock.Snowy);
        ApplyNight();
    }

    private static void ApplyNight()
    {
        if (EditorApplication.isPlaying && RaidManager.HasInstance)
            RaidManager.Instance.Reroll();

        Debug.Log($"[Debug] 달 {MoonTable.Name(NightClock.Moon)} · 날씨 칸 {NightClock.Slot} · 눈 {NightClock.Snowy}"
                  + (RaidManager.HasInstance ? $" — {string.Join(" · ", RaidManager.Current.Describe())}" : string.Empty));
    }

    /// <summary>
    /// 지도 패시브 셋(돌아갈 길목 · 회수 계약 · 옛 지도)을 이 판에만 켜고, 쓰러진 자리 하나를 옆에 세운다 (결정 2-93 · 검증용).
    /// 저장하지 않는다 — 판이 끝나며 저장되면 배운 것으로 남으니 확인 뒤 플레이를 멈춘다.
    /// </summary>
    [MenuItem("Dokkaebi/Debug/지도 패시브 셋 시험")]
    public static void TryMapPassives()
    {
        if (!EditorApplication.isPlaying || !PassiveManager.HasInstance)
            return;

        var ids = new System.Collections.Generic.List<string>(PassiveManager.Instance.State.LearnedIds)
        {
            "rec_mark", "rec_corpse", "reg_map"
        };
        PassiveManager.Instance.State.Restore(ids);

        if (ExtractionDirector.Instance != null)
            ExtractionDirector.Instance.RevealAll();

        MapRuntime runtime = MapRuntime.Current;
        if (runtime != null && runtime.Player != null)
        {
            var items = new System.Collections.Generic.List<SavedItem>
            {
                new SavedItem { id = "con_water", count = 2 },
                new SavedItem { id = "arm_guard_light", count = 1 },
            };
            FallenCache.OnDeath(true, runtime.Chapter, runtime.Player.position + new Vector3(10f, 0f, 10f), items);
            FallenStash.SpawnForRaid(runtime.Chapter);
        }

        Debug.Log("[Debug] 지도 패시브 셋 켬 — 길목 전부 · 쓰러진 자리 하나 · 옛 지도");
    }
}
