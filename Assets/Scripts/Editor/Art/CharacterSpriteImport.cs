using UnityEditor;
using UnityEngine;

/// <summary>
/// 【도트 캐릭터 그림 가져오기】 (결정 2-129) — Resources/Characters/ 아래 PNG는 도트가 번지지 않게
/// 점 필터 · 압축 없음 · 밉맵 없음 · 가장자리 고정으로 가져온다. 새 그림을 폴더에 넣기만 하면 된다.
/// </summary>
public class CharacterSpriteImport : AssetPostprocessor
{
    private const string Folder = "Assets/Resources/Characters/";

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
