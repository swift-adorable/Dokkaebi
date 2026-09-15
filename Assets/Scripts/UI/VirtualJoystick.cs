using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 다이나믹 가상 조이스틱.
///
/// 지정된 영역(RectTransform) 안 아무 곳이나 터치하면 그 지점에 조이스틱이 생성된다.
/// 손가락이 어디 있는지 보지 않아도 조작할 수 있어 실전 조작감이 고정형보다 낫다.
///
/// 멀티터치: pointerId로 자기 손가락만 추적하므로 좌/우 스틱 동시 조작이 가능하다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Visual")]
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;

    [Header("Behaviour")]
    [Tooltip("핸들이 중심에서 벗어날 수 있는 최대 거리(px). 이 거리에서 입력값 1.0")]
    [SerializeField] private float handleRange = 90f;

    [Tooltip("이 값 미만의 기울기는 0으로 처리한다. (손떨림 무시)")]
    [Range(0f, 0.5f)]
    [SerializeField] private float deadzone = 0.15f;

    [Tooltip("터치하지 않을 때 조이스틱을 숨긴다.")]
    [SerializeField] private bool hideWhenIdle = true;

    /// <summary>현재 입력값. 크기 0~1.</summary>
    public Vector2 Value { get; private set; }

    /// <summary>현재 눌려 있는지 여부.</summary>
    public bool IsPressed { get; private set; }

    private RectTransform areaRect;
    private Canvas parentCanvas;
    private int activePointerId = InvalidPointerId;

    private const int InvalidPointerId = -999;

    /// <summary>런타임 생성 시 참조를 주입한다. (Awake 이전에 호출해야 한다)</summary>
    public void Initialize(RectTransform backgroundRect, RectTransform handleRect, float range)
    {
        background = backgroundRect;
        handle = handleRect;
        handleRange = range;
    }

    private void Awake()
    {
        areaRect = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();

        if (background == null || handle == null)
        {
            GameLogger.Error("[VirtualJoystick] background 또는 handle이 할당되지 않았습니다.", this);
            enabled = false;
            return;
        }

        SetVisible(!hideWhenIdle);
    }

    private void OnDisable()
    {
        ResetJoystick();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // 이미 다른 손가락이 이 스틱을 점유 중이면 무시한다.
        if (IsPressed)
            return;

        activePointerId = eventData.pointerId;
        IsPressed = true;

        if (TryGetLocalPoint(eventData, out Vector2 localPoint))
        {
            // 터치한 지점에 조이스틱을 생성한다. (다이나믹 방식의 핵심)
            background.anchoredPosition = localPoint;
        }

        handle.anchoredPosition = Vector2.zero;
        Value = Vector2.zero;

        SetVisible(true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId)
            return;

        if (!TryGetLocalPoint(eventData, out Vector2 localPoint))
            return;

        Vector2 delta = localPoint - background.anchoredPosition;
        Vector2 clamped = Vector2.ClampMagnitude(delta, handleRange);

        handle.anchoredPosition = clamped;

        Vector2 raw = clamped / handleRange;

        Value = raw.magnitude < deadzone ? Vector2.zero : raw;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId)
            return;

        ResetJoystick();
    }

    private void ResetJoystick()
    {
        activePointerId = InvalidPointerId;
        IsPressed = false;
        Value = Vector2.zero;

        if (handle != null)
            handle.anchoredPosition = Vector2.zero;

        if (hideWhenIdle)
            SetVisible(false);
    }

    private bool TryGetLocalPoint(PointerEventData eventData, out Vector2 localPoint)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            areaRect,
            eventData.position,
            GetEventCamera(),
            out localPoint);
    }

    private Camera GetEventCamera()
    {
        if (parentCanvas == null)
            return null;

        // Screen Space - Overlay 모드에서는 카메라를 null로 넘겨야 좌표가 맞는다.
        return parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : parentCanvas.worldCamera;
    }

    private void SetVisible(bool isVisible)
    {
        if (background != null)
            background.gameObject.SetActive(isVisible);
    }
}
