using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 화면 조각을 코드로 만드는 공용 도구.
///
/// 왜 프리팹이 아니라 코드인가 —
/// 조이스틱 UI와 같은 이유다. 씬·프리팹 배치를 잊어 빌드에서 빠지는
/// 실패 지점을 만들지 않는다. (마스터 프롬프트 5-4)
///
/// 왜 창마다 따로 만들지 않고 한 곳에 모으는가 —
/// 전리품 창·가방 창·소켓 창이 각자 자기 색과 여백을 적으면 화면이 따로 논다.
/// 여기 한 곳만 고치면 전부 같이 바뀐다.
/// </summary>
public static class UIFactory
{
    /// <summary>레이아웃 기준 해상도. 가로 모드 1920×1080.</summary>
    public const float ReferenceWidth = 1920f;
    public const float ReferenceHeight = 1080f;

    private static Font font;

    /// <summary>공용 글꼴. 내장 글꼴을 쓰므로 에셋 의존이 없다.</summary>
    public static Font Font =>
        font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    /// <summary>화면 전체를 덮는 캔버스를 만든다.</summary>
    public static Canvas CreateCanvas(string name, int sortingOrder)
    {
        var canvasObject = new GameObject(name);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        return canvas;
    }

    public static GameObject CreateChild(string name, Transform parent)
    {
        var child = new GameObject(name);
        child.AddComponent<RectTransform>();
        child.transform.SetParent(parent, false);

        return child;
    }

    /// <summary>앵커만 지정한 빈 영역. 자식 배치의 기준이 된다.</summary>
    public static RectTransform CreateRegion(
        string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject region = CreateChild(name, parent);

        var rect = region.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return rect;
    }

    /// <summary>색이 채워진 판. 클릭을 막는 배경으로도 쓴다.</summary>
    public static Image CreatePanel(
        string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        RectTransform rect = CreateRegion(name, parent, anchorMin, anchorMax);

        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;

        return image;
    }

    public static Text CreateLabel(
        Transform parent, string text, int fontSize, FontStyle style,
        Vector2 anchorMin, Vector2 anchorMax, TextAnchor alignment, Color? color = null)
    {
        GameObject labelObject = CreateChild("Label", parent);

        var rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var label = labelObject.AddComponent<Text>();
        label.font = Font;
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = color ?? UIPalette.Text;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;

        // 글자가 터치를 가로채면 아래의 칸 버튼이 눌리지 않는다.
        label.raycastTarget = false;

        return label;
    }

    /// <summary>글자 하나짜리 버튼.</summary>
    public static Button CreateButton(
        Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax,
        Color color, UnityAction action, int fontSize = 30)
    {
        Image image = CreatePanel($"Button_{text}", parent, color, anchorMin, anchorMax);

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        if (action != null)
            button.onClick.AddListener(action);

        CreateLabel(image.transform, text, fontSize, FontStyle.Bold,
            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

        return button;
    }

    /// <summary>
    /// 격자 안 한 칸의 앵커를 구한다. 왼쪽 위에서 오른쪽으로, 그다음 아래로 센다.
    ///
    /// 칸 배치를 각 창이 직접 계산하면 창마다 미세하게 어긋난다.
    /// 전리품·가방·창고가 전부 이 함수를 쓴다.
    /// </summary>
    public static void GetCellAnchors(
        int index, int columns, int rows, float padding,
        out Vector2 anchorMin, out Vector2 anchorMax)
    {
        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);

        int column = index % columns;
        int row = index / columns;

        float cellWidth = 1f / columns;
        float cellHeight = 1f / rows;

        anchorMin = new Vector2(
            column * cellWidth + padding,
            1f - (row + 1) * cellHeight + padding);

        anchorMax = new Vector2(
            (column + 1) * cellWidth - padding,
            1f - row * cellHeight - padding);
    }

    /// <summary>자식을 전부 지운다. 격자를 다시 그릴 때 쓴다.</summary>
    public static void ClearChildren(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.Destroy(parent.GetChild(i).gameObject);
    }
}
