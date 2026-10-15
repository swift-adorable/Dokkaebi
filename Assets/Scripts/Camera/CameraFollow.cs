using UnityEngine;

/// <summary>
/// 지정한 대상을 일정 오프셋으로 따라다니는 카메라.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Follow")]
    [Tooltip("0이면 즉시 추적, 값이 클수록 부드럽게 따라온다.")]
    [SerializeField] private float smoothTime = 0.12f;

    /// <summary>【시험용】 따라가는 거리 배율 — 1이면 원래, 작을수록 가까이 (결정 2-115 픽셀 캐릭터 시험).</summary>
    public static float ZoomScale = 1f;

    private Vector3 offset;
    private Camera cam;
    private float baseOrthoSize;
    private Vector3 currentVelocity;
    private bool isReady;

    private bool snapped;

    /// <summary>
    /// 【거리는 Awake에서 잰다】 (결정 2-130) — 씬에 놓인 그대로의 카메라 · 플레이어 거리.
    /// Start에서 재면, 구역 출발 자리로 플레이어를 옮기는 ZoneMap.Start가 먼저 돌았을 때
    /// 옮겨진 뒤의 거리를 재어 카메라가 엉뚱한 곳을 따라갔다 — 0-1 대숲 밤길 등에서 주인공이 화면 밖에 있었다.
    /// </summary>
    private void Awake()
    {
        if (target != null)
            offset = transform.position - target.position;
    }

    private void Start()
    {
        if (target == null)
        {
            GameLogger.Error("[CameraFollow] target이 할당되지 않았습니다.", this);
            return;
        }

        if (TryGetComponent(out cam) && cam.orthographic)
            baseOrthoSize = cam.orthographicSize;   // 2.5D 정사영 카메라(결정 2-127) — 확대는 거리 대신 보이는 크기로
        isReady = true;
    }

    private void LateUpdate()
    {
        if (!isReady || target == null)
            return;

        if (cam != null && cam.orthographic && baseOrthoSize > 0f)
            cam.orthographicSize = baseOrthoSize * ZoomScale;

        Vector3 desiredPosition = target.position + offset * ZoomScale;

        // 첫 프레임은 바로 붙는다 — 출발 자리가 멀면 씬 원점에서 날아오는 것이 보이지 않게.
        if (smoothTime <= 0f || !snapped)
        {
            snapped = true;
            currentVelocity = Vector3.zero;
            transform.position = desiredPosition;
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref currentVelocity,
            smoothTime
        );
    }
}
