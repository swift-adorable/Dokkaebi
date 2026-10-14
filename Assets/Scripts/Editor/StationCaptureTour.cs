using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 【개발용】 가게 · 시설 화면을 차례로 열어 찍는다 (결정 2-124 — UI 비율 점검).
/// 한 화면마다: 다 닫기 → 열기 → 1.2초 뒤 찍기. 파일 이름에 화면 크기와 화면 이름이 붙는다 — Temp/screens/tour_{고른 화면 크기}_{이름}.png
/// 게임이 멈춰 있어도 돌도록 실제 시간(EditorApplication.timeSinceStartup)으로 센다.
/// </summary>
public static class StationCaptureTour
{
    private static readonly List<(string name, Action open)> Steps = new()
    {
        ("shop", () => PlayCaptureMenu.TryShopCart()),
        ("general", () => ShopScreenUI.Open(ShopKind.General)),
        ("stash", StashScreenUI.Open),
        ("brew", () => ApothecaryUI.Open()),
        ("cooking", CookingUI.Open),
        ("workbench", WorkbenchUI.Open),
        ("ledger", () => LedgerUI.Open(1)),
        ("building", BuildingScreenUI.Open),
        ("departure", DepartureUI.Open),
        ("map", WorldMapUI.Open),   // 들판 판에서만 열린다 — 굴에서는 HUD와 같은 그림

        ("hud", () => { }),   // 아무것도 안 연 화면 — 미니맵 · 체력 등 HUD
    };

    /// <summary>들판 판 — 전체 지도와 HUD(미니맵)만.</summary>
    private static readonly List<(string name, Action open)> FieldSteps = new()
    {
        ("map", WorldMapUI.Open),
        ("hud", () => { }),
    };

    private static List<(string name, Action open)> active = Steps;
    private static int step;
    private static double next;
    private static bool captured;

    [MenuItem("Dokkaebi/Debug/가게 · 시설 화면 차례로 찍기")]
    public static void Run() => Start(Steps);

    [MenuItem("Dokkaebi/Debug/지도 · HUD 찍기 (들판 판)")]
    public static void RunField() => Start(FieldSteps);

    private static void Start(List<(string name, Action open)> steps)
    {
        if (!EditorApplication.isPlaying)
            return;

        active = steps;
        System.IO.Directory.CreateDirectory("Temp/screens");
        step = 0;
        captured = false;
        next = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || step >= active.Count)
        {
            EditorApplication.update -= Tick;
            if (EditorApplication.isPlaying)
            {
                PlayCaptureMenu.CloseStations();
                WorldMapUI.Close();
            }
            Debug.Log("[Tour] 끝");
            return;
        }

        if (EditorApplication.timeSinceStartup < next)
            return;

        (string name, Action open) = active[step];

        if (!captured)
        {
            PlayCaptureMenu.CloseStations();
            WorldMapUI.Close();
            DismissNotes();
            open();
            captured = true;
            next = EditorApplication.timeSinceStartup + 1.2;
            return;
        }

        DismissNotes();
        string path = $"Temp/screens/tour_{GameViewSizeMenu.Tag}_{step}_{name}.png";
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[Tour] {path}");

        captured = false;
        step++;
        next = EditorApplication.timeSinceStartup + 0.6;
    }

    /// <summary>첫 사용 안내 창(시스템 설명)은 찍을 때 가리므로 끈다 — 덮는 창 끄기와 같은 정렬 기준.</summary>
    private static void DismissNotes()
    {
        foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            if (c.isRootCanvas && c.sortingOrder >= 1400 && c.name.StartsWith("Story"))
                c.enabled = false;
    }
}
