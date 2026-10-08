using System.Collections.Generic;
using UnityEngine;

/// <summary>그레이박스 덩어리의 종류 — 색 · 높이 · 막히는지를 정한다 (결정 2-86).</summary>
public enum MapBlockKind
{
    /// <summary>맵 가장자리 담.</summary>
    Boundary,
    /// <summary>대숲 — 키 큰 덩어리. 막힌다.</summary>
    Bamboo,
    /// <summary>바위 · 언덕.</summary>
    Rock,
    /// <summary>헛간 · 방앗간 같은 건물.</summary>
    Building,
    /// <summary>수풀 — 낮다. 막힌다.</summary>
    Bush,
    /// <summary>고목 — 장의 표지.</summary>
    Tree,
    /// <summary>물 — 무릎까지 오는 냇물. 막히지 않는다(보이기만).</summary>
    Water,
    /// <summary>울타리 · 담.</summary>
    Fence,
}

/// <summary>맵 위 자리 표시 — 런타임이 보스 · 조각 · 길목 등을 여기 놓는다 (결정 2-86).</summary>
public enum MapAnchorKind
{
    /// <summary>이 구역으로 들어오면 서는 자리.</summary>
    Start,
    Extraction,
    Boss,
    /// <summary>기억의 조각 · 방.</summary>
    Pickup,
    /// <summary>0-1 젖은 봇짐.</summary>
    Bundle,
    BerryTree,
    /// <summary>0-2 큰 요괴.</summary>
    Gift,
}

/// <summary>덩어리 하나 — 바닥(y = 0) 위에 놓인 상자. Center는 바닥 위 x · z, 높이는 Size.y.</summary>
public readonly struct MapBlock
{
    public readonly MapBlockKind Kind;
    public readonly Vector2 Center;
    public readonly Vector3 Size;

    public MapBlock(MapBlockKind kind, float x, float z, float sizeX, float height, float sizeZ)
    {
        Kind = kind;
        Center = new Vector2(x, z);
        Size = new Vector3(sizeX, height, sizeZ);
    }

    public bool Solid => Kind != MapBlockKind.Water;

    /// <summary>바닥(XZ)에서 이 점이 덩어리 안인가 — 반지름만큼 넓혀 본다.</summary>
    public bool Contains(Vector3 p, float radius = 0f)
        => Mathf.Abs(p.x - Center.x) <= Size.x * 0.5f + radius
           && Mathf.Abs(p.z - Center.y) <= Size.z * 0.5f + radius;
}

public readonly struct MapAnchor
{
    public readonly MapAnchorKind Kind;
    public readonly string ZoneId;
    public readonly Vector3 Position;

    public MapAnchor(MapAnchorKind kind, string zoneId, float x, float z)
    {
        Kind = kind;
        ZoneId = zoneId;
        Position = new Vector3(x, 0f, z);
    }
}

/// <summary>구역 하나의 땅 — 맵 안의 직사각형.</summary>
public readonly struct ZoneArea
{
    public readonly string ZoneId;
    public readonly Rect Bounds;   // x · z

    public ZoneArea(string zoneId, float xMin, float zMin, float xMax, float zMax)
    {
        ZoneId = zoneId;
        Bounds = Rect.MinMaxRect(xMin, zMin, xMax, zMax);
    }

    public bool Contains(Vector3 p) => Bounds.Contains(new Vector2(p.x, p.z));
}

/// <summary>금줄 — 아직 열리지 않은 구역으로 가는 길을 막는다. 그 구역이 열리면 걷힌다.</summary>
public readonly struct MapGate
{
    public readonly string ZoneId;
    public readonly MapBlock Block;

    public MapGate(string zoneId, float x, float z, float sizeX, float sizeZ)
    {
        ZoneId = zoneId;
        Block = new MapBlock(MapBlockKind.Fence, x, z, sizeX, 3f, sizeZ);
    }
}

/// <summary>장 하나의 맵 (장 하나 = 맵 하나 · 구역은 그 안의 땅 — Story 2절 · 결정 2-86).</summary>
public sealed class ChapterMap
{
    public readonly int Chapter;
    public readonly string SceneName;
    /// <summary>바닥의 반 폭 (x, z). 바닥은 원점 가운데.</summary>
    public readonly Vector2 HalfSize;
    public readonly MapBlock[] Blocks;
    public readonly ZoneArea[] Areas;
    public readonly MapAnchor[] Anchors;
    public readonly MapGate[] Gates;

    public ChapterMap(int chapter, string sceneName, Vector2 halfSize,
        MapBlock[] blocks, ZoneArea[] areas, MapAnchor[] anchors, MapGate[] gates)
    {
        Chapter = chapter;
        SceneName = sceneName;
        HalfSize = halfSize;
        Blocks = blocks;
        Areas = areas;
        Anchors = anchors;
        Gates = gates;
    }

    public bool InBounds(Vector3 p, float margin = 0f)
        => Mathf.Abs(p.x) <= HalfSize.x - margin && Mathf.Abs(p.z) <= HalfSize.y - margin;

    /// <summary>막힌 덩어리 안인가 (반지름만큼 넓혀).</summary>
    public bool IsBlocked(Vector3 p, float radius = 0f)
    {
        foreach (MapBlock b in Blocks)
            if (b.Solid && b.Contains(p, radius))
                return true;

        return false;
    }

    public ZoneArea? AreaOf(string zoneId)
    {
        foreach (ZoneArea a in Areas)
            if (a.ZoneId == zoneId)
                return a;

        return null;
    }

    /// <summary>이 점이 속한 구역. 어느 구역에도 없으면 null.</summary>
    public string ZoneAt(Vector3 p)
    {
        foreach (ZoneArea a in Areas)
            if (a.Contains(p))
                return a.ZoneId;

        return null;
    }

    public List<MapAnchor> AnchorsOf(MapAnchorKind kind, string zoneId)
    {
        var list = new List<MapAnchor>();

        foreach (MapAnchor a in Anchors)
            if (a.Kind == kind && a.ZoneId == zoneId)
                list.Add(a);

        return list;
    }

    /// <summary>
    /// 적이 나와도 되는 자리인가 — 바닥 안 · 덩어리 밖 · 열린 구역의 땅.
    /// 닫힌 구역(금줄 너머)에서는 나오지 않는다.
    /// </summary>
    public bool CanStand(Vector3 p, System.Func<string, bool> isOpen, float radius = 0.8f)
    {
        if (!InBounds(p, 2f) || IsBlocked(p, radius))
            return false;

        string zone = ZoneAt(p);
        return zone != null && (isOpen == null || isOpen(zone));
    }
}

/// <summary>
/// 【그레이박스 맵 표】 (결정 2-86) — 장마다 덩어리 · 구역 땅 · 자리 · 금줄을 적는다.
/// 「Dokkaebi/Map/장 맵 굽기」가 이 표로 씬을 만든다(SampleScene을 복사해 바닥만 바꾸고 덩어리를 얹는다).
/// 런타임(ZoneMap)은 같은 표에서 출발 자리 · 길목 · 조각 · 열매 나무 자리를 읽는다.
/// 모양은 단색 덩어리 — 아트가 들어오면 장면만 갈아 끼운다. 자리는 표를 고치면 바뀐다. 수치는 [임시값].
/// </summary>
public static class ZoneMapTable
{
    public const string ScenePrefix = "Chapter";

    public static string SceneFor(int chapter) => $"{ScenePrefix}{chapter}";

    public static bool IsMapScene(string sceneName)
        => !string.IsNullOrEmpty(sceneName) && sceneName.StartsWith(ScenePrefix) && Of(ChapterOfScene(sceneName)) != null;

    private static int ChapterOfScene(string sceneName)
        => int.TryParse(sceneName.Substring(ScenePrefix.Length), out int n) ? n : -1;

    public static ChapterMap Of(int chapter)
    {
        foreach (ChapterMap m in maps)
            if (m.Chapter == chapter)
                return m;

        return null;
    }

    public static IReadOnlyList<ChapterMap> All => maps;

    private static MapBlock B(MapBlockKind k, float x, float z, float sx, float h, float sz) => new(k, x, z, sx, h, sz);
    private static MapAnchor A(MapAnchorKind k, string zone, float x, float z) => new(k, zone, x, z);

    // ── 0장 — 대숲 밤길(0-1) · 고목 뒤 수풀(0-2) ─────────────────────────
    //
    // 본문 0-1: 헛간 뒤 대숲으로 → 길가의 젖은 봇짐 → 잡귀 셋 → 언덕 · 무릎까지 오는 강물 · 불 꺼진 방앗간 → 늙은 고목.
    // 본문 0-2: 고목 뒤편 수풀 — 큰 요괴가 불을 싣는 구슬을 떨어뜨린다.
    // 서쪽 끝 헛간에서 동쪽 고목까지가 0-1, 고목 너머(동쪽)가 0-2. 둘 사이는 고목 양옆 길 두 갈래(금줄).
    private static readonly ChapterMap chapter0 = new(
        0, SceneFor(0), new Vector2(80f, 45f),
        new[]
        {
            // 가장자리 담
            B(MapBlockKind.Boundary, 0f, 46f, 162f, 4f, 2f),
            B(MapBlockKind.Boundary, 0f, -46f, 162f, 4f, 2f),
            B(MapBlockKind.Boundary, -81f, 0f, 2f, 4f, 94f),
            B(MapBlockKind.Boundary, 81f, 0f, 2f, 4f, 94f),

            // 0-1 — 헛간(출발 뒤) · 대숲 · 언덕 · 냇물 · 방앗간
            B(MapBlockKind.Building, -73f, -40f, 12f, 5f, 6f),        // 헛간
            B(MapBlockKind.Bamboo, -62f, -6f, 14f, 6f, 26f),          // 첫 대숲 — 길은 그 아래로 동쪽
            B(MapBlockKind.Bamboo, -72f, 26f, 14f, 6f, 30f),
            B(MapBlockKind.Bamboo, -46f, 30f, 10f, 6f, 20f),
            B(MapBlockKind.Bamboo, -40f, -32f, 12f, 6f, 10f),
            B(MapBlockKind.Rock, -36f, 6f, 8f, 3f, 10f),              // 언덕
            B(MapBlockKind.Water, -21f, 0f, 6f, 0.08f, 90f),          // 무릎까지 오는 냇물(막히지 않는다)
            B(MapBlockKind.Building, -6f, 24f, 8f, 5f, 8f),           // 불 꺼진 방앗간
            B(MapBlockKind.Bamboo, -8f, -24f, 10f, 6f, 14f),          // 강 건너 대숲
            B(MapBlockKind.Bamboo, 4f, 36f, 10f, 6f, 16f),

            // 고목과 그 양옆 울타리 — 길은 두 갈래(금줄 자리)
            B(MapBlockKind.Tree, 20f, 0f, 12f, 12f, 12f),
            B(MapBlockKind.Fence, 20f, -30.5f, 2f, 3f, 29f),          // z -45 ~ -16
            B(MapBlockKind.Fence, 20f, 30.5f, 2f, 3f, 29f),           // z 16 ~ 45

            // 0-2 — 고목 뒤 수풀
            B(MapBlockKind.Bush, 40f, -25f, 10f, 1.4f, 6f),
            B(MapBlockKind.Bush, 50f, 25f, 8f, 1.4f, 10f),
            B(MapBlockKind.Bush, 66f, -10f, 6f, 1.4f, 12f),
            B(MapBlockKind.Bush, 46f, 4f, 6f, 1.4f, 6f),
            B(MapBlockKind.Rock, 58f, -34f, 8f, 3f, 6f),
            B(MapBlockKind.Bamboo, 72f, 14f, 8f, 6f, 8f),
        },
        new[]
        {
            new ZoneArea("0-1", -80f, -45f, 19f, 45f),
            new ZoneArea("0-2", 21f, -45f, 80f, 45f),
        },
        new[]
        {
            A(MapAnchorKind.Start, "0-1", -72f, -30f),
            A(MapAnchorKind.Bundle, "0-1", -66f, -30f),
            A(MapAnchorKind.Extraction, "0-1", 8f, -36f),
            A(MapAnchorKind.Extraction, "0-1", 10f, 18f),
            A(MapAnchorKind.Pickup, "0-1", -48f, 12f),
            A(MapAnchorKind.Pickup, "0-1", -14f, -36f),
            A(MapAnchorKind.BerryTree, "0-1", -56f, 22f),
            A(MapAnchorKind.BerryTree, "0-1", -28f, -38f),
            A(MapAnchorKind.BerryTree, "0-1", -4f, -6f),

            A(MapAnchorKind.Start, "0-2", 32f, 0f),
            A(MapAnchorKind.Gift, "0-2", 60f, 12f),
            A(MapAnchorKind.Extraction, "0-2", 74f, -38f),
            A(MapAnchorKind.Extraction, "0-2", 74f, 36f),
            A(MapAnchorKind.Pickup, "0-2", 52f, -30f),
            A(MapAnchorKind.Pickup, "0-2", 38f, 32f),
            A(MapAnchorKind.BerryTree, "0-2", 60f, -22f),
            A(MapAnchorKind.BerryTree, "0-2", 70f, 2f),
            A(MapAnchorKind.BerryTree, "0-2", 36f, -38f),
        },
        new[]
        {
            // 고목 양옆 두 갈래 — 0-2가 열리기 전에는 금줄이 막는다.
            new MapGate("0-2", 20f, -11f, 2f, 10f),
            new MapGate("0-2", 20f, 11f, 2f, 10f),
        });

    private static readonly ChapterMap[] maps = { chapter0 };
}
