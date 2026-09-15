using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어 통합 컨트롤러.
///
/// 주의: 현재 이동/회전/사격/대시/흡수를 모두 담당하는 God Class 상태다.
/// 로드맵 3단계에서 PlayerMovement / PlayerAiming / PlayerWeapon / PlayerDash /
/// PlayerAbsorber로 분리 예정.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInputHandler))]
public class BlobController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Aiming")]
    [Tooltip("조준 스틱이 이 값 미만으로 기울면 회전하지 않는다.")]
    [Range(0f, 1f)]
    [SerializeField] private float aimDeadzone = 0.1f;

    [Tooltip("초당 회전 각도. 0이면 즉시 회전. 조이스틱 미세 떨림 완화용.")]
    [SerializeField] private float rotationSpeed = 900f;

    [Header("Weapon")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireRate = 0.15f;

    [Tooltip("시작 시 미리 생성해둘 총알 개수. 연사 속도 x 수명 이상이면 충분하다.")]
    [SerializeField] private int bulletPrewarmCount = 32;

    [Header("Dash")]
    [SerializeField] private float dashDistance = 5f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("Absorb")]
    [Tooltip("Core 1개 흡수 시 획득하는 경험치")]
    [SerializeField] private int xpPerCore = 1;

    /// <summary>현재 흡수 가능한 시체. 없으면 null. (흡수 프롬프트 UI가 참조한다)</summary>
    public CorpseController NearbyCorpse => nearbyCorpse;

    private Rigidbody rb;
    private PlayerInputHandler inputHandler;
    private GameManager gameManager;
    private PoolManager poolManager;

    private CorpseController nearbyCorpse;
    private Vector3 moveDirection;

    private float nextFireTime;
    private bool canDash = true;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        inputHandler = GetComponent<PlayerInputHandler>();
    }

    private void Start()
    {
        // Unity는 모든 Awake() 이후에 Start()를 실행하므로,
        // 여기서 싱글턴을 캐싱하면 초기화 순서 문제가 원천 차단된다.
        gameManager = GameManager.Instance;
        poolManager = PoolManager.EnsureInstance();

        if (bulletPrefab != null)
            poolManager.Prewarm(bulletPrefab, bulletPrewarmCount);

        if (bulletPrefab == null)
            GameLogger.Error("[BlobController] bulletPrefab이 할당되지 않았습니다.", this);

        if (firePoint == null)
            GameLogger.Error("[BlobController] firePoint가 할당되지 않았습니다.", this);
    }

    private void Update()
    {
        if (gameManager == null || !gameManager.IsPlaying)
            return;

        Move();
        RotateToAim();

        if (inputHandler.ShootHeld)
            TryShoot();

        if (inputHandler.DashPressed)
            TryDash();

        if (inputHandler.AbsorbPressed)
            TryAbsorb();
    }

    private void Move()
    {
        Vector2 input = inputHandler.MoveInput;

        moveDirection = new Vector3(input.x, 0f, input.y);

        rb.linearVelocity = new Vector3(
            moveDirection.x * moveSpeed,
            rb.linearVelocity.y,
            moveDirection.z * moveSpeed
        );
    }

    /// <summary>
    /// 조준 스틱이 가리키는 '방향'으로 회전한다.
    /// 이동 방향과 조준 방향은 완전히 분리되어 있다. (듀얼 스틱의 핵심)
    /// </summary>
    private void RotateToAim()
    {
        Vector2 aim = inputHandler.AimInput;

        if (aim.sqrMagnitude < aimDeadzone * aimDeadzone)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(new Vector3(aim.x, 0f, aim.y));

        if (rotationSpeed <= 0f)
        {
            transform.rotation = targetRotation;
            return;
        }

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime);
    }

    private void TryShoot()
    {
        if (Time.time < nextFireTime)
            return;

        nextFireTime = Time.time + fireRate;

        Shoot();
    }

    private void Shoot()
    {
        if (bulletPrefab == null || firePoint == null)
            return;

        poolManager.Spawn(bulletPrefab, firePoint.position, firePoint.rotation);
    }

    private void TryDash()
    {
        if (!canDash)
            return;

        // 확정 기획: 대시는 '조준 방향'이 아니라 '이동 중인 방향'으로 수행한다.
        if (moveDirection == Vector3.zero)
            return;

        StartCoroutine(DashCoroutine());
    }

    private IEnumerator DashCoroutine()
    {
        canDash = false;

        float elapsed = 0f;

        Vector3 dashDirection = moveDirection.normalized;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + dashDirection * dashDistance;

        // TODO(로드맵 3단계) — transform 직접 조작은 물리 충돌을 무시하여 벽을 관통한다.
        //                     rb.MovePosition 기반으로 재작성 예정.
        while (elapsed < dashDuration)
        {
            elapsed += Time.deltaTime;

            transform.position = Vector3.Lerp(startPos, targetPos, elapsed / dashDuration);

            yield return null;
        }

        // WaitForSeconds는 timeScale의 영향을 받아 일시정지 중 쿨다운이 멈춘다.
        yield return new WaitForSecondsRealtime(dashCooldown);

        canDash = true;
    }

    private void TryAbsorb()
    {
        if (nearbyCorpse == null)
            return;

        int gainedXP = xpPerCore * nearbyCorpse.ValueMultiplier;

        GameObject corpseObject = nearbyCorpse.gameObject;
        nearbyCorpse = null;

        if (poolManager == null || !poolManager.Despawn(corpseObject))
            Destroy(corpseObject);

        PlayerStats.Instance.AddXP(gainedXP);

        GameLogger.Log($"[BlobController] Core Absorbed (+{gainedXP} XP)");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out CorpseController corpse))
            nearbyCorpse = corpse;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out CorpseController corpse) && nearbyCorpse == corpse)
            nearbyCorpse = null;
    }
}
