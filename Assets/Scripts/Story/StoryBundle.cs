using UnityEngine;

/// <summary>
/// 0-1 길가의 젖은 봇짐 (결정 2-79 · 본문 0-1). 앞에서 누르면 연다 — 한 번만.
///
///   · 환목궁 — 무기 칸이 비어 있으면 바로 쥔다. 차 있으면 가방으로
///   · 소환단 · 식혜 · 미숫가루 — 가방으로 (ChapterZeroTable.BundleItems)
/// 모양은 임시(누런 자루). 아트가 들어오면 프리팹으로 바꾼다.
/// </summary>
public class StoryBundle : MonoBehaviour
{
    public bool Opened { get; private set; }

    public static StoryBundle Create(Vector3 at)
    {
        var root = new GameObject("StoryBundle (0-1)");
        root.transform.position = at;

        GameObject sack = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sack.name = "Sack";
        sack.transform.SetParent(root.transform, false);
        sack.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        sack.transform.localScale = new Vector3(0.9f, 0.7f, 0.7f);
        Object.Destroy(sack.GetComponent<Collider>());

        var renderer = sack.GetComponent<Renderer>();
        if (renderer != null)
        {
            var color = new Color(0.55f, 0.45f, 0.28f);
            renderer.material.color = color;
            if (renderer.material.HasProperty("_BaseColor"))
                renderer.material.SetColor("_BaseColor", color);
        }

        var bundle = root.AddComponent<StoryBundle>();
        root.AddComponent<BunkerStation>().Setup(BunkerStation.Kind.StoryBundle, 1.8f);
        return bundle;
    }

    /// <summary>연다. 성공하면 true — 봇짐이 사라진다.</summary>
    public bool Open()
    {
        if (Opened)
            return false;

        ItemCatalog catalog = ItemCatalog.Load();
        PlayerInventory inventory = PlayerInventory.EnsureInstance();
        Inventory bag = inventory.Bag;

        ItemDefinition bow = catalog?.Find(ChapterZeroTable.BowId);
        bool bowToBag = bow != null && inventory.Loadout.Get(EquipmentSlot.Weapon) != null;

        // 전부 들어갈 자리가 있는지 먼저 본다 — 반만 들어가고 봇짐이 사라지면 안 된다.
        int slotsNeeded = bowToBag ? bow.SlotSize : 0;
        foreach ((string id, int count) in ChapterZeroTable.BundleItems)
        {
            ItemDefinition item = catalog?.Find(id);
            if (item != null)
                slotsNeeded += item.SlotSize * UnityEngine.Mathf.CeilToInt(count / (float)UnityEngine.Mathf.Max(1, item.StackMax));
        }

        if (bag.FreeSlots < slotsNeeded)
        {
            StoryDialogueUI.ShowBanner("가방에 자리가 없다.", 1.5f);
            return false;
        }

        if (bow != null)
        {
            var stack = new ItemStack(bow);

            if (bowToBag || !inventory.Loadout.TryEquip(stack, EquipmentSlot.Weapon, out _))
                bag.TryAddStack(stack);
        }

        foreach ((string id, int count) in ChapterZeroTable.BundleItems)
        {
            ItemDefinition item = catalog?.Find(id);
            if (item != null)
                bag.TryAdd(item, count);
        }

        inventory.RefreshCapacity();
        Object.FindAnyObjectByType<PlayerLoadout>()?.Refresh();

        StoryManager.Progress.See(ChapterZeroTable.BundleEvent);
        Opened = true;

        StoryDialogueUI.ShowBanner("봇짐을 풀었다 — 환목궁 · 소환단 · 식혜 · 미숫가루.", 2.5f);
        Destroy(gameObject);
        return true;
    }
}

/// <summary>
/// 0-2의 큰 요괴가 품은 첫 구슬 (결정 2-79). 이 적이 쓰러지면 시체 전리품에 화염 핵심 구슬을 넣고 사라진다.
/// 풀로 돌아간 몸이 다음에 일반 적으로 나올 때 다시 주지 않게, 한 번 주면 컴포넌트를 지운다.
/// </summary>
public class StoryGift : MonoBehaviour
{
    public ItemDefinition Item;
    public string EventId;

    public void Deliver(CorpseController corpse)
    {
        if (Item != null && corpse != null)
        {
            // 칸이 가득 차 있으면 하나를 비워서라도 넣는다 — 이 구슬은 0장의 약속이다.
            if (!corpse.Loot.TryPut(Item))
            {
                corpse.Loot.Clear();
                corpse.Loot.TryPut(Item);
            }

            StoryManager.Progress.See(EventId);
        }

        Destroy(this);
    }
}
