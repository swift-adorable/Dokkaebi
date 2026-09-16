using UnityEngine;

/// <summary>
/// 적 근접 공격.
///
/// 접촉 즉시 피해를 주는 방식(뱀서류)이 아니라
/// '사거리 진입 → 예비동작 → 판정 → 쿨다운' 순서를 갖는다.
///
/// 이유: 접촉 피해는 회피라는 선택지를 없앤다. 예비동작이 있어야
/// 플레이어가 대시로 빠져나갈 수 있고, 그래야 대시가 전투 기술로서 의미를 갖는다.
/// 빠른 템포와 즉각적인 피드백이라는 방향성과도 맞다.
/// </summary>
public class EnemyAttack : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private int damage = 1;

    [Tooltip("공격 판정이 닿는 거리")]
    [SerializeField] private float attackRange = 1.6f;

    [Tooltip("예비동작 시간(초). 이 시간 동안 플레이어는 회피할 수 있다.")]
    [SerializeField] private float windupDuration = 0.35f;

    [Tooltip("공격 후 재사용 대기시간(초)")]
    [SerializeField] private float attackCooldown = 1.2f;

    [Header("Feedback")]
    [Tooltip("예비동작 중 표시할 오브젝트(선택). 없으면 표시하지 않는다.")]
    [SerializeField] private GameObject windupIndicator;

    private EnemyMovement movement;
    private CooldownTimer cooldown;

    private float windupEndTime;

    /// <summary>현재 예비동작 중인지.</summary>
    public bool IsWindingUp { get; private set; }

    public float AttackRange => attackRange;

    private void Awake()
    {
        movement = GetComponent<EnemyMovement>();
    }

    /// <summary>풀에서 재사용될 때 공격 상태를 초기화한다.</summary>
    public void ResetState()
    {
        CancelWindup();
        cooldown.Reset();
    }

    private void Update()
    {
        if (movement == null)
            return;

        if (IsWindingUp)
        {
            UpdateWindup();
            return;
        }

        TryStartWindup();
    }

    private void TryStartWindup()
    {
        if (movement.DistanceToTarget > attackRange)
            return;

        if (!cooldown.TryConsume(Time.time, attackCooldown + windupDuration))
            return;

        IsWindingUp = true;
        windupEndTime = Time.time + windupDuration;

        // 예비동작 중에는 멈춰서 '공격이 온다'는 신호를 명확히 준다.
        movement.IsHalted = true;

        SetIndicator(true);
    }

    private void UpdateWindup()
    {
        if (Time.time < windupEndTime)
            return;

        ResolveAttack();

        CancelWindup();
    }

    /// <summary>예비동작이 끝난 시점에 사거리를 다시 확인한다. 벗어났으면 빗나간다.</summary>
    private void ResolveAttack()
    {
        Transform target = EnemyManager.EnsureInstance().PlayerTransform;

        if (target == null)
            return;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude > attackRange)
        {
            GameLogger.Log("[EnemyAttack] 회피됨");
            return;
        }

        if (!target.TryGetComponent(out Health targetHealth))
            return;

        if (targetHealth.Team == Team.Enemy)
            return;

        int applied = targetHealth.TakeDamage(damage);

        if (applied > 0)
            GameLogger.Log($"[EnemyAttack] 명중 {applied}");
    }

    private void CancelWindup()
    {
        IsWindingUp = false;

        if (movement != null)
            movement.IsHalted = false;

        SetIndicator(false);
    }

    private void SetIndicator(bool visible)
    {
        if (windupIndicator != null)
            windupIndicator.SetActive(visible);
    }

    private void OnDisable()
    {
        CancelWindup();
    }
}
