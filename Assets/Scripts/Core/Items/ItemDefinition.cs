using UnityEngine;

/// <summary>
/// 모든 아이템의 공통 정의. 장비·인자·전리품이 이것을 공유한다.
///
/// 무게와 적재 공간을 여기에 두는 이유 —
/// 인자가 실물 아이템이 되면서 장비·전리품과 같은 인벤토리를 쓰게 되었다.
/// "인자를 많이 챙겨 화력을 확보할까, 가방을 비워 전리품 공간을 남길까"라는
/// 결정이 성립하려면 셋이 같은 자원을 두고 경쟁해야 한다.
/// </summary>
[CreateAssetMenu(fileName = "Item", menuName = "Blob/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("저장·참조용 고유 식별자. 변경하면 세이브가 깨진다.")]
    [SerializeField] private string id = "item_id";

    [SerializeField] private string displayName = "이름 없음";

    [TextArea(2, 4)]
    [SerializeField] private string description = "설명 없음";

    [SerializeField] private ItemKind kind = ItemKind.Material;

    [Header("Tier")]
    [Tooltip("성능 대역 1~6. 재료·소모품은 0으로 둔다.")]
    [Range(0, 6)]
    [SerializeField] private int tier = 0;

    [Header("Carry")]
    [Tooltip("무게(kg). 소수점 1자리.")]
    [Min(0f)]
    [SerializeField] private float weight = 0.5f;

    [Tooltip("차지하는 적재 칸 수. 대부분 1이다.")]
    [Min(1)]
    [SerializeField] private int slotSize = 1;

    [Tooltip("한 칸에 겹칠 수 있는 최대 개수. 1이면 겹치지 않는다.")]
    [Min(1)]
    [SerializeField] private int stackMax = 1;

    [Header("Durability")]
    [Tooltip("최대 내구도. 0이면 내구도가 없는 아이템이다.")]
    [Min(0)]
    [SerializeField] private int maxDurability = 0;

    [Header("Economy")]
    [Tooltip("기본 가치. 상인 판매가는 이 값의 50%다.")]
    [Min(0)]
    [SerializeField] private int baseValue = 0;

    [Header("Skill Gem")]
    [Tooltip("인자일 때 어떤 스킬인지. 다른 종류에서는 비워 둔다.")]
    [SerializeField] private SkillDefinition skill;

    public string Id => id;
    public string DisplayName => displayName;
    public string Description => description;
    public ItemKind Kind => kind;
    public int Tier => tier;
    public float Weight => weight;
    public int SlotSize => Mathf.Max(1, slotSize);
    public int StackMax => Mathf.Max(1, stackMax);
    public int MaxDurability => Mathf.Max(0, maxDurability);
    public int BaseValue => baseValue;

    /// <summary>인자가 가리키는 스킬 정의. 인자가 아니면 null.</summary>
    public SkillDefinition Skill => skill;

    public bool HasDurability => MaxDurability > 0;
    public bool IsStackable => StackMax > 1;

    /// <summary>인자인지. 스킬 참조가 있어야 인자로 인정한다.</summary>
    public bool IsSkillGem => kind == ItemKind.SkillGem && skill != null;

    /// <summary>
    /// 추출에 실패해도 잃지 않는 아이템인지.
    ///
    /// 각인만 해당한다. 유저가 배울 규칙은 하나여야 하므로 예외를 늘리지 않는다.
    /// (docs/Blob_Progression_System.md 6절)
    /// </summary>
    public bool SurvivesDeath => kind == ItemKind.Imprint;

    private void OnValidate()
    {
        // 겹치는 아이템에 내구도를 두면 "내구도가 다른 것들을 어떻게 겹치는가"가 모호해진다.
        if (IsStackable && maxDurability > 0)
            maxDurability = 0;

        // 인자·장비는 개별 상태를 가지므로 겹치지 않는다.
        if (kind == ItemKind.Weapon || kind == ItemKind.Armour
            || kind == ItemKind.Backpack || kind == ItemKind.Imprint
            || kind == ItemKind.SkillGem)
        {
            stackMax = 1;
        }

        if (kind != ItemKind.SkillGem)
            skill = null;
    }
}
