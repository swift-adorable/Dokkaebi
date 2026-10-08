using UnityEditor;
using UnityEngine;

/// <summary>
/// 무기 도면 7종 에셋 (결정 2-95 · WorkbenchTable.BlueprintKinds) — 작업대 Ⅳ에서 바쳐 그 종류의 무기 티어 4~6을 연다.
/// 고유(보스 · 큰 요괴) 시체에서 드물게 나온다. 이름 · 그림은 Naming · 아트 때 바꾼다.
/// </summary>
public static class WorkbenchAssetGenerator
{
    private const string Folder = "Assets/Data/ScriptableObjects/Items/Loot";

    [MenuItem("Dokkaebi/Items/무기 도면 에셋 생성")]
    public static void Generate()
    {
        foreach (WeaponKind kind in WorkbenchTable.BlueprintKinds)
        {
            string id = WorkbenchTable.BlueprintOf(kind);
            string path = $"{Folder}/{id}.asset";
            string name = WeaponKindTable.Of(kind).Name;

            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(item, path);
            }

            var so = new SerializedObject(item);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = $"{name} 도면";
            so.FindProperty("description").stringValue =
                $"【임시】 옛 장인이 남긴 {name} 도면. 작업대 Ⅳ에 아래 티어 {name} 한 자루와 함께 바치면 더 좋은 {Josa.EulReul(name)} 만들 수 있다.";
            so.FindProperty("kind").intValue = (int)ItemKind.Material;
            so.FindProperty("tier").intValue = 4;
            so.FindProperty("weight").floatValue = 0.05f;
            so.FindProperty("slotSize").intValue = 1;
            so.FindProperty("stackMax").intValue = 5;
            so.FindProperty("baseValue").intValue = 1500;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[WorkbenchAssetGenerator] 무기 도면 {WorkbenchTable.BlueprintKinds.Length}종 생성 완료 — 아이템 카탈로그도 다시 만드십시오.");
    }
}
