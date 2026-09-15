using System.Collections;
using UnityEngine;

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
    [SerializeField] private float dashCooldown = 1f;

    private Rigidbody rb;
    private PlayerInputHandler inputHandler;
    private CorpseController nearbyCorpse;

    private Vector3 moveDirection;

    private float nextFireTime;
    private bool canDash = true;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        inputHandler = GetComponent<PlayerInputHandler>();
    }

    private void Update()
    {
        if (!GameManager.Instance.IsPlaying())
            return;

        Move();
        RotateToMouse();

        if (inputHandler.ShootHeld)
        {
            TryShoot();
        }

        if (inputHandler.DashPressed)
        {
            Dash();
        }

        if (inputHandler.AbsorbPressed)
        {
            TryAbsorb();
        }
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

    private void RotateToMouse()
    {
        Vector3 lookDirection =
            inputHandler.LookPoint - transform.position;

        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude > 0.001f)
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
        Instantiate(
            bulletPrefab,
            firePoint.position,
            firePoint.rotation
        );
    }

    private void Dash()
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

        float duration = 0.15f;
        float elapsed = 0f;

        Vector3 dashDirection = moveDirection.normalized;

        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + dashDirection * dashDistance;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            transform.position = Vector3.Lerp(
                startPos,
                targetPos,
                elapsed / duration
            );

            yield return null;
        }

        yield return new WaitForSeconds(dashCooldown);

        canDash = true;
    }

    private void TryAbsorb()
    {
        if (nearbyCorpse == null)
            return;

        Destroy(nearbyCorpse.gameObject);

        PlayerStats.Instance.AddXP(1);

        Debug.Log("Core Absorbed");
    }

    private void OnTriggerEnter(Collider other)
    {
        CorpseController corpse =
            other.GetComponent<CorpseController>();

        if (corpse != null)
        {
            nearbyCorpse = corpse;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        CorpseController corpse =
            other.GetComponent<CorpseController>();

        if (corpse != null && nearbyCorpse == corpse)
        {
            nearbyCorpse = null;
        }
    }
}