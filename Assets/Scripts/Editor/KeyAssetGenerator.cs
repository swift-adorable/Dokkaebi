using UnityEditor;
using UnityEngine;

/// <summary>
/// 【열쇠 · 연장 에셋】 (결정 2-143) — 잠긴 곳 표(LockSiteTable)의 문마다 열쇠 하나 + 쇠지레 · 화약 뭉치.
/// 맵 표를 고쳐 자리 · 이름이 바뀌면 다시 돌린다. 끝나면 「아이템 카탈로그 생성」도 돌린다.
/// </summary>
public static class KeyAssetGenerator
{
    private const string Root = "Assets/Data/ScriptableObjects/Items/Keys";

    public const string CrowbarId = KeyAssetIds.CrowbarId;
    public const string PowderId = KeyAssetIds.PowderId;

    [MenuItem("Dokkaebi/Items/열쇠 · 연장 에셋 생성")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(Root))
            AssetDatabase.CreateFolder("Assets/Data/ScriptableObjects/Items", "Keys");

        LockSiteTable.ClearCache();
        int n = 0;
        foreach (LockSite site in LockSiteTable.All())
        {
            bool gate = site.Kind == LockSiteKind.Compound;
            Write(site.KeyId, $"{site.DoorName} 열쇠",
                $"{site.ZoneId} {site.DoorName}을 연다. 소굴에 작업대가 있으면 열쇠고리에 걸어 둘 수 있다 — "
                + $"걸어 두면 들고 다니지 않아도 열린다. 걸지 않으면 {KeyRing.UsesPerKey}번 쓰고 부러진다.",
                ItemKind.Key, site.Chapter, 0.2f, 1, KeyRing.UsesPerKey, (gate ? 600 : 400) * site.Chapter);
            n++;
        }

        // 쇠지레 — 잠긴 궤를 비틀어 연다. 문은 못 연다. 쓸 때 소리가 난다 [임시값].
        Write(CrowbarId, "쇠지레", "잠긴 궤의 자물쇠를 비틀어 연다. 대문 · 방문은 못 연다. 쓰면 쇳소리가 꽤 멀리 간다.",
            ItemKind.Material, 1, 1.2f, 1, 5, 450);
        // 화약 뭉치 — 금 간 담을 무너뜨린다. 크게 울린다 [임시값].
        Write(PowderId, "화약 뭉치", "금이 간 담에 붙여 무너뜨린다. 온 동네가 들을 만큼 크게 울린다.",
            ItemKind.Material, 1, 0.5f, 3, 0, 900);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[KeyAssetGenerator] 열쇠 {n}개 + 쇠지레 · 화약 뭉치 — 「아이템 카탈로그 생성」도 돌리십시오.");
    }

    private static void Write(string id, string name, string desc, ItemKind kind, int tier, float weight,
                              int stackMax, int maxDurability, int value)
    {
        string path = $"{Root}/{id}.asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(item, path);
        }

        var so = new SerializedObject(item);
        so.FindProperty("id").stringValue = id;
        so.FindProperty("displayName").stringValue = name;
        so.FindProperty("description").stringValue = desc;
        so.FindProperty("kind").intValue = (int)kind;
        so.FindProperty("tier").intValue = tier;
        so.FindProperty("weight").floatValue = weight;
        so.FindProperty("slotSize").intValue = 1;
        so.FindProperty("stackMax").intValue = stackMax;
        so.FindProperty("maxDurability").intValue = maxDurability;
        so.FindProperty("baseValue").intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
    }
}
