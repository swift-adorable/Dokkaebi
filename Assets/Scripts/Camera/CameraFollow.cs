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

    private void Start()
    {
        if (target == null)
        {
            GameLogger.Error("[CameraFollow] target이 할당되지 않았습니다.", this);
            return;
        }

        offset = transform.position - target.position;
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

        if (smoothTime <= 0f)
        {
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
