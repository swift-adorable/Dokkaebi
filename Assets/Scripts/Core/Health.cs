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
    [Tooltip("최대 체력. 플레이어 기준값은 CombatConstants.PlayerBaseHealth(100)이다.")]
    [SerializeField] private int maxHealth = CombatConstants.PlayerBaseHealth;

    [Tooltip("피격 후 무적 시간(초). 0이면 무적 없음.")]
    [SerializeField] private float invulnerableDuration = CombatConstants.InvulnerableDuration;

    [Header("Defence")]
    [Tooltip("원거리·투사체 피격에 적용되는 방어도.")]
    [SerializeField] private int headArmour = 0;

    [Tooltip("근접·접촉·폭발 피격에 적용되는 방어도.")]
    [SerializeField] private int bodyArmour = 0;

    private HealthPool pool;
    private CooldownTimer invulnerability;
    private ElementalResistances resistances = ElementalResistances.Default;

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

    /// <summary>이 대상에게 걸린 상태이상.</summary>
    public StatusEffectState Status { get; } = new StatusEffectState();

    /// <summary>
    /// 피해 계산에 넘길 방어 정보.
    ///
    /// 방어도 수치의 소재는 장비다. 여기서는 합산된 결과만 들고 있는다.
    /// (경계 — 장비가 수치를 공급하고, 공식은 DamageResolver 한 곳에만 있다)
    /// </summary>
    public DefenceProfile Defence => DefenceProfile.Create(headArmour, bodyArmour, resistances);

    /// <summary>장비·몬스터 속성이 합산한 방어 수치를 주입한다.</summary>
    public void SetDefence(int head, int body, in ElementalResistances resist)
    {
        headArmour = head;
        bodyArmour = body;
        resistances = resist;
    }

    /// <summary>
    /// 상태이상을 건다. 도트 처리를 위해 StatusEffectSystem에 자동 등록된다.
    ///
    /// 호출자가 등록을 잊는 실패 지점을 만들지 않으려고 여기서 함께 처리한다.
    /// </summary>
    /// <param name="type">거는 상태</param>
    /// <param name="sourceDamage">부여 시점의 기본 피해. 초당 피해의 기준이 된다.</param>
    public void ApplyStatus(StatusEffectType type, float sourceDamage)
    {
        if (IsDead || type == StatusEffectType.None || sourceDamage <= 0f)
            return;

        // 저항 장비가 면역을 주면 아예 걸리지 않는다. (막는 것은 장비의 몫)
        if (IsImmuneTo(type))
            return;

        Status.Apply(type, sourceDamage);

        StatusEffectSystem.EnsureInstance().Track(this);

        OnStatusChanged?.Invoke(type);
    }

    /// <summary>
    /// 이 상태에 면역인지. 장비(각인 저항형 II 이상)가 면역을 준다.
    ///
    /// ※ 장비 시스템은 6단계에서 착수한다. 지금은 항상 false이며,
    ///    판정 지점만 미리 만들어 두어 나중에 한 곳만 고치면 되게 한다.
    /// </summary>
    public bool IsImmuneTo(StatusEffectType type)
    {
        return false;
    }

    /// <summary>
    /// 상태이상 도트를 한 프레임 진행시킨다. StatusEffectSystem이 호출한다.
    /// 직접 호출하지 않는다. 두 번 돌면 도트가 두 배가 된다.
    /// </summary>
    /// <param name="deltaTime">경과 시간</param>
    /// <param name="buffer">호출자가 재사용하는 버퍼. GC Alloc을 막는다.</param>
    public void TickStatus(float deltaTime, System.Collections.Generic.List<DamageRequest> buffer)
    {
        if (IsDead || buffer == null)
            return;

        bool moving = IsMoving != null && IsMoving();

        Status.Tick(deltaTime, moving, buffer);

        for (int i = 0; i < buffer.Count; i++)
        {
            TakeDamage(buffer[i]);

            if (IsDead)
                return;
        }
    }

    /// <summary>
    /// 이 대상이 이동 중인지 판정하는 함수. 출혈 배증에 쓴다.
    ///
    /// 컴포넌트 참조 대신 델리게이트를 쓰는 이유 — 플레이어와 적의
    /// 이동 컴포넌트가 다르고, 테스트에서는 둘 다 없기 때문이다.
    /// </summary>
    public System.Func<bool> IsMoving { get; set; }

    /// <summary>상태이상이 새로 걸렸을 때. 외형·UI가 구독한다.</summary>
    public event Action<StatusEffectType> OnStatusChanged;

    private void Awake()
    {
        pool = new HealthPool(maxHealth);
    }

    public void OnSpawned()
    {
        // 풀 재사용 시 체력을 되돌리지 않으면 죽은 상태로 스폰되어 즉사한다.
        pool.ResetToFull();
        invulnerability.Reset();

        // 상태이상도 반드시 함께 초기화한다.
        // 남겨 두면 재사용된 개체가 이전 런의 점화를 그대로 들고 나온다.
        Status.ClearAll();

        if (StatusEffectSystem.HasInstance)
            StatusEffectSystem.Instance.Untrack(this);
    }

    public void OnDespawned()
    {
    }

    /// <summary>
    /// 공격 정보를 받아 공식으로 피해를 계산하고 적용한다.
    ///
    /// 호출자가 직접 수치를 깎지 않고 이 경로만 쓰게 해야
    /// 방어도·속성 상성·감전 증폭이 한 곳에서만 계산된다.
    /// </summary>
    public int TakeDamage(in DamageRequest request, float difficultyMultiplier = 1f)
    {
        if (IsDead)
            return 0;

        // 상태이상 피해는 무적을 무시한다.
        // 무적 중에 도트가 멈추면 "맞고 굴렀더니 화상이 사라지는" 이상한 규칙이 된다.
        if (IsInvulnerable && !request.bypassArmour)
            return 0;

        DamageRequest amplified = request;

        // 감전은 받는 피해를 늘린다. 증폭은 피격자 쪽에서 적용해야
        // 여러 공격자가 있어도 한 번만 계산된다.
        float taken = Status.DamageTakenMultiplier;

        if (taken > 1f)
            amplified.increasedPercent += taken - 1f;

        int computed = DamageResolver.Resolve(amplified, Defence, difficultyMultiplier);

        if (computed <= 0)
            return 0;

        return ApplyRaw(computed, skipInvulnerability: request.bypassArmour);
    }

    public int TakeDamage(int amount)
    {
        return ApplyRaw(amount, skipInvulnerability: false);
    }

    private int ApplyRaw(int amount, bool skipInvulnerability)
    {
        if (IsDead)
            return 0;

        if (IsInvulnerable && !skipInvulnerability)
            return 0;

        int applied = pool.TakeDamage(amount);

        if (applied <= 0)
            return 0;

        // 도트가 무적을 갱신하면 도트만으로 영구 무적이 되어 버린다.
        if (invulnerableDuration > 0f && !skipInvulnerability)
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

    /// <summary>
    /// 테스트에서 무적 시간을 조정한다.
    ///
    /// 프리팹 없이 코드로 만든 Health는 Inspector 값을 쓸 수 없고,
    /// 무적이 켜져 있으면 도트 검증이 흔들린다. PlayMode 테스트 전용이다.
    /// </summary>
    public void ConfigureForTest(float invulnerableSeconds)
    {
        invulnerableDuration = Mathf.Max(0f, invulnerableSeconds);
        invulnerability.Reset();
    }

    /// <summary>최대 체력을 변경한다. Skill으로 체력이 늘어나는 경우 등에 쓴다.</summary>
    public void SetMaxHealth(int newMax, bool refill = false)
    {
        maxHealth = Mathf.Max(1, newMax);

        pool?.SetMax(maxHealth, refill);
    }
}
