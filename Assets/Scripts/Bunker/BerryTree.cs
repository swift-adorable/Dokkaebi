using UnityEngine;

/// <summary>
/// 【임시】 열매 나무 — 앞에서 누르면 산열매를 딴다 (결정 2-76). 산열매는 몬스터가 떨어뜨리지 않는다.
///
///   · 구역마다 2~3그루를 플레이어 둘레(6~14m) 무작위 자리에 세운다 — 구역 맵(9단계)이 생기면 제자리에 옮긴다
///   · 한 그루에서 판마다 한 번 · 산열매 1~2
/// 수치는 [임시값].
/// </summary>
public class BerryTree : MonoBehaviour
{
    public const string Name = "열매 나무";
    public const int MinTrees = 2;
    public const int MaxTrees = 3;
    public const float MinDistance = 6f;
    public const float MaxDistance = 14f;

    public bool Harvested { get; private set; }

    /// <summary>딴다 — 산열매 1~2가 가방으로. 성공하면 true.</summary>
    public bool Harvest()
    {
        if (Harvested)
        {
            StoryDialogueUI.ShowBanner("이미 다 땄다.", 1.5f);
            return false;
        }

        ItemDefinition berry = ItemCatalog.Load()?.Find(IngredientTable.Berry);
        int count = Random.Range(1, 3);
        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        if (berry == null || !bag.CanAdd(berry, count))
        {
            StoryDialogueUI.ShowBanner("가방에 자리가 없다.", 1.5f);
            return false;
        }

        bag.TryAdd(berry, count);
        PlayerInventory.Instance.RefreshCapacity();
        Harvested = true;

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            if (r.gameObject.name == "Fruit")
                r.enabled = false;

        StoryDialogueUI.ShowBanner($"산열매 {count}개를 땄다.", 1.5f);
        return true;
    }

    /// <summary>이번 구역에 열매 나무를 세운다. 구역 씬이 열릴 때 스포너가 한 번 부른다.</summary>
    public static void SpawnForRaid(Vector3 center)
    {
        int trees = Random.Range(MinTrees, MaxTrees + 1);

        for (int i = 0; i < trees; i++)
        {
            float angle = (i + Random.value * 0.6f) * Mathf.PI * 2f / trees;
            float distance = Random.Range(MinDistance, MaxDistance);
            Vector3 at = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

            Spawn(at, i);
        }
    }

    private static void Spawn(Vector3 at, int index)
    {
        var root = new GameObject($"BerryTree_{index}");
        root.transform.position = at;

        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.SetParent(root.transform, false);
        trunk.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        trunk.transform.localScale = new Vector3(0.35f, 0.8f, 0.35f);
        Tint(trunk, new Color(0.40f, 0.28f, 0.18f));

        GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crown.name = "Crown";
        crown.transform.SetParent(root.transform, false);
        crown.transform.localPosition = new Vector3(0f, 2.0f, 0f);
        crown.transform.localScale = new Vector3(1.6f, 1.3f, 1.6f);
        Object.Destroy(crown.GetComponent<Collider>());
        Tint(crown, new Color(0.25f, 0.50f, 0.25f));

        GameObject fruit = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fruit.name = "Fruit";
        fruit.transform.SetParent(root.transform, false);
        fruit.transform.localPosition = new Vector3(0.5f, 1.7f, -0.5f);
        fruit.transform.localScale = Vector3.one * 0.35f;
        Object.Destroy(fruit.GetComponent<Collider>());
        Tint(fruit, new Color(0.55f, 0.10f, 0.35f));

        root.AddComponent<BerryTree>();
        root.AddComponent<BunkerStation>().Setup(BunkerStation.Kind.BerryTree, 1.8f);
    }

    private static void Tint(GameObject target, Color color)
    {
        var renderer = target.GetComponent<Renderer>();
        if (renderer == null)
            return;

        Material m = renderer.material;
        m.color = color;
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", color);
    }
}
