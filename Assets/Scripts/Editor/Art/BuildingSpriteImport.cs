using UnityEditor;
using UnityEngine;

/// <summary>
/// 【도트 건물 그림 가져오기】 — Resources/Buildings/ 아래 PNG는 캐릭터 그림처럼
/// 점 필터 · 압축 없음 · 밉맵 없음 · 가장자리 고정으로 가져온다 (BuildingSprite 가 쓴다).
/// </summary>
public class BuildingSpriteImport : AssetPostprocessor
{
    private const string Folder = "Assets/Resources/Buildings/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder))
            return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.sRGBTexture = true;
    }
}
