using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 가방과 장비 슬롯. 【하나의 가방】에 장비·인자·전리품이 전부 들어간다.
/// (docs/Blob_Skill_System.md 11-1절 / docs/Blob_Equipment_System.md 5-1절)
///
/// 인자용 가방을 따로 두지 않은 이유 —
/// "인자를 많이 챙겨 화력을 확보할까, 가방을 비워 전리품 공간을 남길까"라는
/// 결정이 성립하려면 셋이 같은 자원을 두고 경쟁해야 한다.
/// 가방을 나누는 순간 그 결정이 사라진다.
/// </summary>
public class PlayerInventory : Singleton<PlayerInventory>
{
    /// <summary>가방을 착용하지 않았을 때의 기본 적재 칸.</summary>
    public const int BaseSlots = 12;

    /// <summary>가방을 착용하지 않았을 때의 기본 소지 중량(kg).</summary>
    public const float BaseWeightLimit = 20f;

    [Header("기본 수용량 (가방 미착용 시)")]
    [Min(1)]
    [SerializeField] private int baseSlots = BaseSlots;

    [Min(1f)]
    [SerializeField] private float baseWeightLimit = BaseWeightLimit;

    private Inventory bag;
    private EquipmentLoadout loadout;

    /// <summary>가방. 장비·인자·전리품이 전부 여기 들어간다.</summary>
    public Inventory Bag => bag ??= new Inventory(baseSlots, baseWeightLimit);

    /// <summary>착용 중인 장비 8슬롯.</summary>
    public EquipmentLoadout Loadout => loadout ??= new EquipmentLoadout();

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
        // 패시브 총합은 장비 최대치의 1/3을 넘지 않게 표에서 제한한다 —
        // 그렇지 않으면 가방을 고르는 결정이 사라진다. (PassiveEffectType 주석)
        int passiveSlots = 0;
        float passiveWeight = 0f;

        if (PassiveManager.HasInstance)
        {
            passiveSlots = Mathf.RoundToInt(
                PassiveManager.Instance.Total(PassiveEffectType.CarrySlots));

            passiveWeight = PassiveManager.Instance.Total(PassiveEffectType.CarryWeight);
        }

        Bag.SlotCapacity = baseSlots
            + Mathf.RoundToInt(modifiers.Get(EquipmentStatType.SlotCapacity))
            + passiveSlots;

        Bag.WeightLimit = baseWeightLimit
            + modifiers.Get(EquipmentStatType.MaxCarryWeight)
            + passiveWeight;
    }

    /// <summary>
    /// 추출 실패(사망) 처리. 각인을 제외한 가방·장비를 전부 잃는다.
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
