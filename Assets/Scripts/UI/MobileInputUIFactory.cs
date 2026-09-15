using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 모바일 입력 UI를 런타임에 코드로 생성한다.
///
/// 씬에 UI를 미리 배치하는 방식은 '씬 저장을 잊으면 빌드에 반영되지 않는' 실패 지점이 있다.
/// 이 팩토리는 코드에만 의존하므로 빌드에 항상 포함되며, 스프라이트도 절차적으로 만들어
/// 외부 에셋 의존이 전혀 없다.
/// </summary>
public static class MobileInputUIFactory
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    private const float BackgroundSize = 300f;
    private const float HandleSize = 130f;
    private const float HandleRange = 110f;

    private const float DashButtonSize = 190f;
    private const float AbsorbPromptSize = 150f;
    private const float EdgeMargin = 140f;

    /// <summary>
    /// DASH 버튼 위치. 우하단 앵커 기준이며 x는 왼쪽으로, y는 위로 갈수록 값이 커진다.
    /// 코너에 딱 붙이면 엄지가 화면 모서리에 걸리므로 살짝 안쪽으로 들여 배치한다.
    /// </summary>
    private static readonly Vector2 DashButtonPosition =
        new Vector2(-(EdgeMargin + 60f), EdgeMargin + 60f);

    /// <summary>조이스틱 영역 상단 여백. 노치와 기존 UI를 피한다.</summary>
    private const float JoystickAreaTop = 0.85f;

    private static Sprite circleSprite;
    private static Font uiFont;

    /// <summary>모바일 입력 UI 전체를 생성하고 TouchInputSource를 반환한다.</summary>
    public static TouchInputSource Create()
    {
        EnsureEventSystem();

        GameObject canvasObject = new GameObject("MobileInputCanvas (Runtime)");

        // AddComponent는 활성 오브젝트에서 Awake를 즉시 호출한다.
        // 참조 주입(Initialize)이 끝나기 전에 Awake가 돌면 null 검사에 걸리므로
        // 구성이 끝날 때까지 비활성 상태로 둔다.
        canvasObject.SetActive(false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // 기존 디버그/게임 UI가 위에 오도록 낮은 정렬 순서를 쓴다.
        canvas.sortingOrder = -100;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        VirtualJoystick moveJoystick = CreateJoystick(
            canvasObject.transform, "MoveJoystickArea",
            new Vector2(0f, 0f), new Vector2(0.5f, JoystickAreaTop));

        VirtualJoystick aimJoystick = CreateJoystick(
            canvasObject.transform, "AimJoystickArea",
            new Vector2(0.5f, 0f), new Vector2(1f, JoystickAreaTop));

        // 버튼류는 조이스틱 영역보다 나중에 만들어야 위에 그려지고 터치를 먼저 받는다.
        VirtualButton dashButton = CreateDashButton(canvasObject.transform);

        // 흡수는 화면 구석 고정이 아니라, 대상 시체 위에 떠오르는 컨텍스트 버튼이다.
        VirtualButton absorbButton = CreateAbsorbPrompt(canvasObject.transform);

        var touchSource = canvasObject.AddComponent<TouchInputSource>();
        touchSource.Initialize(moveJoystick, aimJoystick, dashButton, absorbButton);

        // 모든 참조 주입이 끝난 뒤 활성화한다.
        canvasObject.SetActive(true);

        GameLogger.Log("[MobileInputUIFactory] 모바일 입력 UI를 런타임 생성했습니다.");

        return touchSource;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Exclude) != null)
            return;

        var eventSystemObject = new GameObject("EventSystem (Runtime)");
        eventSystemObject.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif

        GameLogger.Log("[MobileInputUIFactory] EventSystem이 없어 런타임 생성했습니다.");
    }

    private static VirtualJoystick CreateJoystick(
        Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject areaObject = CreateUIObject(name, parent);

        var areaRect = areaObject.GetComponent<RectTransform>();
        areaRect.anchorMin = anchorMin;
        areaRect.anchorMax = anchorMax;
        areaRect.offsetMin = Vector2.zero;
        areaRect.offsetMax = Vector2.zero;

        // 투명하지만 터치는 받아야 한다.
        var areaImage = areaObject.AddComponent<Image>();
        areaImage.color = new Color(1f, 1f, 1f, 0f);
        areaImage.raycastTarget = true;

        RectTransform background = CreateCircle(areaObject.transform, "Background",
            BackgroundSize, new Color(1f, 1f, 1f, 0.20f));

        RectTransform handle = CreateCircle(background, "Handle",
            HandleSize, new Color(1f, 1f, 1f, 0.55f));

        var joystick = areaObject.AddComponent<VirtualJoystick>();
        joystick.Initialize(background, handle, HandleRange);

        return joystick;
    }

    private static VirtualButton CreateDashButton(Transform parent)
    {
        GameObject buttonObject = CreateUIObject("DashButton", parent);

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(DashButtonSize, DashButtonSize);
        rect.anchoredPosition = DashButtonPosition;

        var image = buttonObject.AddComponent<Image>();
        image.sprite = GetCircleSprite();
        image.color = new Color(0.25f, 0.6f, 1f, 0.5f);
        image.raycastTarget = true;

        CreateLabel(buttonObject.transform, "DASH", 34);

        return buttonObject.AddComponent<VirtualButton>();
    }

    /// <summary>시체 위에 떠오르는 흡수 프롬프트. 평소에는 투명하고 터치도 받지 않는다.</summary>
    private static VirtualButton CreateAbsorbPrompt(Transform parent)
    {
        GameObject promptObject = CreateUIObject("AbsorbPrompt", parent);

        var rect = promptObject.GetComponent<RectTransform>();
        // 월드 좌표를 캔버스 로컬 좌표로 변환해 배치하므로 중앙 앵커를 사용한다.
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(AbsorbPromptSize, AbsorbPromptSize);

        var image = promptObject.AddComponent<Image>();
        image.sprite = GetCircleSprite();
        image.color = new Color(0.35f, 0.9f, 0.45f, 0.8f);
        image.raycastTarget = true;

        CreateLabel(promptObject.transform, "ABSORB", 26);

        promptObject.AddComponent<CanvasGroup>();

        var button = promptObject.AddComponent<VirtualButton>();

        promptObject.AddComponent<AbsorbPrompt>();

        return button;
    }

    private static RectTransform CreateCircle(Transform parent, string name, float size, Color color)
    {
        GameObject circleObject = CreateUIObject(name, parent);

        var rect = circleObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = Vector2.zero;

        var image = circleObject.AddComponent<Image>();
        image.sprite = GetCircleSprite();
        image.color = color;
        image.raycastTarget = false;

        return rect;
    }

    private static void CreateLabel(Transform parent, string text, int fontSize)
    {
        Font font = GetUIFont();

        if (font == null)
            return;

        GameObject labelObject = CreateUIObject("Label", parent);

        var rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var label = labelObject.AddComponent<Text>();
        label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var uiObject = new GameObject(name);
        uiObject.AddComponent<RectTransform>();
        uiObject.transform.SetParent(parent, false);

        return uiObject;
    }

    private static Font GetUIFont()
    {
        if (uiFont != null)
            return uiFont;

        // Unity 2022 이후 내장 폰트 이름. 구버전 대비로 Arial도 시도한다.
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (uiFont == null)
            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        if (uiFont == null)
            GameLogger.Warning("[MobileInputUIFactory] 내장 폰트를 찾지 못해 버튼 라벨을 생략합니다.");

        return uiFont;
    }

    /// <summary>원형 스프라이트를 절차적으로 생성한다. (외부 에셋 의존 제거)</summary>
    private static Sprite GetCircleSprite()
    {
        if (circleSprite != null)
            return circleSprite;

        const int size = 128;
        const float radius = size * 0.5f;

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - radius + 0.5f;
                float dy = y - radius + 0.5f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                // 가장자리 1.5px을 부드럽게 처리해 계단 현상을 없앤다.
                float alpha = Mathf.Clamp01((radius - distance) / 1.5f);

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        circleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f);

        circleSprite.hideFlags = HideFlags.HideAndDontSave;

        return circleSprite;
    }
}
