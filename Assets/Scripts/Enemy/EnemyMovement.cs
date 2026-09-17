using UnityEngine;

/// <summary>
/// 적 추격 이동.
///
/// NavMesh를 쓰지 않고 직접 추격한다. 현재 맵에 장애물이 없고,
/// 모바일에서 적 수십 마리에 NavMeshAgent를 붙이는 비용이 크기 때문이다.
/// 장애물이 생기는 시점에 재검토한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Chase")]
    [SerializeField] private float moveSpeed = 2.5f;

    [Tooltip("이 거리 안으로 들어오면 더 접근하지 않는다. 공격 사거리보다 약간 짧게 둔다.")]
    [SerializeField] private float stoppingDistance = 1.2f;

    [Header("Separation")]
    [Tooltip("적끼리 겹치지 않도록 밀어내는 반경")]
    [SerializeField] private float separationRadius = 1.1f;

    [Tooltip("밀어내는 힘의 비중. 0이면 밀집 방지를 쓰지 않는다.")]
    [Range(0f, 2f)]
    [SerializeField] private float separationWeight = 0.8f;

    [Tooltip("한 번에 검사할 최대 이웃 수. 밀집 상황에서 연산을 제한한다.")]
    [SerializeField] private int maxNeighborChecks = 8;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 540f;

    private Rigidbody rb;
    private EnemyManager enemyManager;

    /// <summary>이동을 멈춘다. 공격 예비동작 중에 사용한다.</summary>
    public bool IsHalted { get; set; }

    /// <summary>플레이어까지의 거리. 대상이 없으면 무한대.</summary>
    public float DistanceToTarget { get; private set; } = float.PositiveInfinity;

    /// <summary>
    /// 정지 거리를 바꾼다. 원거리 적이 사거리 밖에서 멈추게 할 때 쓴다.
    ///
    /// Inspector 값을 직접 바꾸지 않고 함수를 두는 이유 —
    /// 원형별 프리팹을 따로 만들지 않아도 EnemyAttack이 자기 방식에 맞게
    /// 이동을 조정할 수 있다.
    /// </summary>
    public void SetStoppingDistance(float distance)
    {
        stoppingDistance = Mathf.Max(0.1f, distance);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        enemyManager = EnemyManager.EnsureInstance();
    }

    private void FixedUpdate()
    {
        if (enemyManager == null)
            enemyManager = EnemyManager.EnsureInstance();

        Transform target = enemyManager.PlayerTransform;

        if (target == null)
        {
            StopHorizontal();
            return;
        }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        DistanceToTarget = toTarget.magnitude;

        FaceTowards(toTarget);

        if (IsHalted || DistanceToTarget <= stoppingDistance)
        {
            StopHorizontal();
            return;
        }

        Vector3 direction = toTarget.normalized;

        if (separationWeight > 0f)
            direction = (direction + CalculateSeparation() * separationWeight).normalized;

        rb.linearVelocity = new Vector3(
            direction.x * moveSpeed,
            rb.linearVelocity.y,
            direction.z * moveSpeed);
    }

    /// <summary>주변 적에게서 멀어지는 방향을 구한다. 적끼리 한 점에 뭉치는 것을 막는다.</summary>
    private Vector3 CalculateSeparation()
    {
        if (enemyManager == null)
            return Vector3.zero;

        var enemies = enemyManager.ActiveEnemies;
        float sqrRadius = separationRadius * separationRadius;

        Vector3 push = Vector3.zero;
        int checks = 0;

        for (int i = 0; i < enemies.Count && checks < maxNeighborChecks; i++)
        {
            EnemyController other = enemies[i];

            if (other == null || other.transform == transform)
                continue;

            Vector3 away = transform.position - other.transform.position;
            away.y = 0f;

            float sqrDistance = away.sqrMagnitude;

            if (sqrDistance > sqrRadius || sqrDistance <= 0.0001f)
                continue;

            // 가까울수록 강하게 밀어낸다.
            push += away.normalized * (1f - Mathf.Sqrt(sqrDistance) / separationRadius);
            checks++;
        }

        return push;
    }

    private void FaceTowards(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion target = Quaternion.LookRotation(direction);

        rb.MoveRotation(Quaternion.RotateTowards(
            rb.rotation, target, rotationSpeed * Time.fixedDeltaTime));
    }

    private void StopHorizontal()
    {
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }
}
