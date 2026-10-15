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
    /// <summary>결정 2-128 — 새 게임부터 엔딩까지 이야기 흐름을 자동으로 밟는다(따로 저장 · Logs/playthrough).</summary>
    [MenuItem("Dokkaebi/Debug/한 판 끝까지 자동 진행 (따로 저장)")]
    public static void RunWholeGame()
    {
        if (EditorApplication.isPlaying)
            StoryRunner.Begin();
    }

    /// <summary>결정 2-130 — 주인공 그림 앞을 가리는 렌더러를 콘솔에 적는다(카메라 → 그림 가운데 선과 겹치는 것).</summary>
    [MenuItem("Dokkaebi/Debug/주인공 앞을 가리는 것 찾기")]
    public static void FindOccluders()
    {
        if (!EditorApplication.isPlaying)
            return;
        var sprite = Object.FindAnyObjectByType<CharacterSprite>();
        Camera cam = Camera.main;
        if (sprite == null || sprite.CardRenderer == null || cam == null)
        {
            Debug.LogWarning("[Occluder] 주인공 그림 또는 카메라가 없다.");
            return;
        }
        Vector3 target = sprite.CardRenderer.bounds.center;
        var ray = new Ray(target - cam.transform.forward * 200f, cam.transform.forward);
        float targetDist = Vector3.Dot(target - ray.origin, ray.direction);
        var sb = new System.Text.StringBuilder($"[Occluder] 그림 {target:F1} · 카메라 {cam.transform.position:F1}\n");
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!r.enabled || r == sprite.CardRenderer || r.forceRenderingOff)
                continue;
            if (r.bounds.IntersectRay(ray, out float d) && d < targetDist - 0.05f)
                sb.AppendLine($"  {d:F1}m · {r.GetType().Name} · {Path(r.transform)} · 재질 {(r.sharedMaterial != null ? r.sharedMaterial.name + " / " + r.sharedMaterial.shader.name + " q" + r.sharedMaterial.renderQueue : "없음")} · 크기 {r.bounds.size:F1}");
        }
        Debug.Log(sb.ToString());
    }

    private static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;

    [MenuItem("Dokkaebi/Debug/한 판 자동 진행 멈추기")]
    public static void StopWholeGame() => StoryRunner.Stop();

    /// <summary>결정 2-125 — 10초 동안 프레임 · GC · 배치를 재서 콘솔에 한 덩어리로 남긴다 (PerfProbe).</summary>
    [MenuItem("Dokkaebi/Debug/성능 재기 (10초)")]
    public static void MeasurePerf()
    {
        if (EditorApplication.isPlaying)
            PerfProbe.Begin(10f, GameViewSizeMenu.Tag);
    }

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

    [MenuItem("Dokkaebi/Debug/전체 지도 끝까지 축소")]
    public static void ZoomOutWorldMap()
    {
        if (EditorApplication.isPlaying)
            WorldMapUI.DebugZoomOut();
    }

    [MenuItem("Dokkaebi/Debug/전체 지도 한 칸 확대")]
    public static void ZoomWorldMap()
    {
        if (EditorApplication.isPlaying)
            WorldMapUI.DebugZoomIn();
    }

    /// <summary>가게 · 시설 화면을 모두 닫는다 — 차례로 열어 찍을 때 (결정 2-123).</summary>
    [MenuItem("Dokkaebi/Debug/가게 · 시설 화면 모두 닫기")]
    public static void CloseStations()
    {
        if (!EditorApplication.isPlaying)
            return;

        ShopScreenUI.Close();
        StashScreenUI.Close();
        BuildingScreenUI.Close();
        ApothecaryUI.Close();
        LedgerUI.Close();

        foreach (CookingUI c in Object.FindObjectsByType<CookingUI>(FindObjectsInactive.Include))
            Object.Destroy(c.gameObject);
        foreach (WorkbenchUI w in Object.FindObjectsByType<WorkbenchUI>(FindObjectsInactive.Include))
            Object.Destroy(w.gameObject);
        foreach (DepartureUI d in Object.FindObjectsByType<DepartureUI>(FindObjectsInactive.Include))
            Object.Destroy(d.gameObject);
    }

    [MenuItem("Dokkaebi/Debug/부뚜막 화면 열기")]
    public static void OpenCooking()
    {
        if (EditorApplication.isPlaying)
            CookingUI.Open();
    }

    [MenuItem("Dokkaebi/Debug/건설 화면 열기")]
    public static void OpenBuilding()
    {
        if (EditorApplication.isPlaying)
            BuildingScreenUI.Open();
    }

    [MenuItem("Dokkaebi/Debug/창고 화면 열기")]
    public static void OpenStash()
    {
        if (EditorApplication.isPlaying)
            StashScreenUI.Open();
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

    [MenuItem("Dokkaebi/Debug/장부방 화면 열기 (가게 재고)")]
    public static void OpenLedgerShops()
    {
        if (!EditorApplication.isPlaying)
            return;

        ApothecaryUI.Close();
        LedgerUI.OpenShops();
    }

    [MenuItem("Dokkaebi/Debug/장부방 화면 열기 (기억의 조각)")]
    public static void OpenLedgerWritings()
    {
        if (!EditorApplication.isPlaying)
            return;

        ApothecaryUI.Close();
        LedgerUI.OpenWritings();
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
    /// <summary>보스 위험 표시 셋(원 · 고리 · 줄)을 플레이어 곁에 4초 띄운다 — 화면으로 확인하려고 (결정 2-114).</summary>
    [MenuItem("Dokkaebi/Debug/보스 위험 표시 보이기")]
    public static void ShowTelegraphs()
    {
        if (!EditorApplication.isPlaying)
            return;

        GameObject player = GameObject.FindWithTag("Player");
        Vector3 at = player != null ? player.transform.position : Vector3.zero;
        BossTelegraph.Circle(at + new Vector3(4f, 0f, 3f), 2.5f, new Color(0.95f, 0.3f, 0.3f), 4f).PreviewAt(0.5f);
        BossTelegraph.Ring(at + new Vector3(-7f, 0f, 2f), 2.5f, 6f, new Color(1f, 0.45f, 0.15f), 4f).PreviewAt(0.5f);
        BossTelegraph.Line(at + new Vector3(-6f, 0f, -5f), Vector3.right, 13.6f, 3.2f, new Color(0.95f, 0.3f, 0.3f), 4f).PreviewAt(0.5f);
        Debug.Log("[Debug] 보스 위험 표시 셋 — 4초");
    }

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

    /// <summary>
    /// 【검증】 대장간을 열고 몇 가지를 담는다 (결정 2-120 — 탭 · 값 · 담음 · 합계 · 이유 줄을 한 화면에서 보려고).
    /// 엽전은 바꾸지 않는다 — 모자라면 모자란 대로 붉게 보여야 한다.
    /// </summary>
    [MenuItem("Dokkaebi/Debug/가게 담아 두기 시험 (대장간)")]
    public static void TryShopCart()
    {
        if (!EditorApplication.isPlaying)
            return;

        // 여는 것은 한 프레임 뒤(흐린 뒤 화면을 찍고) — 담은 것은 열 때 비우므로 Open 다음에 담는다.
        ShopScreenUI.Open(ShopKind.Smithy);

        const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object window = typeof(ShopScreenUI).GetField("instance", any)?.GetValue(null);
        if (window != null && typeof(ShopScreenUI).GetField("cart", any)?.GetValue(window) is ShopCart cart)
        {
            cart.Add(AmmoTable.Arrow, 60);
            cart.Add("arm_head_t1", 1);
        }

        Debug.Log("[Debug] 대장간 — 화살 60 · 투구 1을 담았다");
    }

    /// <summary>
    /// 【검증】 지도 오른쪽 「진행 중 퀘스트」를 보려고 0장을 다 본 것으로 둔다 (결정 2-120).
    /// 진행은 메모리에만 바뀐다 — 이 실행에서 철수 · 저장을 하지 않으면 세이브는 그대로다.
    /// </summary>
    [MenuItem("Dokkaebi/Debug/지도 퀘스트 목록 시험 (0장 끝낸 것으로 · 저장 금지)")]
    public static void TryMapQuestList()
    {
        if (!EditorApplication.isPlaying)
            return;

        StoryProgress p = StoryManager.Progress;
        foreach (ZoneDefinition z in StoryTable.Chapter(0).Zones)
        {
            p.See(StoryTable.EnterEvent(z.Id));
            foreach (BossDefinition b in StoryTable.BossesIn(z.Id))
                p.Defeat(b.Id);
        }

        // 이 장의 첫 구역에 들어간 것으로 — 구역 이름이 보이게 (결정 2-121).
        MapRuntime runtime = MapRuntime.Current;
        ChapterDefinition here = runtime != null ? StoryTable.Chapter(runtime.Chapter) : null;
        if (here != null && here.Zones.Length > 0)
            p.See(StoryTable.EnterEvent(here.Zones[0].Id));

        WorldMapUI.Close();
        WorldMapUI.Open();
        Debug.Log("[Debug] 0장을 끝낸 것으로 두고 지도를 다시 열었다 — 이 실행에서는 저장하지 않는다");
    }
}
