using System.Collections.Generic;

/// <summary>
/// 동시에 공격할 수 있는 적의 수를 제한하는 차례표.
///
/// 【왜 필요한가】
/// 이것이 없으면 적 다섯이 다섯 방향에서 동시에 달려들어 때린다.
/// 피할 방법이 없으므로 플레이어는 "실력으로 진 것"이 아니라 "숫자에 눌린 것"이 되고,
/// 게임은 어려운 게 아니라 부당하게 느껴진다.
///
/// 사람 플레이어들은 자연히 차례를 지킨다. 한 명이 압박하면 다른 한 명은 각을 잡는다.
/// 그 행동을 규칙으로 못 박은 것이 이 클래스다.
///
/// 임대(lease)를 두는 이유 — 차례를 쥔 적이 죽거나 끼어서 반납하지 못하면
/// 나머지가 영원히 돌기만 한다. 시간이 지나면 자동으로 회수한다.
///
/// MonoBehaviour 의존이 없는 순수 클래스다. EditMode 테스트 대상.
/// </summary>
public class AttackTokenPool
{
    /// <summary>동시 공격 허용 수의 기본값.</summary>
    public const int DefaultCapacity = 2;

    /// <summary>차례를 쥔 뒤 자동 회수되기까지의 시간(초).</summary>
    public const float DefaultLease = 3f;

    private readonly Dictionary<int, float> expiry = new();

    private int capacity = DefaultCapacity;

    public AttackTokenPool(int capacity = DefaultCapacity)
    {
        Capacity = capacity;
    }

    /// <summary>동시 공격 허용 수. 1 미만으로 내려가지 않는다.</summary>
    public int Capacity
    {
        get => capacity;
        set => capacity = value < 1 ? 1 : value;
    }

    /// <summary>지금 차례를 쥔 적의 수.</summary>
    public int HeldCount => expiry.Count;

    public bool Holds(int id) => expiry.ContainsKey(id);

    /// <summary>
    /// 차례를 얻는다. 이미 쥐고 있으면 임대만 갱신하고 true를 돌려준다.
    /// 자리가 없으면 false. 이때 적은 공격하지 않고 자리를 잡는다.
    /// </summary>
    public bool TryAcquire(int id, float now, float lease = DefaultLease)
    {
        Expire(now);

        if (expiry.ContainsKey(id))
        {
            expiry[id] = now + lease;
            return true;
        }

        if (expiry.Count >= capacity)
            return false;

        expiry[id] = now + lease;

        return true;
    }

    public void Release(int id) => expiry.Remove(id);

    /// <summary>임대가 끝난 차례를 회수한다.</summary>
    public void Expire(float now)
    {
        if (expiry.Count == 0)
            return;

        // 열거 중 제거를 피하기 위해 만료 대상을 먼저 모은다.
        List<int> stale = null;

        foreach (KeyValuePair<int, float> pair in expiry)
        {
            if (pair.Value > now)
                continue;

            stale ??= new List<int>(expiry.Count);
            stale.Add(pair.Key);
        }

        if (stale == null)
            return;

        for (int i = 0; i < stale.Count; i++)
            expiry.Remove(stale[i]);
    }

    public void Clear() => expiry.Clear();
}
