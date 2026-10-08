using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 날씨 입자 재료를 Resources에 만든다 (결정 2-91) — 빌드에 반투명 입자 셰이더가 빠지지 않게.
/// </summary>
public static class WeatherAssetGenerator
{
    private const string Folder = "Assets/Resources/Weather";
    private const string TexturePath = Folder + "/SoftDot.png";
    private const string MaterialPath = Folder + "/WeatherParticle.mat";

    [MenuItem("Dokkaebi/Weather/날씨 입자 재료 생성")]
    public static void Generate()
    {
        Directory.CreateDirectory(Folder);

        File.WriteAllBytes(TexturePath, WeatherEffects.SoftDotTexture().EncodeToPNG());
        AssetDatabase.ImportAsset(TexturePath);

        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.shader = shader;
        WeatherEffects.SetupTransparent(material);
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        Debug.Log("[WeatherAssetGenerator] 날씨 입자 재료 생성 완료");
    }
}
