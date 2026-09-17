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
    [Tooltip("저장(도감·적재)과 비교에 쓰이는 고유 식별자. 중복되면 안 된다.")]
    [SerializeField] private string id = "mutation_id";

    [Tooltip("한글 표시명. v5 §6~§9 목록의 이름을 그대로 쓴다.")]
    [SerializeField] private string displayName = "이름 없음";

    [TextArea(2, 4)]
    [SerializeField] private string description = "설명 없음";

    [Header("분류")]
    [SerializeField] private SkillCategory category = SkillCategory.Core;

    [Tooltip("Core일 때만 의미가 있다. 전달 / 적재 / 기폭")]
    [SerializeField] private CoreFamily coreFamily = CoreFamily.None;

    [Tooltip("이 레벨 미만에서는 선택지에 등장하지 않는다. v5 목록의 Lv 값.")]
    [Min(1)]
    [SerializeField] private int requiredLevel = 1;

    // ────────────────────────────────── 태그

    [Header("태그 (10-2 [1] 태그 게이팅)")]
    [Tooltip("이 Skill이 보유한 태그.")]
    [SerializeField] private SkillTag tags = SkillTag.None;

    [Tooltip("Support 전용. 장착 대상 Core가 이 태그를 전부 가져야 장착 가능하다. " +
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

    [Tooltip("유저에게 보여줄 대가 문구.")]
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

    [Header("효과 — 배수 (전부 대가 4종 범위 안)")]
    [Tooltip("발사 간격 배수. 1보다 크면 연사가 느려진다. → 탄막 밀도")]
    [Min(0.1f)]
    [SerializeField] private float fireIntervalMultiplier = 1f;

    [Tooltip("투사체 수명 배수. 1보다 작으면 사거리가 줄어든다. → 유효 사거리")]
    [Min(0.1f)]
    [SerializeField] private float lifetimeMultiplier = 1f;

    [Tooltip("투사체 속도 배수.")]
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

        // 유지형 태그는 Persistent(전령)의 정의상 필수다.
        if (category == SkillCategory.Persistent)
            tags |= SkillTag.Persistent;

        // requiredTags는 Support 전용 개념이다. (10-2 [1])
        if (category != SkillCategory.Support)
            requiredTags = SkillTag.None;

        if (!blocksStatusCreation)
            blockedStatus = StatusEffectType.None;
    }
#endif
}
