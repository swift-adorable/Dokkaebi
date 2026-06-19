using UnityEngine;

public class BulletController : MonoBehaviour
{
    [SerializeField]
    private float speed = 20f;

    private void Update()
    {
        transform.position +=
            transform.forward *
            speed *
            Time.deltaTime;
    }

    private void Start()
    {
        Destroy(gameObject, 3f);
    }

    private void OnTriggerEnter(Collider other)
    {
        EnemyController enemy =
            other.GetComponent<EnemyController>();

        if (enemy != null)
        {
            enemy.TakeDamage(1);

            Destroy(gameObject);
        }
    }
}