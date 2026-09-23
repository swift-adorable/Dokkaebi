using UnityEngine;

/// <summary>
/// 자기 RectTransform을 화면의 안전 영역(Safe Area)에 맞춘다.
///
/// 【왜 필요한가】
/// 노치·다이내믹 아일랜드·홈 인디케이터·둥근 모서리는 화면의 일부를 먹는다.
/// 0~1 앵커로 화면 끝에 붙인 UI는 그 밑에 깔려 보이지 않거나 눌리지 않는다.
/// 좌상단 골드와 우상단 가방 버튼, 하단 퀵슬롯이 정확히 그 자리다.
///
/// 【왜 매 프레임이 아닌가】
/// safeArea는 회전과 분할 화면에서만 바뀐다. 매 프레임 앵커를 다시 쓰면
/// 레이아웃이 매번 더럽혀져 자식 전체가 다시 계산된다.
/// 값이 실제로 바뀐 프레임에만 적용한다.
///
/// 【에디터에서도 돈다】
/// Game 뷰의 해상도를 바꾸거나 Device Simulator로 기기를 바꾸면 즉시 따라간다.
/// 그래야 실제 기기에 올리기 전에 잘리는지 확인할 수 있다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class SafeAreaFitter : MonoBehaviour
{
    private RectTransform self;

    private Rect lastSafeArea;
    private Vector2Int lastScreen;

    /// <summary>
    /// 안전 영역 위에 추가로 두는 여백(참조 해상도 픽셀).
    /// 둥근 모서리는 safeArea가 알려 주지 않는 경우가 있어 조금 더 들여 둔다.
    /// </summary>
    [SerializeField] private float extraMargin = 12f;

    private void Awake()
    {
        self = GetComponent<RectTransform>();

        Apply(force: true);
    }

    private void Update() => Apply(force: false);

    private void Apply(bool force)
    {
        Rect safe = Screen.safeArea;
        var screen = new Vector2Int(Screen.width, Screen.height);

        if (!force && safe == lastSafeArea && screen == lastScreen)
            return;

        lastSafeArea = safe;
        lastScreen = screen;

        if (screen.x <= 0 || screen.y <= 0)
            return;

        Vector2 min = safe.position;
        Vector2 max = safe.position + safe.size;

        // 여백은 화면 픽셀 기준으로 환산한다. 캔버스 스케일러가 이미 곱해져 있으므로
        // 참조 해상도 값을 그대로 빼면 기기마다 다른 크기가 된다.
        float scale = screen.y / 1080f;
        float margin = extraMargin * Mathf.Max(0.1f, scale);

        min.x = Mathf.Max(min.x + margin, 0f);
        min.y = Mathf.Max(min.y + margin, 0f);
        max.x = Mathf.Min(max.x - margin, screen.x);
        max.y = Mathf.Min(max.y - margin, screen.y);

        min.x /= screen.x;
        min.y /= screen.y;
        max.x /= screen.x;
        max.y /= screen.y;

        self.anchorMin = min;
        self.anchorMax = max;
        self.offsetMin = Vector2.zero;
        self.offsetMax = Vector2.zero;
    }
}
