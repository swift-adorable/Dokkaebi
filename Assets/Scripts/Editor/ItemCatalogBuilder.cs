using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 아이템 카탈로그를 만든다. 세이브가 id로 아이템을 되찾을 수 있게 한다.
///
/// 【중복 id를 여기서 막는다.】 id가 겹치면 세이브가 엉뚱한 아이템으로
/// 되살아난다 — 저장할 때는 아무 문제가 없어 보이다가 불러올 때에야
/// 드러나므로, 원인을 찾기가 가장 어려운 종류의 결함이다.
/// </summary>
public static class ItemCatalogBuilder
{
    private const string CatalogPath = "Assets/Resources/ItemCatalog.asset";

    [MenuItem("Dokkaebi/Items/아이템 카탈로그 생성")]
    public static void Build()
    {
        List<ItemDefinition> items = AssetDatabase
            .FindAssets("t:ItemDefinition", new[] { ItemCatalog.DefinitionFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>)
            .Where(item => item != null)
            .OrderBy(item => item.Id)
            .ToList();

        var duplicates = items
            .GroupBy(item => item.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            Debug.LogError("[ItemCatalog] 중복 id — 세이브가 엉뚱한 아이템으로 되살아난다: "
                           + string.Join(", ", duplicates));
            return;
        }

        var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);

        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ItemCatalog>();

            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.EditorSet(items);

        EditorUtility.SetDirty(catalog);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ItemCatalog] 아이템 {items.Count}종 등록");
    }
}
