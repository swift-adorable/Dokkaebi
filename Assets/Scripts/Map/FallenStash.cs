using UnityEngine;

/// <summary>
/// 쓰러진 자리 (결정 2-93) — 「회수 계약」으로 남은 잃은 가방 · 장비. 앞에서 누르면 전리품 창으로 연다.
///
///   · 그 장의 판이 시작될 때 스포너가 한 번 부른다(FallenCache.TakeFor — 이 판 한 번)
///   · 지도 · 미니맵에 「◎」로 보인다
///   · 다 집고 창을 닫으면 사라진다. 남기고 철수하면 이번 판이 끝나며 사라진다
/// 모양은 임시(검붉은 봇짐 + 꽂힌 막대). 아트가 들어오면 프리팹으로 바꾼다.
/// </summary>
public class FallenStash : MonoBehaviour
{
    public const string Name = "쓰러진 자리";

    public static FallenStash Current { get; private set; }

    private LootContainer contents;

    public LootContainer Contents => contents;

    /// <summary>이 장에 남은 쓰러진 자리가 있으면 세운다.</summary>
    public static FallenStash SpawnForRaid(int chapter)
    {
        SavedFallen saved = FallenCache.TakeFor(chapter);
        if (saved == null)
            return null;

        ItemCatalog catalog = ItemCatalog.Load();
        if (catalog == null)
            return null;

        var container = new LootContainer(Mathf.Max(LootContainer.DefaultCapacity, saved.items.Count));
        foreach (SavedItem item in saved.items)
        {
            if (item == null || item.IsEmpty)
                continue;

            ItemDefinition definition = catalog.Find(item.id);
            if (definition != null)
                container.TryPut(new ItemStack(definition, item.count, item.durability));
        }

        if (container.IsEmpty)
            return null;

        var root = new GameObject("FallenStash (Runtime)");
        root.transform.position = new Vector3(saved.x, 0f, saved.z);

        Build(root.transform);

        var stash = root.AddComponent<FallenStash>();
        stash.contents = container;
        root.AddComponent<BunkerStation>().Setup(BunkerStation.Kind.FallenStash, 1.8f);

        GameLogger.Log($"[Fallen] {chapter}장 쓰러진 자리 — {container.UsedSlots}칸");
        StoryDialogueUI.ShowBanner($"{Name}가 이 장에 남아 있다 — 지도에 표시했다.", 3f);
        return stash;
    }

    private static void Build(Transform root)
    {
        var color = new Color(0.45f, 0.16f, 0.14f);

        GameObject sack = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sack.name = "Sack";
        sack.transform.SetParent(root, false);
        sack.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        sack.transform.localScale = new Vector3(0.9f, 0.7f, 0.7f);
        Paint(sack, color);

        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Pole";
        pole.transform.SetParent(root, false);
        pole.transform.localPosition = new Vector3(0.45f, 0.8f, 0f);
        pole.transform.localScale = new Vector3(0.06f, 0.8f, 0.06f);
        Paint(pole, new Color(0.85f, 0.85f, 0.8f));
    }

    private static void Paint(GameObject go, Color color)
    {
        Object.Destroy(go.GetComponent<Collider>());

        var renderer = go.GetComponent<Renderer>();
        if (renderer == null)
            return;

        renderer.material.color = color;
        if (renderer.material.HasProperty("_BaseColor"))
            renderer.material.SetColor("_BaseColor", color);
    }

    private void OnEnable() => Current = this;

    private void OnDisable()
    {
        if (Current == this)
            Current = null;
    }

    /// <summary>연다 — 전리품 창.</summary>
    public void Open()
    {
        if (contents == null || contents.IsEmpty)
            return;

        ExchangeWindowUI.EnsureInstance().Open(contents, Name, ExchangeWindowUI.Mode.Loot);
    }

    private void Update()
    {
        // 다 집고 창을 닫으면 사라진다.
        if (contents != null && contents.IsEmpty && !ExchangeWindowUI.AnyOpen)
            Destroy(gameObject);
    }
}
