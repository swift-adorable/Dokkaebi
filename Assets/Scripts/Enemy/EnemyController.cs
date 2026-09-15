using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [SerializeField]
    private int maxHP = 3;

    private int currentHP;

    [SerializeField]
    private GameObject corpsePrefab;

    private void Start()
    {
        currentHP = maxHP;
    }

    public void TakeDamage(int damage)
    {
        currentHP -= damage;

        Debug.Log($"Enemy HP : {currentHP}");

        if (currentHP <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("Enemy Die");

        GameObject corpse = Instantiate(
            corpsePrefab,
            transform.position,
            Quaternion.identity
        );

        Debug.Log($"Corpse Created : {corpse.name}");

        Destroy(gameObject);
    }

}