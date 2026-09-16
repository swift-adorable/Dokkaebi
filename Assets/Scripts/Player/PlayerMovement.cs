using UnityEngine;

/// <summary>
/// 플레이어 이동 전담. Rigidbody 속도 기반이며 물리 갱신 주기에 맞춰 적용한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody rb;
    private Vector2 input;

    /// <summary>현재 입력이 가리키는 이동 방향(XZ 평면, 정규화 전).</summary>
    public Vector3 MoveDirection => new Vector3(input.x, 0f, input.y);

    /// <summary>true인 동안 이동 입력을 무시한다. 대시가 속도를 점유할 때 사용한다.</summary>
    public bool IsLocked { get; set; }

    public float MoveSpeed => moveSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    /// <summary>입력을 갱신한다. 실제 물리 적용은 FixedUpdate에서 일어난다.</summary>
    public void SetInput(Vector2 moveInput)
    {
        input = moveInput;
    }

    private void FixedUpdate()
    {
        if (IsLocked)
            return;

        Vector3 direction = MoveDirection;

        // y축 속도는 보존한다. 중력이나 외력의 수직 성분을 덮어쓰지 않기 위함이다.
        rb.linearVelocity = new Vector3(
            direction.x * moveSpeed,
            rb.linearVelocity.y,
            direction.z * moveSpeed);
    }

    /// <summary>수평 속도를 즉시 0으로 만든다.</summary>
    public void StopHorizontal()
    {
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }
}
