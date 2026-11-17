using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 【벙커 바닥 그림】 — PixelLab 바닥 타일(Resources/Floor/)을 이어 붙여 방 바닥 한 장을 만든다.
///   · 타일 두 벌: 흙↔이끼(earth_moss) · 흙↔돌길(earth_stone, 흙을 이어 뽑음). 타일 픽셀은 그린 그대로 옮기기만 한다
///   · 꼭짓점마다 땅 종류(흙 · 이끼 · 돌)를 정하고, 칸의 네 모서리에 맞는 Wang 타일을 메타데이터(corners)에서 골라 붙인다
///   · 돌길 — 출발 자리 → 고목 앞 → 곳간 · 샘 · 돌문. 이끼 — 벽가에 많고 가운데는 드문드문(고정 시드)
///   · 이끼와 돌이 한 칸에 같이 오지 않게 돌 둘레는 흙으로 비운다 (타일 한 벌은 땅 두 종류만 잇는다)
///   · 도트 밀도는 캐릭터 · 건물 그림과 같다 (그림 한 칸 = 캐릭터 한 칸)
/// 원래 바닥 상자는 겉모습만 끄고 부딪힘은 그대로 둔다. 타일이 없으면 아무것도 하지 않는다.
/// </summary>
public static class BunkerFloor
{
    private const string Folder = "Floor";
    private const int Earth = 0, Moss = 1, Stone = 2;

    /// <summary>바닥 타일 두 벌 이름 — 흙 ↔ 덮개(이끼 · 뿌리), 흙 ↔ 길. 이어 뽑아 흙이 같다.</summary>
    public static string CoverSheet = "den_roots", PathSheet = "den_path";

    /// <summary>
    /// 이어 까는 바닥 텍스처(Resources/Floor/&lt;이름&gt;) — 비우면 위 타일 두 벌로 짠다.
    /// 텍스처 한 장이 TextureMeters m 마다 되풀이된다. 시험 중 (2026-11-17, 무료 텍스처 팩 Roots 10).
    /// </summary>
    public static string TextureName = "";
    public static float TextureMeters = 6f;

    /// <summary>
    /// 섞어 까는 바닥 — 바탕 흙(BlendBase) 위에 벽가 · 구석에만 덮개(BlendCover, 뿌리)를 얹는다. 둘 다 도트 밀도 텍스처(tex_px_*).
    /// BlendBase 를 비우면 쓰지 않는다. 사용자 「뿌리가 너무 도배 되서 좀 징그러워」 (2026-11-17).
    /// </summary>
    public static string BlendBase = "tex_px_mud1", BlendCover = "tex_px_roots10";

    /// <summary>벽에서 이 거리(m) 안쪽까지 뿌리가 뻗는다.</summary>
    public static float CoverReach = 3.5f;

    /// <summary>
    /// 섞은 바닥의 톤 — 1 이면 텍스처 그대로. 사용자 「살짝만 더 차분하게」: 조금 어둡게 · 채도를 조금 뺀다.
    /// </summary>
    public static float Brightness = 0.85f, Saturation = 0.8f;

    /// <summary>
    /// 고목 뿌리 옆 샘물 (본문 300 「고목 뿌리 옆의 샘물」 · 결정 2-73) — 섞은 바닥에 물웅덩이를 그려 넣고, 둘레에 뿌리를 모은다.
    /// 물 텍스처(SpringWater)가 있으면 샘의 임시 원판은 숨긴다.
    /// </summary>
    public static string SpringWater = "tex_px_water8";
    public static float SpringRadius = 2f;

    /// <summary>샘을 옆(x)으로 늘이는 비 — 북쪽 벽과 약탕간 사이에 들어가게 옆으로 길쭉하게.</summary>
    public static float SpringStretch = 1.35f;

    /// <summary>샘물을 바닥에 그렸는가 — 그렸으면 샘의 임시 원판을 숨긴다.</summary>
    public static bool PaintsSpring => !string.IsNullOrEmpty(BlendBase) && Resources.Load<Texture2D>($"{Folder}/{SpringWater}") != null;

    /// <summary>샘 임시 원판을 바닥 그림에 맞춰 켜고 끈다.</summary>
    public static void SyncSpring()
    {
        GameObject body = GameObject.Find("Spring/Body");
        if (body != null && body.TryGetComponent(out Renderer r))
            r.enabled = !PaintsSpring;
    }

    /// <summary>바닥을 지우고 다시 깐다 — 시험용으로 바닥 종류를 바꿀 때.</summary>
    public static void Rebuild()
    {
        GameObject floor = GameObject.Find("Bunker Room/Floor");
        Transform art = floor != null && floor.transform.parent != null ? floor.transform.parent.Find("FloorArt") : null;
        if (art != null)
            UnityEngine.Object.DestroyImmediate(art.gameObject);
        texture = null;   // 만든 바닥 한 장을 버리고 다시 만든다
        Ensure();
    }

    private static Texture2D texture;

    /// <summary>지금 깐 바닥 한 장 (타일 · 섞은 바닥일 때) — 확인용.</summary>
    public static Texture2D Current => texture;
    private static Material material;

    public static void Ensure()
    {
        GameObject floor = GameObject.Find("Bunker Room/Floor");
        if (floor == null || floor.transform.parent == null || floor.transform.parent.Find("FloorArt") != null)
            return;

        Texture2D tiled = string.IsNullOrEmpty(TextureName) ? null : Resources.Load<Texture2D>($"{Folder}/{TextureName}");
        float widthM = BunkerLayout.RoomWidth, depthM = BunkerLayout.RoomDepth;
        Texture2D tex = tiled != null ? tiled
            : !string.IsNullOrEmpty(BlendBase) ? Blend(out widthM, out depthM)
            : Build(out widthM, out depthM);
        if (tex == null)
            return;

        if (floor.TryGetComponent(out Renderer box))
            box.enabled = false;

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "FloorArt";
        UnityEngine.Object.Destroy(quad.GetComponent<Collider>());
        // 바닥 상자는 크기가 방 크기라 그 옆(방 뿌리 아래)에 둔다.
        quad.transform.SetPositionAndRotation(new Vector3(0f, 0.002f, 0f), Quaternion.Euler(90f, 0f, 0f));
        quad.transform.localScale = new Vector3(widthM, depthM, 1f);
        quad.transform.SetParent(floor.transform.parent, true);

        var r = quad.GetComponent<MeshRenderer>();
        if (tiled != null)
        {
            tiled.wrapMode = TextureWrapMode.Repeat;   // 되풀이해 깐다 (가져오기 설정과 상관없이)
            var m = new Material(Material(tex)) { name = "BunkerFloorTexture" };
            var repeat = new Vector2(widthM / TextureMeters, depthM / TextureMeters);
            m.SetTextureScale("_BaseMap", repeat);
            m.SetTextureScale("_MainTex", repeat);
            r.sharedMaterial = m;
        }
        else
            r.sharedMaterial = Material(tex);
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = true;
        SyncSpring();
    }

    private static Texture2D Build(out float widthM, out float depthM)
    {
        widthM = depthM = 0f;
        if (texture != null)
            return Size(texture, out widthM, out depthM);

        var grass = Sheet.Load(CoverSheet);
        var path = Sheet.Load(PathSheet);
        if (grass == null || path == null)
            return null;

        int t = grass.TileSize;
        float tileM = t / BuildingSprite.PixelsPerMeter;
        int nx = Mathf.CeilToInt(BunkerLayout.RoomWidth / tileM);
        int nz = Mathf.CeilToInt(BunkerLayout.RoomDepth / tileM);
        int[,] v = Terrain(nx + 1, nz + 1, tileM);

        texture = new Texture2D(nx * t, nz * t, TextureFormat.RGBA32, false, false)
        {
            name = "BunkerFloor",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                int nw = v[i, j + 1], ne = v[i + 1, j + 1], sw = v[i, j], se = v[i + 1, j];
                bool stone = nw == Stone || ne == Stone || sw == Stone || se == Stone;
                Sheet sheet = stone ? path : grass;
                int up = stone ? Stone : Moss;
                texture.SetPixels(i * t, j * t, t, t, sheet.Tile(nw == up, ne == up, sw == up, se == up));
            }
        texture.Apply(false, true);
        return Size(texture, out widthM, out depthM);
    }

    /// <summary>바탕 흙 + 벽가 뿌리를 캐릭터 도트 밀도로 한 장에 섞는다. 경계는 잡음으로 들쭉날쭉하게.</summary>
    private static Texture2D Blend(out float widthM, out float depthM)
    {
        widthM = depthM = 0f;
        var a = Resources.Load<Texture2D>($"{Folder}/{BlendBase}");
        var b = Resources.Load<Texture2D>($"{Folder}/{BlendCover}");
        if (a == null || b == null || !a.isReadable || !b.isReadable)
            return null;

        float ppm = BuildingSprite.PixelsPerMeter;
        int w = Mathf.CeilToInt(BunkerLayout.RoomWidth * ppm), h = Mathf.CeilToInt(BunkerLayout.RoomDepth * ppm);
        Color32[] pa = a.GetPixels32(), pb = b.GetPixels32();
        var water = Resources.Load<Texture2D>($"{Folder}/{SpringWater}");
        Color32[] pw = water != null && water.isReadable ? water.GetPixels32() : null;
        Vector2 spring = new(BunkerLayout.SpringPosition.x, BunkerLayout.SpringPosition.z);
        var outPx = new Color32[w * h];
        float halfW = BunkerLayout.RoomWidth * 0.5f, halfD = BunkerLayout.RoomDepth * 0.5f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px = x / ppm - halfW, pz = y / ppm - halfD;

                // 뿌리 — 벽가를 따라 뻗는 깊이가 곳마다 다르다 (액자 테두리처럼 고르지 않게).
                float wall = Mathf.Min(halfW - Mathf.Abs(px), halfD - Mathf.Abs(pz));
                float reach = CoverReach * (0.35f + 1.1f * Mathf.PerlinNoise(px * 0.11f + 50f, pz * 0.11f + 20f));
                float edge = Mathf.Clamp01(1f - wall / reach);
                float noise = Mathf.PerlinNoise(px * 0.3f + 11.3f, pz * 0.3f + 5.1f);

                // 샘 — 옆으로 긴 들쭉날쭉한 웅덩이. 가운데는 깊어 어둡고, 가장자리는 얕은 물 · 젖은 흙.
                Vector2 rel = new Vector2(px, pz) - spring;
                float ds = pw != null ? new Vector2(rel.x / SpringStretch, rel.y).magnitude : 99f;
                float wobble = (Mathf.PerlinNoise(px * 0.9f + 3f, pz * 0.9f + 9f) - 0.5f) * 0.7f
                             + (Mathf.PerlinNoise(px * 2.6f + 7f, pz * 2.6f + 1f) - 0.5f) * 0.25f;
                float rim = SpringRadius + wobble;
                float near = Mathf.Clamp01(1f - Mathf.Abs(ds - rim - 1f) / 1.3f);   // 웅덩이 바깥을 감싸는 뿌리

                float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.7f, edge * 0.75f + noise * 0.45f + near * 0.5f));

                // 흙 — 같은 그림을 엇갈려 두 번(하나는 90° 돌려) 큰 잡음으로 섞고, 넓게 밝고 어두운 얼룩을 얹어 되풀이 무늬를 감춘다.
                Color ca1 = pa[(y % a.height) * a.width + x % a.width];
                Color ca2 = pa[((x + 61) % a.height) * a.width + (y + 23) % a.width];
                float swap = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.65f, Mathf.PerlinNoise(px * 0.09f + 70f, pz * 0.09f + 30f)));
                Color ca = Color.Lerp(ca1, ca2, swap) * Mathf.Lerp(0.85f, 1.1f, Mathf.PerlinNoise(px * 0.06f + 90f, pz * 0.06f + 40f));
                Color cb = pb[((y + 37) % b.height) * b.width + (x + 53) % b.width];
                Color c = Color.Lerp(ca, cb, k);

                if (ds < rim + 0.45f)
                    c *= Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(rim, rim + 0.45f, ds));   // 젖은 흙 테두리
                if (ds < rim)
                {
                    float t = ds / rim;   // 0 가운데 → 1 가장자리
                    Color cw = pw[(y % water.height) * water.width + x % water.width] * new Color(0.42f, 0.55f, 0.62f);
                    cw *= Mathf.Lerp(0.6f, 1f, t * t);                                        // 가운데는 깊어 어둡게
                    c = Color.Lerp(cw, c * 0.8f, Mathf.InverseLerp(0.82f, 1f, t) * 0.55f);    // 가장자리는 얕아 바닥이 비친다
                }

                float grey = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                c = Color.Lerp(new Color(grey, grey, grey), c, Saturation) * Brightness;
                c.a = 1f;
                outPx[y * w + x] = c;
            }

        texture = new Texture2D(w, h, TextureFormat.RGBA32, false, false)
        {
            name = "BunkerFloorBlend",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        texture.SetPixels32(outPx);
        texture.Apply(false, false);   // 확인용으로 다시 읽을 수 있게 둔다
        return Size(texture, out widthM, out depthM);
    }

    private static Texture2D Size(Texture2D tex, out float widthM, out float depthM)
    {
        widthM = tex.width / BuildingSprite.PixelsPerMeter;
        depthM = tex.height / BuildingSprite.PixelsPerMeter;
        return tex;
    }

    /// <summary>꼭짓점 (i, j) 의 땅 — 세계 위치는 방 남서 모서리에서 타일 크기 간격. j 가 커질수록 북쪽(화면 위).</summary>
    private static int[,] Terrain(int cx, int cz, float tileM)
    {
        float halfW = (cx - 1) * tileM * 0.5f, halfD = (cz - 1) * tileM * 0.5f;
        Vector2 depart = new(0f, -BunkerLayout.RoomDepth * 0.5f + 1f);
        Vector2 hub = new(0f, BunkerLayout.RoomDepth * 0.5f - 4f);
        Vector2 stash = new(BunkerLayout.StashPosition.x, BunkerLayout.StashPosition.z);
        Vector2 spring = new(BunkerLayout.SpringPosition.x, BunkerLayout.SpringPosition.z);
        Vector2 gate = new(0f, BunkerLayout.RoomDepth * 0.5f - 1f);
        (Vector2 a, Vector2 b)[] paths =
        {
            (depart, hub), (hub, gate),
            (new Vector2(stash.x, hub.y), new Vector2(spring.x, hub.y)),
            (new Vector2(stash.x, hub.y), stash), (new Vector2(spring.x, hub.y), spring),
        };
        const float pathHalf = 0.8f, clear = 1.6f;

        var v = new int[cx, cz];
        for (int i = 0; i < cx; i++)
            for (int j = 0; j < cz; j++)
            {
                var p = new Vector2(i * tileM - halfW, j * tileM - halfD);
                float d = float.MaxValue;
                foreach (var (a, b) in paths)
                    d = Mathf.Min(d, Distance(p, a, b));
                if (d <= pathHalf) { v[i, j] = Stone; continue; }
                if (d <= clear) { v[i, j] = Earth; continue; }

                float wall = Mathf.Min(halfW - Mathf.Abs(p.x), halfD - Mathf.Abs(p.y));
                float edge = Mathf.Clamp01(1f - wall / 4f);
                float noise = Mathf.PerlinNoise(p.x * 0.22f + 31.7f, p.y * 0.22f + 7.3f);
                v[i, j] = noise * 0.8f + edge * 0.45f > 0.62f ? Moss : Earth;
            }
        return v;
    }

    private static float Distance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float k = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * k);
    }

    private static Material Material(Texture2D tex)
    {
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "BunkerFloor" };
            material.SetFloat("_Smoothness", 0f);
            material.SetColor("_BaseColor", Color.white);
        }
        material.SetTexture("_BaseMap", tex);
        material.SetTexture("_MainTex", tex);
        return material;
    }

    /// <summary>PixelLab 타일 한 벌 — 그림 + 메타데이터(타일마다 네 모서리 땅 · 그림 속 자리).</summary>
    private sealed class Sheet
    {
        private readonly Texture2D image;
        private readonly Dictionary<int, RectInt> tiles = new();
        public int TileSize { get; private set; }

        private Sheet(Texture2D image) => this.image = image;

        public static Sheet Load(string name)
        {
            var image = Resources.Load<Texture2D>($"{Folder}/{name}");
            var json = Resources.Load<TextAsset>($"{Folder}/{name}");
            if (image == null || json == null)
                return null;

            var meta = JsonUtility.FromJson<Meta>(json.text);
            var sheet = new Sheet(image);
            foreach (MetaTile tile in meta.tileset_data.tiles)
            {
                Box b = tile.bounding_box;
                // 메타데이터는 위에서부터 y, 텍스처는 아래에서부터.
                sheet.tiles[Key(tile.corners.NW == "upper", tile.corners.NE == "upper",
                                tile.corners.SW == "upper", tile.corners.SE == "upper")]
                    = new RectInt(b.x, image.height - b.y - b.height, b.width, b.height);
                sheet.TileSize = b.width;
            }
            return sheet.tiles.Count == 16 ? sheet : null;
        }

        public Color[] Tile(bool nw, bool ne, bool sw, bool se)
        {
            RectInt r = tiles[Key(nw, ne, sw, se)];
            return image.GetPixels(r.x, r.y, r.width, r.height);
        }

        private static int Key(bool nw, bool ne, bool sw, bool se) =>
            (nw ? 8 : 0) | (ne ? 4 : 0) | (sw ? 2 : 0) | (se ? 1 : 0);
    }

    [Serializable] private class Meta { public MetaData tileset_data; }
    [Serializable] private class MetaData { public MetaTile[] tiles; }
    [Serializable] private class MetaTile { public Corners corners; public Box bounding_box; }
    [Serializable] private class Corners { public string NW, NE, SW, SE; }
    [Serializable] private class Box { public int x, y, width, height; }
}
