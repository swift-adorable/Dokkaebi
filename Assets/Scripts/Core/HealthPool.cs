using UnityEngine;

/// <summary>
/// 체력 계산 전담 순수 클래스.
///
/// MonoBehaviour에 의존하지 않으므로 단위 테스트가 쉽다.
/// 오버킬 방지, 과회복 방지, 사망 후 중복 처리 방지 같은 경계 조건을
/// 이 한 곳에서만 책임진다.
/// </summary>
[System.Serializable]
public class HealthPool
{
    private int max;
    private int current;

    public int Max => max;
    public int Current => current;

    /// <summary>0~1 비율. 체력바 UI가 그대로 쓸 수 있다.</summary>
    public float Normalized => max > 0 ? (float)current / max : 0f;

    public bool IsDead => current <= 0;
    public bool IsFull => current >= max;

    /// <param name="maxHealth">최대 체력. 1 미만이면 1로 보정한다.</param>
    public HealthPool(int maxHealth)
    {
        max = Mathf.Max(1, maxHealth);
        current = max;
    }

    /// <summary>
    /// 피해를 적용하고 '실제로 깎인 양'을 반환한다.
    /// 남은 체력보다 큰 피해가 들어와도 실제 피해량은 남은 체력을 넘지 않는다.
    /// </summary>
    public int TakeDamage(int amount)
    {
        if (amount <= 0 || IsDead)
            return 0;

        int applied = Mathf.Min(amount, current);

        current -= applied;

        return applied;
    }

    /// <summary>회복하고 '실제로 회복된 양'을 반환한다. 사망 상태에서는 회복되지 않는다.</summary>
    public int Heal(int amount)
    {
        if (amount <= 0 || IsDead)
            return 0;

        int applied = Mathf.Min(amount, max - current);

        current += applied;

        return applied;
    }

    /// <summary>최대 체력을 변경한다. 현재 체력은 새 최대치를 넘지 않도록 잘린다.</summary>
    public void SetMax(int newMax, bool refill)
    {
        max = Mathf.Max(1, newMax);

        current = refill ? max : Mathf.Min(current, max);
    }

    /// <summary>풀에서 재사용할 때처럼 완전 회복이 필요한 경우에 쓴다.</summary>
    public void ResetToFull()
    {
        current = max;
    }
}
