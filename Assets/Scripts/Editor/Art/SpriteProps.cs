using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 【2.5D 세운 그림 재질】 (결정 2-127) — 그림 고르기는 ArtCatalog(런타임), 여기는 그림 가져오기 설정과 재질 만들기만.
/// 재질은 그림마다 하나(알파 잘라내기 · 양면 · 그림자 드리움).
/// </summary>
public static class SpriteProps
{
    public const string MaterialFolder = "Assets/Art/Meterials/Sprites";

    // ── 재질 · 부품 ─────────────────────────────────────────────────

    private static readonly Dictionary<string, Material> Materials = new();
    private static readonly Dictionary<string, float> Aspects = new();

    public static float AspectOf(string path)
    {
        if (Aspects.TryGetValue(path, out float a))
            return a;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        a = tex != null ? tex.width / (float)tex.height : 1f;
        Aspects[path] = a;
        return a;
    }

    public static bool Exists(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null;

    /// <summary>그림 재질 — URP Lit · 알파 잘라내기 · 양면 · 반짝임 없음. 도트 돌은 점 확대, 나무는 부드럽게.</summary>
    public static Material MaterialOf(string path)
    {
        if (Materials.TryGetValue(path, out Material cached) && cached != null)
            return cached;

        Prepare(path);
        LowPolyKit.EnsureFolder(MaterialFolder);
        string matPath = $"{MaterialFolder}/SP_{System.IO.Path.GetFileNameWithoutExtension(path)}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, matPath);
        }

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        m.SetTexture("_BaseMap", tex);
        m.mainTexture = tex;
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.5f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.SetFloat("_Cull", 0f);
        m.SetFloat("_Smoothness", 0f);
        m.SetFloat("_SpecularHighlights", 0f);
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.SetFloat("_EnvironmentReflections", 0f);
        m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        m.SetOverrideTag("RenderType", "TransparentCutout");
        EditorUtility.SetDirty(m);
        Materials[path] = m;
        return m;
    }

    /// <summary>그림 가져오기 설정 — 휴대폰에 맞춰 나무는 512로 줄이고, 도트 돌은 점 확대 · 압축 없음.</summary>
    private static void Prepare(string path)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter ti)
            return;

        bool pixel = path.StartsWith(ArtCatalog.RockFolder);
        int max = pixel ? 64 : 512;
        FilterMode filter = pixel ? FilterMode.Point : FilterMode.Bilinear;
        bool changed = ti.maxTextureSize != max || ti.filterMode != filter || ti.alphaIsTransparency != true || ti.mipmapEnabled != !pixel;
        if (!changed)
            return;

        ti.maxTextureSize = max;
        ti.filterMode = filter;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = !pixel;
        ti.wrapMode = TextureWrapMode.Clamp;
        if (pixel)
            ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.SaveAndReimport();
    }
}
