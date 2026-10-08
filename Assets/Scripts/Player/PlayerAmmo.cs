using UnityEngine;

/// <summary>
/// 화살통 · 탄창 · 무기 두 자루를 돌린다 (결정 2-80 · 2-81 · AmmoFeed). PlayerWeapon이 붙인다.
///
///   · 구역에 들어서면 두 무기의 통을 가방의 탄으로 가득 채운다 (출발 전 손질)
///   · 쏘면 든 무기의 통에서 한 발 · 비면 가방에서 저절로 채운다 — 채우는 동안 못 쏜다
///   · 통이 25% 이하면 미리 채우기 버튼(ReloadButtonUI) · R
///   · 가방에도 없으면 무기를 등에 메고 맨손으로 · 알림
///   · 1 · 2(키 · 퀵슬롯) — 무기를 바꿔 든다 (0.4초)
/// 벙커에서는 돌지 않는다 (쏘지 않는다).
/// </summary>
public class PlayerAmmo : MonoBehaviour
{
    private readonly AmmoFeed feed = new();
    private bool toppedUp;
    private bool reloadRequested;

    public AmmoFeed Feed => feed;

    public static PlayerAmmo Current { get; private set; }

    /// <summary>든 무기. 없으면 null.</summary>
    public static WeaponDefinition CurrentWeapon
        => PlayerInventory.HasInstance ? PlayerInventory.Instance.Loadout.ActiveWeaponDefinition : null;

    public bool UseUnarmed => CurrentWeapon == null || feed.UseUnarmed;

    public bool CanShootWeapon
        => PlayerInventory.HasInstance && feed.CanShootWeapon(PlayerInventory.Instance.Loadout.LoadedAmmo);

    /// <summary>미리 채우기 버튼을 띄울 때인가 (결정 2-81 — 25% 이하).</summary>
    public bool OffersReload
        => !SceneFlow.InBunker && PlayerInventory.HasInstance && CurrentWeapon != null
           && feed.State == AmmoFeedState.Ready
           && AmmoTable.ShouldOfferReload(PlayerInventory.Instance.Loadout.LoadedAmmo,
               PlayerInventory.Instance.Loadout.AmmoCapacity, InBag());

    private void Awake() => Current = this;

    private void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }

    /// <summary>덜 찬 통을 채워 달라 (R · 미리 채우기 버튼 · 가방 화면의 화살통 칸).</summary>
    public void RequestReload() => reloadRequested = true;

    /// <summary>
    /// 무기를 바꿔 든다 (0 · 1). 그 자리가 비었으면 알리고 그대로. 바꿨으면 true.
    /// </summary>
    public bool SwitchTo(int index)
    {
        if (!PlayerInventory.HasInstance)
            return false;

        PlayerInventory inventory = PlayerInventory.Instance;
        EquipmentLoadout loadout = inventory.Loadout;

        if (loadout.ActiveWeapon == index)
            return false;

        if (loadout.WeaponAt(index) == null)
        {
            StoryDialogueUI.ShowBanner($"무기 {index + 1} 자리가 비어 있다.", 1.2f);
            return false;
        }

        loadout.SetActiveWeapon(index);

        if (!SceneFlow.InBunker)
            feed.BeginSwitch();

        Object.FindAnyObjectByType<PlayerLoadout>()?.Refresh();
        return true;
    }

    /// <summary>쏘았다 — 통에서 한 발.</summary>
    public void Consume()
    {
        if (PlayerInventory.HasInstance)
            PlayerInventory.Instance.Loadout.ConsumeAmmo();
    }

    /// <summary>가방에 있는 그 무기(기본 = 든 무기)의 탄 수.</summary>
    public static int InBag(int index = -1)
    {
        if (!PlayerInventory.HasInstance)
            return 0;

        EquipmentLoadout loadout = PlayerInventory.Instance.Loadout;
        int slot = index < 0 ? loadout.ActiveWeapon : index;

        if (string.IsNullOrEmpty(loadout.AmmoIdAt(slot)))
            return 0;

        // 채울 탄(통에 든 것과 같은 것 — 방어 관통탄이면 그것)만 센다 (결정 2-95).
        string id = loadout.FeedIdAt(slot, PlayerInventory.Instance.Bag);

        int count = 0;
        foreach (ItemStack s in PlayerInventory.Instance.Bag.Stacks)
            if (s?.Definition != null && s.Definition.Id == id)
                count += s.Count;

        return count;
    }

    private void Update()
    {
        if (!PlayerInventory.HasInstance)
            return;

        // 1 · 2 — 무기 바꿔 들기. 소굴에서도 고를 수 있다(들고 나갈 쪽).
        if (Input.GetKeyDown(KeyCode.Alpha1))
            SwitchTo(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            SwitchTo(1);

        if (SceneFlow.InBunker)
            return;

        PlayerInventory inventory = PlayerInventory.Instance;
        EquipmentLoadout loadout = inventory.Loadout;

        // 바꿔 든 무기의 탄이 아닌 것은 가방으로.
        loadout.ReturnMismatchedAmmo(inventory.Bag);

        WeaponDefinition weapon = CurrentWeapon;

        // 【출발 전 손질】 구역에 들어선 첫 프레임에 두 통을 가득 — 소굴에서 채우는 번거로움을 없앤다.
        if (!toppedUp)
        {
            toppedUp = true;
            int moved = loadout.RefillAmmo(inventory.Bag, 0) + loadout.RefillAmmo(inventory.Bag, 1);
            if (moved > 0)
                inventory.RefreshCapacity();
        }

        if (Input.GetKeyDown(KeyCode.R))
            reloadRequested = true;

        AmmoFeedEvent e = feed.Tick(
            weapon != null, loadout.LoadedAmmo, loadout.AmmoCapacity, InBag(),
            weapon != null ? weapon.KindInfo.ReloadSeconds : 0f, Time.deltaTime, reloadRequested);

        reloadRequested = false;

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

        AmmoHudUI.EnsureInstance().Show(weapon, loadout, feed, InBag());
        ReloadButtonUI.EnsureInstance().Show(OffersReload, loadout.LoadedAmmo, loadout.AmmoCapacity);
    }

    private static string AmmoName(string id) => AmmoTable.Find(id)?.Name ?? "탄";
}
