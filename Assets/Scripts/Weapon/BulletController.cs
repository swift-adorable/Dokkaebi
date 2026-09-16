using UnityEngine;

/// <summary>
/// 투사체. 전방으로 직진하며 지정한 소속의 대상에게 피해를 준다.
/// 오브젝트 풀에서 재사용되므로 상태 초기화를 OnSpawned에서 수행한다.
/// </summary>
public class BulletController : MonoBehaviour, IPoolable
{
    [Header("Movement")]
    [SerializeField] private float speed = 20f;

    [Header("Combat")]
    [SerializeField] private int damage = 1;

    [Tooltip("이 소속의 대상에게만 피해를 준다.")]
    [SerializeField] private Team targetTeam = Team.Enemy;

    [Header("Lifetime")]
    [Tooltip("자동 소멸까지의 시간(초)")]
    [SerializeField] private float lifetime = 3f;

    private PooledObject pooledObject;
    private float despawnTime;
    private bool isConsumed;

    private void Awake()
    {
        // 풀 인스턴스는 최초 1회만 Awake가 호출된다.
        pooledObject = GetComponent<PooledObject>();
    }

    public void OnSpawned()
    {
        // 재사용 시 이전 상태가 남지 않도록 반드시 초기화한다.
        isConsumed = false;
        despawnTime = Time.time + lifetime;
    }

    public void OnDespawned()
    {
        isConsumed = true;
    }

    private void Update()
    {
        if (isConsumed)
            return;

        if (Time.time >= despawnTime)
        {
            ReturnToPool();
            return;
        }

        transform.position += transform.forward * (speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 한 프레임에 여러 콜라이더와 충돌해도 데미지가 중복 적용되지 않도록 방어한다.
        if (isConsumed)
            return;

        if (!other.TryGetComponent(out Health targetHealth))
            return;

        // 소속이 다르면 무시한다. 없으면 플레이어가 자기 총알에 맞는다.
        if (targetHealth.Team != targetTeam)
            return;

        isConsumed = true;

        targetHealth.TakeDamage(damage);

        ReturnToPool();
    }

    /// <summary>풀 소속이면 반납하고, 아니면 파괴한다. (씬에 직접 배치된 경우 대비)</summary>
    private void ReturnToPool()
    {
        if (pooledObject != null && pooledObject.Despawn())
            return;

        Destroy(gameObject);
    }
}
