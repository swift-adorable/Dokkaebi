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

    /// <summary>
    /// 이동 속도 배율. 장비의 기동 옵션과 과중량이 곱해진 값이다.
    /// PlayerLoadout이 넣어준다. (docs/Blob_Equipment_System.md 「무게와 칸」)
    /// </summary>
    public float SpeedScale { get; set; } = 1f;

    /// <summary>배율이 적용된 실제 이동 속도.</summary>
    /// <summary>
    /// 실제 속도 = 기본 × 장비·생존(SpeedScale) × 상태이상.
    ///
    /// 【두 축을 나눠 둔다.】 SpeedScale은 PlayerLoadout이 0.25초마다
    /// 장비·과중량·탈수를 합쳐 쓴다 — 드물게 바뀌는 것들이다.
    /// 상태이상(냉각·가속·쇠약·동결)은 초 단위로 바뀌므로 여기서 매 프레임
    /// 읽는다. 한 축에 섞으면 냉각이 0.25초 늦게 풀리거나, 반대로 장비
    /// 합산을 매 프레임 하게 된다.
    ///
    /// 이 곱셈이 없던 동안 냉각의 감속도 동결의 정지도, 8-C의 가속·쇠약도
    /// 플레이어에게 전혀 닿지 않았다. 상태 줄에만 떠 있었다.
    /// </summary>
    public float EffectiveSpeed => moveSpeed * Mathf.Max(0f, SpeedScale) * StatusScale;

    /// <summary>상태이상이 곱하는 속도. 행동 불능이면 0이다.</summary>
    public float StatusScale => health != null ? health.Status.SpeedMultiplier : 1f;

    private Health health;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<Health>();
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

        float speed = EffectiveSpeed;

        // y축 속도는 보존한다. 중력이나 외력의 수직 성분을 덮어쓰지 않기 위함이다.
        rb.linearVelocity = new Vector3(
            direction.x * speed,
            rb.linearVelocity.y,
            direction.z * speed);
    }

    /// <summary>수평 속도를 즉시 0으로 만든다.</summary>
    public void StopHorizontal()
    {
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }
}
