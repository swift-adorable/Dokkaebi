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

    /// <summary>
    /// 안전 영역에 맞춰지는 컨테이너를 만들어 돌려준다.
    /// 화면 UI는 캔버스가 아니라 **이것의 자식**으로 붙인다.
    /// 그래야 노치와 홈 인디케이터를 피한다. (SafeAreaFitter)
    /// </summary>
    public static RectTransform CreateSafeArea(Canvas canvas)
    {
        RectTransform area = CreateRegion("SafeArea", canvas.transform,
            Vector2.zero, Vector2.one);

        area.gameObject.AddComponent<SafeAreaFitter>();

        return area;
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

    /// <summary>둥근 모서리 반지름의 기본값. 칸·버튼이 쓴다.</summary>
    public const int Radius = 10;

    /// <summary>패널·카드처럼 큰 면이 쓰는 반지름.</summary>
    public const int RadiusLarge = 18;

    /// <summary>
    /// 색이 채워진 판. 클릭을 막는 배경으로도 쓴다.
    ///
    /// radius가 0보다 크면 둥근 9-슬라이스 스프라이트를 쓴다.
    /// 전부 각진 사각형이면 「대충 만든 화면」으로 보인다. (UISprites)
    /// </summary>
    public static Image CreatePanel(
        string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax,
        int radius = Radius)
    {
        RectTransform rect = CreateRegion(name, parent, anchorMin, anchorMax);

        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;

        if (radius > 0)
        {
            image.sprite = UISprites.Rounded(radius);
            image.type = Image.Type.Sliced;

            // 칸이 반지름보다 작아지면 Unity가 스프라이트를 통째로 줄여 버린다.
            // 그러면 모서리가 뭉개지므로 슬라이스 비율을 유지하게 둔다.
            image.pixelsPerUnitMultiplier = 1f;
        }

        return image;
    }

    /// <summary>
    /// 유리판. 반투명 바탕 + 위쪽 광택 + 밝은 테두리로 「비치는 판」을 만든다.
    ///
    /// 【진짜 블러가 아닌 이유】
    /// ScreenSpaceOverlay 캔버스는 뒤 화면을 텍스처로 받을 수 없어서
    /// 실제 배경 흐림은 별도 카메라와 셰이더가 필요하다. 지금 단계에서는
    /// 그 비용이 얻는 것보다 크다.
    ///
    /// 대신 유리처럼 보이게 하는 세 가지를 쌓는다 —
    ///   1) 낮은 알파 : 뒤가 비친다
    ///   2) 위쪽 광택 : 빛이 위에서 든다는 신호. 이것 하나로 평면이 판으로 바뀐다
    ///   3) 밝은 테두리 : 유리의 모서리. 면과 면을 갈라 준다
    /// </summary>
    public static Image CreateGlass(
        string name, Transform parent, Color tint,
        Vector2 anchorMin, Vector2 anchorMax, int radius = RadiusLarge,
        int rimThickness = 2)
    {
        Image body = CreatePanel(name, parent, tint, anchorMin, anchorMax, radius);

        AddGlassSheen(body, radius);

        CreateOutline(body, UIPalette.Rim, radius, rimThickness);

        return body;
    }

    /// <summary>
    /// 위쪽 광택만 얹는다. 이미 만들어 둔 판을 유리처럼 보이게 할 때 쓴다.
    ///
    /// 두 겹으로 나누는 이유 — 넓고 옅은 띠는 「빛을 받은 면」,
    /// 얇고 밝은 띠는 「모서리에 맺힌 하이라이트」다.
    /// 하나만 쓰면 그냥 밝은 사각형으로 보인다.
    /// </summary>
    public static void AddGlassSheen(Image body, int radius = RadiusLarge)
    {
        Image wide = CreatePanel("Sheen", body.transform, UIPalette.SheenWide,
            new Vector2(0f, 0.52f), new Vector2(1f, 1f), radius);

        wide.raycastTarget = false;

        Image edge = CreatePanel("SheenEdge", body.transform, UIPalette.SheenEdge,
            new Vector2(0.02f, 0.93f), new Vector2(0.98f, 1f), radius);

        edge.raycastTarget = false;
    }

    /// <summary>
    /// 윤곽선을 얹는다. 대상의 자식으로 들어가며 클릭을 가로채지 않는다.
    ///
    /// 왜 별도 이미지인가 — Image는 색을 하나만 가진다.
    /// 바탕색과 테두리색을 따로 두려면 판이 둘 있어야 한다.
    /// </summary>
    public static Image CreateOutline(
        Image target, Color color, int radius = Radius, int thickness = 2)
    {
        Image outline = CreatePanel("Outline", target.transform, color,
            Vector2.zero, Vector2.one, radius: 0);

        outline.sprite = UISprites.RoundedOutline(radius, thickness);
        outline.type = Image.Type.Sliced;
        outline.raycastTarget = false;

        return outline;
    }

    /// <summary>
    /// 숫자 배지. 개수·수량처럼 칸 위에 얹는 작은 표시다.
    ///
    /// 어두운 알약을 깔고 그 위에 글자를 얹는 이유 —
    /// 칸 배경색이 아이템 종류마다 달라서, 글자만 올리면
    /// 밝은 칸에서 숫자가 사라진다.
    /// </summary>
    public static Text CreateBadge(
        Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax,
        int fontSize = 20, Color? textColor = null)
    {
        Image pill = CreatePanel("Badge", parent, UIPalette.Badge,
            anchorMin, anchorMax, radius: 8);

        pill.raycastTarget = false;

        Text label = CreateLabel(pill.transform, text, fontSize, FontStyle.Bold,
            new Vector2(0.04f, 0f), new Vector2(0.96f, 1f),
            TextAnchor.MiddleCenter, textColor ?? UIPalette.Text);

        // 【줄이 칸보다 크면 Unity는 글자를 아예 그리지 않는다.】
        // 배지는 높이가 20픽셀도 안 되는데 글꼴은 그보다 크다.
        // Truncate로 두면 알약만 까맣게 남고 숫자가 사라진다 — 실제로 그랬다.
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;

        label.raycastTarget = false;

        return label;
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
        Color color, UnityAction action, int fontSize = 30, int radius = Radius)
    {
        Image image = CreatePanel($"Button_{text}", parent, color, anchorMin, anchorMax, radius);

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        // 눌린 것이 보여야 한다. 색만 살짝 밝히고 되돌린다.
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.06f;
        button.colors = colors;

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
        => GetCellAnchors(index, columns, rows, padding, padding, out anchorMin, out anchorMax);

    /// <summary>
    /// 가로·세로 여백을 따로 준다.
    ///
    /// 여백은 【부모 기준 정규화】 값이다. 그래서 스크롤 내용물처럼 부모가
    /// 길어지는 격자에서 같은 값을 쓰면 세로 간격만 픽셀로 벌어진다.
    /// 부르는 쪽에서 행 수에 반비례하게 줄여 픽셀 간격을 고정한다.
    /// (InventoryScreenUI.RefreshBag)
    /// </summary>
    public static void GetCellAnchors(
        int index, int columns, int rows, float paddingX, float paddingY,
        out Vector2 anchorMin, out Vector2 anchorMax)
    {
        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);

        int column = index % columns;
        int row = index / columns;

        float cellWidth = 1f / columns;
        float cellHeight = 1f / rows;

        anchorMin = new Vector2(
            column * cellWidth + paddingX,
            1f - (row + 1) * cellHeight + paddingY);

        anchorMax = new Vector2(
            (column + 1) * cellWidth - paddingX,
            1f - row * cellHeight - paddingY);
    }

    /// <summary>
    /// 정수 슬라이더. 코드로 만들 때 필요한 자식 3종(배경·채움·손잡이)을 함께 깐다.
    ///
    /// 손잡이를 크게 잡는 이유 — 모바일에는 마우스 커서가 없다.
    /// 손가락이 덮는 넓이보다 작으면 값을 정확히 맞출 수 없다.
    /// </summary>
    public static Slider CreateIntSlider(
        Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        int min, int max, int value)
    {
        RectTransform root = CreateRegion("Slider", parent, anchorMin, anchorMax);

        var slider = root.gameObject.AddComponent<Slider>();

        CreatePanel("Background", root, UIPalette.SlotLocked,
            new Vector2(0f, 0.36f), new Vector2(1f, 0.64f));

        RectTransform fillArea = CreateRegion("Fill Area", root,
            new Vector2(0f, 0.36f), new Vector2(1f, 0.64f));

        Image fill = CreatePanel("Fill", fillArea, UIPalette.Action,
            Vector2.zero, Vector2.one);

        RectTransform handleArea = CreateRegion("Handle Slide Area", root,
            Vector2.zero, Vector2.one);

        Image handle = CreatePanel("Handle", handleArea, UIPalette.SlotSelected,
            Vector2.zero, Vector2.one);

        var handleRect = handle.rectTransform;
        handleRect.sizeDelta = new Vector2(56f, 0f);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.wholeNumbers = true;
        slider.minValue = min;
        slider.maxValue = max;
        slider.SetValueWithoutNotify(value);

        return slider;
    }

    /// <summary>숫자만 받는 입력칸. 슬라이더로 맞추기 어려운 값을 직접 친다.</summary>
    public static InputField CreateIntField(
        Transform parent, Vector2 anchorMin, Vector2 anchorMax, int value, int fontSize = 30)
    {
        Image back = CreatePanel("IntField", parent, UIPalette.SlotLocked, anchorMin, anchorMax);

        var field = back.gameObject.AddComponent<InputField>();

        Text text = CreateLabel(back.transform, string.Empty, fontSize, FontStyle.Bold,
            new Vector2(0.08f, 0f), new Vector2(0.92f, 1f), TextAnchor.MiddleCenter);

        text.supportRichText = false;

        field.targetGraphic = back;
        field.textComponent = text;
        field.contentType = InputField.ContentType.IntegerNumber;
        field.characterLimit = 6;
        field.SetTextWithoutNotify(value.ToString());

        return field;
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
