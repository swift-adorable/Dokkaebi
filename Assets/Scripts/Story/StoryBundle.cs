using UnityEngine;

/// <summary>
/// 0-1 길가의 젖은 봇짐 (결정 2-79 · 본문 0-1). 앞에서 누르면 【전리품 창으로 연다】 (결정 2-83) —
/// 시체처럼 하나씩 · 전부 줍기로 가방에 넣는다. 바로 다 들어오지 않는다.
///
///   · 안의 것: 환목궁 · 화살 60 · 소환단 3 · 식혜 2 · 미숫가루 2 (ChapterZeroTable)
///   · 처음 열면 「연 것」으로 적는다 — 다음 판부터는 길가에 없다. 이번 판에는 다 집을 때까지 남는다
///   · 다 집고 창을 닫으면 사라진다
/// 모양은 임시(누런 자루). 아트가 들어오면 프리팹으로 바꾼다.
/// </summary>
public class StoryBundle : MonoBehaviour
{
    private LootContainer contents;

    public LootContainer Contents => contents;

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
        bundle.Fill();
        root.AddComponent<BunkerStation>().Setup(BunkerStation.Kind.StoryBundle, 1.8f);
        return bundle;
    }

    private void Fill()
    {
        ItemCatalog catalog = ItemCatalog.Load();
        contents = new LootContainer(LootContainer.DefaultCapacity);

        if (catalog == null)
            return;

        ItemDefinition bow = catalog.Find(ChapterZeroTable.BowId);
        if (bow != null)
            contents.TryPut(bow);

        foreach ((string id, int count) in ChapterZeroTable.BundleItems)
        {
            ItemDefinition item = catalog.Find(id);
            if (item != null)
                contents.TryPut(item, count);
        }
    }

    /// <summary>연다 — 전리품 창. 처음 열면 「연 것」으로 적는다.</summary>
    public void Open()
    {
        if (contents == null || contents.IsEmpty)
        {
            StoryDialogueUI.ShowBanner("빈 봇짐이다.", 1.2f);
            return;
        }

        StoryManager.Progress.See(ChapterZeroTable.BundleEvent);
        ExchangeWindowUI.EnsureInstance().Open(contents, ChapterZeroTable.BundleName, ExchangeWindowUI.Mode.Loot);
    }

    private void Update()
    {
        // 다 집고 창을 닫으면 사라진다.
        if (contents != null && contents.IsEmpty && !ExchangeWindowUI.AnyOpen)
            Destroy(gameObject);
    }
}
