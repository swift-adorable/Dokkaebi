using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 탄 7종 아이템 에셋 (결정 2-80 · AmmoTable이 원본). 전리품 표 생성도 이것을 먼저 부른다.
/// </summary>
public static class AmmoAssetGenerator
{
    public const string Folder = "Assets/Data/ScriptableObjects/Items/Ammo";

    [MenuItem("Dokkaebi/Items/탄 에셋 생성")]
    public static void Generate()
    {
        CreateAll();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AmmoAssetGenerator] 탄 {AmmoTable.All.Count}종 생성 완료");
    }

    public static List<ItemDefinition> CreateAll()
    {
        EnsureFolder(Folder);
        var made = new List<ItemDefinition>();

        foreach (AmmoInfo a in AmmoTable.All)
        {
            string path = $"{Folder}/{a.Id}.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);

            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(item, path);
            }

            var so = new SerializedObject(item);
            so.FindProperty("id").stringValue = a.Id;
            so.FindProperty("displayName").stringValue = a.Name;
            so.FindProperty("description").stringValue = $"{a.Description} (화살통 · 탄창 {a.Capacity})";
            so.FindProperty("kind").intValue = (int)ItemKind.Ammo;
            // 탄에는 티어가 없다 — 「대장간은 티어 3까지」는 장비의 규칙이다. 처음 쓰는 장은 AmmoTable.FirstTier.
            so.FindProperty("tier").intValue = 0;
            so.FindProperty("weight").floatValue = a.Weight;
            so.FindProperty("slotSize").intValue = 1;
            so.FindProperty("stackMax").intValue = a.StackMax;
            so.FindProperty("maxDurability").intValue = 0;
            so.FindProperty("baseValue").intValue = a.Value;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(item);
            made.Add(item);
        }

        return made;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int split = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, split));
        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }
}
