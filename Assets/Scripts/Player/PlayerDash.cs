using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어 대시 전담.
///
/// 기획 확정 사항: 대시는 조준 방향이 아니라 '현재 이동 중인 방향'으로 수행한다.
///
/// 이전 구현은 transform.position을 직접 Lerp하여 물리 충돌을 건너뛰었고
/// 그 결과 벽을 관통했다. 이제 Rigidbody 속도를 부여하는 방식으로 바꿔
/// 충돌 판정이 정상적으로 작동한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerDash : MonoBehaviour
{
    [Header("Dash")]
    [Tooltip("대시로 이동하려는 거리(m). 벽에 막히면 실제 이동은 짧아진다.")]
    [SerializeField] private float dashDistance = 5f;

    [Tooltip("대시 지속 시간(초)")]
    [SerializeField] private float dashDuration = 0.15f;

    [Tooltip("대시 재사용 대기시간(초)")]
    [SerializeField] private float dashCooldown = 1f;

    private Rigidbody rb;
    private PlayerMovement movement;
    private CooldownTimer cooldown;

    /// <summary>현재 대시 중인지.</summary>
    public bool IsDashing { get; private set; }

    /// <summary>대시 속도(m/s). 거리와 지속시간에서 유도된다.</summary>
    public float DashSpeed => dashDuration > 0f ? dashDistance / dashDuration : 0f;

    /// <summary>남은 쿨다운(초).</summary>
    public float RemainingCooldown => cooldown.RemainingTime(Time.unscaledTime);

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        movement = GetComponent<PlayerMovement>();
    }

    /// <summary>
    /// 주어진 방향으로 대시를 시도한다. 방향이 0이거나 쿨다운 중이면 false.
    /// </summary>
    public bool TryDash(Vector3 direction)
    {
        if (IsDashing)
            return false;

        if (direction.sqrMagnitude <= 0f)
            return false;

        // 쿨다운은 Time.unscaledTime 기준이다.
        // Time.time을 쓰면 일시정지(timeScale 0) 동안 쿨다운이 흐르지 않는다.
        if (!cooldown.TryConsume(Time.unscaledTime, dashCooldown + dashDuration))
            return false;

        StartCoroutine(DashRoutine(direction.normalized));

        return true;
    }

    private IEnumerator DashRoutine(Vector3 direction)
    {
        IsDashing = true;

        // 이동 컴포넌트가 속도를 덮어쓰지 않도록 잠근다.
        movement.IsLocked = true;

        float elapsed = 0f;
        float speed = DashSpeed;

        while (elapsed < dashDuration)
        {
            // 속도를 매 물리 프레임 재설정한다.
            // 충돌로 속도가 깎여도 남은 시간 동안 계속 밀어붙이기 위함이다.
            rb.linearVelocity = new Vector3(
                direction.x * speed,
                rb.linearVelocity.y,
                direction.z * speed);

            yield return new WaitForFixedUpdate();

            elapsed += Time.fixedDeltaTime;
        }

        movement.IsLocked = false;
        movement.StopHorizontal();

        IsDashing = false;
    }
}
