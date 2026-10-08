using System;
using UnityEngine;

/// <summary>
/// 장 맵 씬의 지도 (결정 2-90) — ZoneMap이 붙인다.
///
///   · 지도 그림(MapRaster)과 안개 그림(FogGrid)을 텍스처로 들고 있다 — 미니맵 · 전체 지도가 같이 쓴다
///   · 플레이어 둘레를 0.25초마다 「가 본 땅」으로 밝힌다(MapMemory — 장마다 · 세이브에 남는다)
///   · 금줄이 걷히면(RefreshGates) 지도를 다시 그린다
/// </summary>
public class MapRuntime : MonoBehaviour
{
    private static readonly Color FogColor = new(0.03f, 0.035f, 0.05f, 0.94f);

    public static MapRuntime Current { get; private set; }

    public int Chapter { get; private set; }
    public ChapterMap Map { get; private set; }
    public FogGrid Fog { get; private set; }
    public Texture2D MapTexture { get; private set; }
    public Texture2D FogTexture { get; private set; }

    /// <summary>전체 지도의 양피지 그림 — 처음 열 때 · 금줄이 걷힌 뒤 다시 열 때 만든다 (2026-10-08 참고 그림).</summary>
    public Texture2D ParchmentTexture { get; private set; }

    /// <summary>양피지 그림의 1m당 화소 — 확대해도 덜 뭉개지게 미니맵보다 촘촘하다.</summary>
    public const int ParchmentPixelsPerMeter = 5;

    private bool parchmentDirty = true;

    /// <summary>플레이어 (없으면 null).</summary>
    public Transform Player { get; private set; }

    /// <summary>지도 · 안개 그림이 바뀌었다.</summary>
    public event Action Redrawn;

    private float nextReveal;
    private Color32[] fogPixels;

    public static MapRuntime Attach(GameObject host, int chapter)
    {
        ChapterMap map = ZoneMapTable.Of(chapter);
        if (map == null)
            return null;

        // ?? 는 Unity의 가짜 null을 못 거른다 — TryGetComponent로.
        if (!host.TryGetComponent(out MapRuntime runtime))
            runtime = host.AddComponent<MapRuntime>();

        runtime.Init(chapter, map);
        return runtime;
    }

    private void Init(int chapter, ChapterMap map)
    {
        Chapter = chapter;
        Map = map;
        Fog = MapMemory.Fog(chapter);
        Current = this;

        MapTexture = new Texture2D(MapRaster.WidthOf(map), MapRaster.HeightOf(map), TextureFormat.RGBA32, false)
        {
            name = $"Map_Ch{chapter}",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        FogTexture = new Texture2D(Fog.Width, Fog.Height, TextureFormat.RGBA32, false)
        {
            name = $"Fog_Ch{chapter}",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        fogPixels = new Color32[Fog.Width * Fog.Height];

        RedrawMap();
        RevealAroundPlayer();
        RedrawFog();

        MinimapUI.EnsureInstance();
    }

    private void OnDestroy()
    {
        if (Current == this)
            Current = null;

        if (MapTexture != null) Destroy(MapTexture);
        if (FogTexture != null) Destroy(FogTexture);
        if (ParchmentTexture != null) Destroy(ParchmentTexture);
    }

    /// <summary>지도를 다시 그린다 — 금줄이 걷혔을 때.</summary>
    public void RedrawMap()
    {
        if (Map == null || MapTexture == null)
            return;

        MapTexture.SetPixels32(MapRaster.Render(Map, StoryManager.Progress.IsZoneOpen));
        MapTexture.Apply(false);
        parchmentDirty = true;
        Redrawn?.Invoke();
    }

    private void Update()
    {
        if (Time.time < nextReveal)
            return;

        nextReveal = Time.time + MapTable.RevealInterval;

        if (RevealAroundPlayer())
            RedrawFog();
    }

    private bool RevealAroundPlayer()
    {
        if (Player == null)
        {
            var movement = FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Exclude);
            Player = movement != null ? movement.transform : null;
        }

        return Player != null && Fog != null && Fog.Reveal(Player.position, MapTable.RevealRadius);
    }

    private void RedrawFog()
    {
        if (FogTexture == null)
            return;

        Color32 dark = FogColor;
        Color32 clear = new(dark.r, dark.g, dark.b, 0);

        for (int z = 0; z < Fog.Height; z++)
        for (int x = 0; x < Fog.Width; x++)
            fogPixels[z * Fog.Width + x] = Fog.IsRevealed(x, z) ? clear : dark;

        FogTexture.SetPixels32(fogPixels);
        FogTexture.Apply(false);
        Redrawn?.Invoke();
    }

    /// <summary>양피지 그림을 (필요하면 새로) 만들어 돌려준다.</summary>
    public Texture2D EnsureParchment()
    {
        if (Map == null)
            return null;

        if (!parchmentDirty && ParchmentTexture != null)
            return ParchmentTexture;

        int ppm = ParchmentPixelsPerMeter;
        int w = MapRaster.WidthOf(Map, ppm), h = MapRaster.HeightOf(Map, ppm);

        if (ParchmentTexture == null || ParchmentTexture.width != w || ParchmentTexture.height != h)
        {
            if (ParchmentTexture != null) Destroy(ParchmentTexture);
            ParchmentTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = $"Parchment_Ch{Chapter}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        Color32[] raw = MapRaster.Render(Map, StoryManager.Progress.IsZoneOpen, ppm, parchment: true);
        ParchmentTexture.SetPixels32(MapRaster.Parchment(raw, w, h));
        ParchmentTexture.Apply(false);
        parchmentDirty = false;
        return ParchmentTexture;
    }

    /// <summary>월드 자리 → 그림의 0 ~ 1 좌표.</summary>
    public Vector2 UvOf(Vector3 world) => MapRaster.UvOf(Map, world);

    /// <summary>그림의 0 ~ 1 좌표 → 월드 자리.</summary>
    public Vector3 WorldOfUv(Vector2 uv) => MapRaster.WorldOfUv(Map, uv);
}
