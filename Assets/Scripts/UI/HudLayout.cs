using UnityEngine.SceneManagement;

/// <summary>
/// 화면 구석 자리 나눔 (결정 2-90 → 2-130) — 미니맵은 **왼쪽 위**, 그 오른쪽에 디버그 버튼.
/// 장비 · 스킬 · 패시브 · 도감 버튼은 오른쪽 위 맨 위. 체력 · 수분 · 기력 · 상태이상 줄은 왼쪽 아래(SurvivalHudUI).
/// 미니맵이 없는 씬(소굴 · 옛 평지)에서는 디버그 버튼이 왼쪽 위 구석으로 간다.
/// </summary>
public static class HudLayout
{
    public const float Margin = 10f;

    /// <summary>미니맵 한 변 (캔버스 픽셀 · 1920 × 1080 기준).</summary>
    public const float MinimapSize = 250f;

    /// <summary>지금 씬에 미니맵이 뜨는가 — 장 맵 씬.</summary>
    public static bool HasMinimap => ZoneMapTable.IsMapScene(SceneManager.GetActiveScene().name);

    /// <summary>오른쪽 위 버튼 줄의 위쪽 자리 (위에서부터, 음수). 미니맵이 왼쪽으로 옮겨 가 늘 맨 위.</summary>
    public static float TopRightButtonsY => -Margin;

    /// <summary>디버그 버튼의 왼쪽 자리 — 미니맵 오른쪽, 미니맵이 없으면 구석.</summary>
    public static float DebugButtonX => HasMinimap ? Margin + MinimapSize + Margin : Margin;
}
