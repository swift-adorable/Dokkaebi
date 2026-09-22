using UnityEngine;

/// <summary>
/// 모든 아이템의 공통 정의. 장비·젬·전리품이 이것을 공유한다.
///
/// 무게와 적재 공간을 여기에 두는 이유 —
/// 젬이 실물 아이템이 되면서 장비·전리품과 같은 인벤토리를 쓰게 되었다.
/// "젬을 많이 챙겨 화력을 확보할까, 가방을 비워 전리품 공간을 남길까"라는
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

    [Tooltip("아이콘. 비워 두면 UI가 종류별 색 판으로 대신 그린다.")]
    [SerializeField] private Sprite icon;

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

    [Header("Tags")]
    [Tooltip("반출 불가. 출격 안에서만 쓰이고 추출해도 남지 않는다.")]
    [SerializeField] private bool noExtract = false;

    [Tooltip("등록 불가. 등록대에 넣을 수 없어 매번 들고 가야 한다. (최고 등급 열쇠)")]
    [SerializeField] private bool noRegister = false;

    [Tooltip("거래 불가. 상인에게 팔 수 없다. (각인)")]
    [SerializeField] private bool noTrade = false;

    [Header("Skill Gem")]
    [Tooltip("젬일 때 어떤 스킬인지. 다른 종류에서는 비워 둔다.")]
    [SerializeField] private SkillDefinition skill;

    public string Id => id;
    public string DisplayName => displayName;
    public string Description => description;
    public ItemKind Kind => kind;
    public int Tier => tier;
    public float Weight => weight;
    /// <summary>아이콘. 아직 아트가 없으므로 null이 정상이다.</summary>
    public Sprite Icon => icon;

    public int SlotSize => Mathf.Max(1, slotSize);
    public int StackMax => Mathf.Max(1, stackMax);
    public int MaxDurability => Mathf.Max(0, maxDurability);
    public int BaseValue => baseValue;

    /// <summary>젬이 가리키는 스킬 정의. 젬이 아니면 null.</summary>
    public SkillDefinition Skill => skill;

    /// <summary>
    /// 반출 불가.
    ///
    /// 「출격 안에서만 사는 축」을 만드는 태그다. 덕코프의 「강화」가 그 자리인데
    /// Blob의 젬은 추출 가능하므로 이 축이 비어 있었다.
    /// 지금은 태그만 두고, 쓰는 아이템은 7단계 이후에 만든다.
    /// </summary>
    public bool NoExtract => noExtract;

    /// <summary>등록대에 넣을 수 없는지. 최고 등급 문은 매번 열쇠를 들고 가야 한다.</summary>
    public bool NoRegister => noRegister;

    /// <summary>
    /// 상인에게 팔 수 없는지.
    ///
    /// 각인은 거래 불가다 — 사망에도 남는 것을 사고팔 수 있으면
    /// 「교환으로만 얻는다」는 각인의 설계가 돈으로 풀린다.
    /// </summary>
    public bool NoTrade => noTrade;

    public bool HasDurability => MaxDurability > 0;
    public bool IsStackable => StackMax > 1;

    /// <summary>젬인지. 스킬 참조가 있어야 젬으로 인정한다.</summary>
    public bool IsSkillGem => kind == ItemKind.SkillGem && skill != null;

    /// <summary>
    /// 가방에서 칸을 쓰지 않는가. 【스킬 젬만 해당한다.】
    ///
    /// 【왜 젬을 칸에서 빼는가】
    /// 젬은 "챙겨 오는 물건"이 아니라 빌드 그 자체다. 칸을 두고 전리품과
    /// 경쟁시키면 유저가 내리는 결정은 "화력이냐 전리품이냐"가 아니라
    /// "쓰지도 않을 젬을 버려야 하나"가 된다. 소켓 자리 수가 이미 젬의
    /// 상한이고, 그 상한은 각성 레벨이 정한다 — 가방이 두 번 제한할 이유가 없다.
    /// 반대 방향도 막는다. 가방이 꽉 차 있다고 젬을 못 빼면,
    /// 전리품을 버려야 빌드를 바꿀 수 있게 된다.
    ///
    /// 【무게는 그대로다.】 칸과 무게는 별개의 축이고, 젬도 무게는 있다.
    /// 무게는 넘어도 담을 수 있으므로(느려질 뿐) 잠기는 일은 생기지 않는다.
    ///
    /// IsSkillGem이 아니라 종류만 보는 이유 — 스킬 참조가 비어 있는
    /// 잘못된 에셋이 갑자기 칸을 먹기 시작하면 원인을 찾기 어렵다.
    /// </summary>
    public bool IsSlotless => kind == ItemKind.SkillGem;

    /// <summary>이 물건 한 칸치가 실제로 잡아먹는 칸 수. 젬은 0이다.</summary>
    public int SlotCost => IsSlotless ? 0 : SlotSize;

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

        // 젬·장비는 개별 상태를 가지므로 겹치지 않는다.
        if (kind == ItemKind.Weapon || kind == ItemKind.Armour
            || kind == ItemKind.Backpack || kind == ItemKind.Imprint
            || kind == ItemKind.SkillGem)
        {
            stackMax = 1;
        }

        if (kind != ItemKind.SkillGem)
            skill = null;

        // 각인은 예외 없이 거래 불가다. 개별 에셋에서 실수로 켜지 못하게 여기서 강제한다.
        if (kind == ItemKind.Imprint)
            noTrade = true;
    }
}
