/// <summary>
/// 피해를 받을 수 있는 대상.
///
/// 총알은 "적"을 아는 대신 이 인터페이스만 알면 되고,
/// 적의 공격도 "플레이어"가 아니라 이 인터페이스를 향한다.
/// 덕분에 플레이어와 적이 같은 피해 처리 경로를 공유한다.
/// </summary>
public interface IDamageable
{
    /// <summary>이미 사망했는지.</summary>
    bool IsDead { get; }

    /// <summary>피해를 적용하고 실제로 깎인 양을 반환한다.</summary>
    int TakeDamage(int amount);
}
