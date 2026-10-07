using UnityEngine;

/// <summary>
/// 화살통 · 탄창을 돌린다 (결정 2-80 · AmmoFeed). PlayerWeapon이 붙인다.
///
///   · 구역에 들어서면 가방의 탄으로 통을 가득 채운다 (출발 전 손질)
///   · 쏘면 통에서 한 발 · 비면 가방에서 저절로 채운다 — 채우는 동안 못 쏜다
///   · 가방에도 없으면 무기를 등에 메고 맨손으로 · 알림
///   · R(데스크톱) — 덜 찬 통을 채운다
/// 벙커에서는 돌지 않는다 (쏘지 않는다).
/// </summary>
public class PlayerAmmo : MonoBehaviour
{
    private readonly AmmoFeed feed = new();
    private bool toppedUp;

    public AmmoFeed Feed => feed;

    /// <summary>들고 있는 무기 · 그 종류. 없으면 null.</summary>
    public static WeaponDefinition CurrentWeapon
        => PlayerInventory.HasInstance
            ? PlayerInventory.Instance.Loadout.Get(EquipmentSlot.Weapon)?.Definition as WeaponDefinition
            : null;

    public bool UseUnarmed => feed.UseUnarmed || CurrentWeapon == null;

    public bool CanShootWeapon
        => PlayerInventory.HasInstance && feed.CanShootWeapon(PlayerInventory.Instance.Loadout.LoadedAmmo);

    private bool reloadRequested;

    /// <summary>덜 찬 통을 채워 달라 (R · 가방 화면의 화살통 칸).</summary>
    public void RequestReload() => reloadRequested = true;

    /// <summary>쏘았다 — 통에서 한 발.</summary>
    public void Consume()
    {
        if (PlayerInventory.HasInstance)
            PlayerInventory.Instance.Loadout.ConsumeAmmo();
    }

    /// <summary>가방에 있는 이 무기의 탄 수.</summary>
    public static int InBag()
    {
        if (!PlayerInventory.HasInstance)
            return 0;

        string id = PlayerInventory.Instance.Loadout.AmmoId;
        if (string.IsNullOrEmpty(id))
            return 0;

        int count = 0;
        foreach (ItemStack s in PlayerInventory.Instance.Bag.Stacks)
            if (s?.Definition != null && s.Definition.Id == id)
                count += s.Count;

        return count;
    }

    private void Update()
    {
        if (SceneFlow.InBunker || !PlayerInventory.HasInstance)
            return;

        PlayerInventory inventory = PlayerInventory.Instance;
        EquipmentLoadout loadout = inventory.Loadout;

        // 바꿔 든 무기의 탄이 아닌 것은 가방으로.
        loadout.ReturnMismatchedAmmo(inventory.Bag);

        WeaponDefinition weapon = CurrentWeapon;

        // 【출발 전 손질】 구역에 들어선 첫 프레임에 통을 가득 — 소굴에서 채우는 번거로움을 없앤다.
        if (!toppedUp)
        {
            toppedUp = true;
            if (weapon != null && loadout.RefillAmmo(inventory.Bag) > 0)
                inventory.RefreshCapacity();
        }

        if (Input.GetKeyDown(KeyCode.R))
            reloadRequested = true;

        AmmoFeedEvent e = feed.Tick(
            weapon != null, loadout.LoadedAmmo, loadout.AmmoCapacity, InBag(),
            weapon != null ? weapon.KindInfo.ReloadSeconds : 0f, Time.deltaTime, reloadRequested);

        reloadRequested = false;

        AmmoHudUI.EnsureInstance().Show(weapon, loadout, feed, InBag());

        switch (e)
        {
            case AmmoFeedEvent.ReloadCompleted:
                if (loadout.RefillAmmo(inventory.Bag) > 0)
                    inventory.RefreshCapacity();
                break;

            case AmmoFeedEvent.Slung:
                StoryDialogueUI.ShowBanner(
                    $"{Josa.IGa(AmmoName(loadout.AmmoId))} 떨어졌다 — {Josa.EulReul(weapon.DisplayName)} 등에 멨다.", 2f);
                break;

            case AmmoFeedEvent.Drawn:
                StoryDialogueUI.ShowBanner($"{Josa.EulReul(weapon.DisplayName)} 다시 들었다.", 1.5f);
                break;
        }
    }

    private static string AmmoName(string id) => AmmoTable.Find(id)?.Name ?? "탄";
}
