using System.Collections.Generic;
using UnityEngine;
using K = MapBlockKind;
using AK = MapAnchorKind;

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
    /// <summary>좌판 · 아궁이 · 돌 탁자 — 허리 높이. 막힌다.</summary>
    Stall,
    /// <summary>우물 — 둥근 돌 테두리.</summary>
    Well,
    /// <summary>장승 · 기둥 · 화로 · 물레방아 — 가는 덩어리. 길게 늘이면 장승 담장.</summary>
    Post,
    /// <summary>꽃밭 — 발목 높이. 막히지 않는다(보이기만).</summary>
    Flowerbed,
    /// <summary>장 끝의 금줄 — 그 너머는 끊긴 밤길(들어갈 수 없다). 구역 사이의 금줄(MapGate)과 다르다.</summary>
    Geumjul,
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
    /// <summary>기억의 조각 — 본문이 정한 자리(주막 부뚜막 밑 · 포목전 기둥 …). 방(榜)은 Pickup에 놓는다.</summary>
    Piece,
    /// <summary>이야기 표지 — 서브 퀘스트 · 환경 연출 자리(처마 · 절구 · 아궁이 · 화로 …). 지금은 씬 표시만 — 레이어 · 아트 작업 때 쓴다.</summary>
    Landmark,
    /// <summary>역행 비밀 통로 입구 — 이전 장으로 간다 (결정 2-52 · 2-88). 장마다 하나(1 ~ 6장). 그 구역을 끝낸 뒤에만 나타난다.</summary>
    Secret,
    /// <summary>비밀 통로로 오면 서는 자리 — 다음 장의 통로가 여기로 온다. 장마다 하나(0 ~ 5장).</summary>
    SecretExit,
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

    public bool Solid => Kind != MapBlockKind.Water && Kind != MapBlockKind.Flowerbed;

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
    /// <summary>이야기 속 자리 이름 (조각 · 표지). 없으면 null.</summary>
    public readonly string Label;

    public MapAnchor(MapAnchorKind kind, string zoneId, float x, float z, string label = null)
    {
        Kind = kind;
        ZoneId = zoneId;
        Position = new Vector3(x, 0f, z);
        Label = label;
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

    public static int ChapterOfScene(string sceneName)
        => int.TryParse(sceneName.Substring(ScenePrefix.Length), out int n) ? n : -1;

    public static ChapterMap Of(int chapter)
    {
        foreach (ChapterMap m in maps)
            if (m.Chapter == chapter)
                return m;

        return null;
    }

    public static IReadOnlyList<ChapterMap> All => maps;

    /// <summary>그 장의 맵에서 첫 번째 그 종류 자리 (구역 무관). 없으면 null.</summary>
    public static MapAnchor? FirstOf(int chapter, MapAnchorKind kind)
    {
        ChapterMap map = Of(chapter);
        if (map == null)
            return null;

        foreach (MapAnchor a in map.Anchors)
            if (a.Kind == kind)
                return a;

        return null;
    }

    private static MapBlock B(MapBlockKind k, float x, float z, float sx, float h, float sz) => new(k, x, z, sx, h, sz);
    private static MapAnchor A(MapAnchorKind k, string zone, float x, float z, string label = null) => new(k, zone, x, z, label);
    private static MapAnchor L(string zone, float x, float z, string label) => new(MapAnchorKind.Landmark, zone, x, z, label);
    private static MapGate G(string zone, float x, float z, float sx, float sz) => new(zone, x, z, sx, sz);
    private static ZoneArea Z(string zone, float xMin, float zMin, float xMax, float zMax) => new(zone, xMin, zMin, xMax, zMax);

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
            A(MapAnchorKind.SecretExit, "0-1", -21f, -20f, "무릎 냇물 — 1장 우물에서"),

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

    // ── 1장 — 비 오는 폐장터 (젖은 장터길 · 북 · 현무) ─────────────────────
    //
    // 본문 1-1: 북쪽 밤길 끝 옛 장터 — 무너진 처마 · 좌판(이 빠진 사발 · 짝 잃은 짚신) · 빗물 고인 바닥 · 잡귀.
    //           옛 주막 부뚜막 밑에서 조각 하나. 어귀를 빠져나가려는데 야광귀가 봇짐을 훔쳐 좌판 지붕 위로 →
    //           좌판의 헌 체를 그것이 지나가는 길목 처마에 건다.
    // 본문 1-2: 골목골목 붉은 팥 · 쿵쿵 발소리 · 장터 깊숙이 무너진 포목전 앞 달걀귀신 → 그 뒤 포목전 기둥에 조각 둘.
    //           장터 끝 우물 너머로 금줄이 보인다 — 북쪽 밤길은 우물 곁을 지나 금줄까지.
    // 본문 1-3: 우물 위 현무 · 등 너머로 길을 가로지르는 금줄 · 얼어붙는 빗방울.
    // 고목(남쪽)에서 들어와 북쪽으로: 1-1 장터 어귀(남) → 1-2 골목(가운데) → 1-3 우물(북).
    // 구역 사이는 장터 뒷줄 건물 · 그 사이 길목(금줄).
    private static readonly ChapterMap chapter1 = new(
        1, SceneFor(1), new Vector2(55f, 85f),
        new[]
        {
            B(K.Boundary, 0, 86, 112, 4, 2), B(K.Boundary, 0, -86, 112, 4, 2),
            B(K.Boundary, -56, 0, 2, 4, 174), B(K.Boundary, 56, 0, 2, 4, 174),

            // 1-1 장터 어귀 — 큰길 양옆 좌판 두 줄(사이로 빠져나갈 틈)
            B(K.Stall, -9, -64, 4, 1.2f, 6), B(K.Stall, -9, -55, 4, 1.2f, 6), B(K.Stall, -9, -46, 4, 1.2f, 6), B(K.Stall, -9, -37, 4, 1.2f, 6),
            B(K.Stall, 9, -64, 4, 1.2f, 6), B(K.Stall, 9, -55, 4, 1.2f, 6), B(K.Stall, 9, -46, 4, 1.2f, 6), B(K.Stall, 9, -37, 4, 1.2f, 6),
            // 옛 주막 — 동쪽(큰길 쪽)으로 문, 안에 부뚜막
            B(K.Building, -36, -44.5f, 16, 4, 1), B(K.Building, -36, -55.5f, 16, 4, 1), B(K.Building, -43.5f, -50, 1, 4, 10),
            B(K.Building, -28.5f, -53, 1, 4, 4), B(K.Building, -28.5f, -46, 1, 4, 2),
            B(K.Stall, -40, -53, 4, 1, 2),
            // 무너진 처마들 · 빗물 웅덩이
            B(K.Building, 32, -62, 14, 4, 10), B(K.Building, 36, -40, 12, 4, 10), B(K.Building, -36, -70, 10, 4, 6), B(K.Building, 26, -78, 8, 4, 6),
            B(K.Water, 0, -70, 6, 0.05f, 4), B(K.Water, -20, -60, 5, 0.05f, 6), B(K.Water, 20, -48, 4, 0.05f, 4),
            // 1-1 | 1-2 — 장터 뒷줄. 길목 둘(서쪽 샛길 · 큰길 처마)
            B(K.Building, -44.5f, -25, 21, 4, 6), B(K.Building, -16, -25, 24, 4, 6), B(K.Building, 29.5f, -25, 51, 4, 6),

            // 1-2 장터 깊은 곳 — 가게 덩어리 사이 골목(팥). 가운데 북쪽에 무너진 포목전과 그 앞 빈터
            B(K.Building, -40, -11, 14, 4, 12), B(K.Building, -20, -11, 14, 4, 12), B(K.Building, 0, -11, 14, 4, 12),
            B(K.Building, 20, -11, 14, 4, 12), B(K.Building, 42, -12, 10, 4, 10),
            B(K.Building, -40, 8, 14, 4, 12), B(K.Building, -22, 8, 12, 4, 12), B(K.Building, 0, 8, 14, 4, 12), B(K.Building, 40, 8, 14, 4, 12),
            B(K.Building, -42, 27, 10, 4, 10), B(K.Building, -20, 26, 14, 4, 12), B(K.Building, 0, 26, 14, 4, 12), B(K.Building, 40, 26, 14, 4, 12),
            B(K.Building, 20, 24, 16, 5, 8),                         // 무너진 포목전
            B(K.Post, 13, 18.5f, 1, 3, 1),                           // 포목전 기둥
            // 1-2 | 1-3 — 장터 끝. 우물로 가는 길목 하나
            B(K.Building, -29.5f, 40, 51, 4, 6), B(K.Building, 29.5f, 40, 51, 4, 6),

            // 1-3 우물 — 우물 · 등 너머 금줄(장 끝)
            B(K.Well, 0, 66, 6, 1.5f, 6),
            B(K.Geumjul, 0, 78, 110, 1.6f, 1),
            B(K.Rock, -36, 60, 10, 3, 8), B(K.Rock, 36, 62, 8, 3, 10), B(K.Building, -40, 48, 10, 4, 6),
            B(K.Stall, 22, 48, 6, 1.2f, 3), B(K.Stall, -18, 70, 6, 1.2f, 3),
        },
        new[]
        {
            Z("1-1", -55, -85, 55, -25),
            Z("1-2", -55, -25, 55, 40),
            Z("1-3", -55, 40, 55, 78),                                // 금줄 너머(끊긴 밤길)는 구역이 아니다
        },
        new[]
        {
            A(AK.Start, "1-1", 0, -80),
            A(AK.Extraction, "1-1", -46, -78), A(AK.Extraction, "1-1", 46, -76),
            A(AK.Boss, "1-1", 0, -33),                               // 야광귀 — 길목 처마 앞
            A(AK.Piece, "1-1", -35, -49.5f, "옛 주막 부뚜막 밑"),
            A(AK.Pickup, "1-1", -20, -40), A(AK.Pickup, "1-1", 22, -54),
            A(AK.BerryTree, "1-1", -48, -34), A(AK.BerryTree, "1-1", 46, -30), A(AK.BerryTree, "1-1", -24, -80),
            L("1-1", 13, -46, "헌 체가 걸린 좌판"),
            L("1-1", 0, -29.5f, "길목 처마 — 체를 거는 자리 (야광귀)"),

            A(AK.Start, "1-2", 0, -20),
            A(AK.Extraction, "1-2", -51, 20), A(AK.Extraction, "1-2", 51, 26),
            A(AK.Boss, "1-2", 20, 11),                               // 달걀귀신 — 포목전 앞
            A(AK.Piece, "1-2", 15.5f, 16.5f, "무너진 포목전 기둥"),
            A(AK.Pickup, "1-2", -30, 17), A(AK.Pickup, "1-2", 30, -2),
            A(AK.BerryTree, "1-2", -51, -14), A(AK.BerryTree, "1-2", 51, -14), A(AK.BerryTree, "1-2", -10, 35),
            L("1-2", -30, -1, "붉은 팥 뿌린 골목"),

            A(AK.Start, "1-3", 0, 46),
            A(AK.Extraction, "1-3", -46, 70), A(AK.Extraction, "1-3", 46, 72),
            A(AK.Boss, "1-3", 0, 57),                                // 현무 — 우물 앞
            A(AK.Pickup, "1-3", -24, 52), A(AK.Pickup, "1-3", 26, 70),
            A(AK.BerryTree, "1-3", -48, 58), A(AK.BerryTree, "1-3", 48, 50), A(AK.BerryTree, "1-3", 12, 74),
            L("1-3", 0, 75, "현무 등 너머 금줄 — 북쪽 밤길 끝"),
            A(AK.Secret, "1-3", -4.5f, 62.5f, "우물 — 물이 땅 밑으로 흘러간다"),
            A(AK.SecretExit, "1-3", 6, 70, "우물 곁 — 2장 상류 강바닥에서"),
        },
        new[]
        {
            G("1-2", -31, -25, 6, 2), G("1-2", 0, -25, 8, 2),
            G("1-3", 0, 40, 8, 2),
        });

    // ── 2장 — 대숲과 물레방아 (물레방아길 · 동 · 청룡) ─────────────────────
    //
    // 본문 2-1: 동쪽 대숲 · 사당 터로 가는 길은 대나무가 빽빽(어둑시니) → 대숲 깊은 곳 오래된 사당 터에서 조각 셋.
    //           대숲을 가르는 강가에 무너져 가는 방앗간 · 멈춘 물레방아 · 방앗간지기.
    // 본문 2-2: 마른 강바닥을 건너 언덕으로 — 언덕 안쪽으로 옮겨진 금줄 · 그 너머 잡귀가 갉는 헛간(어린 도깨비가 깨어난 곳)
    //           · 금줄 바로 너머에 반쯤 걸친 절구 · 헛간 쪽에서 날아온 조각 넷.
    // 본문 2-3: 마른 강바닥을 거슬러 — 물레방아를 지나 한참 위, 바위와 통나무가 무너져 쌓인 자리에 강철이.
    // 본문 2-4: 물레방아 소리를 뒤로하고 동쪽 밤길 끝 — 대숲 끝 금줄 앞에 청룡.
    // 고목(서쪽)에서 들어와 동쪽으로. 강은 북(상류)에서 남으로 맵을 가른다.
    //   2-1 = 강 서쪽 대숲(방앗간 · 사당 터) · 2-2 = 강 건너 남동쪽 언덕 · 2-3 = 북쪽 상류 · 2-4 = 동쪽 끝 대숲.
    //   강 동쪽 둑은 바위 — 마른 강바닥을 건너는 자리 하나(2-2 금줄). 상류는 강바닥 길목(2-3 금줄). 동쪽 끝은 언덕 길에서(2-4 금줄).
    private static readonly ChapterMap chapter2 = new(
        2, SceneFor(2), new Vector2(95f, 70f),
        new[]
        {
            B(K.Boundary, 0, 71, 192, 4, 2), B(K.Boundary, 0, -71, 192, 4, 2),
            B(K.Boundary, -96, 0, 2, 4, 144), B(K.Boundary, 96, 0, 2, 4, 144),

            B(K.Water, -8, 0, 8, 0.06f, 140),                        // 강(지금은 마른 강바닥) — 북에서 남으로
            B(K.Rock, -2, -41, 4, 4, 58), B(K.Rock, -2, 13, 4, 4, 34), // 강 동쪽 둑 — 건너는 자리 z -12 ~ -4
            B(K.Bamboo, -53.5f, 30, 83, 6, 6), B(K.Bamboo, 18, 30, 44, 6, 6),   // 2-1 · 2-2 | 2-3 — 빽빽한 대숲(강바닥만 열림)
            B(K.Bamboo, 40, -37, 6, 6, 66), B(K.Bamboo, 40, 37, 6, 6, 66),      // | 2-4 — 대숲(언덕 길만 열림)

            // 2-1 대숲 — 서쪽 어귀에서 방앗간까지 밤길 · 남쪽으로 사당 터 가는 좁은 길
            B(K.Bamboo, -80, 18, 14, 6, 14), B(K.Bamboo, -58, 16, 14, 6, 16), B(K.Bamboo, -36, 19, 12, 6, 10),
            B(K.Bamboo, -70, -14, 16, 6, 10), B(K.Bamboo, -44, -14, 14, 6, 12),
            B(K.Bamboo, -80, -42, 26, 6, 30), B(K.Bamboo, -46, -44, 14, 6, 26), B(K.Bamboo, -24, -40, 12, 6, 24),
            B(K.Building, -60, -64, 8, 4, 5),                        // 오래된 사당 터
            B(K.Building, -20, 8, 10, 5, 10),                        // 무너져 가는 방앗간
            B(K.Post, -10, 8, 3, 4, 6),                              // 멈춘 물레방아(강 위)

            // 2-2 언덕 너머 — 언덕 바위 · 안쪽으로 옮겨진 금줄 · 그 너머 헛간(들어갈 수 없다)
            B(K.Rock, 18, -22, 12, 3, 8), B(K.Rock, 10, 14, 8, 3, 8), B(K.Rock, 28, 16, 8, 3, 10), B(K.Rock, 28, -32, 6, 3, 6),
            B(K.Geumjul, 18.5f, -44, 37, 1.6f, 1),
            B(K.Building, 18, -58, 12, 5, 8),                        // 갉히는 헛간

            // 2-3 강 상류 — 강을 막은 바위 · 통나무
            B(K.Rock, -8, 62, 20, 4, 6),
            B(K.Bamboo, -60, 52, 30, 6, 12), B(K.Bamboo, -30, 44, 10, 6, 8), B(K.Bamboo, 22, 52, 14, 6, 14), B(K.Rock, -86, 44, 10, 3, 8),

            // 2-4 대숲 끝 — 금줄(장 끝)
            B(K.Geumjul, 88, 0, 1, 1.6f, 140),
            B(K.Bamboo, 60, 30, 20, 6, 30), B(K.Bamboo, 62, -32, 22, 6, 28), B(K.Bamboo, 80, 40, 10, 6, 20), B(K.Bamboo, 80, -44, 8, 6, 20),
        },
        new[]
        {
            Z("2-1", -95, -70, -4, 30),
            Z("2-2", -4, -44, 40, 30),                                // 옮겨진 금줄 너머(헛간)는 구역이 아니다
            Z("2-3", -95, 30, 40, 70),
            Z("2-4", 40, -70, 88, 70),
        },
        new[]
        {
            A(AK.Start, "2-1", -88, 0),
            A(AK.Extraction, "2-1", -88, -64), A(AK.Extraction, "2-1", -30, -64),
            A(AK.Boss, "2-1", -60, -40),                             // 어둑시니 — 사당 터 가는 길
            A(AK.Piece, "2-1", -60, -57, "대숲 깊은 곳 오래된 사당 터"),
            A(AK.Pickup, "2-1", -58, -4), A(AK.Pickup, "2-1", -30, -4),
            A(AK.BerryTree, "2-1", -88, -20), A(AK.BerryTree, "2-1", -48, 2), A(AK.BerryTree, "2-1", -22, -20),
            L("2-1", -20, 0.5f, "방앗간 — 방앗간지기 · 멈춘 물레방아"),
            L("2-1", -60, -24, "사당 터로 가는 길 — 대나무가 빽빽하다"),
            A(AK.SecretExit, "2-1", -70, 2, "대숲 — 3장 약방골에서"),

            A(AK.Start, "2-2", 4, -8),
            A(AK.Extraction, "2-2", 16, 23), A(AK.Extraction, "2-2", 32, -38),
            A(AK.Piece, "2-2", 24, -41, "언덕 위 — 갉히는 헛간에서 날아온 종잇장"),
            A(AK.Pickup, "2-2", 8, 24), A(AK.Pickup, "2-2", 14, -32),
            A(AK.BerryTree, "2-2", 2, 22), A(AK.BerryTree, "2-2", 34, -14), A(AK.BerryTree, "2-2", 6, -30),
            L("2-2", 14, -41, "금줄에 반쯤 걸친 절구"),
            L("2-2", 6, -41, "옮겨진 금줄 — 너머에서 잡귀가 헛간을 갉는다"),

            A(AK.Start, "2-3", -8, 36),
            A(AK.Extraction, "2-3", -80, 60), A(AK.Extraction, "2-3", 30, 64),
            A(AK.Boss, "2-3", -8, 52),                               // 강철이 — 막힌 강 앞에 똬리
            A(AK.Pickup, "2-3", -40, 60), A(AK.Pickup, "2-3", 14, 38),
            A(AK.BerryTree, "2-3", -60, 38), A(AK.BerryTree, "2-3", -30, 64), A(AK.BerryTree, "2-3", 10, 58),
            L("2-3", -8, 57, "바위와 통나무가 무너져 쌓인 자리 — 막힌 강물"),
            A(AK.Secret, "2-3", -8, 67.5f, "막힌 자리 너머 상류 강바닥"),

            A(AK.Start, "2-4", 46, 0),
            A(AK.Extraction, "2-4", 50, 60), A(AK.Extraction, "2-4", 50, -62),
            A(AK.Boss, "2-4", 78, 0),                                // 청룡 — 금줄 앞에 내려앉는다
            A(AK.Pickup, "2-4", 70, 6), A(AK.Pickup, "2-4", 60, -8),
            A(AK.BerryTree, "2-4", 47, 30), A(AK.BerryTree, "2-4", 84, 16), A(AK.BerryTree, "2-4", 47, -30),
            L("2-4", 84, 6, "대숲 끝 금줄 — 동쪽 밤길 끝"),
        },
        new[]
        {
            G("2-2", -2, -8, 4, 8),
            G("2-3", -8, 30, 8, 2),
            G("2-4", 40, 0, 2, 8),
        });

    // ── 3장 — 약방골과 주작의 가마 (불씨길 · 남 · 주작) ───────────────────
    //
    // 본문 3-1: 약재 말리는 냄새 · 골목마다 멍석 · 먼지 앉은 약봉지 선반(조각 다섯) · 골짜기 끝에 주작의 가마.
    //           약방골 한가운데 커다란 부엌 — 부뚜막 · 꺼져 가는 아궁이 · 늙은 요괴(총명탕).
    // 본문 3-2: 불씨 골목의 아궁이 여럿 — 한 아궁이 앞 처녀귀신 · 건너편 골목 아궁이 앞 몽달귀신(둘이 함께) →
    //           마지막 아궁이 앞 두억시니 → 부엌으로 돌아가는 골목 끝 약방 처마 밑 새색시(1회 이벤트) →
    //           부엌에서 부뚜막 밑 서랍의 조각 여섯(구역은 3-2 · 자리는 3-1 부엌).
    // 본문 3-3: 골짜기 끝 가마 · 불길 속 주작 · 가마 재 속 조각 일곱 · 남쪽 밤길은 가마 뒤로 이어져 금줄에 닿는다.
    // 고목(북쪽)에서 들어와 남쪽으로: 3-1 약방골(북) → 3-2 불씨 골목(가운데) → 3-3 골짜기 끝 가마(남).
    private static readonly ChapterMap chapter3 = new(
        3, SceneFor(3), new Vector2(60f, 85f),
        new[]
        {
            B(K.Boundary, 0, 86, 122, 4, 2), B(K.Boundary, 0, -86, 122, 4, 2),
            B(K.Boundary, -61, 0, 2, 4, 174), B(K.Boundary, 61, 0, 2, 4, 174),

            // 3-1 약방골 — 약방들 · 한가운데 부엌(북쪽으로 문, 안에 부뚜막)
            B(K.Building, -34, 64, 16, 4, 10), B(K.Building, 34, 66, 16, 4, 10),
            B(K.Building, -36, 34, 14, 4, 10), B(K.Building, 36, 34, 14, 4, 10), B(K.Building, -12, 72, 8, 4, 6),
            B(K.Building, -5.5f, 51.5f, 7, 4, 1), B(K.Building, 5.5f, 51.5f, 7, 4, 1), B(K.Building, 0, 40.5f, 18, 4, 1),
            B(K.Building, -8.5f, 46, 1, 4, 10), B(K.Building, 8.5f, 46, 1, 4, 10),
            B(K.Stall, -5, 48.5f, 5, 1, 2),                          // 부뚜막 · 아궁이
            // 3-1 | 3-2 — 약방 뒷줄. 골목 어귀 둘
            B(K.Building, -45, 20, 30, 4, 6), B(K.Building, -14, 20, 20, 4, 6), B(K.Building, 32, 20, 56, 4, 6),

            // 3-2 불씨 골목 — 두 줄 사이 넓은 골목, 아궁이들
            B(K.Building, -40, 7, 20, 4, 10), B(K.Building, -14, 7, 16, 4, 10), B(K.Building, 14, 7, 16, 4, 10), B(K.Building, 40, 7, 20, 4, 10),
            B(K.Building, -40, -19, 20, 4, 10), B(K.Building, -14, -19, 16, 4, 10), B(K.Building, 14, -19, 16, 4, 10), B(K.Building, 40, -19, 20, 4, 10),
            B(K.Stall, -14, 1, 3, 1, 2),                             // 아궁이 — 처녀귀신
            B(K.Stall, 14, -13, 3, 1, 2),                            // 건너편 골목 아궁이 — 몽달귀신
            B(K.Stall, -40, 1, 3, 1, 2), B(K.Stall, 40, -13, 3, 1, 2),
            B(K.Stall, -40, -25, 3, 1, 2),                           // 마지막 아궁이 — 두억시니
            // 3-2 | 3-3 — 골짜기가 좁아진다. 가마로 오르는 길 하나
            B(K.Rock, -33, -40, 54, 5, 6), B(K.Rock, 33, -40, 54, 5, 6),

            // 3-3 주작의 가마 — 골짜기 끝 · 가마 · 가마 뒤 남쪽 밤길과 금줄(장 끝)
            B(K.Rock, -40, -60, 40, 6, 34), B(K.Rock, 40, -60, 40, 6, 34),
            B(K.Building, 0, -68, 16, 7, 10),                        // 주작의 가마
            B(K.Geumjul, 0, -81, 120, 1.6f, 1),
        },
        new[]
        {
            Z("3-1", -60, 20, 60, 85),
            Z("3-2", -60, -40, 60, 20),
            Z("3-3", -60, -81, 60, -40),
        },
        new[]
        {
            A(AK.Start, "3-1", 0, 80),
            A(AK.Extraction, "3-1", -52, 46), A(AK.Extraction, "3-1", 52, 48),
            A(AK.Piece, "3-1", -34, 56.5f, "약방 선반 — 먼지 앉은 약봉지 사이"),
            A(AK.Pickup, "3-1", -20, 70), A(AK.Pickup, "3-1", 22, 50),
            A(AK.BerryTree, "3-1", -50, 78), A(AK.BerryTree, "3-1", 50, 80), A(AK.BerryTree, "3-1", -16, 30),
            L("3-1", 4, 46, "약방골 한가운데 부엌 — 늙은 요괴 · 꺼져 가는 아궁이"),
            L("3-1", -18, 58, "멍석에 널린 약재"),
            A(AK.Secret, "3-1", -54, 64, "멍석 골목 끝"),
            A(AK.SecretExit, "3-1", 44, 54, "약방골 — 4장 장승 발치에서"),

            A(AK.Start, "3-2", 0, 14),
            A(AK.Extraction, "3-2", -55, -32), A(AK.Extraction, "3-2", 55, -6),
            A(AK.Boss, "3-2", 0, -6),                                // 처녀귀신 · 몽달귀신 — 골목을 사이에 둔 두 아궁이
            A(AK.Boss, "3-2", -40, -32),                             // 두억시니 — 마지막 아궁이
            A(AK.Piece, "3-2", -5, 44.5f, "부엌 부뚜막 밑 서랍"),    // 자리는 3-1 부엌(본문)
            A(AK.Pickup, "3-2", -26, -6), A(AK.Pickup, "3-2", 26, -30),
            A(AK.BerryTree, "3-2", -55, 8), A(AK.BerryTree, "3-2", 55, 10), A(AK.BerryTree, "3-2", 20, -32),
            L("3-2", -14, -2.5f, "아궁이 — 처녀귀신"),
            L("3-2", 14, -9.5f, "건너편 골목 아궁이 — 몽달귀신"),
            L("3-2", -40, -2.5f, "꺼져 가는 아궁이"),
            L("3-2", 40, -9.5f, "꺼져 가는 아궁이"),
            L("3-2", -36, -29, "마지막 아궁이 — 두억시니"),
            L("3-2", 10, 26, "약방 처마 밑 — 새색시 (1회 이벤트)"), // 부엌으로 돌아가는 골목 끝(3-1 땅)

            A(AK.Start, "3-3", 0, -46),
            A(AK.Extraction, "3-3", -14, -77), A(AK.Extraction, "3-3", 14, -77),
            A(AK.Boss, "3-3", 0, -55),                               // 주작 — 가마 앞
            A(AK.Piece, "3-3", 5, -60, "가마의 재 속"),
            A(AK.Pickup, "3-3", -14, -50), A(AK.Pickup, "3-3", 14, -66),
            A(AK.BerryTree, "3-3", -15, -66), A(AK.BerryTree, "3-3", 15, -50), A(AK.BerryTree, "3-3", 0, -78),
            L("3-3", 0, -77, "가마 뒤 남쪽 밤길 — 금줄"),
        },
        new[]
        {
            G("3-2", -27, 20, 6, 2), G("3-2", 0, 20, 8, 2),
            G("3-3", 0, -40, 12, 2),
        });

    // ── 4장 — 장승 벌판 (장승들길 · 서 · 백호) ────────────────────────────
    //
    // 본문 4-1: 서쪽은 벌판 — 끝없이 늘어선 장승(나무 · 돌) · 벌판 끝 금줄을 장승들이 어깨를 맞대고 붙든다 ·
    //           금줄 너머에서 잡귀 떼(장승 지키기) · 오래된 장승 발치의 조각 여덟 · 벌판 안쪽으로 나무 장승들 사이 두두리.
    //           저 안쪽 높은 바위에서 백호가 지켜본다.
    // 본문 4-2: 벌판 가운데를 가로지르는 가느다란 냇물 · 장승을 긁는 꺼먹살이 → 냇물을 건너면 따라오지 못한다.
    // 본문 4-3: 냇물 너머 벌판 안쪽 — 더 빽빽한 장승 · 장승 담장 사이 좁은 길 하나 · 바위의 백호가 내려와 길 앞을 막는다.
    // 본문 4-4: 좁은 길 끝 장승 하나 — 빗자루에서 난 다른 장승 · 발치의 몽당 빗자루.
    // 고목(동쪽)에서 들어와 서쪽으로: 4-1(동) → 4-2 냇물 앞 → 냇물 건너 4-3 → 북쪽 좁은 길 4-4.
    //   벌판 끝 금줄 = 남쪽 가장자리 전체. 구역 사이는 장승 줄(틈 = 금줄).
    private static readonly ChapterMap chapter4 = new(
        4, SceneFor(4), new Vector2(100f, 65f),
        new[]
        {
            B(K.Boundary, 0, 66, 202, 4, 2), B(K.Boundary, 0, -66, 202, 4, 2),
            B(K.Boundary, -101, 0, 2, 4, 134), B(K.Boundary, 101, 0, 2, 4, 134),
            B(K.Geumjul, 0, -60, 200, 1.6f, 1),                     // 벌판 끝 금줄(장승들이 붙든다)

            // 4-1 | 4-2 — 장승 줄. 틈 둘
            B(K.Post, 30.5f, -54.5f, 1, 2.5f, 21), B(K.Post, 30.5f, -21, 1, 2.5f, 34), B(K.Post, 30.5f, 34.5f, 1, 2.5f, 61),
            // 4-2 | 4-3 — 냇물 서쪽 둑의 빽빽한 장승. 건너는 틈 둘
            B(K.Water, -16, 0, 6, 0.06f, 130),                       // 가느다란 냇물
            B(K.Post, -20.5f, -47.5f, 1, 2.5f, 35), B(K.Post, -20.5f, -10, 1, 2.5f, 28), B(K.Post, -20.5f, 37.5f, 1, 2.5f, 55),

            // 4-1 벌판 어귀 — 늘어선 장승 · 나무 장승들(두두리) · 오래된 장승
            B(K.Post, 80, 22, 1, 2.5f, 24), B(K.Post, 80, -22, 1, 2.5f, 24), B(K.Post, 64, 40, 24, 2.5f, 1), B(K.Post, 64, -40, 24, 2.5f, 1),
            B(K.Post, 40, 28, 1.2f, 2.5f, 1.2f), B(K.Post, 52, 28, 1.2f, 2.5f, 1.2f), B(K.Post, 40, 12, 1.2f, 2.5f, 1.2f),
            B(K.Post, 54, 13, 1.2f, 2.5f, 1.2f), B(K.Post, 46, 30, 1.2f, 2.5f, 1.2f),
            B(K.Post, 70, -32, 1.5f, 2.5f, 1.5f),                    // 오래된 장승
            B(K.Post, 62, 8, 1.2f, 2.5f, 1.2f), B(K.Post, 62, -8, 1.2f, 2.5f, 1.2f), B(K.Post, 72, 18, 1.2f, 2.5f, 1.2f),
            B(K.Post, 72, -14, 1.2f, 2.5f, 1.2f), B(K.Post, 88, -30, 1.2f, 2.5f, 1.2f), B(K.Post, 88, 40, 1.2f, 2.5f, 1.2f),
            B(K.Post, 56, 50, 1.2f, 2.5f, 1.2f), B(K.Post, 76, 54, 1.2f, 2.5f, 1.2f), B(K.Post, 44, -50, 1.2f, 2.5f, 1.2f),
            B(K.Post, 56, -26, 1.2f, 2.5f, 1.2f), B(K.Post, 92, -12, 1.2f, 2.5f, 1.2f),

            // 4-2 냇물 앞 — 꺼먹살이가 긁는 장승들
            B(K.Post, 0, 20, 1.2f, 2.5f, 1.2f), B(K.Post, 10, -30, 1.2f, 2.5f, 1.2f), B(K.Post, 16, 34, 1.2f, 2.5f, 1.2f),
            B(K.Post, 20, -10, 1.2f, 2.5f, 1.2f), B(K.Post, -6, -40, 1.2f, 2.5f, 1.2f),
            B(K.Post, -6, 50, 1.2f, 2.5f, 1.2f), B(K.Post, 24, 20, 1.2f, 2.5f, 1.2f), B(K.Post, 12, 52, 1.2f, 2.5f, 1.2f),
            B(K.Post, -2, -20, 1.2f, 2.5f, 1.2f), B(K.Post, 22, -36, 1.2f, 2.5f, 1.2f),

            // 4-3 벌판 안쪽 — 더 빽빽한 장승 줄 · 높은 바위(백호)
            B(K.Rock, -50, 10, 14, 6, 10),
            B(K.Post, -40, -12, 1, 2.5f, 30), B(K.Post, -62, -28, 1, 2.5f, 40), B(K.Post, -82, 2, 1, 2.5f, 44),
            B(K.Post, -58, 24, 30, 2.5f, 1), B(K.Post, -44, -44, 30, 2.5f, 1),
            B(K.Post, -34, 15, 12, 2.5f, 1), B(K.Post, -90, -20, 1, 2.5f, 20), B(K.Post, -72, -50, 1, 2.5f, 16),

            // 4-4 가장 안쪽 장승 — 장승 담장 사이 좁은 길 · 끝의 빈터
            B(K.Post, -66, 36.5f, 68, 2.5f, 1), B(K.Post, -23, 36.5f, 6, 2.5f, 1),   // 남쪽 담장(어귀 x -32 ~ -26)
            B(K.Post, -45, 44.5f, 50, 2.5f, 1),                                     // 길 북쪽 담장
            B(K.Post, -85, 58.5f, 30, 2.5f, 1), B(K.Post, -70.5f, 51.5f, 1, 2.5f, 13), // 빈터 담장
            B(K.Post, -94, 48, 1.5f, 2.2f, 1.5f),                                   // 빗자루에서 난 다른 장승
            B(K.Boundary, -45, 55, 50, 4, 20), B(K.Boundary, -85, 62, 30, 4, 6),     // 담장 너머 — 들어갈 수 없는 땅을 메운다
        },
        new[]
        {
            Z("4-1", 30, -60, 100, 65),
            Z("4-2", -20, -60, 30, 65),
            Z("4-3", -100, -60, -20, 37),
            Z("4-4", -100, 37, -20, 65),
        },
        new[]
        {
            A(AK.Start, "4-1", 94, 0),
            A(AK.Extraction, "4-1", 60, 55), A(AK.Extraction, "4-1", 60, -52),
            A(AK.Boss, "4-1", 46, 20),                               // 두두리 — 나무 장승들 사이
            A(AK.Piece, "4-1", 70, -29, "오래된 장승 발치"),
            A(AK.Pickup, "4-1", 88, 30), A(AK.Pickup, "4-1", 50, -20),
            A(AK.BerryTree, "4-1", 90, 50), A(AK.BerryTree, "4-1", 90, -45), A(AK.BerryTree, "4-1", 40, 50),
            L("4-1", 34, 20, "나무 장승들 — 두두리가 섞여 선다"),
            L("4-1", 70, -54, "벌판 끝 금줄 — 장승들이 붙든다 · 잡귀 떼가 몰려온다"),
            L("4-1", 88, 22, "늘어선 장승들 — 새겨진 이야기"),
            A(AK.Secret, "4-1", 73, -32, "오래된 장승 뒤"),
            A(AK.SecretExit, "4-1", 90, 10, "벌판 어귀 — 5장 문 그림에서"),

            A(AK.Start, "4-2", 25, 0),
            A(AK.Extraction, "4-2", 0, 52), A(AK.Extraction, "4-2", 2, -52),
            A(AK.Boss, "4-2", 2, -6),                                // 꺼먹살이 — 냇물 앞
            A(AK.Pickup, "4-2", 10, 40), A(AK.Pickup, "4-2", 6, -34),
            A(AK.BerryTree, "4-2", 22, 50), A(AK.BerryTree, "4-2", 22, -50), A(AK.BerryTree, "4-2", -6, 30),
            L("4-2", 2, 22.5f, "꺼먹살이가 긁은 장승"),
            L("4-2", -16, 30, "가느다란 냇물 — 꺼먹살이는 건너지 못한다"),

            A(AK.Start, "4-3", -25, 7),
            A(AK.Extraction, "4-3", -90, -50), A(AK.Extraction, "4-3", -90, 30),
            A(AK.Boss, "4-3", -29, 30),                              // 백호 — 좁은 길 어귀를 막는다
            A(AK.Pickup, "4-3", -70, 15), A(AK.Pickup, "4-3", -48, -36),
            A(AK.BerryTree, "4-3", -92, 14), A(AK.BerryTree, "4-3", -32, -52), A(AK.BerryTree, "4-3", -70, -20),
            L("4-3", -50, 18, "벌판 안쪽 높은 바위 — 백호가 지켜본다"),
            L("4-3", -29, 34.5f, "장승 담장 사이 좁은 길 어귀"),

            A(AK.Start, "4-4", -29, 40.5f),
            A(AK.Extraction, "4-4", -80, 52), A(AK.Extraction, "4-4", -86, 41),
            A(AK.Pickup, "4-4", -50, 40.5f), A(AK.Pickup, "4-4", -88, 55),
            A(AK.BerryTree, "4-4", -76, 40), A(AK.BerryTree, "4-4", -97, 41), A(AK.BerryTree, "4-4", -78, 55),
            L("4-4", -90, 48, "길 끝 장승 — 빗자루에서 난 다른 장승 · 발치의 몽당 빗자루"),
        },
        new[]
        {
            G("4-2", 30.5f, -41, 1, 6), G("4-2", 30.5f, 0, 1, 8),
            G("4-3", -20.5f, -27, 1, 6), G("4-3", -20.5f, 7, 1, 6),
            G("4-4", -29, 36.5f, 6, 1),
        });

    // ── 5장 — 궁궐 문 (돌문 아래 길 · 해태) ─────────────────────────────
    //
    // 본문 5-1: 고목 뿌리 아래 돌문 → 넓은 돌길 · 양옆으로 끝없는 담장 · 바랜 문 그림(처용 · 호랑이 · 용) · 떠도는 지방 ·
    //           담장을 따라 군데군데 화로(지방 보내기) · 돌길은 산허리를 감고 점점 높이 · 산길 모퉁이마다 창귀 →
    //           산허리 위 담장의 바랜 호랑이 그림에서 산군 → 마지막 화로에서 조각 아홉.
    // 본문 5-2: 돌길 끝 오래된 궁궐 문 · 문 앞의 해태 · 해태 곁 돌 탁자 위 두루마리.
    // 본문 5-3: 두루마리를 푼다 → 해태가 궁궐 문을 연다(문 너머는 꽃밭 — 6장).
    // 돌문(남서쪽 아래)에서 들어와 산허리를 갈지자로 감아 오른다: 5-1 돌길(아래 네 굽이) → 5-2 궁궐 문 앞마당 → 5-3 문 앞 단(돌 탁자).
    private static readonly ChapterMap chapter5 = new(
        5, SceneFor(5), new Vector2(60f, 80f),
        new[]
        {
            B(K.Boundary, 0, 81, 122, 4, 2), B(K.Boundary, 0, -81, 122, 4, 2),
            B(K.Boundary, -61, 0, 2, 4, 164), B(K.Boundary, 61, 0, 2, 4, 164),

            B(K.Building, -52, -79, 12, 6, 2),                       // 돌문(뒤)
            // 산허리 — 굽이 사이의 산(담장이 둘러선 돌길)
            B(K.Rock, -8, -53, 104, 8, 14), B(K.Rock, 8, -19, 104, 8, 14), B(K.Rock, -8, 15, 104, 8, 14), B(K.Rock, 8, 45, 104, 8, 6),
            // 담장 화로
            B(K.Post, -30, -61, 1.2f, 1.2f, 1.2f), B(K.Post, 10, -61, 1.2f, 1.2f, 1.2f),
            B(K.Post, -30, -45, 1.2f, 1.2f, 1.2f), B(K.Post, 0, -27, 1.2f, 1.2f, 1.2f),
            B(K.Post, 20, -11, 1.2f, 1.2f, 1.2f), B(K.Post, -20, 7, 1.2f, 1.2f, 1.2f),
            B(K.Post, 24, 41, 1.2f, 1.2f, 1.2f),                     // 마지막 화로

            // 5-2 · 5-3 — 궁궐 문 앞마당 · 문 앞 단(낮은 담 · 가운데 계단)
            B(K.Fence, -32, 64.5f, 56, 2, 1), B(K.Fence, 32, 64.5f, 56, 2, 1),
            B(K.Stall, 12, 70, 4, 1, 2),                             // 돌 탁자
            B(K.Building, 0, 77, 24, 9, 6),                          // 궁궐 문
        },
        new[]
        {
            Z("5-1", -60, -80, 60, 48),
            Z("5-2", -60, 48, 60, 64.5f),
            Z("5-3", -60, 64.5f, 60, 80),
        },
        new[]
        {
            A(AK.Start, "5-1", -52, -72),
            A(AK.Extraction, "5-1", 52, -72), A(AK.Extraction, "5-1", -52, -36),
            A(AK.Boss, "5-1", 52, -53),                              // 창귀 — 산길 모퉁이
            A(AK.Boss, "5-1", 10, 32),                               // 산군 — 산허리 위 호랑이 그림 앞
            A(AK.Piece, "5-1", 24, 38, "마지막 화로 — 지방 사이에 섞인 종이"),
            A(AK.Pickup, "5-1", -20, -36), A(AK.Pickup, "5-1", 30, -2),
            A(AK.BerryTree, "5-1", 56, -30), A(AK.BerryTree, "5-1", -56, -2), A(AK.BerryTree, "5-1", 56, 30),
            L("5-1", -55, -66, "고목 뿌리 아래 돌문 — 네 홈(사신패)"),
            L("5-1", -20, -63, "담장의 바랜 문 그림"),
            L("5-1", -30, -63, "담장 화로"), L("5-1", 10, -63, "담장 화로"), L("5-1", -30, -43, "담장 화로"),
            L("5-1", 0, -29, "담장 화로"), L("5-1", 20, -9, "담장 화로"), L("5-1", -20, 5, "담장 화로"),
            L("5-1", 10, 40, "담장의 바랜 호랑이 그림 — 산군"),
            A(AK.Secret, "5-1", -14, -62, "바랜 문 그림 하나"),
            A(AK.SecretExit, "5-1", -26, -43, "담장 화로 곁 — 6장 꽃밭에서"),

            A(AK.Start, "5-2", -52, 52),
            A(AK.Extraction, "5-2", 50, 52), A(AK.Extraction, "5-2", -20, 59),
            A(AK.Boss, "5-2", 0, 56),                                // 해태 — 궁궐 문 앞 · 돌 탁자 앞을 막는다
            A(AK.Pickup, "5-2", -36, 58), A(AK.Pickup, "5-2", 30, 56),
            A(AK.BerryTree, "5-2", -56, 60), A(AK.BerryTree, "5-2", 56, 58), A(AK.BerryTree, "5-2", 20, 52),

            A(AK.Start, "5-3", 0, 68),
            A(AK.Extraction, "5-3", -40, 70), A(AK.Extraction, "5-3", 40, 70),
            A(AK.Pickup, "5-3", -24, 70), A(AK.Pickup, "5-3", 26, 72),
            A(AK.BerryTree, "5-3", -52, 72), A(AK.BerryTree, "5-3", 52, 72), A(AK.BerryTree, "5-3", -26, 76),
            L("5-3", 12, 67, "해태 곁 돌 탁자 — 두루마리"),
            L("5-3", 0, 72.5f, "궁궐 문 — 문 너머는 꽃밭"),
        },
        new[]
        {
            G("5-2", -52, 45, 16, 2),
            G("5-3", 0, 64.5f, 8, 1),
        });

    // ── 6장 — 결계의 가장자리 (꽃밭 · 구미호) ───────────────────────────
    //
    // 본문 6-1: 궁궐 문 너머 끝없는 꽃밭 · 꽃마다 이름표 · 참봉의 꽃(반쯤 시듦) · 빚쟁이의 꽃(거의 다 짐) ·
    //           한가운데 봉오리도 없는 빈 꽃대 · 시든 꽃에서 빠져나온 혼들이 한쪽으로 흘러간다 · 혼을 따라가다 삼족오.
    // 본문 6-2: 혼들이 흘러간 끝 결계의 가장자리(가장 오래된 금줄) · 희미한 인간 세상 · 저승길 · 구미호.
    // 궁궐 문(남쪽)에서 들어와 북쪽으로: 6-1 꽃밭 → 시든 꽃 덤불 사이 틈(혼이 흘러가는 쪽) → 6-2 가장자리.
    private static readonly ChapterMap chapter6 = new(
        6, SceneFor(6), new Vector2(90f, 70f),
        new[]
        {
            B(K.Boundary, 0, 71, 182, 4, 2), B(K.Boundary, 0, -71, 182, 4, 2),
            B(K.Boundary, -91, 0, 2, 4, 144), B(K.Boundary, 91, 0, 2, 4, 144),

            B(K.Building, 0, -68, 20, 8, 4),                         // 궁궐 문(뒤)
            // 6-1 꽃밭 — 이름표 달린 꽃(막히지 않는다) · 시든 꽃 덤불
            B(K.Flowerbed, -52, -34, 60, 0.3f, 40), B(K.Flowerbed, 50, -36, 56, 0.3f, 36), B(K.Flowerbed, -50, 14, 64, 0.3f, 30),
            B(K.Flowerbed, 50, 14, 64, 0.3f, 30), B(K.Flowerbed, 0, 0, 16, 0.3f, 16), B(K.Flowerbed, 0, -36, 20, 0.3f, 20),
            B(K.Bush, -20, -40, 6, 1.4f, 4), B(K.Bush, 24, -12, 4, 1.4f, 6), B(K.Bush, -60, 0, 6, 1.4f, 6),
            // 6-1 | 6-2 — 시든 꽃 덤불 줄. 혼이 흘러가는 틈 하나
            B(K.Bush, -48, 36, 84, 1.4f, 2), B(K.Bush, 48, 36, 84, 1.4f, 2),

            // 6-2 가장자리 — 가장 오래된 금줄(그 너머 저승길 · 인간 세상)
            B(K.Flowerbed, -60, 50, 50, 0.3f, 14), B(K.Flowerbed, 60, 50, 50, 0.3f, 14),
            B(K.Geumjul, 0, 62, 180, 1.6f, 1),
        },
        new[]
        {
            Z("6-1", -90, -70, 90, 36),
            Z("6-2", -90, 36, 90, 62),
        },
        new[]
        {
            A(AK.Start, "6-1", 0, -60),
            A(AK.Extraction, "6-1", -70, -50), A(AK.Extraction, "6-1", 70, -50),
            A(AK.Boss, "6-1", 0, 18),                                // 삼족오 — 혼을 따라 꽃밭을 가로지르다
            A(AK.Pickup, "6-1", -40, -16), A(AK.Pickup, "6-1", 40, 2),
            A(AK.BerryTree, "6-1", -62, 22), A(AK.BerryTree, "6-1", 62, -18), A(AK.BerryTree, "6-1", -30, -52),
            L("6-1", 0, 0, "꽃밭 한가운데 — 이름표가 빈 꽃대"),
            L("6-1", -10, 8, "참봉의 꽃"),
            L("6-1", 10, 8, "빚쟁이의 꽃"),
            L("6-1", 0, 30, "시든 꽃에서 빠져나온 혼들이 흘러가는 쪽"),
            A(AK.Secret, "6-1", -86, -10, "꽃밭 가장자리 — 반쯤 탄 지방"),

            A(AK.Start, "6-2", 0, 42),
            A(AK.Extraction, "6-2", -50, 52), A(AK.Extraction, "6-2", 50, 52),
            A(AK.Boss, "6-2", 0, 50),                                // 구미호 — 가장자리에서 기다린다
            A(AK.Pickup, "6-2", -30, 46), A(AK.Pickup, "6-2", 30, 56),
            A(AK.BerryTree, "6-2", -70, 44), A(AK.BerryTree, "6-2", 70, 44), A(AK.BerryTree, "6-2", -20, 56),
            L("6-2", 0, 58, "결계의 가장자리 — 가장 오래된 금줄 · 저승길 · 희미한 인간 세상"),
        },
        new[]
        {
            G("6-2", 0, 36, 12, 2),
        });

    private static readonly ChapterMap[] maps = { chapter0, chapter1, chapter2, chapter3, chapter4, chapter5, chapter6 };
}
