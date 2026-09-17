using UnityEngine;

/// <summary>
/// 흡수 버튼. DASH 버튼 옆 고정 위치에 있으며, 흡수할 대상이 있을 때만 켜진다.
///
/// 이전에는 시체 위에 떠오르는 월드 추적 프롬프트였으나 바꿨다.
/// 시체 위에 뜨면 버튼 위치가 매번 달라져서 엄지를 옮겨야 하고,
/// 시체가 화면 가장자리나 다른 UI 아래에 있으면 누르기 어렵다.
/// 조작 버튼은 항상 같은 자리에 있어야 손이 기억한다.
///
/// "지금 누를 수 있는지"는 위치 대신 【표시 여부】로 알린다.
/// 대상이 없으면 완전히 사라지고 터치도 받지 않으므로,
/// 버튼이 보인다는 것 자체가 곧 "지금 누를 수 있다"는 신호다.
///
/// 입력 경로는 그대로다. 내부의 VirtualButton을 TouchInputSource가 읽는다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class AbsorbPrompt : MonoBehaviour
{
    [Header("Animation")]
    [Tooltip("나타나고 사라지는 속도. 클수록 빠르다.")]
    [SerializeField] private float fadeSpeed = 12f;

    [Tooltip("주목을 끌기 위한 맥동 크기. 0이면 사용하지 않는다.")]
    [SerializeField] private float pulseAmount = 0.06f;

    [SerializeField] private float pulseSpeed = 4f;

    private RectTransform selfRect;
    private CanvasGroup canvasGroup;
    private PlayerAbsorber absorber;

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
        absorber = FindAnyObjectByType<PlayerAbsorber>(FindObjectsInactive.Exclude);

        if (absorber == null)
            GameLogger.Error("[AbsorbPrompt] 씬에서 PlayerAbsorber를 찾지 못했습니다.", this);
    }

    private void Update()
    {
        bool canAbsorb = absorber != null && absorber.CanAbsorb;

        float target = canAbsorb ? 1f : 0f;

        canvasGroup.alpha = Mathf.MoveTowards(
            canvasGroup.alpha, target, fadeSpeed * Time.unscaledDeltaTime);

        // 거의 보일 때만 터치를 받는다. 투명한 버튼이 터치를 먹으면
        // 뒤에 있는 조이스틱이 반응하지 않는다.
        bool interactive = canvasGroup.alpha > 0.5f;

        canvasGroup.blocksRaycasts = interactive;
        canvasGroup.interactable = interactive;

        UpdatePulse(canAbsorb);
    }

    /// <summary>누를 수 있을 때만 맥동시켜 시선을 끈다.</summary>
    private void UpdatePulse(bool canAbsorb)
    {
        if (pulseAmount <= 0f || selfRect == null)
            return;

        float scale = canAbsorb
            ? 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount
            : 1f;

        selfRect.localScale = new Vector3(scale, scale, 1f);
    }
}
