using UnityEngine;

/// <summary>
/// 흡수 가능한 시체 위에 떠오르는 컨텍스트 버튼.
///
/// 화면 구석에 상시 떠 있는 버튼은 "지금 누를 수 있는지"를 알 수 없어
/// 눌러도 반응이 없는 것처럼 느껴진다. 이 프롬프트는 대상이 있을 때만
/// 그 대상 위에 나타나므로, 버튼의 존재 자체가 곧 "지금 누를 수 있다"는 신호가 된다.
///
/// 입력 경로는 기존과 동일하다. 내부의 VirtualButton을 TouchInputSource가 읽는다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class AbsorbPrompt : MonoBehaviour
{
    [Header("Placement")]
    [Tooltip("시체 기준 월드 오프셋. 캐릭터 머리 위로 띄우는 높이.")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Animation")]
    [Tooltip("나타나고 사라지는 속도. 클수록 빠르다.")]
    [SerializeField] private float fadeSpeed = 10f;

    [Tooltip("주목을 끌기 위한 맥동 크기. 0이면 사용하지 않는다.")]
    [SerializeField] private float pulseAmount = 0.08f;

    [SerializeField] private float pulseSpeed = 4f;

    private RectTransform selfRect;
    private RectTransform canvasRect;
    private CanvasGroup canvasGroup;
    private Camera mainCamera;
    private PlayerAbsorber absorber;

    private bool isVisible;

    private void Awake()
    {
        selfRect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private void Start()
    {
        mainCamera = Camera.main;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            canvasRect = canvas.GetComponent<RectTransform>();

        absorber = FindAnyObjectByType<PlayerAbsorber>(FindObjectsInactive.Exclude);

        if (absorber == null)
            GameLogger.Error("[AbsorbPrompt] 씬에서 PlayerAbsorber를 찾지 못했습니다.", this);
    }

    private void LateUpdate()
    {
        CorpseController target = absorber != null ? absorber.NearbyCorpse : null;

        bool shouldShow = target != null && mainCamera != null && canvasRect != null;

        if (shouldShow)
            shouldShow = TryPlaceOver(target.transform.position + worldOffset);

        SetVisible(shouldShow);

        if (isVisible && pulseAmount > 0f)
        {
            float scale = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
            selfRect.localScale = new Vector3(scale, scale, 1f);
        }
    }

    /// <summary>월드 좌표를 캔버스 좌표로 변환해 배치한다. 화면 뒤쪽이면 false.</summary>
    private bool TryPlaceOver(Vector3 worldPosition)
    {
        Vector3 screenPoint = mainCamera.WorldToScreenPoint(worldPosition);

        // 카메라 뒤에 있으면 숨긴다.
        if (screenPoint.z <= 0f)
            return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPoint, null, out Vector2 localPoint))
        {
            return false;
        }

        selfRect.anchoredPosition = localPoint;

        return true;
    }

    private void SetVisible(bool visible)
    {
        isVisible = visible;

        float target = visible ? 1f : 0f;

        canvasGroup.alpha = Mathf.MoveTowards(
            canvasGroup.alpha, target, fadeSpeed * Time.unscaledDeltaTime);

        // 완전히 사라진 상태에서는 터치를 가로채지 않도록 한다.
        canvasGroup.blocksRaycasts = canvasGroup.alpha > 0.05f;

        if (!visible)
            selfRect.localScale = Vector3.one;
    }
}
