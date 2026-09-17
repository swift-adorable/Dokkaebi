using System;
using UnityEngine;

/// <summary>
/// 체력 컴포넌트. 플레이어와 적이 공용으로 사용한다.
/// 실제 계산은 HealthPool이 담당하고, 이 클래스는 Unity 연결과 이벤트만 다룬다.
/// </summary>
public class Health : MonoBehaviour, IDamageable, IPoolable
{
    [Header("Identity")]
    [Tooltip("이 대상의 소속. 공격자는 자신과 다른 소속만 공격한다.")]
    [SerializeField] private Team team = Team.Enemy;

    [Header("Health")]
    [SerializeField] private int maxHealth = 3;

    [Tooltip("피격 후 무적 시간(초). 0이면 무적 없음.")]
    [SerializeField] private float invulnerableDuration = 0f;

    private HealthPool pool;
    private CooldownTimer invulnerability;

    /// <summary>피해를 입었을 때. (실제 피해량, 현재 체력)</summary>
    public event Action<int, int> OnDamaged;

    /// <summary>체력이 0이 되었을 때. 한 번만 발행된다.</summary>
    public event Action OnDied;

    /// <summary>이 대상의 소속.</summary>
    public Team Team => team;

    public int Max => pool?.Max ?? maxHealth;
    public int Current => pool?.Current ?? maxHealth;
    public float Normalized => pool?.Normalized ?? 1f;
    public bool IsDead => pool != null && pool.IsDead;

    /// <summary>현재 무적 상태인지.</summary>
    public bool IsInvulnerable => !invulnerability.IsReady(Time.time);

    private void Awake()
    {
        pool = new HealthPool(maxHealth);
    }

    public void OnSpawned()
    {
        // 풀 재사용 시 체력을 되돌리지 않으면 죽은 상태로 스폰되어 즉사한다.
        pool.ResetToFull();
        invulnerability.Reset();
    }

    public void OnDespawned()
    {
    }

    public int TakeDamage(int amount)
    {
        if (IsDead || IsInvulnerable)
            return 0;

        int applied = pool.TakeDamage(amount);

        if (applied <= 0)
            return 0;

        if (invulnerableDuration > 0f)
            invulnerability.TryConsume(Time.time, invulnerableDuration);

        OnDamaged?.Invoke(applied, pool.Current);

        if (pool.IsDead)
            OnDied?.Invoke();

        return applied;
    }

    public int Heal(int amount)
    {
        if (pool == null)
            return 0;

        return pool.Heal(amount);
    }

    /// <summary>최대 체력을 변경한다. Skill으로 체력이 늘어나는 경우 등에 쓴다.</summary>
    public void SetMaxHealth(int newMax, bool refill = false)
    {
        maxHealth = Mathf.Max(1, newMax);

        pool?.SetMax(maxHealth, refill);
    }
}
