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
    private const string Menu = "Blob/Playtest/";

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

    [MenuItem(Menu + "벙커 — 잡화 상점 열기")]
    public static void OpenShop() => Run(PlaytestActions.OpenShop);

    [MenuItem(Menu + "벙커 — 건물 넷 짓고 놓기 (검증)")]
    public static void BuildAll()
    {
        if (!Application.isPlaying) { Debug.LogError("[Playtest] 플레이 중에만 씁니다."); return; }
        Debug.Log("[Playtest] " + PlaytestActions.BuildAllForTest());
    }

    [MenuItem(Menu + "벙커 — 무기 상점 열기")]
    public static void OpenWeaponShop()
    {
        if (!Application.isPlaying) { Debug.LogError("[Playtest] 플레이 중에만 씁니다."); return; }
        ExchangeWindowUI.EnsureInstance().OpenShop(ShopKind.Weapon);
        Debug.Log($"[Playtest] 무기 상점 — 가방 칸의 판매 줄 {(ExchangeWindowUI.IsBuyingShopOpen ? "뜬다" : "안 뜬다")}");
    }
}
