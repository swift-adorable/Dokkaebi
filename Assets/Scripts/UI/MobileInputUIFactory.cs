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
    private const float ButtonSize = 190f;
    private const float EdgeMargin = 140f;

    private static Sprite circleSprite;

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

        // 상단 15%는 비워둔다. 노치 영역과 기존 UI(DEBUG 버튼 등)를 가리지 않기 위함이다.
        const float areaTop = 0.85f;

        VirtualJoystick moveJoystick = CreateJoystick(
            canvasObject.transform, "MoveJoystickArea", new Vector2(0f, 0f), new Vector2(0.5f, areaTop));

        VirtualJoystick aimJoystick = CreateJoystick(
            canvasObject.transform, "AimJoystickArea", new Vector2(0.5f, 0f), new Vector2(1f, areaTop));

        // 버튼은 조이스틱 영역보다 나중에 생성해야 위에 그려지고 터치를 먼저 받는다.
        VirtualButton dashButton = CreateButton(
            canvasObject.transform, "DashButton",
            new Vector2(-EdgeMargin, EdgeMargin + ButtonSize + 40f),
            new Color(0.25f, 0.6f, 1f, 0.5f));

        VirtualButton absorbButton = CreateButton(
            canvasObject.transform, "AbsorbButton",
            new Vector2(-EdgeMargin - ButtonSize - 40f, EdgeMargin),
            new Color(0.35f, 0.85f, 0.4f, 0.5f));

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

    private static VirtualButton CreateButton(
        Transform parent, string name, Vector2 anchoredPosition, Color color)
    {
        GameObject buttonObject = CreateUIObject(name, parent);

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);
        rect.anchoredPosition = anchoredPosition;

        var image = buttonObject.AddComponent<Image>();
        image.sprite = GetCircleSprite();
        image.color = color;
        image.raycastTarget = true;

        return buttonObject.AddComponent<VirtualButton>();
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var uiObject = new GameObject(name);
        uiObject.AddComponent<RectTransform>();
        uiObject.transform.SetParent(parent, false);

        return uiObject;
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
