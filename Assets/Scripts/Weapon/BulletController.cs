using UnityEngine;

/// <summary>
/// 투사체. 전방으로 직진하며 적에게 피해를 준다.
/// </summary>
public class BulletController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 20f;

    [Header("Combat")]
    [SerializeField] private int damage = 1;

    [Header("Lifetime")]
    [Tooltip("자동 소멸까지의 시간(초)")]
    [SerializeField] private float lifetime = 3f;

    private bool isConsumed;

    private void Start()
    {
        // TODO(로드맵 2단계) — Object Pooling 전환 시 Destroy 대신 풀 반납으로 교체
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        transform.position += transform.forward * (speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 한 프레임에 여러 콜라이더와 충돌해도 데미지가 중복 적용되지 않도록 방어한다.
        if (isConsumed)
            return;

        if (!other.TryGetComponent(out EnemyController enemy))
            return;

        isConsumed = true;

        enemy.TakeDamage(damage);

        Destroy(gameObject);
    }
}
