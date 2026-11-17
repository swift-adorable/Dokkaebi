using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 【소굴 소품】 — 벽가 · 구석에 항아리 · 자루 · 짚 · 바구니 · 버섯 · 뿌리 같은 잡동사니를 둔다 (2026-11-17).
///   · 그림: Resources/Props/&lt;이름&gt;.png — nanasha 「Dark Dungeons Free Critters &amp; Clutter」(상업용 가능 · AI 학습 금지라 학습에는 안 씀)
///   · 판: 건물 그림판과 같은 재질(빛 받음 · 그림자) · 카메라 쪽으로 눕힌다 · 부딪힘 없음(장식)
///   · 크기: 1m 에 PixelsPerMeter 칸 — 캐릭터(23.5칸)보다 잘게 찍어 항아리가 사람 키를 넘지 않게
///   · 등잔: 곳간 · 출발 자리 · 돌문 등불 아래 바닥에 흙 등잔 + 노란 불꽃(devkidd 불꽃 팩, 움직임)
///   · 건물을 그 자리에 지으면 그 둘레 소품은 숨긴다 (BunkerBuildings.Rebuild 가 Refresh 를 부른다)
/// </summary>
public static class BunkerProps
{
    /// <summary>1m 에 몇 칸 — 캐릭터 · 바닥 · 건물과 같은 칸 크기(그림마다 도트 굵기가 달라 어색했다, 2026-11-17).</summary>
    public static float PixelsPerMeter => BuildingSprite.PixelsPerMeter;
    private const string RootName = "Bunker Props";

    /// <summary>
    /// 무더기 — (가운데 x, z, 퍼짐 반지름, 그림들). 쓰임새 곁에 모아 둔다: 곳간 옆 항아리 · 자루, 샘가 버섯 · 이끼,
    /// 출발 자리 짚 · 양동이, 장부방 쪽 책 · 걸상. 무더기 안 자리는 씨앗 고정 난수로 흩는다(늘 같은 모습).
    /// (2026-11-17 사용자 「너무 부자연스럽게 구석 벽 쪽에만」 → 벽 한 줄에서 쓰임새 무더기 + 바닥 흩뿌림으로)
    /// </summary>
    private static readonly (float x, float z, float r, string[] sprites)[] Clusters =
    {
        (-12.0f, 10.4f, 1.5f, new[] { "clay_jar_with_a_lid", "cracked_clay_pot", "clay_water_jug", "patched_sack", "small_sack_of_grain" }),
        (-6.4f, 9.6f, 0.9f, new[] { "woven_basket", "small_wooden_crate" }),
        (3.8f, 8.6f, 1.1f, new[] { "small_ring_of_mushrooms", "glowing_moss_patch", "white_puffball_mushroom" }),
        (4.6f, 11.8f, 0.7f, new[] { "tall_ink_cap_mushroom", "cave_fern" }),
        (-3.2f, -10.2f, 1.2f, new[] { "small_hay_bale", "pile_of_straw", "rusty_bucket" }),
        (3.6f, -10.8f, 0.8f, new[] { "small_wooden_crate", "patched_sack" }),
        (-17.6f, 2.8f, 1.1f, new[] { "small_pile_of_dusty_books", "broken_wooden_stool" }),
        (17.8f, 11.2f, 1.2f, new[] { "tangle_of_roots", "orange_shelf_fungus", "cave_fern" }),
        (-18.2f, -11.4f, 0.9f, new[] { "tangle_of_roots", "lichen_covered_stone" }),
        (18.4f, -3.0f, 1.0f, new[] { "bucket_of_water", "mossy_log_piece" }),
    };

    /// <summary>바닥 흩뿌림 — 방 안 빈 땅에 하나씩(고정 시설 둘레는 비운다).</summary>
    private static readonly string[] Scatter =
    {
        "pile_of_pebbles", "lichen_covered_stone", "white_puffball_mushroom", "mossy_log_piece", "tangle_of_roots",
        "pile_of_pebbles", "glowing_moss_patch", "orange_shelf_fungus", "lichen_covered_stone", "small_ring_of_mushrooms",
        "pile_of_pebbles", "tangle_of_roots",
    };

    /// <summary>비워 둘 자리 (x, z, 반지름) — 곳간 · 샘 · 돌문 · 출발 · 설계도 · 방 가운데 길.</summary>
    private static readonly (float x, float z, float r)[] KeepOut =
    {
        (-9f, 11f, 2.4f), (7f, 11f, 3.4f), (0f, 11f, 2.2f), (0f, -11f, 2.6f), (3f, 0f, 2.4f), (0f, 0f, 2.2f),
    };

    private static readonly Dictionary<string, Texture2D> cache = new();
    private static readonly List<(GameObject go, Vector2 at)> spawned = new();

    public static void Ensure()
    {
        if (GameObject.Find(RootName) != null)
            return;
        spawned.Clear();
        var root = new GameObject(RootName).transform;

        var rnd = new System.Random(1117);
        var taken = new List<Vector2>();
        foreach (var (cx, cz, r, sprites) in Clusters)
            foreach (string sprite in sprites)
            {
                Vector2 at = Pick(rnd, () => new Vector2(cx, cz) + Disc(rnd) * r, taken, 0.7f);
                Spawn(root, sprite, at, rnd);
            }

        float hx = BunkerLayout.RoomWidth * 0.5f - 1.5f, hz = BunkerLayout.RoomDepth * 0.5f - 1.5f;
        foreach (string sprite in Scatter)
        {
            Vector2 at = Pick(rnd, () => new Vector2(Lerp(rnd, -hx, hx), Lerp(rnd, -hz, hz)), taken, 3.5f, keepOut: true);
            Spawn(root, sprite, at, rnd);
        }

        // 등잔 — 소굴 등불(BunkerMood) 아래 바닥에. 불꽃은 그림 위에 얹는다.
        foreach (Vector3 at in new[]
                 {
                     BunkerLayout.StashPosition + new Vector3(1.4f, 0f, -0.9f),
                     new Vector3(1.3f, 0f, -BunkerLayout.RoomDepth * 0.5f + 2.2f),
                     new Vector3(1.6f, 0f, BunkerLayout.RoomDepth * 0.5f - 1.6f),
                 })
        {
            GameObject lamp = Card(root, "clay_oil_lamp", at);
            if (lamp == null)
                continue;
            spawned.Add((lamp, new Vector2(at.x, at.z)));
            Flame(lamp.transform, at);
        }
        Refresh();
    }

    /// <summary>건물 자리와 겹치는 소품은 숨긴다 — 건물을 짓거나 옮길 때마다.</summary>
    public static void Refresh()
    {
        var footprints = new List<Rect>();
        foreach (KeyValuePair<string, BuildingPose> pair in BuildingManager.State.Placed)
        {
            BuildingDefinition d = BuildingTable.Find(pair.Key);
            if (d != null)
                footprints.Add(BunkerLayout.Footprint(d, pair.Value));
        }

        foreach (var (go, at) in spawned)
        {
            if (go == null)
                continue;
            bool covered = false;
            foreach (Rect r in footprints)
                if (r.Contains(at))
                {
                    covered = true;
                    break;
                }
            go.SetActive(!covered);
        }
    }

    private static void Spawn(Transform root, string sprite, Vector2 at, System.Random rnd)
    {
        GameObject card = Card(root, sprite, new Vector3(at.x, 0f, at.y));
        if (card == null)
            return;
        if (rnd.NextDouble() < 0.5)   // 좌우만 뒤집어 같은 그림이 덜 겹쳐 보이게
        {
            Vector3 s = card.transform.localScale;
            card.transform.localScale = new Vector3(-s.x, s.y, s.z);
        }
        spawned.Add((card, at));
    }

    private static float Lerp(System.Random rnd, float a, float b) => a + (b - a) * (float)rnd.NextDouble();

    private static Vector2 Disc(System.Random rnd)
    {
        float a = (float)rnd.NextDouble() * Mathf.PI * 2f, d = Mathf.Sqrt((float)rnd.NextDouble());
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.7f) * d;   // 앞뒤로 덜 퍼지게(눕힌 판이 겹쳐 보이지 않게)
    }

    /// <summary>다른 소품과 minGap 이상 떨어진 자리를 몇 번 뽑아 본다. 못 찾으면 마지막 자리.</summary>
    private static Vector2 Pick(System.Random rnd, System.Func<Vector2> sample, List<Vector2> taken, float minGap, bool keepOut = false)
    {
        Vector2 p = sample();
        for (int tries = 0; tries < 40; tries++, p = sample())
        {
            bool ok = true;
            foreach (Vector2 q in taken)
                if ((q - p).sqrMagnitude < minGap * minGap) { ok = false; break; }
            if (ok && keepOut)
                foreach (var (x, z, r) in KeepOut)
                    if ((new Vector2(x, z) - p).sqrMagnitude < r * r) { ok = false; break; }
            if (ok)
                break;
        }
        taken.Add(p);
        return p;
    }

    private static GameObject Card(Transform root, string sprite, Vector3 at)
    {
        if (!cache.TryGetValue(sprite, out Texture2D tex) || tex == null)
        {
            tex = Resources.Load<Texture2D>($"Props/{sprite}");
            if (tex == null)
                return null;
            cache[sprite] = tex;   // 정적으로 붙잡아 장면을 바꿔도 내려가지 않게 (BuildingSprite 와 같은 이유)
        }

        var card = new GameObject($"Prop_{sprite}");
        card.transform.SetParent(root, false);
        card.transform.SetPositionAndRotation(at, Quaternion.Euler(ArtCatalog.Pitch, 0f, 0f));
        card.transform.localScale = new Vector3(tex.width / PixelsPerMeter, tex.height / PixelsPerMeter, 1f);
        card.AddComponent<MeshFilter>().sharedMesh = BuildingSprite.Quad();
        var renderer = card.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = BuildingSprite.Material();
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        var block = new MaterialPropertyBlock();
        block.SetTexture("_BaseMap", tex);
        block.SetTexture("_MainTex", tex);
        renderer.SetPropertyBlock(block);
        return card;
    }

    /// <summary>등잔 위 노란 불꽃 — 불꽃 팩 띠 그림(Wisp/yellow_g4-5)을 돌린다. 빛을 받지 않는 그림이라 어둠 속에서도 밝다.</summary>
    private static void Flame(Transform lamp, Vector3 at)
    {
        var go = new GameObject("Flame");
        go.transform.SetParent(lamp.parent, false);
        go.transform.SetPositionAndRotation(at + new Vector3(0f, 0.32f, -0.05f), Quaternion.Euler(ArtCatalog.Pitch, 0f, 0f));
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<FlameLoop>();
    }

    /// <summary>불꽃 띠 그림을 8장으로 잘라 돌린다.</summary>
    private sealed class FlameLoop : MonoBehaviour
    {
        private static Sprite[] frames;
        private SpriteRenderer sr;
        private float t;

        private void Start()
        {
            sr = GetComponent<SpriteRenderer>();
            if (frames == null || frames.Length == 0 || frames[0] == null)
            {
                var sheet = Resources.Load<Texture2D>("Wisp/yellow_g4-5");
                if (sheet == null)
                {
                    enabled = false;
                    return;
                }
                int n = Mathf.Max(1, sheet.width / 32);
                frames = new Sprite[n];
                for (int i = 0; i < n; i++)
                    frames[i] = Sprite.Create(sheet, new Rect(i * 32, 0, 32, sheet.height), new Vector2(0.5f, 0.1f), 64f, 0, SpriteMeshType.FullRect);
            }
            t = Random.value;
        }

        private void Update()
        {
            t += Time.deltaTime;
            sr.sprite = frames[(int)(t * 10f) % frames.Length];
        }
    }
}
