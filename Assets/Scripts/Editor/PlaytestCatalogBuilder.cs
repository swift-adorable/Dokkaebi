using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 검증 카탈로그를 만든다. (로드맵 6-P)
///
/// AssetDatabase는 에디터에만 있다. 그래서 「무엇을 지급할지」의 목록은
/// 에디터에서 한 번 모아 Resources에 에셋으로 남기고,
/// 런타임(실기 빌드 포함)은 그 에셋만 읽는다.
///
/// 에셋을 추가하거나 이름을 바꾸면 이 메뉴를 다시 실행해야 한다.
/// </summary>
public static class PlaytestCatalogBuilder
{
    private const string ItemRoot = "Assets/Data/ScriptableObjects/Items";
    private const string CatalogPath = "Assets/Resources/PlaytestCatalog.asset";

    [MenuItem("Blob/Playtest/검증 카탈로그 생성")]
    public static void Build()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<PlaytestCatalog>(CatalogPath);

        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<PlaytestCatalog>();

            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.EditorSet(
            Pick("Weapons/wpn_t1_pipe", "Armour/arm_head_t1", "Armour/arm_body_t1",
                 "Armour/arm_face_t1", "Armour/arm_ears_t1", "Backpacks/bag_t1_sack"),
            Pick("Weapons/wpn_t6_eraser", "Armour/arm_head_t6", "Armour/arm_body_t6_heavy",
                 "Armour/arm_face_fire_t6", "Armour/arm_ears_t6", "Backpacks/bag_t6_haul"),
            Folder("Weapons").OrderBy(w => w.Tier).ToList(),
            Folder("Imprints").OrderBy(i => i.Id).ToList(),
            Pick("Imprints/imp_charge_t2", "Imprints/imp_bulwark_t3",
                 "Imprints/imp_feather_t3", "Imprints/imp_precision_t3"),
            Gems(SkillCategory.Core),
            Gems(SkillCategory.Support),
            Pick("Gems/gem_core_frost", "Gems/gem_core_laceration",
                 "Gems/gem_sup_deep_cuts", "Gems/gem_sup_far_shot"),
            One("Loot/scrap_metal"),
            Folder("Loot").Where(i => i.StackMax > 1).OrderBy(i => i.Id).ToList());

        EditorUtility.SetDirty(catalog);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Playtest] 검증 카탈로그 생성 — "
                  + $"티어1 {catalog.StarterKit.Count} · 티어6 {catalog.EndgameKit.Count} · "
                  + $"무기 {catalog.Weapons.Count} · 각인 {catalog.Imprints.Count} · "
                  + $"Core {catalog.CoreGems.Count} · Support {catalog.SupportGems.Count}");
    }

    private static ItemDefinition One(string path)
    {
        var asset = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemRoot}/{path}.asset");

        if (asset == null)
            Debug.LogWarning($"[Playtest] {path} 을(를) 찾지 못했습니다.");

        return asset;
    }

    private static List<ItemDefinition> Pick(params string[] paths)
        => paths.Select(One).Where(a => a != null).ToList();

    private static List<ItemDefinition> Folder(string folder)
        => AssetDatabase.FindAssets("t:ItemDefinition", new[] { $"{ItemRoot}/{folder}" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>)
            .Where(a => a != null)
            .ToList();

    private static List<ItemDefinition> Gems(SkillCategory category)
        => Folder("Gems")
            .Where(g => g.Skill != null && g.Skill.Category == category)
            .OrderBy(g => g.Skill.RequiredLevel)
            .ThenBy(g => g.Id)
            .ToList();
}
