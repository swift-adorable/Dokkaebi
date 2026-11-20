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

    [System.NonSerialized] private HealthPool pool;   // 다시 컴파일할 때 유니티가 빈 풀(최대 0)을 만들어 넣지 않게 — 프리팹 체력이 0 으로 읽혔다
    private CooldownTimer invulnerability;
    private ElementalResistances resistances = ElementalResistances.Default;

    /// <summary>피해를 입었을 때. (실제 피해량, 현재 체력)</summary>
    public event Action<int, int> OnDamaged;

    /// <summary>체력이 0이 되었을 때. 한 번만 발행된다.</summary>
    public event Action OnDied;

    /// <summary>
    /// 총알·적 공격에 【직접】 맞았을 때. 상태이상 도트와 허기는 여기 오지 않는다.
    ///
    /// 소모품 시전이 이것을 듣는다. HP가 줄었는지로 판정하면 점화 중에는
    /// 붕대를 감을 수 없고, 폭주(부자탕) 중에는 회복이 아예 불가능해진다 —
    /// 폭주는 스스로 1초마다 피를 깎기 때문이다.
    /// </summary>
    public event Action OnDirectHit;

    /// <summary>직접 타격이 실제로 들어갔다 — 장비 닳기(PlayerWear)가 듣는다 (결정 2-96).</summary>
    public event Action<DamageRequest, int> OnArmourHit;

    /// <summary>이 대상의 소속.</summary>
    public Team Team => team;

    public int Max => pool?.Max ?? maxHealth;
    public int Current => pool?.Current ?? maxHealth;
    public float Normalized => pool?.Normalized ?? 1f;
    public bool IsDead => pool != null && pool.IsDead;

    /// <summary>현재 무적 상태인지.</summary>
    public bool IsInvulnerable => !invulnerability.IsReady(Time.time);

    /// <summary>껍질 — 남은 「직접 맞은 한 번을 통째로 막는」 횟수 (적 속성 · 결정 2-117). 상태이상 틱은 막지 않는다.</summary>
    public int ShellCharges { get; set; }

    /// <summary>껍질이 깨졌다 — 표시용.</summary>
    public event Action OnShellBroken;

    /// <summary>육중 — 동결 · 마비(행동 불능)에 걸리지 않는다 (적 속성 · 결정 2-117).</summary>
    public bool ImmuneToIncapacitation { get; set; }

    /// <summary>이 대상에게 걸린 상태이상.</summary>
    public StatusEffectState Status { get; } = new StatusEffectState();

    /// <summary>
    /// 마지막으로 들어온 타격이 치명타였는가 (KillContext에 담긴다).
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
        everIgnited = EverIgnited,
        killedByPlayer = LastHitByPlayer
    };

    /// <summary>마지막으로 때린 쪽이 플레이어인가 (상태이상 틱은 바꾸지 않는다 · 결정 2-93).</summary>
    public bool LastHitByPlayer { get; private set; }

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

            // 저항(석고환 등)는 장비 저항에 곱해진다. 원본을 건드리지 않으려고
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
    /// <param name="byPlayer">플레이어가 건 것인가 — 새로 걸린 점화 · 동결 · 감전을 발동 구슬에 알린다 (결정 2-102)</param>
    public void ApplyStatus(StatusEffectType type, float sourceDamage, float durationScale = 1f, bool byPlayer = false)
    {
        if (IsDead || type == StatusEffectType.None || sourceDamage <= 0f)
            return;

        // 저항 장비가 면역을 주면 아예 걸리지 않는다. (막는 것은 장비의 몫)
        // 【이로운 상태는 면역이 막지 않는다.】 면역은 해로운 것을 막으라고
        // 붙인 것이다. 각인을 낀 대가로 축지환을 못 쓰게 되면 그것은 규칙이
        // 아니라 사고다.
        if (!StatusEffectTable.IsBeneficial(type) && IsImmuneTo(type))
            return;

        if (type == StatusEffectType.Ignite)
            EverIgnited = true;

        bool report = byPlayer && Team != Team.Player;
        bool hadIgnite = report && Status.Has(StatusEffectType.Ignite);
        bool hadFreeze = report && Status.Has(StatusEffectType.Freeze);
        bool hadShock = report && Status.Has(StatusEffectType.Shock);

        Status.Apply(type, sourceDamage, durationScale);

        // 육중 — 한계치로 넘어간 행동 불능은 바로 지운다(쌓인 냉기 · 감전은 그대로 셈해 둔다).
        if (ImmuneToIncapacitation)
        {
            Status.Clear(StatusEffectType.Freeze);
            Status.Clear(StatusEffectType.Paralyze);
        }

        StatusEffectSystem.EnsureInstance().Track(this);

        OnStatusChanged?.Invoke(type);

        // 냉기가 한계를 넘어 동결로 바뀐 것도 「동결을 걸었다」로 센다.
        if (report)
        {
            if (!hadIgnite && Status.Has(StatusEffectType.Ignite)) PlayerHitEvents.ReportInflicted(this, StatusEffectType.Ignite);
            if (!hadFreeze && Status.Has(StatusEffectType.Freeze)) PlayerHitEvents.ReportInflicted(this, StatusEffectType.Freeze);
            if (!hadShock && Status.Has(StatusEffectType.Shock))   PlayerHitEvents.ReportInflicted(this, StatusEffectType.Shock);
        }
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
    /// 아무 일도 하지 않았다. (docs/Dokkaebi_Audit.md A절)
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

        // 【회복은 피해 목록에 섞이지 않는다.】 버퍼는 피해 요청만 담으므로
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

    /// <summary>받는 피해 배율 — 보스 패턴만 바꾼다 (결정 2-110). 0이면 피해를 받지 않는다. 다시 나올 때 1로.</summary>
    public float IncomingDamageScale { get; set; } = 1f;

    public void OnSpawned()
    {
        IncomingDamageScale = 1f;
        // 풀 재사용 시 체력을 되돌리지 않으면 죽은 상태로 스폰되어 즉사한다.
        pool.ResetToFull();
        invulnerability.Reset();

        // 상태이상도 반드시 함께 초기화한다.
        // 남겨 두면 재사용된 개체가 이전 파밍의 점화를 그대로 들고 나온다.
        Status.ClearAll();

        // 처치 조건의 이력도 같이 지운다. 안 지우면 이전에 태웠던 개체가
        // 재사용될 때마다 「미연소 포자」가 영영 나오지 않는다.
        LastHitWasCritical = false;
        EverIgnited = false;
        LastHitByPlayer = false;

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

        // 껍질 — 첫 직접 피격 한 번은 통째로 막는다. 답은 다중 적중 (Hunting_System 3절).
        if (!request.bypassArmour && ShellCharges > 0 && request.baseDamage > 0)
        {
            ShellCharges--;
            OnShellBroken?.Invoke();
            return 0;
        }

        DamageRequest amplified = request;

        // 감전은 받는 피해를 늘린다. 증폭은 피격자 쪽에서 적용해야
        // 여러 공격자가 있어도 한 번만 계산된다.
        float taken = Status.DamageTakenMultiplier;

        if (taken > 1f)
            amplified.increasedPercent += taken - 1f;

        // 날씨가 키우는 속성 — 장마비에 감전 2배 (결정 2-64). 맞는 쪽에서 한 번만.
        float environment = RaidManager.HasInstance ? RaidManager.Current.DamageMultiplier(request.element) : 1f;
        if (environment > 1f)
            amplified.increasedPercent += environment - 1f;

        int computed = DamageResolver.Resolve(amplified, Defence, difficultyMultiplier);

        // 보스 패턴이 받는 피해를 바꾼다 — 흐려짐 · 뿌리내림 · 함께 섬 (결정 2-110). 평소 1.
        if (IncomingDamageScale != 1f && computed > 0)
            computed = Mathf.Max(IncomingDamageScale > 0f ? 1 : 0, Mathf.RoundToInt(computed * IncomingDamageScale));

        if (computed <= 0)
            return 0;

        // 【0 피해는 기록하지 않는다.】 빗나간 타격이 「마지막 타격」이 되면
        // 조건부 드롭의 판정이 실제로 죽인 공격과 어긋난다.
        LastHitWasCritical = request.isCritical;

        if (request.source != DamageSource.Unknown)
            LastHitByPlayer = request.source == DamageSource.Player;

        // bypassArmour는 상태이상 틱만 켠다(StatusEffectState.Tick). 그래서
        // 이것이 꺼져 있으면 누군가 실제로 쏘거나 때린 것이다.
        if (!request.bypassArmour)
            OnDirectHit?.Invoke();

        int dealt = ApplyRaw(computed, skipInvulnerability: request.bypassArmour);

        // 치명타 시 시전 (결정 2-102) — 플레이어의 치명타가 적에게 실제로 들어갔다.
        if (dealt > 0 && request.isCritical && request.source == DamageSource.Player && Team != Team.Player)
            PlayerHitEvents.ReportCritical(this);

        // 장비 닳기 (결정 2-96) — 실제로 들어간 직접 타격만. 상태이상 틱은 닳게 하지 않는다.
        if (dealt > 0 && !request.bypassArmour)
            OnArmourHit?.Invoke(request, dealt);

        return dealt;
    }

    /// <summary>
    /// 허기 · 갈증 · 동상 · 흙비처럼 몸이 깎이는 피해 (결정 2-99). 맞은 뒤 무적을 켜지도 않고, 무적 중에도 들어간다 —
    /// 켜면 동상만으로 적 공격의 40%쯤을 흘려 맞지 않게 되고, 무적 중의 틱은 사라졌다.
    /// </summary>
    public int TakeTickDamage(int amount) => ApplyRaw(amount, skipInvulnerability: true);

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
    /// (docs/Dokkaebi_Combat_Baseline.md 「위험 상태」)
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
