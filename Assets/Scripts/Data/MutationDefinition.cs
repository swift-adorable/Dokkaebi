using UnityEngine;

/// <summary>
/// Mutation 한 종류의 정의. ScriptableObject로 데이터화하여 코드 수정 없이 추가한다.
///
/// 설계 원칙:
/// - 단순 수치 증가만 주는 Mutation은 만들지 않는다. 메커니즘을 바꾼다.
/// - 메커니즘을 얻으면 대가를 치른다. 모바일에서는 DPS 페널티가 체감되지 않으므로
///   연사속도/사거리/탄막 밀도를 통화로 쓴다.
/// </summary>
[CreateAssetMenu(fileName = "Mutation_", menuName = "Blob/Mutation Definition")]
public class MutationDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("저장/비교에 쓰이는 고유 식별자. 중복되면 안 된다.")]
    [SerializeField] private string id = "mutation_id";

    [SerializeField] private string displayName = "이름 없음";

    [TextArea(2, 4)]
    [SerializeField] private string description = "설명 없음";

    [SerializeField] private MutationRarity rarity = MutationRarity.Common;

    [Tooltip("최대 중첩 횟수. 이 횟수에 도달하면 선택지에 더 이상 나오지 않는다.")]
    [Min(1)]
    [SerializeField] private int maxStacks = 1;

    [Header("부여 — 투사체 행동")]
    [Tooltip("이 Mutation이 부여하는 충돌 행동. None이면 행동을 주지 않는다.")]
    [SerializeField] private ProjectileBehaviourType grantedBehaviour = ProjectileBehaviourType.None;

    [Tooltip("1중첩당 부여하는 행동 횟수")]
    [Min(0)]
    [SerializeField] private int chargesPerStack = 1;

    [Header("부여 — 발사 형태")]
    [Tooltip("1중첩당 추가되는 동시 발사 수")]
    [Min(0)]
    [SerializeField] private int extraProjectilesPerStack = 0;

    [Tooltip("추가 투사체 간 각도(도)")]
    [SerializeField] private float spreadAngle = 8f;

    [Header("비용 — 1중첩당 곱해지는 값")]
    [Tooltip("발사 간격 배수. 1보다 크면 연사가 느려진다.")]
    [Min(0.1f)]
    [SerializeField] private float fireIntervalMultiplier = 1f;

    [Tooltip("투사체 수명 배수. 1보다 작으면 사거리가 줄어든다.")]
    [Min(0.1f)]
    [SerializeField] private float lifetimeMultiplier = 1f;

    [Tooltip("투사체 속도 배수")]
    [Min(0.1f)]
    [SerializeField] private float speedMultiplier = 1f;

    public string Id => id;
    public string DisplayName => displayName;
    public string Description => description;
    public MutationRarity Rarity => rarity;
    public int MaxStacks => Mathf.Max(1, maxStacks);

    public ProjectileBehaviourType GrantedBehaviour => grantedBehaviour;
    public int ChargesPerStack => Mathf.Max(0, chargesPerStack);
    public int ExtraProjectilesPerStack => Mathf.Max(0, extraProjectilesPerStack);
    public float SpreadAngle => spreadAngle;

    public float FireIntervalMultiplier => Mathf.Max(0.1f, fireIntervalMultiplier);
    public float LifetimeMultiplier => Mathf.Max(0.1f, lifetimeMultiplier);
    public float SpeedMultiplier => Mathf.Max(0.1f, speedMultiplier);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(id))
            id = name;
    }
#endif
}
