using UnityEditor;
using UnityEngine;

/// <summary>
/// 6-C 전체를 한 번에 돌리는 진입점.
///
/// 개별 생성기를 따로 두고 이것을 얹은 이유 —
/// 무기만 고치고 싶을 때 방어구 76종을 다시 쓰는 것은 낭비다.
/// </summary>
public static class EquipmentAssetGenerator
{
    [MenuItem("Dokkaebi/Equipment/장비 에셋 전체 생성")]
    public static void GenerateAll()
    {
        AssetDatabase.StartAssetEditing();

        try
        {
            WeaponAssetGenerator.Generate();
            ArmourAssetGenerator.Generate();
            ImprintAssetGenerator.Generate();
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[EquipmentAssetGenerator] 6-C 장비 에셋 전체 생성 완료");
    }
}
