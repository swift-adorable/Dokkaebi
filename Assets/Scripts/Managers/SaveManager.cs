using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 세이브를 매니저들에 나눠 주고, 매니저들에서 모은다. (로드맵 8-F)
///
/// 【언제 저장하는가 — 덕코프를 따른다.】
/// 덕코프는 파밍 **전과 후에만** 자동 저장한다. 파밍 중에 끄면 그 파밍에서
/// 얻은 것도 잃은 것도 없다 — 롤백이다. [커뮤니티 확인]
///
///   덕코프        Blob (지금)
///   파밍 전       게임 시작        ← 읽기만 한다. 디스크와 달라진 것이 없다
///   파밍 후       사망             ← 저장한다 (BlobController.HandleDied)
///                 철수             ← 아직 없다 (9단계)
///   벙커에서의 일  —               ← 벙커가 아직 없다 (8단계 뒤쪽)
///
/// 【파밍 중에는 저장하지 않는다.】 앱이 백그라운드로 가도, 강제로 꺼져도
/// 마찬가지다. 그렇게 해야 「꺼 버리면 죽음을 무를 수 있다」가 생기지 않는다 —
/// 끄면 그 파밍의 골드도 같이 사라진다. 덕코프와 같은 값을 치른다.
///
/// MonoBehaviour가 아니다. 부트스트랩(RuntimeInitializeOnLoadMethod)으로
/// 씬 오브젝트의 Awake 뒤, Start 앞에 한 번 불린다 — SkillManager.Start가
/// 드롭 풀을 만들기 전에 도감이 돌아와 있어야 하기 때문이다.
/// </summary>
public static class SaveManager
{
    /// <summary>테스트가 경로를 바꾼다. null이면 기본 경로다.</summary>
    public static string PathOverride;

    private static string SavePath => PathOverride ?? SaveStore.DefaultPath;

    /// <summary>
    /// 【더 새 판의 세이브를 만났다】 — 이번 실행에서는 아무것도 쓰지 않는다.
    /// 옛 빌드가 새 세이브를 덮으면 모르는 필드가 소리 없이 사라진다.
    /// </summary>
    public static bool WritesBlocked { get; private set; }

    /// <summary>마지막으로 읽은 결과. 디버그 패널이 보여 준다.</summary>
    public static SaveLoadResult LastLoad { get; private set; } = SaveLoadResult.None;

    /// <summary>
    /// 이번 실행에서 세이브를 쓰는가.
    ///
    /// 【PlayMode 테스트에서는 끈다.】 테스트가 플레이어를 죽이면 사망 저장이
    /// 불리고, 그러면 테스트가 개발자의 실제 세이브를 덮어쓴다. 테스트 러너는
    /// 「InitTestScene…」 이름의 씬에서 돈다 — 그것으로 가른다.
    /// </summary>
    public static bool Enabled { get; private set; } = true;

    private const string TestScenePrefix = "InitTestScene";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        WritesBlocked = false;

        Enabled = !SceneManager.GetActiveScene().name.StartsWith(TestScenePrefix);

        if (!Enabled)
        {
            GameLogger.Log("[Save] 테스트 씬 — 세이브를 읽지도 쓰지도 않습니다.");
            return;
        }

        Load();
    }

    /// <summary>읽어서 매니저들에 나눠 준다. 결과를 로그로 남긴다.</summary>
    public static SaveLoadResult Load()
    {
        SaveLoadResult result = SaveStore.Read(SavePath, out SaveData data);

        LastLoad = result;

        switch (result)
        {
            case SaveLoadResult.None:
                GameLogger.Log("[Save] 세이브 없음 — 처음 시작합니다.");
                return result;

            case SaveLoadResult.TooNew:
                WritesBlocked = true;
                GameLogger.Error("[Save] 더 새 판의 세이브입니다. 이번 실행에서는 저장하지 않습니다.");
                return result;

            case SaveLoadResult.Corrupt:
                // 새로 쓰기 전에 깨진 둘을 비켜 둔다 — 첫 저장이 그 둘을 지우지 않게.
                SaveStore.Quarantine(SavePath);
                GameLogger.Error("[Save] 세이브와 백업이 모두 깨졌습니다. 옆으로 옮겨 두고 새로 시작합니다.");
                return result;

            case SaveLoadResult.Backup:
                GameLogger.Error("[Save] 본 세이브가 깨져 백업에서 되살렸습니다.");
                break;
        }

        Apply(data);

        GameLogger.Log($"[Save] 불러옴 — Lv.{data.level} · 골드 {data.gold} · "
                       + $"패시브 {data.learnedPassives.Count} · 도감 {data.codex.Count} · "
                       + $"각인 {CountFilled(data.imprints)}");

        return result;
    }

    /// <summary>
    /// 지금 상태를 저장한다. 【파밍 경계에서만 부른다.】
    /// </summary>
    /// <param name="reason">로그에 남길 이유. 「사망」 「철수」 같은 것.</param>
    public static bool Commit(string reason)
    {
        if (!Enabled)
            return false;

        if (WritesBlocked)
        {
            GameLogger.Error($"[Save] 저장하지 않았습니다 ({reason}) — 더 새 판의 세이브를 지키는 중입니다.");
            return false;
        }

        SaveData data = Capture();

        SaveStore.Write(SavePath, data);

        GameLogger.Log($"[Save] 저장 ({reason}) — Lv.{data.level} · 골드 {data.gold}");

        return true;
    }

    /// <summary>지우고 처음 상태로 되돌린다. 디버그 도구만 쓴다.</summary>
    public static void Wipe()
    {
        SaveStore.Delete(SavePath);

        WritesBlocked = false;
        LastLoad = SaveLoadResult.None;
    }

    // ── 모으기 ────────────────────────────────────────────────────────

    public static SaveData Capture()
    {
        var data = new SaveData();

        if (PlayerStats.HasInstance)
        {
            data.level = PlayerStats.Instance.Level;
            data.experience = PlayerStats.Instance.CurrentXP;
        }

        if (PassiveManager.HasInstance)
        {
            PassiveManager passive = PassiveManager.Instance;

            data.gold = passive.Gold;
            data.discoveredRegression = passive.DiscoveredRegression;
            data.learnedPassives = new List<string>(passive.State.LearnedIds);
        }

        if (SkillManager.HasInstance)
            data.codex = new List<string>(SkillManager.Instance.Codex.UnlockedIds);

        if (PlayerInventory.HasInstance)
        {
            EquipmentLoadout loadout = PlayerInventory.Instance.Loadout;

            data.imprints = new List<SavedItem>
            {
                ToSaved(loadout.Get(EquipmentSlot.ImprintA)),
                ToSaved(loadout.Get(EquipmentSlot.ImprintB))
            };

            data.stash = new List<SavedItem>();

            foreach (ItemStack stack in PlayerInventory.Instance.Stash.Stacks)
            {
                if (stack?.Definition != null && !stack.IsEmpty)
                    data.stash.Add(ToSaved(stack));
            }
        }

        data.shop = ShopManager.General.Capture(ShopTable.General);

        return data;
    }

    private static SavedItem ToSaved(ItemStack stack)
    {
        if (stack?.Definition == null)
            return new SavedItem { id = string.Empty, count = 0 };

        return new SavedItem
        {
            id = stack.Definition.Id,
            count = stack.Count,
            durability = stack.Definition.HasDurability ? stack.Durability : -1
        };
    }

    // ── 나눠 주기 ─────────────────────────────────────────────────────

    public static void Apply(SaveData data)
    {
        if (data == null)
            return;

        PlayerStats.EnsureInstance().Restore(data.level, data.experience);

        PassiveManager passive = PassiveManager.EnsureInstance();

        passive.Gold = data.gold;
        passive.DiscoveredRegression = data.discoveredRegression;
        passive.State.Restore(data.learnedPassives);

        SkillCodex codex = SkillManager.EnsureInstance().Codex;

        // 【덮어쓰지 않고 더한다.】 SkillManager.Start가 개발 설정으로 도감을
        // 전부 열 수 있다(unlockAllOnStart). 그 설정이 꺼져 있을 때 세이브가
        // 유일한 출처가 되고, 켜져 있을 때도 이 줄이 해를 끼치지 않는다.
        foreach (string id in data.codex)
            codex.Unlock(id);

        RestoreImprints(data.imprints);
        RestoreStash(data.stash);
        ShopManager.Restore(data.shop);

        // 레벨이 돌아왔으니 소켓 수도 맞춘다. 알림은 내지 않는다.
        SkillManager.EnsureInstance().ResyncLevel();

        PlayerInventory.EnsureInstance().RefreshCapacity();
    }

    /// <summary>
    /// 각인을 제자리에 다시 끼운다.
    ///
    /// 【못 찾은 id는 건너뛴다.】 에셋을 지웠거나 id를 바꾸면 세이브에 없는
    /// 아이템이 남는다. 그때 게임이 멈추면 세이브 전체가 못 쓰게 된다.
    /// 하나를 잃는 쪽이 전부를 잃는 것보다 낫다.
    /// </summary>
    private static void RestoreImprints(List<SavedItem> saved)
    {
        if (saved == null || saved.Count == 0)
            return;

        ItemCatalog catalog = ItemCatalog.Load();

        if (catalog == null)
        {
            GameLogger.Error("[Save] Resources/ItemCatalog가 없어 각인을 되살리지 못했습니다. "
                             + "「Blob/Items/아이템 카탈로그 생성」을 실행하십시오.");
            return;
        }

        EquipmentLoadout loadout = PlayerInventory.EnsureInstance().Loadout;

        EquipmentSlot[] slots = { EquipmentSlot.ImprintA, EquipmentSlot.ImprintB };

        for (int i = 0; i < slots.Length && i < saved.Count; i++)
        {
            SavedItem item = saved[i];

            if (item == null || item.IsEmpty)
                continue;

            ItemDefinition definition = catalog.Find(item.id);

            if (definition == null)
            {
                GameLogger.Error($"[Save] 각인 「{item.id}」을 찾지 못해 건너뜁니다.");
                continue;
            }

            var stack = new ItemStack(definition, 1, item.durability);

            if (!loadout.TryEquip(stack, slots[i], out _))
                GameLogger.Error($"[Save] 각인 「{item.id}」을 {slots[i]}에 끼우지 못했습니다.");
        }
    }

    /// <summary>
    /// 창고를 되살린다. 【칸 수를 넘어도 넣는다】 — 패시브를 바꾸거나 표를 고쳐
    /// 칸이 줄었을 때 물건이 사라지면 안 된다. 넘친 만큼은 새로 넣지 못할 뿐이다.
    /// 못 찾은 id는 각인과 같은 이유로 건너뛴다.
    /// </summary>
    private static void RestoreStash(List<SavedItem> saved)
    {
        Inventory stash = PlayerInventory.EnsureInstance().Stash;

        stash.Clear();

        if (saved == null || saved.Count == 0)
            return;

        ItemCatalog catalog = ItemCatalog.Load();

        if (catalog == null)
        {
            GameLogger.Error("[Save] Resources/ItemCatalog가 없어 창고를 되살리지 못했습니다.");
            return;
        }

        int capacity = stash.SlotCapacity;
        stash.SlotCapacity = int.MaxValue / 2;

        foreach (SavedItem item in saved)
        {
            if (item == null || item.IsEmpty)
                continue;

            ItemDefinition definition = catalog.Find(item.id);

            if (definition == null)
            {
                GameLogger.Error($"[Save] 창고의 「{item.id}」을 찾지 못해 건너뜁니다.");
                continue;
            }

            stash.TryAddStack(new ItemStack(definition, item.count, item.durability));
        }

        stash.SlotCapacity = capacity;
    }

    private static int CountFilled(List<SavedItem> items)
    {
        int count = 0;

        if (items == null)
            return 0;

        foreach (SavedItem item in items)
        {
            if (item != null && !item.IsEmpty)
                count++;
        }

        return count;
    }
}
