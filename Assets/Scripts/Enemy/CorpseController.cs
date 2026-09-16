using UnityEngine;

/// <summary>
/// 적 사망 후 남는 시체(Core). 플레이어가 접근해 흡수할 수 있다.
///
/// 근접 여부 판정은 PlayerAbsorber의 OnTriggerEnter/Exit가 단독으로 담당한다.
/// (이전에는 이 클래스도 CanAbsorb를 중복 관리했으나, 아무도 읽지 않는
///  데드 코드였고 상태가 두 곳으로 갈라지는 위험이 있어 제거했다.)
/// </summary>
public class CorpseController : MonoBehaviour
{
    [Header("Reward")]
    [Tooltip("흡수 시 획득량 배수. TBD — 아이템/희귀도 시스템 도입 시 확장 예정")]
    [SerializeField] private int valueMultiplier = 1;

    public int ValueMultiplier => Mathf.Max(1, valueMultiplier);
}
