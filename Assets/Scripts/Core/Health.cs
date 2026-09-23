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
    [SerializeField] private float headArmour = 0f;

    [Tooltip("근접·접촉·폭발 피격에 적용되는 방어도.")]
    [SerializeField] private float bodyArmour = 0f;

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
    /// 마지막으로 들어온 타격이 치명타였는가. 조건부 드롭이 읽는다.
    /// 「온전한 신경절 — 치명타가 아닌 공격으로 처치」 (Hunting 6-3절)
    /// </summary>
    public bool LastHitWasCritical { get; private set; }

    /// <summary>
    /// 살아 있는 동안 한 번이라도 점화된 적이 있는가.
    ///
    /// 【죽는 순간만 보면 안 된다.】 불을 붙였다가 꺼진 뒤에 죽여도
    /// 「점화를 걸지 않고」가 되어 버려 조건이 거짓말이 된다.
    /// 그래서 순간이 아니라 이력을 남긴다. OnSpawned에서 지운다.
    /// </summary>
    public bool EverIgnited { get; private set; }

    /// <summary>지금 이 순간의 처치 조건. 죽을 때 읽는다.</summary>
    public KillContext KillContext => new KillContext
    {
        killedByCritical = LastHitWasCritical,
        frozenAtDeath = Status.Has(StatusEffectType.Freeze),
        everIgnited = EverIgnited
    };

    /// <summary>
    /// 피해 계산에 넘길 방어 정보.
    ///
    /// 방어도 수치의 소재는 장비다. 여기서는 합산된 결과만 들고 있는다.
    /// (경계 — 장비가 수치를 공급하고, 공식은 DamageResolver 한 곳에만 있다)
    /// </summary>
    /// <summary>
    /// 지금 이 순간의 방어 정보.
    ///
    /// 상태이상이 방어도를 바꾼다 — 점화는 깎고, 부식은 절반으로 만든다.
    /// 장비 값을 그대로 주면 「점화가 다음 피해를 키운다」가 성립하지 않는다.
    /// </summary>
    public DefenceProfile Defence
    {
        get
        {
            float multiplier = Status.ArmourMultiplier;
            float reduction = Status.ArmourReduction;
            float bonus = Status.ArmourBonus;

            // 방호(내화제 등)는 장비 저항에 곱해진다. 원본을 건드리지 않으려고
            // 복사본에 적용한다 — resistances는 장비가 넣어 준 값이다.
            ElementalResistances warded = resistances;

            Status.ApplyWards(ref warded);

            return DefenceProfile.Create(
                Mathf.Max(0f, headArmour * multiplier - reduction + bonus),
                Mathf.Max(0f, bodyArmour * multiplier - reduction + bonus),
                warded);
        }
    }

    /// <summary>장비·몬스터 속성이 합산한 방어 수치를 주입한다.</summary>
    public void SetDefence(float head, float body, in ElementalResistances resist)
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
    public void ApplyStatus(StatusEffectType type, float sourceDamage, float durationScale = 1f)
    {
        if (IsDead || type == StatusEffectType.None || sourceDamage <= 0f)
            return;

        // 저항 장비가 면역을 주면 아예 걸리지 않는다. (막는 것은 장비의 몫)
        // 【이로운 상태는 면역이 막지 않는다.】 면역은 해로운 것을 막으라고
        // 붙인 것이다. 각인을 낀 대가로 각성제를 못 쓰게 되면 그것은 규칙이
        // 아니라 사고다.
        if (!StatusEffectTable.IsBeneficial(type) && IsImmuneTo(type))
            return;

        if (type == StatusEffectType.Ignite)
            EverIgnited = true;

        Status.Apply(type, sourceDamage, durationScale);

        StatusEffectSystem.EnsureInstance().Track(this);

        OnStatusChanged?.Invoke(type);
    }

    /// <summary>
    /// 면역을 주는 장비 합산 결과. PlayerLoadout이 착용이 바뀔 때마다 넣어준다.
    /// null이면 면역이 없다 — 적은 장비를 입지 않으므로 계속 null이다.
    /// </summary>
    private EquipmentModifiers immunitySource;

    /// <summary>면역 출처를 연결한다. 참조만 들고 있으므로 장비가 바뀌면 즉시 반영된다.</summary>
    public void SetImmunitySource(EquipmentModifiers source)
    {
        immunitySource = source;
    }

    /// <summary>
    /// 이 상태에 면역인지. 각인 「역치」와 얼굴 방어구 티어 4 이상이 면역을 준다.
    ///
    /// 이 판정이 항상 false였던 동안 역치 각인 3종과 얼굴 마스크 15종이
    /// 아무 일도 하지 않았다. (docs/Blob_Audit.md A절)
    /// </summary>
    public bool IsImmuneTo(StatusEffectType type)
    {
        return immunitySource != null && immunitySource.IsImmuneTo(type);
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

        // 【재생은 피해 목록에 섞이지 않는다.】 버퍼는 피해 요청만 담으므로
        // 회복은 따로 가져온다. 피해보다 먼저 넣어 「회복 중에 죽는」 한 틱을 줄인다.
        int healed = Status.ConsumeHealing();

        if (healed > 0)
            Heal(healed);

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
        // 남겨 두면 재사용된 개체가 이전 출격의 점화를 그대로 들고 나온다.
        Status.ClearAll();

        // 처치 조건의 이력도 같이 지운다. 안 지우면 이전에 태웠던 개체가
        // 재사용될 때마다 「미연소 포자」가 영영 나오지 않는다.
        LastHitWasCritical = false;
        EverIgnited = false;

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

        // 【0 피해는 기록하지 않는다.】 빗나간 타격이 「마지막 타격」이 되면
        // 조건부 드롭의 판정이 실제로 죽인 공격과 어긋난다.
        LastHitWasCritical = request.isCritical;

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

    /// <summary>
    /// 회복한다. 【부식 중이면 절반만 회복된다.】
    ///
    /// 소모품이 만능이 아니게 하는 유일한 장치다 —
    /// 회복약 하나로 모든 상황이 풀리면 가방을 그것만으로 채우게 된다.
    /// (docs/Blob_Combat_Baseline.md 「위험 상태」)
    /// </summary>
    public int Heal(int amount)
    {
        if (pool == null || amount <= 0)
            return 0;

        float multiplier = Status.HealingMultiplier;

        // 절반이 되어도 최소 1은 회복한다. 0이 되면 "약을 썼는데 아무 일도 없다"가 된다.
        int scaled = multiplier < 1f
            ? Mathf.Max(1, Mathf.FloorToInt(amount * multiplier))
            : amount;

        return pool.Heal(scaled);
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
