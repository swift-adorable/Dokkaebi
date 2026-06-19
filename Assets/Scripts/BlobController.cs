using System.Collections;
using UnityEngine;

public class BlobController : MonoBehaviour
{
    [Header("Movement")]

    private Vector3 moveDirection;
    private Rigidbody rb;

    [SerializeField] private float moveSpeed = 5f;

    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireRate = 0.15f;
    private float nextFireTime;

    [SerializeField] private float dashDistance = 5f;
    [SerializeField] private float dashCooldown = 1f;

    private CorpseController nearbyCorpse;

    private bool canDash = true;

    private void Update()
    {
        Move();
        RotateToMouse();

        if (Input.GetMouseButton(0))
        {
            TryShoot();
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Dash();
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            TryAbsorb();
        }
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Move()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        moveDirection =
            new Vector3(horizontal, 0f, vertical).normalized;

        rb.linearVelocity =
            new Vector3(
                moveDirection.x * moveSpeed,
                rb.linearVelocity.y,
                moveDirection.z * moveSpeed
            );
    }

    private void RotateToMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 point = ray.GetPoint(distance);

            Vector3 lookDirection = point - transform.position;

            lookDirection.y = 0f;

            if (lookDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }
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
        Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
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

        Vector3 dashDirection = moveDirection; // 방향 고정

        Vector3 startPos = transform.position;

        Vector3 targetPos =
            startPos +
            dashDirection * dashDistance;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            transform.position =
                Vector3.Lerp(
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

        Debug.Log("Core Absorbed");
    }
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger Enter : " + other.name);

        CorpseController corpse = other.GetComponent<CorpseController>();

        if (corpse != null)
        {
            Debug.Log("Corpse Nearby");
            nearbyCorpse = corpse;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log("Trigger Exit: " + other.name);

        CorpseController corpse = other.GetComponent<CorpseController>();

        if (corpse != null && nearbyCorpse == corpse)
        {
            nearbyCorpse = null;
        }
    }
}