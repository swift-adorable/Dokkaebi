using UnityEditor;
using UnityEngine;

/// <summary>
/// 유형 9종의 프리팹을 모아 카탈로그 에셋을 만든다. (로드맵 7-G)
///
/// AssetDatabase는 에디터에만 있다. 「무엇을 스폰할지」의 목록은 여기서
/// 한 번 모아 Resources에 남기고, 런타임은 그 에셋만 읽는다.
///
/// 프리팹을 추가하거나 이름을 바꾸면 이 메뉴를 다시 실행해야 한다.
/// </summary>
public static class EnemyPrefabCatalogBuilder
{
    private const string CatalogPath = "Assets/Resources/EnemyPrefabCatalog.asset";

    [MenuItem("Dokkaebi/Enemy/유형 프리팹 카탈로그 생성")]
    public static void Build()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<EnemyPrefabCatalog>(CatalogPath);

        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<EnemyPrefabCatalog>();

            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        var prefabs = new GameObject[EnemyArchetypeTable.Count];

        int missing = 0;

        for (int i = 0; i < prefabs.Length; i++)
        {
            var archetype = (EnemyArchetype)i;

            string path = EnemyArchetypeWiring.PrefabPathOf(archetype);

            prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefabs[i] != null)
                continue;

            missing++;

            Debug.LogWarning($"[EnemyPrefabCatalog] {EnemyArchetypeTable.Name(archetype)} "
                             + $"프리팹을 찾지 못했습니다: {path}");
        }

        catalog.EditorSet(prefabs);

        EditorUtility.SetDirty(catalog);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[EnemyPrefabCatalog] {prefabs.Length - missing}/{prefabs.Length}종 등록");
    }
}
