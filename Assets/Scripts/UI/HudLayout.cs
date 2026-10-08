using UnityEngine.SceneManagement;

/// <summary>
/// 오른쪽 위 구석의 자리 나눔 (결정 2-90) — 장 맵에서는 미니맵이 맨 위, 장비 · 스킬 · 패시브 버튼은 그 아래.
/// 미니맵이 없는 씬(소굴 · 옛 평지)에서는 버튼이 맨 위로 올라간다.
/// </summary>
public static class HudLayout
{
    public const float Margin = 10f;

    /// <summary>미니맵 한 변 (캔버스 픽셀 · 1920 × 1080 기준).</summary>
    public const float MinimapSize = 250f;

    /// <summary>지금 씬에 미니맵이 뜨는가 — 장 맵 씬.</summary>
    public static bool HasMinimap => ZoneMapTable.IsMapScene(SceneManager.GetActiveScene().name);

    /// <summary>세 버튼 줄의 위쪽 자리 (위에서부터, 음수).</summary>
    public static float TopRightButtonsY => HasMinimap ? -(Margin + MinimapSize + Margin) : -Margin;
}
