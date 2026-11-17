using UnityEditor;
using UnityEngine;

/// <summary>
/// 【도트 건물 그림 가져오기】 — Resources/Buildings/ 아래 PNG는 캐릭터 그림처럼
/// 점 필터 · 압축 없음 · 밉맵 없음 · 가장자리 고정으로 가져온다 (BuildingSprite 가 쓴다).
/// Resources/Floor/ 바닥 타일도 같게, 픽셀을 읽을 수 있게 가져온다 (BunkerFloor 가 쓴다).
/// </summary>
public class BuildingSpriteImport : AssetPostprocessor
{
    private const string Folder = "Assets/Resources/Buildings/";
    private const string FloorFolder = "Assets/Resources/Floor/";
    private const string WispFolder = "Assets/Resources/Wisp/";     // 도깨비불 불꽃 (가로 띠 애니메이션) — PlayerWisp 가 쓴다   // 바닥 타일 — BunkerFloor 가 픽셀을 읽어 이어 붙인다

    private void OnPreprocessTexture()
    {
        bool floor = assetPath.StartsWith(FloorFolder);
        bool wisp = assetPath.StartsWith(WispFolder) || assetPath.StartsWith("Assets/Resources/Props/");   // 소굴 소품도 같은 설정
        bool wild = assetPath.StartsWith("Assets/Resources/Wild/");   // 장 맵 도트 꾸밈 — PixelDress 가 읽어 한 장(아틀라스)으로 묶는다
        bool enemy = assetPath.StartsWith("Assets/Resources/Enemies/");   // 적 띠 그림(설화 적 팩) — EnemySprite 가 칸을 잘라 돌린다
        if (!assetPath.StartsWith(Folder) && !floor && !wisp && !wild && !enemy)
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
        importer.isReadable = floor || wild;

        // 이어 까는 바닥 텍스처(tex_*) — 되풀이해 깔아야 해서 반복. 매끈한 그림(tex_hd_*)은 부드럽게 줄여 보이게.
        string file = System.IO.Path.GetFileName(assetPath);
        if (floor && file.StartsWith("tex_"))
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.isReadable = file.StartsWith("tex_px_");   // 도트 밀도 텍스처는 BunkerFloor 가 섞어 한 장으로 만든다
            if (file.StartsWith("tex_hd_"))
            {
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = 2048;
            }
        }
    }
}
