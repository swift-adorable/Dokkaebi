using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 아이템 정의의 목록. 【세이브가 id로 아이템을 되찾는 곳이다.】
///
/// 세이브는 에셋을 직접 가리킬 수 없고 id 문자열만 남긴다. 불러올 때
/// 그 id가 어느 에셋인지 알아야 하는데, 아이템은 Assets/Data 아래에 있어
/// Resources.LoadAll이 닿지 않는다. SkillGemCatalog · PlaytestCatalog와
/// 같은 방식으로, 참조만 모은 에셋 하나를 Resources에 둔다.
///
/// 「Dokkaebi/Items/아이템 카탈로그 생성」이 채운다. 아이템을 추가하면 다시 실행한다.
/// </summary>
[CreateAssetMenu(fileName = "ItemCatalog", menuName = "Dokkaebi/Item Catalog")]
public class ItemCatalog : ScriptableObject
{
    public const string ResourcePath = "ItemCatalog";

    public const string DefinitionFolder = "Assets/Data/ScriptableObjects/Items";

    [Tooltip("ItemCatalogBuilder가 자동으로 채운다. 직접 편집하지 않는다.")]
    [SerializeField] private List<ItemDefinition> items = new();

    private Dictionary<string, ItemDefinition> byId;

    public IReadOnlyList<ItemDefinition> Items => items;

    public int Count => items.Count;

    /// <summary>id로 찾는다. 없으면 null — 지운 아이템이 세이브에 남아 있을 수 있다.</summary>
    public ItemDefinition Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        if (byId == null)
            BuildIndex();

        if (byId.TryGetValue(id, out ItemDefinition found))
            return found;

        // 합쳐서 없어진 옛 id — 세이브에 남아 있으면 새 아이템으로 읽는다.
        return LegacyIds.TryGetValue(id, out string now) && byId.TryGetValue(now, out found) ? found : null;
    }

    /// <summary>
    /// 옛 id → 지금 id. 들판의 「맑은 물」(water_bottle)은 호리병 물(con_water)로 합쳤다 (결정 2-73 · 세이브 12판).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> LegacyIds = new Dictionary<string, string>
    {
        { "water_bottle", "con_water" },
    };

    private void BuildIndex()
    {
        byId = new Dictionary<string, ItemDefinition>(items.Count);

        foreach (ItemDefinition item in items)
        {
            // 중복 id는 빌더가 막는다. 여기서는 먼저 온 것을 쓴다.
            if (item != null && !string.IsNullOrEmpty(item.Id) && !byId.ContainsKey(item.Id))
                byId.Add(item.Id, item);
        }
    }

    private static ItemCatalog cached;

    public static ItemCatalog Load()
    {
        if (cached == null)
            cached = Resources.Load<ItemCatalog>(ResourcePath);

        return cached;
    }

#if UNITY_EDITOR
    public void EditorSet(List<ItemDefinition> list)
    {
        items = list;
        byId = null;
    }
#endif
}
