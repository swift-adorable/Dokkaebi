using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 【개발용】 화면 비율 점검 — 소굴의 창들을 하나씩 열어 Temp/screens/ui_&lt;이름&gt;.png 로 찍는다.
/// 칸 크기 · 비율이 화면마다 맞는지 한 번에 보려고 (2026-11-17 사용자 「다른 화면들도 비율 체크해봐」).
/// 찍은 뒤 창을 닫는다. 소굴에서 플레이 중에만 쓴다.
/// </summary>
public static class UiTourMenu
{
    private const string Folder = "Temp/screens";

    private static readonly List<(string name, Action open, Action close)> Steps = new()
    {
        ("inventory_bag",   () => InventoryScreenUI.EnsureInstance().Open(0), () => InventoryScreenUI.Instance?.Close()),
        ("inventory_skill", () => InventoryScreenUI.EnsureInstance().Open(1), () => InventoryScreenUI.Instance?.Close()),
        ("inventory_passive", () => InventoryScreenUI.EnsureInstance().Open(2), () => InventoryScreenUI.Instance?.Close()),
        ("inventory_codex", () => InventoryScreenUI.EnsureInstance().Open(3), () => InventoryScreenUI.Instance?.Close()),
        ("stash",           StashScreenUI.Open,                    StashScreenUI.Close),
        ("loot",            () => ExchangeWindowUI.EnsureInstance().Open(new LootContainer(), "전리품", ExchangeWindowUI.Mode.Loot),
                            () => { ExchangeWindowUI.CloseIfOpen(); InventoryScreenUI.Instance?.Close(); }),
        ("shop_general",    () => ShopScreenUI.Open(ShopKind.General), ShopScreenUI.Close),
        ("shop_smithy",     () => ShopScreenUI.Open(ShopKind.Smithy),  ShopScreenUI.Close),
        ("building",        BuildingScreenUI.Open,                 BuildingScreenUI.Close),
        ("quest",           QuestScreenUI.Open,                    QuestScreenUI.Close),
        ("workbench",       WorkbenchUI.Open,                      () => Hide("WorkbenchCanvas")),
        ("apothecary",      () => ApothecaryUI.Open(),             ApothecaryUI.Close),
        ("cooking",         CookingUI.Open,                        () => Hide("CookingCanvas")),
        ("ledger",          () => LedgerUI.Open(),                 LedgerUI.Close),
        ("departure",       DepartureUI.Open,                      () => Hide("DepartureCanvas")),
        ("worldmap",        WorldMapUI.Open,                       WorldMapUI.Close),
    };

    private static int step;
    private static double nextAt;
    private static bool captured;
    private static Action pendingClose;   // 찍은 창은 다음 틱에 닫는다 — 찍기는 프레임 끝에 일어난다

    [MenuItem("Dokkaebi/Debug/화면 비율 점검 — 소굴 창 전부 찍기")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[UiTour] 플레이 중에만 쓴다.");
            return;
        }
        System.IO.Directory.CreateDirectory(Folder);
        step = 0;
        captured = false;
        pendingClose = null;
        nextAt = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextAt)
            return;
        if (pendingClose != null)
        {
            try { pendingClose(); } catch (Exception e) { Debug.LogWarning($"[UiTour] 닫기: {e.Message}"); }
            pendingClose = null;
            nextAt = EditorApplication.timeSinceStartup + 0.3;
            return;
        }
        if (!EditorApplication.isPlaying || step >= Steps.Count)
        {
            EditorApplication.update -= Tick;
            Debug.Log("[UiTour] 끝");
            return;
        }
        var (name, open, close) = Steps[step];
        // 처음 한 번 뜨는 안내 창(거점 건축 · 요리 …)은 닫고 찍는다 — 떠 있으면 다른 창이 열리지 않고 화면을 덮는다.
        if (StoryDialogueUI.IsShowing)
        {
            CloseDialogue();
            nextAt = EditorApplication.timeSinceStartup + 0.4;
            return;
        }
        try
        {
            if (!captured)
            {
                open();
                captured = true;
                nextAt = EditorApplication.timeSinceStartup + 0.6;   // 창이 그려질 시간
                return;
            }
            ScreenCapture.CaptureScreenshot($"{Folder}/ui_{name}.png");
            Debug.Log($"[UiTour] {name}");
            pendingClose = close;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UiTour] {name}: {e.Message}");
        }
        captured = false;
        step++;
        nextAt = EditorApplication.timeSinceStartup + 0.5;
    }

    private static void CloseDialogue()
    {
        var ui = UnityEngine.Object.FindAnyObjectByType<StoryDialogueUI>();
        typeof(StoryDialogueUI).GetMethod("Finish", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(ui, null);
    }

    private static void Hide(string canvasPrefix)
    {
        foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            if (c.name.StartsWith(canvasPrefix))
                c.gameObject.SetActive(false);
    }
}

/// <summary>
/// 【개발용】 여러 개 고르기 시험 — 열린 가게(또는 창고) 틀에서 고르기를 켜고, 가방 앞 칸 몇 개를 고른 화면을 찍은 뒤 한 번에 처리한다.
/// 화면을 직접 누를 수 없을 때(원격) 흐름을 확인하려고 둔다.
/// </summary>
public static class MultiSelectProbe
{
    private static object Frame(Type screen)
    {
        var inst = screen.GetField("instance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.GetValue(null);
        return inst == null ? null : screen.GetField("frame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(inst);
    }

    private static void Call(object frame, string method, params object[] args)
        => frame.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(frame, args);

    private static object OpenFrame()
        => Frame(typeof(ShopScreenUI)) is StationFrame shop && shop.IsOpen ? shop
         : Frame(typeof(StashScreenUI)) is StationFrame stash && stash.IsOpen ? stash : null;

    [MenuItem("Dokkaebi/Debug/여러 개 고르기 — 켜고 앞 3칸 고르기")]
    public static void SelectThree()
    {
        object frame = OpenFrame();
        if (frame == null) { Debug.LogWarning("[MultiSelect] 가게나 창고를 먼저 연다."); return; }
        Call(frame, "SetSelecting", true);
        IReadOnlyList<ItemStack> stacks = PlayerInventory.EnsureInstance().Bag.Stacks;
        for (int i = 0, n = 0; i < stacks.Count && n < 3; i++, n++)
            Call(frame, "ToggleChosen", stacks[i]);
        Debug.Log("[MultiSelect] 3칸 고름");
    }

    [MenuItem("Dokkaebi/Debug/여러 개 고르기 — 한 번에 처리")]
    public static void Apply()
    {
        object frame = OpenFrame();
        if (frame == null) return;
        Call(frame, "ApplyBatch");
        Debug.Log("[MultiSelect] 처리");
    }
}

/// <summary>【개발용】 소굴 전체를 한 장으로 — 게임 카메라와 같은 각도(정사영 · Pitch)로 방 전체가 들어오게 따로 찍는다.</summary>
public static class RoomOverviewCapture
{
    [MenuItem("Dokkaebi/Debug/소굴 전체 한 장 찍기")]
    public static void Capture()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[Overview] 플레이 중에만."); return; }
        Camera main = Camera.main;
        var go = new GameObject("OverviewCamera");
        var cam = go.AddComponent<Camera>();
        if (main != null) cam.CopyFrom(main);
        cam.orthographic = true;
        cam.orthographicSize = BunkerLayout.RoomDepth * 0.5f * Mathf.Cos(ArtCatalog.Pitch * Mathf.Deg2Rad) + 2.5f;
        cam.transform.rotation = Quaternion.Euler(ArtCatalog.Pitch, 0f, 0f);
        cam.transform.position = new Vector3(0f, 0f, 0.5f) - cam.transform.forward * 60f;
        cam.farClipPlane = 200f;
        int w = 2400, h = 1500;
        var rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        System.IO.Directory.CreateDirectory("Temp/screens");
        System.IO.File.WriteAllBytes("Temp/screens/room_overview.png", tex.EncodeToPNG());
        cam.targetTexture = null;
        UnityEngine.Object.Destroy(rt);
        UnityEngine.Object.Destroy(go);
        Debug.Log("[Overview] Temp/screens/room_overview.png");
    }
}

/// <summary>【개발용】 에디터에서도 휴대폰 터치 조작(조이스틱 · 대시 · 파밍)을 켜 본다 — 버튼이 다른 UI에 가려지는지 보려고 (2026-11-17).</summary>
public static class TouchControlsProbe
{
    [MenuItem("Dokkaebi/Debug/터치 조작 켜기 (화면 점검)")]
    public static void Force()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[Touch] 플레이 중에만."); return; }
        const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        foreach (PlayerInputHandler h in UnityEngine.Object.FindObjectsByType<PlayerInputHandler>(FindObjectsInactive.Exclude))
        {
            typeof(PlayerInputHandler).GetField("mode", F)?.SetValue(h, InputSourceMode.ForceTouch);
            typeof(PlayerInputHandler).GetMethod("ResolveActiveSource", F)?.Invoke(h, null);
        }
        Debug.Log("[Touch] 터치 조작 켬");
    }

    [MenuItem("Dokkaebi/Debug/튜토리얼 줄 띄워 보기 (화면 점검)")]
    public static void FakeTutorial()
    {
        if (!EditorApplication.isPlaying) return;
        TutorialHudUI.EnsureInstance().Show(6, 19, "잡귀를 쓰러뜨려라 (0/3)", "조준해 쏜다 · 붉은 빛이 번뜩이면 대시로 피한다", "구역");
    }
}

/// <summary>
/// 【개발용】 결정 2-142 화면 점검 — 등급 색 · 뒤지기 · 철수 결산을 차례로 찍는다 (Temp/screens/m142_*.png). 소굴에서 플레이 중.
///   1) 가치가 다른 물건 6개를 넣은 상자를 연다 → 0.5초 뒤(뒤지는 중) · 6초 뒤(다 드러남) 찍기
///   2) 결산(가짜 기록)을 띄워 찍기
/// </summary>
public static class Decision142Probe
{
    private static int phase;
    private static double nextAt;

    [MenuItem("Dokkaebi/Debug/결정 2-142 화면 점검 (등급 · 뒤지기 · 결산)")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[2-142] 플레이 중에만 쓴다.");
            return;
        }
        System.IO.Directory.CreateDirectory("Temp/screens");
        phase = 0;
        nextAt = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static LootContainer Filled()
    {
        var box = new LootContainer();
        ItemCatalog catalog = ItemCatalog.Load();
        int[] wants = { 20, 150, 500, 1500, 5000, 20000 };
        foreach (int want in wants)
        {
            ItemDefinition best = null;
            foreach (ItemDefinition d in catalog.Items)
                if (d != null && ItemGrade.Of(d) == ItemGrade.Of(want) && !d.IsSkillGem)
                { best = d; break; }
            if (best != null)
                box.TryPut(best, 1);
        }
        return box;
    }

    private static void Shot(string name) => ScreenCapture.CaptureScreenshot($"Temp/screens/m142_{name}.png");

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextAt)
            return;
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= Tick;
            return;
        }

        switch (phase++)
        {
            case 0: ExchangeWindowUI.EnsureInstance().Open(Filled(), "반닫이 궤", ExchangeWindowUI.Mode.Loot); nextAt += 0.9; break;
            case 1: Shot("loot_searching"); nextAt += 7.0; break;
            case 2: Shot("loot_revealed"); nextAt += 0.3; break;
            case 3: ExchangeWindowUI.CloseIfOpen(); InventoryScreenUI.Instance?.Close(); nextAt += 0.4; break;
            case 4:
                RaidLedger.Begin(1200, 0f, "1-1");
                RaidLedger.CountKill(); RaidLedger.CountKill(); RaidLedger.CountKill();
                RaidLedger.End(5480, 754f);
                nextAt += 1.6;
                break;
            case 5: Shot("summary"); nextAt += 0.3; break;
            default:
                EditorApplication.update -= Tick;
                Debug.Log("[2-142] 끝");
                break;
        }
    }
}

/// <summary>
/// 【개발용】 결정 2-143 화면 점검 — 장 맵에서 플레이 중. 잠긴 곳으로 순간 이동해 찍는다 (Temp/screens/m143_*.png).
///   마당 대문 앞 → 열쇠로 연 뒤 → 집 바깥 → 집 안(지붕 걷힘)
/// </summary>
public static class Decision143Probe
{
    private static int phase;
    private static double nextAt;

    [MenuItem("Dokkaebi/Debug/결정 2-143 화면 점검 (마당 · 집)")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying)
            return;
        System.IO.Directory.CreateDirectory("Temp/screens");
        phase = 0;
        nextAt = EditorApplication.timeSinceStartup + 0.2;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static LockedSite Find(LockSiteKind kind)
    {
        foreach (LockedSite s in LockedSite.Active)
            if (s != null && s.Site.Kind == kind)
                return s;
        return null;
    }

    private static void MoveTo(Vector3 p)
    {
        var movement = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
        if (movement == null)
            return;
        p.y = movement.transform.position.y;
        if (movement.TryGetComponent(out Rigidbody body))
        {
            body.position = p;
            body.linearVelocity = Vector3.zero;
        }
        movement.transform.position = p;
    }

    private static void Shot(string name) => ScreenCapture.CaptureScreenshot($"Temp/screens/m143_{name}.png");

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextAt || !EditorApplication.isPlaying)
            return;

        LockedSite yard = Find(LockSiteKind.Compound);
        LockedSite house = Find(LockSiteKind.House);
        switch (phase++)
        {
            case 0:
                if (yard == null && house == null && ZoneMap.Current != null)
                {
                    ChapterMap map = ZoneMap.Current.Map;
                    foreach (ZoneArea a in map.Areas)
                        if (LockSiteTable.For(map, a.ZoneId).Count == 2)
                        {
                            foreach (LockSite site in LockSiteTable.For(map, a.ZoneId))
                                LockedSite.Create(site);
                            break;
                        }
                    yard = Find(LockSiteKind.Compound);
                    house = Find(LockSiteKind.House);
                }
                Debug.Log($"[2-143] 마당 {(yard != null ? yard.Site.Id : "없음")} · 집 {(house != null ? house.Site.Id : "없음")}");
                if (yard != null) MoveTo(yard.Site.Door + new Vector3(0f, 0f, -2.5f));
                nextAt += 1.0; break;
            case 1: Shot("yard_locked"); nextAt += 0.3; break;
            case 2:
                if (yard != null)
                {
                    KeyRing.Register(yard.Site.KeyId);
                    yard.UseDoor();
                }
                nextAt += 1.8; break;
            case 3:
                if (yard != null) MoveTo(new Vector3(yard.Site.Center.x, 0f, yard.Site.Center.y - 1f));
                nextAt += 0.8; break;
            case 4: Shot("yard_open_inside"); nextAt += 0.3; break;
            case 5:
                if (house != null) MoveTo(house.Site.Door + new Vector3(0f, 0f, -2.5f));
                nextAt += 0.8; break;
            case 6: Shot("house_outside"); nextAt += 0.3; break;
            case 7:
                if (house != null) MoveTo(new Vector3(house.Site.Center.x, 0f, house.Site.Center.y));
                nextAt += 0.8; break;
            case 8: Shot("house_inside"); nextAt += 0.3; break;
            default:
                KeyRing.Reset();
                EditorApplication.update -= Tick;
                Debug.Log("[2-143] 끝");
                break;
        }
    }
}

/// <summary>
/// 【개발용】 결정 2-144 장 빛깔 전후 — 플레이 중에 부르면 0 ~ 6장 맵을 차례로 열어 같은 자리를 「빛깔 끔 / 켬」으로 찍는다
/// (Temp/screens/mood_ch{n}_off.png · _on.png).
/// </summary>
public static class ChapterMoodProbe
{
    private static int chapter, phase;
    private static double nextAt;

    [MenuItem("Dokkaebi/Debug/결정 2-144 장 빛깔 전후 찍기")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying)
            return;
        System.IO.Directory.CreateDirectory("Temp/screens");
        chapter = 0;
        phase = 0;
        nextAt = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    [MenuItem("Dokkaebi/Debug/장 빛깔 켜기 · 끄기")]
    public static void Toggle() => ChapterLighting.SetEnabled(!ChapterLighting.Enabled);

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextAt || !EditorApplication.isPlaying)
            return;

        if (StoryDialogueUI.IsShowing)
        {
            var ui = UnityEngine.Object.FindAnyObjectByType<StoryDialogueUI>();
            typeof(StoryDialogueUI).GetMethod("Finish", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(ui, null);
            nextAt = EditorApplication.timeSinceStartup + 0.4;
            return;
        }

        switch (phase)
        {
            case 0:
                if (chapter > 6) { EditorApplication.update -= Tick; Debug.Log("[2-144] 끝"); return; }
                UnityEngine.SceneManagement.SceneManager.LoadScene(ZoneMapTable.SceneFor(chapter));
                phase = 1; nextAt = EditorApplication.timeSinceStartup + 3.0; return;
            case 1:
                ChapterLighting.SetEnabled(false);
                phase = 2; nextAt = EditorApplication.timeSinceStartup + 0.5; return;
            case 2:
                ScreenCapture.CaptureScreenshot($"Temp/screens/mood_ch{chapter}_off.png");
                phase = 3; nextAt = EditorApplication.timeSinceStartup + 0.4; return;
            case 3:
                ChapterLighting.SetEnabled(true);
                phase = 4; nextAt = EditorApplication.timeSinceStartup + 0.5; return;
            case 4:
                ScreenCapture.CaptureScreenshot($"Temp/screens/mood_ch{chapter}_on.png");
                Debug.Log($"[2-144] {chapter}장");
                chapter++;
                phase = 0; nextAt = EditorApplication.timeSinceStartup + 0.4; return;
        }
    }
}

/// <summary>【개발용】 결정 2-146 HUD 전후 — 터치 조작을 켜고 HUD를 찍는다 (Temp/screens/hud_touch_{before|after}.png). 장 맵에서 플레이 중.</summary>
public static class HudProbe
{
    private static string tag;
    private static double at;

    [MenuItem("Dokkaebi/Debug/HUD 찍기 — 전")]
    public static void Before() => Run("before");

    [MenuItem("Dokkaebi/Debug/HUD 찍기 — 후")]
    public static void After() => Run("after");

    private static void Run(string t)
    {
        if (!EditorApplication.isPlaying) return;
        TouchControlsProbe.Force();
        tag = t;
        at = EditorApplication.timeSinceStartup + 1.2;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < at) return;
        EditorApplication.update -= Tick;
        System.IO.Directory.CreateDirectory("Temp/screens");
        ScreenCapture.CaptureScreenshot($"Temp/screens/hud_touch_{tag}.png");
        Debug.Log($"[HudProbe] {tag}");
    }
}
