using UnityEditor;
using UnityEngine;

/// <summary>
/// 플레이 검증 메뉴. (로드맵 6-M · 6-P)
///
/// 【얇은 껍데기다】
/// 실제 동작은 전부 런타임의 PlaytestActions에 있다. 이 파일은 메뉴를 걸어 줄 뿐이다.
///
/// 그렇게 바꾼 이유 — 원래는 여기에 AssetDatabase로 에셋을 찾는 코드가 들어 있었다.
/// MenuItem도 AssetDatabase도 **빌드에 존재하지 않는다.** 이 게임은 모바일이고
/// 실제 기기(iOS)에서 확인하는 것이 당연한데, 거기서는 장비를 얻을 방법이 하나도 없었다.
/// 검증 도구가 검증하는 자리에서 돌지 않으면 도구가 아니다.
///
/// 실제 기기에서는 화면 좌하단 「검증」 버튼(PlaytestPanelUI)이 같은 것을 부른다.
/// </summary>
public static class PlaytestTools
{
    private const string Menu = "Dokkaebi/Playtest/";

    private static bool RequirePlayMode()
    {
        if (Application.isPlaying)
            return true;

        EditorUtility.DisplayDialog("플레이 중에만 씁니다",
            "플레이 모드에서 실행하십시오. 가방은 실행 중에만 존재합니다.\n\n"
            + "빌드(실제 기기)에서는 화면 좌하단 「검증」 버튼을 쓰십시오.", "확인");

        return false;
    }

    /// <summary>결과를 콘솔과 가방 화면 양쪽에 남긴다. 실제 기기에는 Console 창이 없다.</summary>
    private static void Run(System.Func<string> action)
    {
        if (!RequirePlayMode())
            return;

        string message = action();

        Debug.Log("[Playtest] " + message);

        InventoryScreenUI.ShowBagWithMessage(message.Replace("\n", "  ·  "));
    }

    // ── 지급 ──────────────────────────────────────────────────────────

    [MenuItem(Menu + "파밍 장비 — 티어 1 한 벌")]
    public static void GiveStarterKit() => Run(PlaytestActions.GiveStarterKit);

    [MenuItem(Menu + "파밍 장비 — 티어 6 한 벌")]
    public static void GiveEndgameKit() => Run(PlaytestActions.GiveEndgameKit);

    [MenuItem(Menu + "무기 6종 전부")]
    public static void GiveAllWeapons() => Run(PlaytestActions.GiveAllWeapons);

    [MenuItem(Menu + "각인 24종 전부")]
    public static void GiveAllImprints() => Run(PlaytestActions.GiveAllImprints);

    [MenuItem(Menu + "각인 — 진격Ⅱ · 중장Ⅲ · 경량Ⅲ · 정밀Ⅲ")]
    public static void GiveKeyImprints() => Run(PlaytestActions.GiveKeyImprints);

    [MenuItem(Menu + "젬 — 전부 (핵심 · 보조 · 발동 · 전령)")]
    public static void GiveAllGems() => Run(PlaytestActions.GiveAllGems);

    [MenuItem(Menu + "겹치는 재료 지급 — 개수 배지 확인")]
    public static void GiveStackables() => Run(PlaytestActions.GiveStackables);

    [MenuItem(Menu + "가방 채우기 — 과중량 유발")]
    public static void FillBag() => Run(PlaytestActions.FillBag);

    [MenuItem(Menu + "가방 비우기")]
    public static void ClearBag() => Run(PlaytestActions.ClearBag);

    // ── 상태 확인 ─────────────────────────────────────────────────────

    [MenuItem(Menu + "현재 능력치 출력")]
    public static void DumpStats() => Run(PlaytestActions.DumpStats);

    [MenuItem(Menu + "현재 젬 빌드 출력")]
    public static void DumpBuild() => Run(PlaytestActions.DumpBuild);

    // ── 상태이상 ──────────────────────────────────────────────────────

    [MenuItem(Menu + "가까운 적에게 냉각 6중첩 (→ 동결)")]
    public static void ChillNearest()
        => Run(() => PlaytestActions.StackOnNearest(StatusEffectType.Chill, 6));

    [MenuItem(Menu + "가까운 적에게 감전 6중첩 (→ 마비)")]
    public static void ShockNearest()
        => Run(() => PlaytestActions.StackOnNearest(StatusEffectType.Shock, 6));

    [MenuItem(Menu + "가까운 적에게 중독 10중첩 (→ 부식)")]
    public static void PoisonNearest()
        => Run(() => PlaytestActions.StackOnNearest(StatusEffectType.Poison, 10));

    // ── 벙커 (8-J) ───────────────────────────────────────────────────
    //
    // 씬을 넘기는 것이라 가방 화면에 결과를 띄우지 않는다 — 곧 사라진다.
    // 플레이 중이 아니면 창을 띄우지 않고 로그만 남긴다 (외부 도구가 누를 때 멈추지 않게).

    [MenuItem(Menu + "벙커 — 파밍 출발")]
    public static void Depart()
    {
        if (!Application.isPlaying) { Debug.LogError("[Playtest] 플레이 중에만 씁니다."); return; }
        SceneFlow.Depart();
    }

    [MenuItem(Menu + "벙커 — 즉시 철수")]
    public static void Extract()
    {
        if (!Application.isPlaying) { Debug.LogError("[Playtest] 플레이 중에만 씁니다."); return; }
        Debug.Log("[Playtest] " + PlaytestActions.ExtractNow());
    }

    [MenuItem(Menu + "벙커 — 창고 열기")]
    public static void OpenStash() => Run(PlaytestActions.OpenStash);

    [MenuItem(Menu + "벙커 — 잡화 가게 열기")]
    public static void OpenShop() => Run(PlaytestActions.OpenShop);

    [MenuItem(Menu + "벙커 — 건물 다섯 짓고 놓기 (검증)")]
    public static void BuildAll()
    {
        if (!Application.isPlaying) { Debug.LogError("[Playtest] 플레이 중에만 씁니다."); return; }
        Debug.Log("[Playtest] " + PlaytestActions.BuildAllForTest());
    }

    [MenuItem(Menu + "벙커 — 작업대 배치 모드 (그림자 색 확인)")]
    public static void PlaceWorkbench()
    {
        if (!Application.isPlaying || BunkerBuildings.Instance == null) { Debug.LogError("[Playtest] 벙커 플레이 중에만 씁니다."); return; }
        BuildingDefinition workbench = BuildingTable.Find(BuildingTable.Workbench);
        if (!BuildingManager.State.Owns(workbench.Id)) { Debug.LogError("[Playtest] 작업대를 먼저 지으세요 (건물 다섯 짓고 놓기)."); return; }
        BunkerBuildings.Instance.BeginPlacing(workbench);
        Debug.Log("[Playtest] 작업대 배치 모드 — 그림자가 초록이면 놓을 수 있다");
    }

    [MenuItem(Menu + "벙커 바닥 — PixelLab 타일")]
    public static void FloorTiles()
    {
        BunkerFloor.BlendBase = "";
        SetFloor("");
    }

    [MenuItem(Menu + "벙커 바닥 — 텍스처 그대로 (뿌리 10)")]
    public static void FloorTextureHd() => SetFloor("tex_hd_roots10");

    [MenuItem(Menu + "벙커 바닥 — 흙 + 벽가 뿌리 (도트 밀도)")]
    public static void FloorBlend()
    {
        BunkerFloor.BlendBase = "tex_px_mud1";
        SetFloor("");
    }

    [MenuItem(Menu + "벙커 바닥 — 텍스처 도트 밀도 (뿌리 10)")]
    public static void FloorTexturePx() => SetFloor("tex_px_roots10");

    private static void SetFloor(string texture)
    {
        if (!Application.isPlaying) { Debug.LogError("[Playtest] 플레이 중에만 씁니다."); return; }
        BunkerFloor.TextureName = texture;
        BunkerFloor.Rebuild();
        if (BunkerFloor.Current != null && BunkerFloor.Current.isReadable)
            System.IO.File.WriteAllBytes("Temp/screens/floor_full.png", BunkerFloor.Current.EncodeToPNG());   // 바닥 한 장 전체 (확인용)
        Debug.Log($"[Playtest] 벙커 바닥 — {(texture == "" ? "PixelLab 타일" : texture)}");
    }

    [MenuItem(Menu + "도깨비불 — 둥근 불 (G4-5)")]
    public static void WispRound() { PlayerWisp.Shape = "g4-5"; PlayerWisp.DebugShow = true; }

    [MenuItem(Menu + "도깨비불 — 길쭉한 불 (G4-4)")]
    public static void WispTall() { PlayerWisp.Shape = "g4-4"; PlayerWisp.DebugShow = true; }

    [MenuItem(Menu + "전리품 창 열기 (빈 상자 · 화면 확인)")]
    public static void OpenTestLoot()
    {
        if (!Application.isPlaying) { Debug.LogError("[Playtest] 플레이 중에만 씁니다."); return; }
        ExchangeWindowUI.EnsureInstance().Open(new LootContainer(), "전리품", ExchangeWindowUI.Mode.Loot);
    }

    [MenuItem(Menu + "벙커 — 대장간 열기")]
    public static void OpenSmithy()
    {
        if (!Application.isPlaying) { Debug.LogError("[Playtest] 플레이 중에만 씁니다."); return; }
        ShopScreenUI.Open(ShopKind.Smithy);
        Debug.Log("[Playtest] 대장간을 열었다 — 오른쪽 「내 가방」에서 판다");
    }
}
