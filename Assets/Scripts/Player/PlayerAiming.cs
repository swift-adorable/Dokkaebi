using UnityEngine;

/// <summary>
/// 플레이어 회전 전담. 이동 방향과 완전히 분리된 조준 방향을 바라본다.
///
/// Rigidbody를 가진 오브젝트이므로 transform.rotation을 직접 쓰지 않고
/// rb.MoveRotation으로 물리 엔진을 경유한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerAiming : MonoBehaviour
{
    [Header("Aiming")]
    [Tooltip("조준 입력이 이 값보다 작으면 회전하지 않는다. (미세 떨림 무시)")]
    [Range(0f, 1f)]
    [SerializeField] private float aimDeadzone = 0.1f;

    [Tooltip("초당 회전 각도. 0이면 즉시 회전.")]
    [SerializeField] private float rotationSpeed = 900f;

    private Rigidbody rb;
    private Quaternion targetRotation;
    private bool hasTarget;

    /// <summary>현재 바라보는 방향(XZ 평면).</summary>
    public Vector3 FacingDirection => transform.forward;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        targetRotation = transform.rotation;
    }

    /// <summary>
    /// 조준 입력을 갱신한다. 데드존 미만이면 목표를 갱신하지 않으므로
    /// 캐릭터는 마지막으로 바라보던 방향을 유지한다.
    /// </summary>
    public void SetAim(Vector2 aimInput)
    {
        if (aimInput.sqrMagnitude < aimDeadzone * aimDeadzone)
            return;

        targetRotation = Quaternion.LookRotation(new Vector3(aimInput.x, 0f, aimInput.y));
        hasTarget = true;
    }

    private void FixedUpdate()
    {
        if (!hasTarget)
            return;

        if (rotationSpeed <= 0f)
        {
            rb.MoveRotation(targetRotation);
            return;
        }

        Quaternion next = Quaternion.RotateTowards(
            rb.rotation,
            targetRotation,
            rotationSpeed * Time.fixedDeltaTime);

        rb.MoveRotation(next);
    }
}
