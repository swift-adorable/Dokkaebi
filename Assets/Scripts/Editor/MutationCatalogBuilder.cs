using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/Data/ScriptableObjects/Mutations/ 폴더를 스캔해
/// Resources/MutationCatalog.asset 을 자동으로 채우는 에디터 도구.
///
/// 왜 필요한가:
/// v5 §10-10이 정의 에셋의 저장 위치를 Resources 밖으로 확정했다.
/// 런타임이 폴더를 직접 스캔할 수단이 없으므로 카탈로그 에셋 하나를 중간에 둔다.
/// 이 빌더가 임포트 시점에 자동으로 갱신하므로,
/// "에셋을 만들고 Inspector에 드래그하는 것을 잊어 선택지에 안 나오는" 실패 지점이 없다.
/// </summary>
public static class MutationCatalogBuilder
{
    private const string CatalogAssetPath = "Assets/Resources/MutationCatalog.asset";

    [MenuItem("Blob/Mutation/카탈로그 다시 만들기")]
    public static void Rebuild()
    {
        MutationCatalog catalog = LoadOrCreateCatalog();

        var definitions = new List<MutationDefinition>();
        var seenIds = new HashSet<string>();

        string[] guids = AssetDatabase.FindAssets(
            "t:MutationDefinition", new[] { MutationCatalog.DefinitionFolder });

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var definition = AssetDatabase.LoadAssetAtPath<MutationDefinition>(path);

            if (definition == null)
                continue;

            // id 중복은 도감/적재 저장을 조용히 망가뜨리므로 임포트 시점에 잡는다.
            if (!seenIds.Add(definition.Id))
            {
                Debug.LogError(
                    $"[MutationCatalogBuilder] id가 중복됩니다: '{definition.Id}' ({path})", definition);
                continue;
            }

            definitions.Add(definition);
        }

        definitions.Sort(CompareDefinitions);

        catalog.EditorSetDefinitions(definitions);

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        Debug.Log($"[MutationCatalogBuilder] 카탈로그 갱신 완료: {definitions.Count}종");
    }

    /// <summary>분류 → 요구 레벨 → id 순으로 정렬해 목록을 안정적으로 유지한다.</summary>
    private static int CompareDefinitions(MutationDefinition a, MutationDefinition b)
    {
        int byCategory = a.Category.CompareTo(b.Category);

        if (byCategory != 0)
            return byCategory;

        int byLevel = a.RequiredLevel.CompareTo(b.RequiredLevel);

        return byLevel != 0 ? byLevel : string.CompareOrdinal(a.Id, b.Id);
    }

    private static MutationCatalog LoadOrCreateCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MutationCatalog>(CatalogAssetPath);

        if (catalog != null)
            return catalog;

        EnsureFolder("Assets/Resources");
        EnsureFolder(MutationCatalog.DefinitionFolder);

        catalog = ScriptableObject.CreateInstance<MutationCatalog>();

        AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"[MutationCatalogBuilder] 카탈로그 에셋을 새로 만들었습니다: {CatalogAssetPath}");

        return catalog;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string[] parts = path.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }

    /// <summary>정의 에셋이 추가·삭제·이동될 때 카탈로그를 자동 갱신한다.</summary>
    private class Postprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (Touches(imported) || Touches(deleted) || Touches(moved) || Touches(movedFrom))
                EditorApplication.delayCall += Rebuild;
        }

        private static bool Touches(string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                if (paths[i].StartsWith(MutationCatalog.DefinitionFolder))
                    return true;
            }

            return false;
        }
    }
}
