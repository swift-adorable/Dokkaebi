using UnityEngine;

/// <summary>
/// 적 사망 후 남는 시체(Core). 플레이어가 접근해 흡수할 수 있다.
///
/// 근접 여부 판정은 PlayerAbsorber의 OnTriggerEnter/Exit가 단독으로 담당한다.
/// (이전에는 이 클래스도 CanAbsorb를 중복 관리했으나, 아무도 읽지 않는
///  데드 코드였고 상태가 두 곳으로 갈라지는 위험이 있어 제거했다.)
/// </summary>
public class CorpseController : MonoBehaviour, IPoolable
{
    [Header("Reward")]
    [Tooltip("흡수 시 획득량 배수. TBD — 아이템/희귀도 시스템 도입 시 확장 예정")]
    [SerializeField] private int valueMultiplier = 1;

    public int ValueMultiplier => Mathf.Max(1, valueMultiplier);

    private void Awake()
    {
        EnsurePassable();
    }

    public void OnSpawned()
    {
        // 풀에서 꺼낼 때마다 확인한다. 프리팹이 바뀌어도 규칙이 깨지지 않는다.
        EnsurePassable();
    }

    public void OnDespawned()
    {
    }

    /// <summary>
    /// 시체를 밟고 지나갈 수 있게 만든다.
    ///
    /// 시체는 파밍 대상이지 장애물이 아니다. 물리 충돌을 남겨 두면
    /// 적을 많이 잡을수록 바닥이 막혀 이동이 나빠진다.
    /// 흡수 감지는 PlayerAbsorber의 트리거가 담당하므로 트리거로 두면
    /// "지나갈 수 있으면서 감지도 되는" 두 요구가 동시에 충족된다.
    ///
    /// 프리팹 설정에 의존하지 않고 코드로 보장한다.
    /// (씬·프리팹 설정을 잊어 런타임에 어긋나는 실패 지점을 만들지 않는다)
    /// </summary>
    private void EnsurePassable()
    {
        var colliders = GetComponentsInChildren<Collider>(includeInactive: true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (!colliders[i].isTrigger)
                colliders[i].isTrigger = true;
        }

        // Rigidbody가 있으면 물리 밀림도 받지 않게 한다.
        if (TryGetComponent(out Rigidbody body))
        {
            body.isKinematic = true;
            body.detectCollisions = true;
        }
    }
}
