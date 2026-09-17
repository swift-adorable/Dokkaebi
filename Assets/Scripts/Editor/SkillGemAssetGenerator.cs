using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 스킬 정의 53종에 대응하는 「인자」 아이템 에셋을 만든다. (로드맵 6-D)
///
/// 왜 필요한가 —
/// 스킬이 실물 아이템이 되면서 SkillDefinition 하나당 ItemDefinition 하나가 필요해졌다.
/// 53개를 Inspector로 만들면 누락과 오타를 잡을 수단이 없다.
///
/// 이미 있는 에셋은 덮어쓴다. GUID는 보존되므로 참조가 끊기지 않는다.
/// </summary>
public static class SkillGemAssetGenerator
{
    private const string Root = SkillGemCatalog.DefinitionFolder;
    private const string CatalogAssetPath = "Assets/Resources/SkillGemCatalog.asset";

    /// <summary>
    /// 분류별 무게(kg)와 기본 가치.
    ///
    /// Core를 가장 무겁게 둔 이유 — Core는 빌드의 뼈대라 반드시 들고 나가야 한다.
    /// 그것이 무거워야 "예비 Core를 하나 더 챙길까"가 실제 판단이 된다.
    /// Support는 가볍다. 여러 개를 주워 조합을 시험하는 것이 이 게임의 재미이기 때문이다.
    /// </summary>
    private static readonly float[] WeightByCategory = { 0.8f, 0.4f, 0.6f, 0.6f };

    private static readonly int[] ValueByCategory = { 420, 180, 300, 360 };

    [MenuItem("Blob/Skill/인자 아이템 에셋 생성")]
    public static void Generate()
    {
        SkillCatalog catalog = SkillCatalog.Load();

        if (catalog == null || catalog.Count == 0)
        {
            Debug.LogError(
                "[SkillGemAssetGenerator] SkillCatalog이 비어 있습니다. " +
                "먼저 Blob > Skill > 카탈로그 다시 만들기 를 실행하십시오.");
            return;
        }

        EnsureFolders();

        int created = 0;
        int updated = 0;

        var gems = new List<ItemDefinition>(catalog.Count);

        for (int i = 0; i < catalog.Count; i++)
        {
            SkillDefinition skill = catalog.Definitions[i];

            if (skill == null)
                continue;

            string path = $"{Root}/gem_{skill.Id}.asset";

            var gem = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            bool isNew = gem == null;

            if (isNew)
            {
                gem = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(gem, path);
                created++;
            }
            else
            {
                updated++;
            }

            Apply(gem, skill);

            gems.Add(gem);
        }

        AssetDatabase.SaveAssets();

        RebuildCatalog(gems);

        Debug.Log($"[SkillGemAssetGenerator] 인자 아이템 {gems.Count}종 " +
                  $"(신규 {created} / 갱신 {updated})");
    }

    private static void Apply(ItemDefinition gem, SkillDefinition skill)
    {
        int index = Mathf.Clamp((int)skill.Category, 0, WeightByCategory.Length - 1);

        var so = new SerializedObject(gem);

        so.FindProperty("id").stringValue = $"gem_{skill.Id}";
        so.FindProperty("displayName").stringValue = skill.DisplayName;
        so.FindProperty("description").stringValue = skill.Description;

        so.FindProperty("kind").intValue = (int)ItemKind.SkillGem;

        // 인자에는 티어가 없다. 스킬은 단일 정의이며 티어 체계를 두지 않는다. (14절 5번)
        so.FindProperty("tier").intValue = 0;

        so.FindProperty("weight").floatValue = WeightByCategory[index];
        so.FindProperty("slotSize").intValue = 1;
        so.FindProperty("stackMax").intValue = 1;

        // 인자는 닳지 않는다. 내구도는 장비의 축이다.
        so.FindProperty("maxDurability").intValue = 0;

        so.FindProperty("baseValue").intValue = ValueByCategory[index];

        so.FindProperty("skill").objectReferenceValue = skill;

        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(gem);
    }

    /// <summary>Resources/SkillGemCatalog.asset을 만들거나 갱신한다.</summary>
    [MenuItem("Blob/Skill/인자 카탈로그 다시 만들기")]
    public static void RebuildCatalogFromFolder()
    {
        var gems = new List<ItemDefinition>();

        string[] guids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { Root });

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var gem = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);

            if (gem != null && gem.Kind == ItemKind.SkillGem)
                gems.Add(gem);
        }

        RebuildCatalog(gems);
    }

    private static void RebuildCatalog(List<ItemDefinition> gems)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var catalog = AssetDatabase.LoadAssetAtPath<SkillGemCatalog>(CatalogAssetPath);

        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<SkillGemCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        }

        gems.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

        catalog.EditorSetGems(gems);

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[SkillGemAssetGenerator] 인자 카탈로그 갱신: {gems.Count}종");
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Data");
        EnsureFolder("Assets/Data/ScriptableObjects");
        EnsureFolder("Assets/Data/ScriptableObjects/Items");
        EnsureFolder(Root);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int split = path.LastIndexOf('/');

        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }
}
