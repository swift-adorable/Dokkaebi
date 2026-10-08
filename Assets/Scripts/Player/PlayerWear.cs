using UnityEngine;

/// <summary>
/// 【장비 닳기】 (결정 2-96 · WearTable) — 구역에서 쏘고 맞으면 장비가 닳는다. PlayerSurvival이 붙인다.
///   · 무기: 쏠 때마다 · 머리 · 몸통: 그 방어도로 받은 피해만큼 · 탈: 속성 피해만큼
///   · 「눅눅한 밤」은 ×1.5 (RaidConditions.DurabilityLossScale)
///   · 33% · 0에 걸리면 장비 수치를 다시 셈하고, 망가지면 알린다
/// 소굴에서는 닳지 않는다.
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerWear : MonoBehaviour
{
    public static PlayerWear Current { get; private set; }

    private Health health;
    private readonly WearAccumulator weapon = new();
    private readonly WearAccumulator weapon2 = new();
    private readonly WearAccumulator head = new();
    private readonly WearAccumulator body = new();
    private readonly WearAccumulator face = new();

    private void Awake()
    {
        health = GetComponent<Health>();
        Current = this;
        health.OnArmourHit += OnHit;
    }

    private void OnDestroy()
    {
        if (health != null)
            health.OnArmourHit -= OnHit;

        if (Current == this)
            Current = null;
    }

    private static bool Active => !SceneFlow.InBunker && PlayerInventory.HasInstance;

    private static float Scale => RaidManager.HasInstance ? RaidManager.Current.DurabilityLossScale : 1f;

    /// <summary>한 발 쐈다 — PlayerWeapon이 부른다.</summary>
    public void OnShot()
    {
        if (!Active)
            return;

        EquipmentLoadout loadout = PlayerInventory.Instance.Loadout;
        EquipmentSlot slot = EquipmentLoadout.WeaponSlot(loadout.ActiveWeapon);
        Wear(slot, loadout.ActiveWeapon == 1 ? weapon2 : weapon, WearTable.WeaponPerShot);
    }

    private void OnHit(DamageRequest request, int dealt)
    {
        if (!Active)
            return;

        EquipmentSlot armour = WearTable.ArmourSlotFor(request.hitKind);
        Wear(armour, armour == EquipmentSlot.Head ? head : body, dealt * WearTable.ArmourPerDamage);

        if (request.element != DamageElement.Physical)
            Wear(EquipmentSlot.Face, face, dealt * WearTable.MaskPerDamage);
    }

    private void Wear(EquipmentSlot slot, WearAccumulator pending, float amount)
    {
        EquipmentLoadout loadout = PlayerInventory.Instance.Loadout;
        ItemStack stack = loadout.Get(slot);

        if (stack?.Definition == null || !stack.Definition.HasDurability || stack.IsBroken)
            return;

        int whole = pending.Add(amount * Scale);
        if (whole <= 0)
            return;

        bool wasWorn = stack.IsWorn;
        stack.Damage(whole);

        if (stack.IsWorn == wasWorn && !stack.IsBroken)
            return;

        // 33% 아래 · 0 — 장비 수치를 다시 센다 (성능 저하 · 방어 옵션 정지).
        loadout.MarkDirty();
        if (TryGetComponent(out PlayerLoadout playerLoadout))
            playerLoadout.Refresh();

        StoryDialogueUI.ShowBanner(stack.IsBroken
            ? $"{Josa.IGa(stack.Definition.DisplayName)} 망가졌다 — 작업대에서 고친다."
            : $"{Josa.IGa(stack.Definition.DisplayName)} 많이 닳았다.", 2f);
    }
}
