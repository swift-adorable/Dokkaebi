using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 모바일 입력 UI(듀얼 가상 조이스틱 + 액션 버튼)를 씬에 자동 생성하는 에디터 도구.
///
/// 손으로 만들면 RectTransform 앵커, 계층 순서, 참조 연결에서 실수가 나기 쉬워
/// 도구로 만들었다. 메뉴: Tools > Blob > 모바일 입력 UI 생성
/// </summary>
public static class MobileInputUIBuilder
{
    private const string CanvasName = "MobileInputCanvas";

    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    private const float JoystickBackgroundSize = 260f;
    private const float JoystickHandleSize = 110f;
    private const float ButtonSize = 170f;
    private const float EdgeMargin = 120f;

    [MenuItem("Tools/Blob/모바일 입력 UI 생성", false, 10)]
    public static void Build()
    {
        if (Object.FindAnyObjectByType<TouchInputSource>(FindObjectsInactive.Include) != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "모바일 입력 UI",
                "이미 TouchInputSource가 씬에 존재합니다.\n기존 오브젝트를 삭제하고 새로 만들까요?",
                "새로 만들기", "취소");

            if (!replace)
                return;

            var existing = Object.FindAnyObjectByType<TouchInputSource>(FindObjectsInactive.Include);
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        EnsureEventSystem();

        GameObject canvasObject = CreateCanvas();

        GameObject moveArea = CreateJoystickArea(
            canvasObject.transform, "MoveJoystickArea",
            new Vector2(0f, 0f), new Vector2(0.5f, 1f),
            out VirtualJoystick moveJoystick);

        GameObject aimArea = CreateJoystickArea(
            canvasObject.transform, "AimJoystickArea",
            new Vector2(0.5f, 0f), new Vector2(1f, 1f),
            out VirtualJoystick aimJoystick);

        // 버튼은 조이스틱 영역보다 뒤(계층상 아래)에 두어야 터치를 먼저 받는다.
        VirtualButton dashButton = CreateActionButton(
            canvasObject.transform, "DashButton", "DASH",
            new Vector2(-EdgeMargin, EdgeMargin + ButtonSize + 30f),
            new Color(0.25f, 0.65f, 1f, 0.55f));

        VirtualButton absorbButton = CreateActionButton(
            canvasObject.transform, "AbsorbButton", "ABSORB",
            new Vector2(-EdgeMargin - ButtonSize - 30f, EdgeMargin),
            new Color(0.4f, 0.9f, 0.4f, 0.55f));

        WireJoystick(moveJoystick, moveArea);
        WireJoystick(aimJoystick, aimArea);

        TouchInputSource touchSource = canvasObject.AddComponent<TouchInputSource>();
        WireTouchSource(touchSource, moveJoystick, aimJoystick, dashButton, absorbButton);

        LinkPlayerInputHandler(touchSource);

        Undo.RegisterCreatedObjectUndo(canvasObject, "Create Mobile Input UI");
        Selection.activeGameObject = canvasObject;

        EditorUtility.DisplayDialog(
            "모바일 입력 UI",
            "생성 완료.\n\n" +
            "· 좌측 절반: 이동 스틱\n" +
            "· 우측 절반: 조준 스틱 (기울이면 자동 사격)\n" +
            "· DASH / ABSORB 버튼\n\n" +
            "씬을 저장하세요 (Cmd+S).",
            "확인");

        Debug.Log("[MobileInputUIBuilder] 모바일 입력 UI 생성 완료. 씬을 저장하세요.");
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            return;

        var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
        Undo.RegisterCreatedObjectUndo(eventSystemObject, "Create EventSystem");

        // Input System 패키지가 있으면 전용 모듈이 필요하므로, 표준 모듈을 붙인 뒤
        // Unity가 자동으로 교체 제안을 띄우도록 둔다.
        eventSystemObject.AddComponent<StandaloneInputModule>();

        Debug.Log("[MobileInputUIBuilder] EventSystem이 없어 새로 생성했습니다.");
    }

    private static GameObject CreateCanvas()
    {
        var canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvasObject;
    }

    private static GameObject CreateJoystickArea(
        Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, out VirtualJoystick joystick)
    {
        var areaObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VirtualJoystick));
        areaObject.transform.SetParent(parent, false);

        var rect = areaObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // 투명하지만 터치를 받아야 하므로 raycastTarget은 켜둔다.
        var image = areaObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;

        GameObject background = CreateCircle(areaObject.transform, "Background", JoystickBackgroundSize,
            new Color(1f, 1f, 1f, 0.22f));

        CreateCircle(background.transform, "Handle", JoystickHandleSize,
            new Color(1f, 1f, 1f, 0.55f));

        joystick = areaObject.GetComponent<VirtualJoystick>();

        return areaObject;
    }

    private static GameObject CreateCircle(Transform parent, string name, float size, Color color)
    {
        var circleObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        circleObject.transform.SetParent(parent, false);

        var rect = circleObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = Vector2.zero;

        var image = circleObject.GetComponent<Image>();
        image.sprite = GetBuiltinSprite("UI/Skin/Knob.psd");
        image.color = color;
        image.raycastTarget = false;

        return circleObject;
    }

    private static VirtualButton CreateActionButton(
        Transform parent, string name, string label, Vector2 anchoredPosition, Color color)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VirtualButton));
        buttonObject.transform.SetParent(parent, false);

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);
        rect.anchoredPosition = anchoredPosition;

        var image = buttonObject.GetComponent<Image>();
        image.sprite = GetBuiltinSprite("UI/Skin/Knob.psd");
        image.color = color;
        image.raycastTarget = true;

        CreateLabel(buttonObject.transform, label);

        return buttonObject.GetComponent<VirtualButton>();
    }

    private static void CreateLabel(Transform parent, string text)
    {
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);

        var rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var label = labelObject.GetComponent<Text>();
        label.text = text;
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = 30;
        label.color = Color.white;
        label.raycastTarget = false;
        label.font = AssetDatabase.GetBuiltinExtraResource<Font>("Arial.ttf");
    }

    private static Sprite GetBuiltinSprite(string path)
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
    }

    private static void WireJoystick(VirtualJoystick joystick, GameObject areaObject)
    {
        var serialized = new SerializedObject(joystick);

        Transform background = areaObject.transform.Find("Background");
        Transform handle = background != null ? background.Find("Handle") : null;

        serialized.FindProperty("background").objectReferenceValue = background as RectTransform;
        serialized.FindProperty("handle").objectReferenceValue = handle as RectTransform;

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireTouchSource(
        TouchInputSource source,
        VirtualJoystick moveJoystick,
        VirtualJoystick aimJoystick,
        VirtualButton dashButton,
        VirtualButton absorbButton)
    {
        var serialized = new SerializedObject(source);

        serialized.FindProperty("moveJoystick").objectReferenceValue = moveJoystick;
        serialized.FindProperty("aimJoystick").objectReferenceValue = aimJoystick;
        serialized.FindProperty("dashButton").objectReferenceValue = dashButton;
        serialized.FindProperty("absorbButton").objectReferenceValue = absorbButton;

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void LinkPlayerInputHandler(TouchInputSource source)
    {
        var handler = Object.FindAnyObjectByType<PlayerInputHandler>(FindObjectsInactive.Include);

        if (handler == null)
        {
            Debug.LogWarning("[MobileInputUIBuilder] 씬에서 PlayerInputHandler를 찾지 못했습니다. " +
                             "Player 오브젝트의 Touch Source 필드를 직접 연결하세요.");
            return;
        }

        var serialized = new SerializedObject(handler);
        serialized.FindProperty("touchSource").objectReferenceValue = source;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(handler);
    }
}
