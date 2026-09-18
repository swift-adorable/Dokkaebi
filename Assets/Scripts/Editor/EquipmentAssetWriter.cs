using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 장비 에셋을 쓰는 공용 헬퍼. (로드맵 6-C)
///
/// 무기·방어구·가방·각인 생성기가 전부 이것을 쓴다.
/// SerializedObject 경로가 한 곳에만 있어야 필드 이름이 바뀔 때 고칠 곳이 하나다.
/// </summary>
public static class EquipmentAssetWriter
{
    public const string Root = "Assets/Data/ScriptableObjects/Items";

    /// <summary>한 장비의 모든 입력값. 생성기들이 표 형태로 채운다.</summary>
    public struct Row
    {
        public string id;
        public string name;
        public string desc;
        public ItemKind kind;
        public EquipmentSlot slot;
        public int tier;
        public float weight;
        public int durability;
        public int value;
        public string setFamily;
        public string imprintFamily;
        public StatusEffectType immunity;
        public StatusEffectType[] extraImmunities;
        public List<EquipmentStat> stats;

        // 무기 전용
        public float baseDamage;
        public float fireInterval;
        public float effectiveRange;
        public float projectileSpeed;
        public int attachmentSlots;
        public string weaponFamily;
    }

    /// <summary>
    /// 에셋을 만들거나 덮어쓴다. 이미 있으면 필드만 갱신한다 —
    /// 지우고 다시 만들면 프리팹·표의 참조(GUID)가 끊어진다.
    /// </summary>
    public static EquipmentDefinition Write(Row row, string folder, bool isWeapon)
    {
        EnsureFolder(folder);

        string path = $"{folder}/{row.id}.asset";

        var asset = AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(path);

        // 종류가 바뀌었으면(방어구 → 무기) 인스턴스를 새로 만들어야 한다.
        bool typeMismatch = asset != null && (asset is WeaponDefinition) != isWeapon;

        if (asset == null || typeMismatch)
        {
            if (typeMismatch)
                AssetDatabase.DeleteAsset(path);

            asset = isWeapon
                ? ScriptableObject.CreateInstance<WeaponDefinition>()
                : ScriptableObject.CreateInstance<EquipmentDefinition>();

            AssetDatabase.CreateAsset(asset, path);
        }

        var so = new SerializedObject(asset);

        so.FindProperty("id").stringValue = row.id;
        so.FindProperty("displayName").stringValue = row.name;
        so.FindProperty("description").stringValue = row.desc;
        so.FindProperty("kind").intValue = (int)row.kind;
        so.FindProperty("tier").intValue = row.tier;
        so.FindProperty("weight").floatValue = row.weight;
        so.FindProperty("slotSize").intValue = 1;
        so.FindProperty("stackMax").intValue = 1;
        so.FindProperty("maxDurability").intValue = row.durability;
        so.FindProperty("baseValue").intValue = row.value;

        so.FindProperty("slot").intValue = (int)row.slot;
        so.FindProperty("setFamily").stringValue = row.setFamily ?? string.Empty;
        so.FindProperty("imprintFamily").stringValue = row.imprintFamily ?? string.Empty;
        so.FindProperty("immunity").intValue = (int)row.immunity;

        SerializedProperty extra = so.FindProperty("extraImmunities");
        int extraCount = row.extraImmunities?.Length ?? 0;
        extra.arraySize = extraCount;

        for (int i = 0; i < extraCount; i++)
            extra.GetArrayElementAtIndex(i).intValue = (int)row.extraImmunities[i];

        WriteStats(so, row.stats);

        if (isWeapon)
        {
            so.FindProperty("baseDamage").floatValue = row.baseDamage;
            so.FindProperty("fireInterval").floatValue = row.fireInterval;
            so.FindProperty("effectiveRange").floatValue = row.effectiveRange;
            so.FindProperty("projectileSpeed").floatValue = row.projectileSpeed;
            so.FindProperty("attachmentSlots").intValue = row.attachmentSlots;
            so.FindProperty("weaponFamily").stringValue = row.weaponFamily ?? string.Empty;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(asset);

        return asset;
    }

    private static void WriteStats(SerializedObject so, List<EquipmentStat> stats)
    {
        SerializedProperty array = so.FindProperty("stats");

        int count = stats?.Count ?? 0;

        array.arraySize = count;

        for (int i = 0; i < count; i++)
        {
            SerializedProperty element = array.GetArrayElementAtIndex(i);

            element.FindPropertyRelative("type").intValue = (int)stats[i].type;
            element.FindPropertyRelative("value").floatValue = stats[i].value;
        }
    }

    /// <summary>표를 짧게 쓰기 위한 도우미. `S(HeadArmour, 2f)` 형태로 쓴다.</summary>
    public static EquipmentStat S(EquipmentStatType type, float value)
        => new(type, value);

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int split = path.LastIndexOf('/');

        EnsureFolder(path.Substring(0, split));

        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }
}
