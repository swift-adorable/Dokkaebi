using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 【벙커 건물 그림판】 — 상자 대신 도트 한옥 그림(Resources/Buildings/&lt;건물 id&gt;.png)을 세운다.
///   · 그림이 있으면 상자 · 부뚜막 · 약탕의 겉모습(Renderer)만 끄고, 부딪힘(Collider) · 자리(Station)는 그대로 둔다
///   · 판 · 재질은 캐릭터 판(CharacterSprite)과 같다: 카메라 쪽으로 ArtCatalog.Pitch 만큼 눕힌다 — 건물을 돌려도 그림은 늘 정면
///   · 아래 가운데가 원점: 건물 자리의 앞쪽 가장자리(-Z)에 그림 바닥이 닿는다
///   · 진짜 도트 그림(폭 NativeMaxPixels 이하, PixelLab 등)은 그림 한 칸 = 캐릭터 한 칸 — 크기는 그림이 정한다
///   · 큰 그림(AI 1024 가짜 도트)은 폭 = 건물 자리 폭 × WidthScale. 높이는 그림 비율대로
///   · 도트 밀도를 캐릭터와 맞춘다 — 캐릭터는 키 2m 가 47칸(CharacterSprite.BodyPixels)이라, 건물 그림도 1m 에 같은 칸 수가
///     되도록 불러올 때 줄여 쓴다(파일은 그대로). 줄인 그림은 점 필터라 캐릭터처럼 또렷한 도트로 보인다
///   · 이름표 — 가게 이름은 지붕 위로, 부뚜막 · 약탕 이름은 건물 앞 땅 위로 옮긴다
///   · 등불 — 소굴이 어두워 그림 앞에 따뜻한 점 빛 하나를 단다 (BunkerMood.Lamp)
///   · 그림자 — 캐릭터 판처럼 판이 해 그림자를 땅에 드리우고, 다른 그림자도 받는다
/// 그림이 없으면 아무것도 하지 않는다(상자 그대로).
/// </summary>
public static class BuildingSprite
{
    public const string Folder = "Buildings";

    /// <summary>그림 폭 ÷ 건물 자리 폭. 화면을 보며 맞춘다.</summary>
    public static float WidthScale = 1.6f;

    /// <summary>이 폭(칸) 이하의 그림은 진짜 도트로 보고 줄이지 않는다.</summary>
    public const int NativeMaxPixels = 512;

    /// <summary>1m 에 몇 칸 — 캐릭터 판과 같게 (키 PixelArtView.CharacterHeight m 가 BodyPixels 칸).</summary>
    public static float PixelsPerMeter => CharacterSprite.BodyPixels / PixelArtView.CharacterHeight;

    private static readonly Dictionary<string, Texture2D> cache = new();
    private static Mesh quad;
    private static Material material;

    public static bool TryAttach(Transform root, BuildingDefinition definition)
    {
        // 정적 캐시에 붙잡아 둔다 — 그림이 MaterialPropertyBlock 에만 걸려 있으면 장면을 새로 불러온 뒤
        // 안 쓰는 에셋 정리(UnloadUnusedAssets)에 내려가 회색 판만 남았다 (파밍에서 돌아오거나 죽고 돌아올 때, 2026-11-17).
        if (!cache.TryGetValue(definition.Id, out Texture2D source) || source == null)
        {
            source = Resources.Load<Texture2D>($"{Folder}/{definition.Id}");
            if (source == null)
                return false;
            cache[definition.Id] = source;
        }

        // 진짜 도트 그림(작은 그림)은 그림 한 칸 = 캐릭터 한 칸으로 그대로 세운다 — 크기는 그림이 정한다.
        // 큰 그림(AI 가 1024 로 그린 가짜 도트)만 건물 자리 폭에 맞춰 캐릭터 밀도로 줄인다.
        bool native = source.width <= NativeMaxPixels;
        float width = native ? source.width / PixelsPerMeter : definition.Width * WidthScale;
        Texture2D tex = native ? source : Load(definition.Id, source, width);

        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            if (r.GetComponent<TextMesh>() == null)   // 이름표는 남긴다
                r.enabled = false;

        var card = new GameObject("Sprite");
        card.transform.SetParent(root, false);

        // 건물이 돌아가 있어도 그림은 세계 기준으로 카메라를 향한다.
        Quaternion tilt = Quaternion.Euler(ArtCatalog.Pitch, 0f, 0f);
        Vector3 front = root.position + Vector3.back * (definition.Depth * 0.5f);
        float height = width * tex.height / tex.width;
        card.transform.SetPositionAndRotation(front, tilt);
        card.transform.localScale = new Vector3(width, height, 1f);
        // 부모(건물 뿌리)는 크기 1이라 localScale 이 곧 세계 크기다.

        card.AddComponent<MeshFilter>().sharedMesh = Quad();
        var renderer = card.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = Material();
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        var block = new MaterialPropertyBlock();
        block.SetTexture("_BaseMap", tex);
        block.SetTexture("_MainTex", tex);
        renderer.SetPropertyBlock(block);

        // 소굴은 어두워 가게 앞에 등불 하나 (BunkerMood).
        BunkerMood.Lamp(card.transform.parent, "Shop", front + new Vector3(0f, 1f, -1f), BunkerMood.Fire, 6f, 7f);

        // 이름표가 그림 한가운데를 가리지 않게 — 가게 이름은 지붕 위, 곁 시설(부뚜막 · 약탕) 이름은 건물 앞 땅 위.
        Vector3 roofTop = front + tilt * Vector3.up * (height + 0.4f);
        foreach (TextMesh label in root.GetComponentsInChildren<TextMesh>())
        {
            Transform t = label.transform;
            bool main = t.parent != null && t.parent.name == "Station";
            t.position = main
                ? new Vector3(t.position.x, roofTop.y, roofTop.z)
                : new Vector3(t.position.x, 0.4f, front.z - 0.5f);
        }
        return true;
    }

    private static Texture2D Load(string id, Texture2D source, float widthMeters)
    {
        int targetW = Mathf.Max(8, Mathf.RoundToInt(widthMeters * PixelsPerMeter));
        string key = $"{id}@{targetW}";
        if (cache.TryGetValue(key, out Texture2D tex))
            return tex;
        tex = Shrink(source, targetW);
        cache[key] = tex;
        return tex;
    }

    /// <summary>
    /// 큰 그림을 캐릭터 도트 밀도로 줄인다. 반씩 여러 번 줄여(이중 선형) 한 번에 줄일 때의 지글거림을 막고,
    /// 마지막에 투명도를 0 · 1 로 딱 잘라 가장자리가 도트처럼 떨어지게 한다. 결과는 점 필터.
    /// </summary>
    private static Texture2D Shrink(Texture2D source, int targetW)
    {
        int targetH = Mathf.Max(1, Mathf.RoundToInt((float)targetW * source.height / source.width));
        RenderTexture previous = RenderTexture.active;
        FilterMode sourceFilter = source.filterMode;
        source.filterMode = FilterMode.Bilinear;

        Texture current = source;
        RenderTexture temp = null;
        int w = source.width, h = source.height;
        while (w / 2 >= targetW)
        {
            w /= 2; h = Mathf.Max(1, h / 2);
            RenderTexture next = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            next.filterMode = FilterMode.Bilinear;
            Graphics.Blit(current, next);
            if (temp != null)
                RenderTexture.ReleaseTemporary(temp);
            temp = next; current = next;
        }

        RenderTexture final = RenderTexture.GetTemporary(targetW, targetH, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(current, final);
        if (temp != null)
            RenderTexture.ReleaseTemporary(temp);

        RenderTexture.active = final;
        var result = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false, false)
        {
            name = source.name + "_px",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        result.ReadPixels(new Rect(0, 0, targetW, targetH), 0, 0);
        Color32[] px = result.GetPixels32();
        for (int i = 0; i < px.Length; i++)
            px[i].a = px[i].a >= 128 ? (byte)255 : (byte)0;
        result.SetPixels32(px);
        result.Apply(false, false);

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(final);
        source.filterMode = sourceFilter;
        return result;
    }

    /// <summary>아래 가운데가 원점인 1×1 판. 앞이 -z(카메라 쪽).</summary>
    internal static Mesh Quad()
    {
        if (quad != null)
            return quad;
        quad = new Mesh { name = "BuildingCard" };
        quad.SetVertices(new List<Vector3> { new(-0.5f, 0f, 0f), new(0.5f, 0f, 0f), new(-0.5f, 1f, 0f), new(0.5f, 1f, 0f) });
        quad.SetUVs(0, new List<Vector2> { new(0f, 0f), new(1f, 0f), new(0f, 1f), new(1f, 1f) });
        quad.SetNormals(new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
        quad.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
        quad.RecalculateBounds();
        return quad;
    }

    /// <summary>
    /// 캐릭터 판과 같은 재질(빛 받음 · 알파 잘라내기 · 양면) — 아트 꾸러미의 나무 판 재질을 복사한다.
    /// 깊이를 써서 캐릭터와 앞뒤가 맞고, 밤 · 날씨 빛도 캐릭터와 똑같이 받는다. 꾸러미가 없으면 URP Lit에 같은 설정.
    /// </summary>
    internal static Material Material()
    {
        if (material != null)
            return material;

        Material source = null;
        ArtKit kit = ArtKit.Load();
        if (kit != null)
            foreach (ArtKit.Entry e in kit.Entries)
                if (e.Key != null && e.Key.StartsWith("S:") && e.Material != null)
                {
                    source = e.Material;
                    break;
                }

        if (source != null)
            material = new Material(source) { name = "BuildingCard" };
        else
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "BuildingCard" };
            material.SetFloat("_AlphaClip", 1f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", 0f);
            material.SetFloat("_Smoothness", 0f);
            material.renderQueue = (int)RenderQueue.AlphaTest;
        }
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Cutoff", 0.5f);
        return material;
    }
}
