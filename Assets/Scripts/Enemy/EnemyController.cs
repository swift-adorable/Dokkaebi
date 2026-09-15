using UnityEngine;

/// <summary>
/// 적 개체의 체력/사망 처리.
///
/// 주의: 이동/추격 AI는 아직 미구현이다. (로드맵 4단계 예정)
/// </summary>
public class EnemyController : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private int maxHP = 3;

    [Header("Death")]
    [SerializeField] private GameObject corpsePrefab;

    private int currentHP;

    private void Start()
    {
        currentHP = maxHP;

        if (corpsePrefab == null)
            GameLogger.Error("[EnemyController] corpsePrefab이 할당되지 않았습니다.", this);
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0)
            return;

        currentHP -= damage;

        GameLogger.Log($"[EnemyController] HP {currentHP}/{maxHP}");

        if (currentHP <= 0)
            Die();
    }

    private void Die()
    {
        GameLogger.Log("[EnemyController] Die");

        if (corpsePrefab != null)
        {
            // TODO(로드맵 2단계) — Object Pooling으로 교체
            Instantiate(corpsePrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}
