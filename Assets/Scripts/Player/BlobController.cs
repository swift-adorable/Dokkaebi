using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어 통합 컨트롤러.
///
/// 주의: 현재 이동/회전/사격/대시/흡수를 모두 담당하는 God Class 상태다.
/// 로드맵 3단계에서 PlayerMovement / PlayerAiming / PlayerWeapon / PlayerDash /
/// PlayerAbsorber로 분리 예정. 0단계에서는 구조를 바꾸지 않고 안정성만 확보한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInputHandler))]
public class BlobController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Weapon")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireRate = 0.15f;

    [Header("Dash")]
    [SerializeField] private float dashDistance = 5f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("Absorb")]
    [Tooltip("Core 1개 흡수 시 획득하는 경험치")]
    [SerializeField] private int xpPerCore = 1;

    [Header("Rotation")]
    [Tooltip("최소 조준 거리. 이보다 가까우면 회전하지 않는다. (떨림 방지)")]
    [SerializeField] private float minLookDistanceSqr = 0.001f;

    private Rigidbody rb;
    private PlayerInputHandler inputHandler;
    private GameManager gameManager;

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
        RotateToLookPoint();

        if (inputHandler.ShootHeld)
            TryShoot();

        if (inputHandler.DashPressed)
            TryDash();

        if (inputHandler.AbsorbPressed)
            TryAbsorb();
    }

    private void Move()
    {
        moveDirection = new Vector3(
            inputHandler.MoveInput.x,
            0f,
            inputHandler.MoveInput.y
        );

        rb.linearVelocity = new Vector3(
            moveDirection.x * moveSpeed,
            rb.linearVelocity.y,
            moveDirection.z * moveSpeed
        );
    }

    private void RotateToLookPoint()
    {
        Vector3 lookDirection = inputHandler.LookPoint - transform.position;

        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude > minLookDistanceSqr)
        {
            transform.rotation = Quaternion.LookRotation(lookDirection);
        }
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

        // TODO(로드맵 2단계) — Object Pooling으로 교체
        Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
    }

    private void TryDash()
    {
        if (!canDash)
            return;

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
        // Realtime 버전을 사용해 실제 경과 시간 기준으로 쿨다운을 진행시킨다.
        yield return new WaitForSecondsRealtime(dashCooldown);

        canDash = true;
    }

    private void TryAbsorb()
    {
        if (nearbyCorpse == null)
            return;

        // TODO(로드맵 2단계) — Object Pooling으로 교체
        Destroy(nearbyCorpse.gameObject);
        nearbyCorpse = null;

        PlayerStats.Instance.AddXP(xpPerCore);

        GameLogger.Log("[BlobController] Core Absorbed");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out CorpseController corpse))
        {
            nearbyCorpse = corpse;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out CorpseController corpse) && nearbyCorpse == corpse)
        {
            nearbyCorpse = null;
        }
    }
}
