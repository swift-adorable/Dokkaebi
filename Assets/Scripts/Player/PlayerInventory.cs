using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 가방과 장비 슬롯. 【하나의 가방】에 장비·젬·전리품이 전부 들어간다.
/// (docs/Blob_Skill_System.md 11-1절 / docs/Blob_Equipment_System.md 5-1절)
///
/// 젬용 가방을 따로 두지 않은 이유 —
/// "젬을 많이 챙겨 화력을 확보할까, 가방을 비워 전리품 공간을 남길까"라는
/// 결정이 성립하려면 셋이 같은 자원을 두고 경쟁해야 한다.
/// 가방을 나누는 순간 그 결정이 사라진다.
/// </summary>
public class PlayerInventory : Singleton<PlayerInventory>
{
    /// <summary>
    /// 가방을 착용하지 않았을 때의 기본 칸.
    ///
    /// 【20이다.】 이전 12는 너무 작았다 —
    /// 덕코프는 벙커에서 장비를 갖춰 파밍하므로 맨몸 상태가 사실상 없지만,
    /// Blob은 벙커가 8단계라 맨몸이 곧 초반 경험이다.
    /// 가장 작은 가방(+8)을 끼기 전에도 한 판은 돌 수 있어야 한다.
    /// (docs/research/duckov/08_전투_실측과_교전.md 6절)
    /// </summary>
    public const int BaseSlots = 20;

    /// <summary>
    /// 창고의 기본 칸. 패시브 「창고 정리」(+20 · +20)로 는다.
    /// 덕코프의 기본 창고 크기는 위키에 없다 [확인 불가] — 스킬 「창고 확장 Lv.1」이
    /// +35다. 맨몸 가방(20)의 두 배로 두었다. 【창고에는 무게 제한이 없다.】
    /// </summary>
    public const int StashBaseSlots = 40;

    /// <summary>가방을 착용하지 않았을 때의 기본 소지 중량(kg).</summary>
    public const float BaseWeightLimit = 30f;

    [Header("기본 수용량 (가방 미착용 시)")]
    [Min(1)]
    [SerializeField] private int baseSlots = BaseSlots;

    [Min(1f)]
    [SerializeField] private float baseWeightLimit = BaseWeightLimit;

    private Inventory bag;
    private Inventory stash;
    private EquipmentLoadout loadout;
    private QuickSlots quickSlots;

    /// <summary>
    /// 창고. 【죽어도 잃지 않는다.】 무게는 보지 않고 칸만 본다 —
    /// 무게는 「들고 다닐 수 있는가」의 제한이고, 창고는 들고 다니지 않는다.
    /// </summary>
    public Inventory Stash => stash ??= new Inventory(StashBaseSlots, float.MaxValue);

    /// <summary>가방. 장비·젬·전리품이 전부 여기 들어간다.</summary>
    public Inventory Bag => bag ??= new Inventory(baseSlots, baseWeightLimit);

    /// <summary>착용 중인 장비 8슬롯.</summary>
    public EquipmentLoadout Loadout => loadout ??= new EquipmentLoadout();

    /// <summary>화면 하단 줄이 읽는 퀵슬롯 8칸. 가방을 가리키기만 한다.</summary>
    public QuickSlots Quick => quickSlots ??= new QuickSlots();

    /// <summary>과중량 단계. 이동 속도 보정이 이 값을 본다.</summary>
    public EncumbranceLevel Encumbrance => Bag.Encumbrance;

    public static PlayerInventory EnsureInstance()
    {
        if (HasInstance)
            return Instance;

        var existing = FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Include);

        if (existing != null)
            return existing;

        return new GameObject("PlayerInventory (Runtime)").AddComponent<PlayerInventory>();
    }

    protected override void OnSingletonAwake()
    {
        _ = Bag;
        _ = Loadout;
    }

    /// <summary>
    /// 가방 아이템이 주는 수용량을 반영한다. 장비를 갈아입을 때마다 호출한다.
    /// </summary>
    public void RefreshCapacity()
    {
        EquipmentModifiers modifiers = Loadout.Modifiers;

        // 장비(가방)와 패시브(계정)가 합산된다.
        // 패시브 총합은 장비 최대치의 1/2을 넘지 않게 표에서 제한한다 —
        // 그렇지 않으면 가방을 고르는 결정이 사라진다.
        // 상한은 PassiveCapacityTests가 강제한다. (docs/Blob_Passive_System.md 5절)
        int passiveSlots = 0;
        float passiveWeight = 0f;

        if (PassiveManager.HasInstance)
        {
            passiveSlots = Mathf.RoundToInt(
                PassiveManager.Instance.Total(PassiveEffectType.CarrySlots));

            passiveWeight = PassiveManager.Instance.Total(PassiveEffectType.CarryWeight);
        }

        int stashSlots = PassiveManager.HasInstance
            ? Mathf.RoundToInt(PassiveManager.Instance.Total(PassiveEffectType.StashSlots))
            : 0;

        // 【줄어도 안의 물건은 그대로다.】 칸이 모자라면 새로 넣지 못할 뿐이다.
        Stash.SlotCapacity = StashBaseSlots + stashSlots;

        Bag.SlotCapacity = baseSlots
            + Mathf.RoundToInt(modifiers.Get(EquipmentStatType.SlotCapacity))
            + passiveSlots;

        float limit = baseWeightLimit
            + modifiers.Get(EquipmentStatType.MaxCarryWeight)
            + passiveWeight;

        // 【중량 주사약은 맨 마지막에 곱한다.】 장비·패시브를 다 더한 뒤라야
        // 「지금 내 한도의 +50%」가 된다. 기본값에만 곱하면 가방을 좋은
        // 것으로 바꿀수록 중량 주사약이 초라해진다.
        Bag.WeightLimit = limit * CarryWeightMultiplier();
    }

    /// <summary>
    /// 상태이상이 곱하는 소지 중량 배율. 지금은 소지 중량 증가(중량 주사약)뿐이다.
    ///
    /// 【상태가 끝날 때 다시 불러야 한다.】 배율이 사라지면 한도가 줄어
    /// 과중량이 될 수 있다 — 그것이 중량 주사약의 대가다. PlayerSurvival이
    /// 탈수·허기가 바뀔 때 Refresh를 부르는 것과 같은 자리다.
    /// </summary>
    private static float CarryWeightMultiplier()
    {
        var player = FindAnyObjectByType<BlobController>(FindObjectsInactive.Exclude);

        Health health = player != null ? player.GetComponent<Health>() : null;

        return health != null ? health.Status.CarryWeightMultiplier : 1f;
    }

    /// <summary>
    /// 철수 실패(사망) 처리. 각인을 제외한 가방·장비를 전부 잃는다.
    /// 규칙은 하나다 — 죽으면 들고 있던 것 전부.
    /// </summary>
    public int DropOnDeath()
    {
        List<ItemStack> lostEquipment = Loadout.DropOnDeath();
        int lostFromBag = Bag.DropOnDeath();

        RefreshCapacity();

        return lostFromBag + (lostEquipment?.Count ?? 0);
    }
}
