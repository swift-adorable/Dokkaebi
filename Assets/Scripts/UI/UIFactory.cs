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

    // ── 여백 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 화면 어디서나 쓰는 여백 한 칸. 【캔버스 기준 픽셀】이다.
    ///
    /// 【정규화(0~1) 여백을 쓰지 않는 이유】
    /// 앵커 여백 0.02는 「부모 폭의 2%」라서, 같은 숫자를 세로로도 쓰면
    /// 세로로 긴 칸에서는 훨씬 두꺼워 보인다. 이 화면의 우측 패널은
    /// 세로가 가로의 약 3배라, 가로 0.020 / 세로 0.028을 주면 실제로는
    /// 세로 여백이 가로의 네 배가 넘는다. 눈에 보이는 폭을 맞추려면
    /// 비율이 아니라 픽셀로 줘야 하고, 그것이 offsetMin/offsetMax다.
    /// 캔버스 배율은 가로·세로가 같으므로 이 값은 어느 축에서나 같은 두께다.
    /// </summary>
    public const float Gap = 26f;

    /// <summary>rect의 네 변을 각각 픽셀만큼 안으로 들인다. 앵커는 건드리지 않는다.</summary>
    public static RectTransform Inset(
        RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.offsetMin = new Vector2(rect.offsetMin.x + left, rect.offsetMin.y + bottom);
        rect.offsetMax = new Vector2(rect.offsetMax.x - right, rect.offsetMax.y - top);

        return rect;
    }

    /// <summary>네 변을 같은 픽셀만큼 들인다.</summary>
    public static RectTransform Inset(RectTransform rect, float all)
        => Inset(rect, all, all, all, all);

    /// <summary>
    /// 앵커로 자른 칸을 만들고, 지정한 변만 반 칸(Gap/2)씩 안으로 들인다.
    ///
    /// 두 칸이 맞닿는 자리에서 양쪽이 반 칸씩 물러나면 사이 간격이 정확히
    /// 한 칸이 된다. 바깥 테두리도 한 칸이므로 전부 같은 두께로 보인다.
    /// </summary>
    public static RectTransform CreateSlice(
        string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
    {
        RectTransform rect = CreateRegion(name, parent, anchorMin, anchorMax);

        return Inset(rect, left, bottom, right, top);
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
    /// 유리판. 반투명 바탕 + 밝은 테두리.
    ///
    /// 【광택(Sheen)을 뺀 이유】
    /// 처음에는 위쪽 절반에 흰 막을 깔아 「빛 받은 면」을 만들었다.
    /// 판이 반투명(0.42)일 때는 은은했지만, 글자가 안 읽혀 판을 0.80까지
    /// 올리자 그 막이 **회색 띠**로 굳어 버렸다. 패널마다 위쪽에
    /// 정체를 알 수 없는 회색 막대가 걸린 것처럼 보였다.
    ///
    /// 광택은 「바탕이 비칠 때만」 광택으로 읽힌다. 불투명한 판 위에서는
    /// 그냥 다른 색 사각형이다. 유리의 인상은 테두리가 만들게 두고 광택은 버린다.
    ///
    /// 【진짜 블러가 아닌 이유】
    /// ScreenSpaceOverlay 캔버스는 뒤 화면을 텍스처로 받을 수 없어서
    /// 실제 배경 흐림은 별도 카메라와 셰이더가 필요하다.
    /// </summary>
    public static Image CreateGlass(
        string name, Transform parent, Color tint,
        Vector2 anchorMin, Vector2 anchorMax, int radius = RadiusLarge,
        int rimThickness = 2)
    {
        Image body = CreatePanel(name, parent, tint, anchorMin, anchorMax, radius);

        CreateOutline(body, UIPalette.Rim, radius, rimThickness);

        return body;
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

        // 숫자는 줄바꿈하지 않는다. 「12」가 「1 / 2」로 갈리면 안 된다.
        label.horizontalOverflow = HorizontalWrapMode.Overflow;

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

        // 【Truncate로 두지 않는다.】
        // Unity Text는 한 줄이 칸보다 크면 그 줄을 **아예 그리지 않는다.**
        // 잘라서 보여 주는 것이 아니라 통째로 사라진다.
        // 개수 배지와 패시브 계열의 진행도가 그렇게 없어졌고,
        // 「표시가 안 된다」와 「값이 없다」를 구분할 수 없었다.
        // 넘치더라도 보이는 편이 낫다 — 넘치면 눈에 띄어 고치게 된다.
        label.verticalOverflow = VerticalWrapMode.Overflow;

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
        const float HandleDiameter = 56f;

        RectTransform root = CreateRegion("Slider", parent, anchorMin, anchorMax);

        var slider = root.gameObject.AddComponent<Slider>();

        CreatePanel("Background", root, UIPalette.SlotLocked,
            new Vector2(0f, 0.36f), new Vector2(1f, 0.64f));

        RectTransform fillArea = CreateRegion("Fill Area", root,
            new Vector2(0f, 0.36f), new Vector2(1f, 0.64f));

        Image fill = CreatePanel("Fill", fillArea, UIPalette.Action,
            Vector2.zero, Vector2.one);

        // 【손잡이가 슬라이더 밖으로 나가지 않게 한다.】
        // 미끄럼 영역을 0~1로 두면 최댓값에서 손잡이 중심이 오른쪽 끝에 붙어
        // 반지름만큼 밖으로 삐져나간다. 옆에 붙은 입력칸을 덮던 원인이다.
        // 좌우를 반지름만큼 들여서, 손잡이는 어느 값에서도 안쪽에 머문다.
        //
        // 세로도 손잡이 지름으로 고정한다. Slider가 손잡이의 세로 앵커를
        // 0~1로 덮어쓰기 때문에, 영역이 높으면 손잡이가 길쭉해져 원이 안 된다.
        RectTransform handleArea = CreateRegion("Handle Slide Area", root,
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));

        handleArea.sizeDelta = new Vector2(-HandleDiameter, HandleDiameter);

        Image handle = CreatePanel("Handle", handleArea, UIPalette.SlotSelected,
            Vector2.zero, Vector2.one, radius: 0);

        // 동그란 손잡이. 반지름이 지름의 절반인 둥근 사각형은 곧 원이다.
        handle.sprite = UISprites.Rounded(Mathf.RoundToInt(HandleDiameter * 0.5f));
        handle.type = Image.Type.Simple;

        var handleRect = handle.rectTransform;
        handleRect.sizeDelta = new Vector2(HandleDiameter, 0f);

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
