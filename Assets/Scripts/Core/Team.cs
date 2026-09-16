/// <summary>
/// 피해 대상 구분.
///
/// 총알과 적 공격이 모두 IDamageable을 향하므로, 소속이 없으면
/// 플레이어의 총알이 플레이어 자신을 때리게 된다. 소속으로 대상을 거른다.
/// </summary>
public enum Team
{
    Player,
    Enemy,

    /// <summary>중립. 누구에게도 피해를 받지 않는다. (파괴 가능한 오브젝트 등에 사용)</summary>
    Neutral
}
