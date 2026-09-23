using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Skill 한 종류의 정의 — Skill System v5 확정 스키마.
///
/// 저장 위치: Assets/Data/ScriptableObjects/Skills/ (마스터 프롬프트 10-10)
///
/// 설계 원칙 (10-1 절대 금지 사항 7개를 스키마 수준에서 방어한다):
/// - 단순 수치 증가만 주는 정의를 만들지 않는다. 메커니즘을 바꾼다.
/// - 대가에 피해 감소를 쓸 수 없다. CostType enum에 Damage가 없다.
/// - 티어(I/II/III)를 두지 않는다. 중첩 필드가 없다. 모든 Skill은 단일 정의다.
/// - 소환수 계열을 두지 않는다. 태그에 소환수가 없다.
/// </summary>
[CreateAssetMenu(fileName = "Skill_", menuName = "Blob/Skill Definition")]
public class SkillDefinition : ScriptableObject
{
    // ────────────────────────────────── 식별

    [Header("식별")]
    [Tooltip("저장(도감·장비 구성)과 비교에 쓰이는 고유 식별자. 중복되면 안 된다.")]
    [SerializeField] private string id = "mutation_id";

    [Tooltip("한글 표시명. v5 §6~§9 목록의 이름을 그대로 쓴다.")]
    [SerializeField] private string displayName = "이름 없음";

    [TextArea(2, 4)]
    [SerializeField] private string description = "설명 없음";

    [Header("분류")]
    [SerializeField] private SkillCategory category = SkillCategory.Core;

    [Tooltip("핵심 젬일 때만 의미가 있다. 전달 / 칸 / 기폭")]
    [SerializeField] private CoreFamily coreFamily = CoreFamily.None;

    [Tooltip("이 레벨 미만에서는 선택지에 등장하지 않는다. v5 목록의 Lv 값.")]
    [Min(1)]
    [SerializeField] private int requiredLevel = 1;

    // ────────────────────────────────── 태그

    [Header("태그 (10-2 [1] 태그 조건)")]
    [Tooltip("이 Skill이 보유한 태그.")]
    [SerializeField] private SkillTag tags = SkillTag.None;

    [Tooltip("보조 젬 전용. 장착 대상 핵심 젬이 이 태그를 전부 가져야 장착 가능하다. " +
             "None이면 태그 제약이 없다.")]
    [SerializeField] private SkillTag requiredTags = SkillTag.None;

    // ────────────────────────────────── 상태 어휘

    [Header("상태 어휘 (10-2 [2])")]
    [Tooltip("이 Skill이 적에게 생성하는 상태.")]
    [SerializeField] private StatusEffectType createsStatus = StatusEffectType.None;

    [Tooltip("이 Skill이 소모하는 상태. 원소 작렬처럼 소모 대상이 가스킬면 None으로 두고 런타임에 판정한다.")]
    [SerializeField] private StatusEffectType consumesStatus = StatusEffectType.None;

    [Tooltip("이 Skill이 바닥에 남기는 지형 상태.")]
    [SerializeField] private GroundEffectType createsGroundEffect = GroundEffectType.None;

    [Header("기능 배타 (10-2 [3])")]
    [Tooltip("켜면 이 Skill을 보유하는 동안 blockedStatus를 유발할 수 없게 된다. " +
             "번제 / 감전 전도 / 독성 축적 / 유혈 충동 패턴.")]
    [SerializeField] private bool blocksStatusCreation = false;

    [Tooltip("blocksStatusCreation이 켜졌을 때 유발이 차단되는 상태.")]
    [SerializeField] private StatusEffectType blockedStatus = StatusEffectType.None;

    [Tooltip("이 id들과 동시에 장착되면 양쪽 모두 무효화된다. 긴 퓨즈 ↔ 짧은 퓨즈. " +
             "※ 획득 자체는 막지 않는다. 중복 금지로 다양성을 강제하지 않는다는 10-1-2 원칙 때문이다.")]
    [SerializeField] private string[] mutuallyExclusiveIds = new string[0];

    // ────────────────────────────────── 자원

    // ────────────────────────────────── 대가

    [Header("대가 (10-3 — 4종만 사용, 피해 감소 금지)")]
    [SerializeField] private CostType costType = CostType.None;

    [Tooltip("플레이어에게 보여줄 대가 문구.")]
    [SerializeField] private string costDescription = string.Empty;

    // ────────────────────────────────── 효과 — 투사체

    [Header("효과 — 투사체 행동 (충돌 우선순위 큐)")]
    [Tooltip("부여하는 충돌 행동. None이면 행동을 주지 않는다.")]
    [SerializeField] private ProjectileBehaviourType grantedBehaviour = ProjectileBehaviourType.None;

    [Tooltip("부여하는 행동 횟수.")]
    [Min(0)]
    [SerializeField] private int behaviourCharges = 0;

    [Tooltip("지형 충돌 튕김 횟수. 「튕겨 쏘기」 3회 / 「추가 튕김」 +3. " +
             "충돌 우선순위 큐와 별개로 동작한다. (v5 §10)")]
    [Min(0)]
    [SerializeField] private int ricochetBounces = 0;

    [Header("효과 — 발사 형태")]
    [Tooltip("기본 1발에 더해지는 동시 발사 수.")]
    [Min(0)]
    [SerializeField] private int extraProjectiles = 0;

    [Tooltip("추가 투사체 간 각도(도).")]
    [SerializeField] private float spreadAngle = 8f;

    [Header("효과 — 배율 (전부 대가 4종 범위 안)")]
    [Tooltip("발사 간격 배율. 1보다 크면 연사가 느려진다. → 탄막 밀도")]
    [Min(0.1f)]
    [SerializeField] private float fireIntervalMultiplier = 1f;

    [Tooltip("투사체 수명 배율. 1보다 작으면 사거리가 줄어든다. → 유효 사거리")]
    [Min(0.1f)]
    [SerializeField] private float lifetimeMultiplier = 1f;

    [Header("Effect — 효과")]
    // 여기부터가 「이 보조 젬이 무엇을 해 주는가」다.
    //
    // 이 축이 없던 동안 보조 젬 35종 중 21종은 대가만 적용되고 효과가 없었다.
    // 끼우면 손해만 보는 젬이었다. (docs/Blob_Audit.md D1)
    //
    // 전부 가산 합산이다. PoE의 「증가 / 더 증가」 2단 구조를 쓰지 않는다 —
    // 모바일에서 플레이어가 곱연산 폭발을 예측할 수 없다.

    [Tooltip("기본 피해 증가율. 0.2 = +20%")]
    [SerializeField] private float damageIncrease = 0f;

    [Tooltip("상태이상 위력 증가율. 직접 피해와 분리된 축이다.")]
    [SerializeField] private float ailmentPower = 0f;

    [Tooltip("상태이상·잔류물 지속시간 배율. 【투사체 수명과 다른 축이다.】")]
    [Min(0.1f)]
    [SerializeField] private float ailmentDurationMultiplier = 1f;

    [Tooltip("유효 사거리 배율. 대가 「유효 사거리」가 여기를 깎는다.")]
    [Min(0.1f)]
    [SerializeField] private float rangeMultiplier = 1f;

    [Tooltip("잔류물 반경 배율. 【유효 사거리와 다른 축이다.】")]
    [Min(0.1f)]
    [SerializeField] private float zoneRadiusMultiplier = 1f;

    [Header("Effect — 조건부")]
    [Tooltip("조건. 명중 시점에 판정한다.")]
    [SerializeField] private SkillConditionKind conditionKind = SkillConditionKind.None;

    [Tooltip("TargetHasStatus일 때 볼 상태이상.")]
    [SerializeField] private StatusEffectType conditionStatus = StatusEffectType.None;

    [Tooltip("조건을 만족할 때 추가로 더해지는 피해 증가율.")]
    [SerializeField] private float conditionalDamageIncrease = 0f;

    [Header("Effect — 속성 전환")]
    // 속성 전환 보조 젬이 자기가 꽂힌 핵심 젬의 부여 속성을 바꾼다.
    //
    // 이 축이 배타형 보조 젬을 살린다 —
    // 「번제」가 화염 핵심 젬의 점화를 끄면, 「화염 조율」을 낀 다른 핵심 젬이
    // 점화를 대신 공급한다. 그래야 「점화된 적에게 큰 피해」가 성립한다.
    // (docs/Blob_Audit.md D3)

    [Tooltip("이 보조 젬이 꽂힌 핵심 젬의 부여 속성을 이것으로 바꾼다. (전환)")]
    [SerializeField] private StatusEffectType ailmentOverride = StatusEffectType.None;

    [Tooltip("원래 속성을 유지한 채 이것을 추가로 부여한다. (융합)")]
    [SerializeField] private StatusEffectType ailmentAddition = StatusEffectType.None;

    [Tooltip("투사체 속도 배율.")]
    [Min(0.1f)]
    [SerializeField] private float speedMultiplier = 1f;

    // ────────────────────────────────── 읽기 전용 접근자

    public string Id => id;
    public string DisplayName => displayName;
    public string Description => description;

    public SkillCategory Category => category;
    public CoreFamily Family => coreFamily;
    public int RequiredLevel => Mathf.Max(1, requiredLevel);

    public SkillTag Tags => tags;
    public SkillTag RequiredTags => requiredTags;

    public StatusEffectType CreatesStatus => createsStatus;
    public StatusEffectType ConsumesStatus => consumesStatus;
    public GroundEffectType CreatesGroundEffect => createsGroundEffect;

    public bool BlocksStatusCreation => blocksStatusCreation;
    public StatusEffectType BlockedStatus => blockedStatus;
    public IReadOnlyList<string> MutuallyExclusiveIds => mutuallyExclusiveIds;


    public CostType Cost => costType;
    public string CostDescription => costDescription;

    public ProjectileBehaviourType GrantedBehaviour => grantedBehaviour;
    public int BehaviourCharges => Mathf.Max(0, behaviourCharges);
    public int RicochetBounces => Mathf.Max(0, ricochetBounces);
    public int ExtraProjectiles => Mathf.Max(0, extraProjectiles);
    public float SpreadAngle => spreadAngle;

    public float FireIntervalMultiplier => Mathf.Max(0.1f, fireIntervalMultiplier);
    public float LifetimeMultiplier => Mathf.Max(0.1f, lifetimeMultiplier);
    public float SpeedMultiplier => Mathf.Max(0.1f, speedMultiplier);

    // ── 효과 ──────────────────────────────────────────────────────────
    public float DamageIncrease => damageIncrease;
    public float AilmentPower => ailmentPower;
    public float AilmentDurationMultiplier => Mathf.Max(0.1f, ailmentDurationMultiplier);
    public float RangeMultiplier => Mathf.Max(0.1f, rangeMultiplier);

    /// <summary>
    /// 잔류물 반경 배율.
    ///
    /// 【rangeMultiplier와 나눠 둔 이유 — 같은 실수가 두 번 났다.】
    /// 문서 5-A는 이미 「ailmentDurationMultiplier와 lifetimeMultiplier는
    /// 다른 축이다」를 경고하고 있었다. 그런데 잔류물 반경에는 축이 아예 없어서
    ///   · 「잔류물 효력」(잔류물 범위가 커진다)  → 담을 곳이 없어 구현이 빠졌다
    ///   · 「유지되는 대지」(대가: 잔류물 범위 -25%) → rangeMultiplier를 깎아
    ///     엉뚱하게 유효 사거리가 줄었다
    /// 축이 없으면 설명과 구현이 갈라진다. 그래서 축을 만든다.
    ///
    /// PoE2에도 「효과 범위 증가」가 독립 유형으로 있다.
    /// [확인됨 — docs/research/poe2/03_보조젬_유형표.md]
    /// </summary>
    public float ZoneRadiusMultiplier => Mathf.Max(0.1f, zoneRadiusMultiplier);

    public StatusEffectType AilmentOverride => ailmentOverride;
    public StatusEffectType AilmentAddition => ailmentAddition;

    public SkillConditionKind ConditionKind => conditionKind;
    public StatusEffectType ConditionStatus => conditionStatus;
    public float ConditionalDamageIncrease => conditionalDamageIncrease;

    /// <summary>조건부 효과. 조건이 없으면 IsValid가 false다.</summary>
    public SkillCondition Condition =>
        new(conditionKind, conditionStatus, conditionalDamageIncrease);

    /// <summary>
    /// 이 스킬이 무언가를 해 주는지.
    ///
    /// 「대가만 있고 효과가 없는 젬은 존재할 수 없다」를 테스트로 강제하는 데 쓴다.
    /// 상태 생성·소모·차단·잔류물도 효과로 친다 — 수치만 효과인 것은 아니다.
    /// </summary>
    public bool HasEffect =>
        grantedBehaviour != ProjectileBehaviourType.None
        || ricochetBounces > 0
        || extraProjectiles > 0
        || damageIncrease != 0f
        || ailmentPower != 0f
        || ailmentDurationMultiplier != 1f
        || rangeMultiplier != 1f
        || zoneRadiusMultiplier != 1f
        || conditionKind != SkillConditionKind.None
        || createsStatus != StatusEffectType.None
        || consumesStatus != StatusEffectType.None
        || createsGroundEffect != GroundEffectType.None
        || ailmentOverride != StatusEffectType.None
        || ailmentAddition != StatusEffectType.None
        || blocksStatusCreation
        || speedMultiplier > 1f
        || lifetimeMultiplier > 1f
        || fireIntervalMultiplier < 1f;

    /// <summary>전령인지. 전령은 동시에 1개만 장착 가능하다. (v8 §8-1)</summary>
    public bool IsHerald => category == SkillCategory.Persistent;

    /// <summary>이 정의가 다른 정의와 상호 배타 관계인지.</summary>
    public bool IsMutuallyExclusiveWith(SkillDefinition other)
    {
        if (other == null || other == this)
            return false;

        for (int i = 0; i < mutuallyExclusiveIds.Length; i++)
        {
            if (mutuallyExclusiveIds[i] == other.id)
                return true;
        }

        return false;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 에디터에서 기획 실수를 즉시 잡는다.
    /// 런타임 검증이 아니라 에셋 작성 시점 검증이므로 빌드에 포함되지 않는다.
    /// </summary>
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(id))
            id = name;

        if (category != SkillCategory.Core)
            coreFamily = CoreFamily.None;

        // 유지형 태그는 유지형 젬(전령)의 정의상 필수다.
        if (category == SkillCategory.Persistent)
            tags |= SkillTag.Persistent;

        // requiredTags는 보조 젬 전용 개념이다. (10-2 [1])
        if (category != SkillCategory.Support)
            requiredTags = SkillTag.None;

        if (!blocksStatusCreation)
            blockedStatus = StatusEffectType.None;
    }
#endif
}
