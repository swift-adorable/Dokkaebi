using UnityEngine;

/// <summary>
/// 전체 지도의 확대 · 이동 셈 (2026-10-08 사용자 결정 「확대 · 이동 넣기」).
///   · 배율 1 = 장 하나가 화면에 다 들어온다 · 4까지 · 열면 2배로 내 자리가 가운데
///   · 가운데(그림의 0 ~ 1 좌표)는 그림이 화면 밖으로 빠지지 않게 잡는다 — 화면보다 작으면 한가운데
/// MonoBehaviour 의존 없음 — EditMode 테스트 대상.
/// </summary>
public static class MapView
{
    public const float MinZoom = 1f;
    public const float MaxZoom = 4f;
    public const float OpenZoom = 2f;

    /// <summary>+ · − 단추 한 번.</summary>
    public const float ZoomStep = 0.5f;

    public static float ClampZoom(float zoom) => Mathf.Clamp(zoom, MinZoom, MaxZoom);

    /// <summary>배율 1일 때 그림의 크기 — 화면 안에 비율을 지켜 꽉 차게.</summary>
    public static Vector2 FitSize(Vector2 viewport, float aspect)
    {
        if (viewport.x <= 0f || viewport.y <= 0f || aspect <= 0f)
            return Vector2.one;

        return viewport.x / viewport.y > aspect
            ? new Vector2(viewport.y * aspect, viewport.y)
            : new Vector2(viewport.x, viewport.x / aspect);
    }

    /// <summary>가운데를 잡는다 — 축마다 화면이 그림보다 넓으면 0.5, 아니면 그림 끝이 화면 끝을 넘지 않게.</summary>
    public static Vector2 ClampCenter(Vector2 center, Vector2 viewport, Vector2 content)
        => new(ClampAxis(center.x, viewport.x, content.x), ClampAxis(center.y, viewport.y, content.y));

    private static float ClampAxis(float c, float view, float content)
    {
        if (content <= 0f)
            return 0.5f;

        float half = view * 0.5f / content;   // 화면 반폭이 그림의 몇 할인가
        return half >= 0.5f ? 0.5f : Mathf.Clamp(c, half, 1f - half);
    }

    /// <summary>그림 위 자리(uv) → 화면 가운데 기준 자리.</summary>
    public static Vector2 ToView(Vector2 uv, Vector2 center, Vector2 content)
        => Vector2.Scale(uv - center, content);

    /// <summary>화면 가운데 기준 자리 → 그림 위 자리(uv).</summary>
    public static Vector2 ToUv(Vector2 view, Vector2 center, Vector2 content)
        => center + new Vector2(content.x > 0f ? view.x / content.x : 0f, content.y > 0f ? view.y / content.y : 0f);

    /// <summary>화면 한 점을 붙든 채 배율을 바꾼다 — 휠 · 두 손가락이 가리킨 곳이 그대로 남는다.</summary>
    public static Vector2 ZoomAround(Vector2 center, Vector2 anchorView, Vector2 contentBefore, Vector2 contentAfter)
    {
        Vector2 anchorUv = ToUv(anchorView, center, contentBefore);
        return anchorUv - new Vector2(contentAfter.x > 0f ? anchorView.x / contentAfter.x : 0f,
                                      contentAfter.y > 0f ? anchorView.y / contentAfter.y : 0f);
    }
}
