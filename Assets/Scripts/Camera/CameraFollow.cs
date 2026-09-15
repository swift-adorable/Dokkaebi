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

    private Vector3 offset;
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
        isReady = true;
    }

    private void LateUpdate()
    {
        if (!isReady || target == null)
            return;

        Vector3 desiredPosition = target.position + offset;

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
