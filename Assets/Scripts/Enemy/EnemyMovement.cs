using UnityEngine;

/// <summary>
/// 적 추격 이동.
///
/// NavMeshAgent는 쓰지 않는다 — 모바일에서 적 수십 마리에 붙이는 비용이 크다.
/// 장 맵(덩어리가 있는 맵)에서는 NavMesh에서 경로만 빌려(EnemyPathing · 결정 2-89) 돌아가고,
/// 대상이 곧장 보이면 예전처럼 곧장 간다. 끼이면 경로를 다시 구하고, 오래 끼인 졸개는 풀로 돌려보낸다.
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

    /// <summary>길찾기 — 덩어리를 돌아간다 (결정 2-89).</summary>
    private readonly EnemyPathing pathing = new();

    /// <summary>끼임 — 1초마다 움직인 거리를 본다. 3초면 경로 다시 · 10초면 졸개는 풀로 (결정 2-89 · [임시값]).</summary>
    public const float StuckCheckInterval = 1f;
    public const float StuckMoveThreshold = 0.3f;
    public const int StuckRepathSeconds = 3;
    public const int StuckDespawnSeconds = 10;

    private Vector3 stuckAnchor;
    private float nextStuckCheck;
    private int stuckSeconds;
    private bool wantedToMove;

    /// <summary>이동 방향을 대신 정해 주는 두뇌. 없으면 기존 직선 추격을 쓴다.</summary>
    private IEnemySteering steering;
    private EnemyAggro aggro;

    /// <summary>이동 속도 배율. 예비동작·과중량 같은 일시적 감속에 쓴다.</summary>
    public float SpeedScale { get; set; } = 1f;

    /// <summary>프리팹에 적힌 기본 이동 속도(m/s). 유형 표와 같은지 테스트가 본다.</summary>
    public float BaseMoveSpeed => moveSpeed;

    /// <summary>
    /// 개체 고유 이동 배율. 몬스터 속성 「신속」(×1.4) · 「육중」(×0.8)이 정한다.
    ///
    /// 【SpeedScale과 나눠 둔 이유】
    /// SpeedScale은 원거리 예비동작 동안 0.45로 줄였다가 【1f로 되돌린다】.
    /// 개체 배율을 같은 칸에 넣으면 첫 사격 한 번으로 「신속」이 사라진다.
    /// 성격이 다른 두 값을 한 변수에 담지 않는다.
    /// </summary>
    public float BaseSpeedScale { get; set; } = 1f;

    /// <summary>이동을 멈춘다. 공격 예비동작 중에 사용한다.</summary>
    public bool IsHalted { get; set; }

    /// <summary>플레이어까지의 거리. 대상이 없으면 무한대.</summary>
    public float DistanceToTarget { get; private set; } = float.PositiveInfinity;

    /// <summary>
    /// 정지 거리를 바꾼다. 원거리 적이 사거리 밖에서 멈추게 할 때 쓴다.
    ///
    /// Inspector 값을 직접 바꾸지 않고 함수를 두는 이유 —
    /// 유형별 프리팹을 따로 만들지 않아도 EnemyAttack이 자기 방식에 맞게
    /// 이동을 조정할 수 있다.
    /// </summary>
    public void SetStoppingDistance(float distance)
    {
        stoppingDistance = Mathf.Max(0.1f, distance);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // 인터페이스로 받으므로 두뇌 구현이 바뀌어도 이동은 고치지 않는다.
        steering = GetComponent<IEnemySteering>();

        aggro = GetComponent<EnemyAggro>();

        health = GetComponent<Health>();
    }

    private Health health;

    /// <summary>
    /// 상태이상이 곱하는 속도. 행동 불능이면 0.
    ///
    /// 【6-K에서 상태이상을 다시 설계한 뒤로 이 곱셈이 없었다.】 냉각은 적을
    /// 느리게 하지 못했고, 동결·마비는 적을 멈추지 못했다. 상태 판정과
    /// 테스트는 전부 맞았는데 몸에 닿는 선 하나가 빠져 있었다.
    /// </summary>
    public float StatusScale => health != null ? health.Status.SpeedMultiplier : 1f;

    /// <summary>동결·마비인가. 공격도 이 값을 본다.</summary>
    public bool IsIncapacitated => health != null && health.Status.IsIncapacitated;

    private void Start()
    {
        enemyManager = EnemyManager.EnsureInstance();
    }

    private void OnEnable()
    {
        // 풀에서 다시 나올 때 지난 삶의 경로 · 끼임을 지운다.
        pathing.Reset();
        stuckSeconds = 0;
        stuckAnchor = transform.position;
        nextStuckCheck = Time.time + StuckCheckInterval;
    }

    private void FixedUpdate()
    {
        wantedToMove = false;

        if (enemyManager == null)
            enemyManager = EnemyManager.EnsureInstance();

        // 【대상은 플레이어로 고정되지 않는다.】
        // 진영이 다르면 적끼리도 싸운다. EnemyAggro가 없는 프리팹은
        // 예전처럼 플레이어를 따라간다 — 유형 프리팹이 붙기 전까지의 경로다.
        Transform target = aggro != null ? aggro.Target : enemyManager.PlayerTransform;

        if (target == null)
        {
            StopHorizontal();
            return;
        }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        DistanceToTarget = toTarget.magnitude;

        // 얼어붙은 적은 돌아서지도 않는다. 방향을 틀면 「멈춘 척」으로 보인다.
        if (IsIncapacitated)
        {
            StopHorizontal();
            return;
        }

        // 덩어리를 돌아가야 하면 경로의 다음 꺾임점 쪽으로 (결정 2-89). 곧장 보이면 대상 쪽 그대로.
        Vector3 chaseDirection = pathing.Direction(transform.position, target.position, out bool detour);
        if (chaseDirection.sqrMagnitude < 0.0001f)
            chaseDirection = toTarget.normalized;

        // 돌아가는 동안은 가는 쪽을 본다 — 벽 너머 대상을 노려보며 옆걸음치면 어색하다.
        FaceTowards(detour ? chaseDirection : toTarget);

        CheckStuck();

        // 삭에는 순라귀가 멈춘다 (결정 2-62) — 제자리에서 돌아보기만 한다.
        RaidConditions conditions = RaidManager.Current;
        if (conditions.SentryAsleep && IsSentry())
        {
            StopHorizontal();
            return;
        }

        if (IsHalted)
        {
            StopHorizontal();
            return;
        }

        float speed = moveSpeed
                      * Mathf.Max(0f, BaseSpeedScale)
                      * Mathf.Max(0f, SpeedScale)
                      * StatusScale
                      * conditions.MoveScale;   // 장마비 (결정 2-64)
        Vector3 direction;

        // 돌아가는 중이면 두뇌의 유지 거리 · 측면 이동을 쓰지 않고 길을 따라간다 — 벽 앞에서 옆걸음만 치지 않게.
        // 두뇌가 붙어 있으면 방향은 두뇌가 정한다. (EnemyBrain — 유지 거리·측면 이동·차례)
        // 붙어 있지 않으면 기존 직선 추격 그대로다. 프리팹을 한꺼번에 고치지 않아도 된다.
        if (detour)
        {
            direction = chaseDirection;
        }
        else if (steering != null &&
            steering.TryGetSteering(toTarget.normalized, DistanceToTarget,
                                    out Vector3 steered, out float steerScale))
        {
            if (steered.sqrMagnitude < 0.0001f)
            {
                StopHorizontal();
                return;
            }

            direction = steered;
            speed *= Mathf.Max(0f, steerScale);
        }
        else
        {
            if (DistanceToTarget <= stoppingDistance)
            {
                StopHorizontal();
                return;
            }

            direction = chaseDirection;
        }

        if (separationWeight > 0f)
            direction = (direction + CalculateSeparation() * separationWeight).normalized;

        // 【Y를 0으로 눌러 둔다.】
        // 전에는 rb.linearVelocity.y를 그대로 물려줬다. 중력이 꺼져 있고
        // Rigidbody의 Y 고정도 없어서, 적끼리 부딪혀 한 번 위로 밀리면
        // 그 속도가 영영 남아 하늘로 올라갔다. 탑다운이라 높이는 쓰지 않는다.
        rb.linearVelocity = new Vector3(direction.x * speed, 0f, direction.z * speed);
        wantedToMove = speed > 0.1f;
    }

    /// <summary>
    /// 끼임 (결정 2-89) — 가려는데 1초에 0.3m도 못 움직인 초가 이어지면 3초에 경로를 다시 구하고,
    /// 10초면 졸개는 풀로 돌려보낸다(스포너가 다른 자리에 새로 낸다). 이야기 보스 · 고유 등급 · 큰 요괴는 돌려보내지 않는다.
    /// 판정은 지난 프레임의 「가려 했는가」로 한다 — 이번 프레임 속도는 아직 정해지지 않았다.
    /// </summary>
    private void CheckStuck()
    {
        if (Time.time < nextStuckCheck)
            return;

        nextStuckCheck = Time.time + StuckCheckInterval;

        Vector3 moved = transform.position - stuckAnchor;
        moved.y = 0f;
        stuckAnchor = transform.position;

        if (!lastWantedToMove || moved.magnitude >= StuckMoveThreshold)
        {
            stuckSeconds = 0;
            return;
        }

        stuckSeconds++;

        if (stuckSeconds == StuckRepathSeconds)
            pathing.ForceRepath();

        if (stuckSeconds >= StuckDespawnSeconds && MayDespawnWhenStuck())
        {
            stuckSeconds = 0;
            if (TryGetComponent(out EnemyController controller))
                controller.ReturnToPool();
        }
    }

    private bool lastWantedToMove;

    private void LateUpdate() => lastWantedToMove = wantedToMove;

    private bool IsSentry()
        => TryGetComponent(out EnemyIdentity identity) && identity.Profile.archetype == EnemyArchetype.Sentry;

    private bool MayDespawnWhenStuck()
    {
        if (TryGetComponent(out StoryGift _))
            return false;

        return !TryGetComponent(out EnemyIdentity identity) || identity.Profile.rarity != EnemyRarity.Unique;
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
        // 멈출 때도 Y를 남기지 않는다 — 남기면 밀려 올라간 채로 떠 있게 된다.
        rb.linearVelocity = Vector3.zero;
    }
}
