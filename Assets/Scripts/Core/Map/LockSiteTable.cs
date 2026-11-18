using System.Collections.Generic;
using UnityEngine;

/// <summary>잠긴 곳의 생김새 (결정 2-143).</summary>
public enum LockSiteKind
{
    /// <summary>【담 두른 마당】 — 사방이 담, 남쪽에 대문. 마당 안에 좋은 상자. 담 한 칸은 금이 가 화약으로 뚫린다.</summary>
    Compound = 0,

    /// <summary>【들어가는 집】 — 벽 · 지붕 · 방문. 안에 들어서면 지붕이 걷혀 방이 보인다.</summary>
    House = 1,

    /// <summary>【열린 집】 (결정 2-149) — 잠기지 않은 큰 집. 그냥 들어가 뒤진다(덕코프: 대부분의 건물은 그냥 들어간다). 안에 상자 2 ~ 3.</summary>
    OpenHouse = 2,
}

/// <summary>잠긴 곳 하나 — 구역마다 마당 하나 · 집 하나. 열쇠는 그 문 하나만 연다.</summary>
public sealed class LockSite
{
    public readonly string Id;
    public readonly string ZoneId;
    public readonly int Chapter;
    public readonly LockSiteKind Kind;
    public readonly Vector2 Center;   // x · z
    public readonly Vector2 Size;     // 폭(x) · 깊이(z)
    public readonly string Name;
    /// <summary>집 그림 (Resources/Locks) — 열린 집만. 잠긴 집은 LockedSite 가 고른다.</summary>
    public readonly string Art;

    public LockSite(string zoneId, int chapter, LockSiteKind kind, Vector2 center, Vector2 size, string name, int index = 0, string art = null)
    {
        ZoneId = zoneId;
        Chapter = chapter;
        Kind = kind;
        Center = center;
        Size = size;
        Name = name;
        Art = art;
        Id = kind switch
        {
            LockSiteKind.Compound => $"{zoneId}_gate",
            LockSiteKind.House => $"{zoneId}_room",
            _ => $"{zoneId}_house{index}",
        };
    }

    public bool Locked => Kind != LockSiteKind.OpenHouse;

    /// <summary>이 문을 여는 열쇠의 아이템 id.</summary>
    public string KeyId => $"key_{Id}";

    /// <summary>문 이름 — 「김진사댁 대문」 · 「주막 뒷방 방문」.</summary>
    public string DoorName => Kind == LockSiteKind.Compound ? $"{Name} 대문" : Kind == LockSiteKind.House ? $"{Name} 방문" : Name;

    public Rect Footprint => new(Center.x - Size.x * 0.5f, Center.y - Size.y * 0.5f, Size.x, Size.y);

    /// <summary>문 자리 — 남쪽(카메라 쪽) 벽 가운데.</summary>
    public Vector3 Door => new(Center.x, 0f, Center.y - Size.y * 0.5f);

    public bool Inside(Vector3 p, float inset = 0f)
        => Mathf.Abs(p.x - Center.x) <= Size.x * 0.5f - inset && Mathf.Abs(p.z - Center.y) <= Size.y * 0.5f - inset;
}

/// <summary>
/// 【잠긴 곳 자리 표】 (결정 2-143 · 2026-11-18 사용자 「A와 B 둘 다 적용」 · 덕코프 조사)
/// 덕코프는 구역마다 잠긴 문이 몇 곳뿐이고(Ground Zero 3곳) 대부분은 그냥 들어간다. 그래서 【구역마다 마당 하나 · 집 하나】.
/// 열쇠가 「그 문 하나」를 가리키려면 자리가 판마다 같아야 한다 — 맵 표(ZoneMapTable)에서 구역 id로 씨앗을 정해
/// 늘 같은 빈 땅을 찾는다(맵 표를 고치면 자리도 따라 옮겨진다). 씬을 다시 굽지 않는다 — 런타임이 세운다(LockedSite).
/// 자리 조건: 덩어리 · 금줄과 겹치지 않고 · 출발 · 길목 · 보스 · 조각 · 통로 자리에서 떨어지고 · 문 앞 3m가 비어 있다.
/// [임시값] 크기 · 거리.
/// </summary>
public static class LockSiteTable
{
    public static readonly Vector2 CompoundSize = new(14f, 12f);
    public static readonly Vector2 HouseSize = new(9f, 7f);
    public static readonly Vector2 CompoundSmall = new(10f, 9f);
    public static readonly Vector2 HouseSmall = new(7f, 6f);

    /// <summary>덩어리와 띄우는 거리 (m).</summary>
    public const float BlockGap = 1.6f;
    /// <summary>출발 · 길목 · 보스 · 조각 자리와 띄우는 거리 (m).</summary>
    public const float AnchorGap = 8f;
    /// <summary>마당과 집 사이 (m).</summary>
    public const float SiteGap = 6f;
    /// <summary>문 앞에 비워 둘 깊이 (m).</summary>
    public const float DoorClear = 3f;

    private static readonly string[] CompoundNames =
        { "김진사댁", "최부자댁", "향청", "객사 뒷마당", "관아 곳간", "서원 마당", "역참", "정승댁", "도가(都家)", "향교 뒷마당" };

    private static readonly string[] HouseNames =
        { "주막 뒷방", "약방", "서당", "포목전 안방", "대장간 안채", "무당집", "훈장댁", "사랑채", "물방앗간 방", "외딴 초가" };

    private static readonly Dictionary<string, List<LockSite>> cache = new();

    /// <summary>그 구역의 잠긴 곳 (0 ~ 2곳). 맵이 없거나 자리가 없으면 빈 목록. 0장(길잡이)은 없다.</summary>
    public static IReadOnlyList<LockSite> For(ChapterMap map, string zoneId)
    {
        if (map == null || string.IsNullOrEmpty(zoneId) || map.Chapter <= 0)
            return System.Array.Empty<LockSite>();

        string key = $"{map.Chapter}/{zoneId}";
        if (cache.TryGetValue(key, out List<LockSite> found))
            return found;

        var list = new List<LockSite>();
        ZoneArea? area = map.AreaOf(zoneId);
        if (area.HasValue)
        {
            int seed = StableSeed(zoneId);
            var rng = new System.Random(seed);
            // 빽빽한 구역이면 한 치수 작게 다시 찾는다.
            if (!TryPlace(map, area.Value, LockSiteKind.Compound, CompoundSize, CompoundNames[Mod(seed, CompoundNames.Length)], rng, list))
                TryPlace(map, area.Value, LockSiteKind.Compound, CompoundSmall, CompoundNames[Mod(seed, CompoundNames.Length)], rng, list);
            if (!TryPlace(map, area.Value, LockSiteKind.House, HouseSize, HouseNames[Mod(seed / 7, HouseNames.Length)], rng, list))
                TryPlace(map, area.Value, LockSiteKind.House, HouseSmall, HouseNames[Mod(seed / 7, HouseNames.Length)], rng, list);
        }

        cache[key] = list;
        return list;
    }

    // ── 열린 집 (결정 2-149 · 사용자 「파밍 가능한 건물도 학습했던 이미지들로 만들어서 좀 크게 넣어」) ───────────
    /// <summary>열린 집의 생김새 — 그림(Resources/Locks) · 크기(m). 그림은 dkbpl + dkbworld 0.3 으로 새로 그린 것.</summary>
    public static readonly (string Art, Vector2 Size, string Name)[] OpenHouseTypes =
    {
        ("house_choga", new Vector2(13f, 9f), "빈 초가"),
        ("house_giwa", new Vector2(15f, 10f), "버려진 기와집"),
        ("house_gotgan", new Vector2(11f, 8f), "곳간"),
        ("house_jumak", new Vector2(14f, 9f), "문 닫은 주막"),
        ("house_ruin", new Vector2(12f, 9f), "무너진 초가"),
    };

    /// <summary>구역마다 열린 집 수 — 0장은 맵을 2배로 늘려(결정 2-149) 넉넉히, 다른 장은 아직 원래 크기라 하나.</summary>
    public static int OpenHousesPerZone(int chapter) => chapter == 0 ? 3 : 1;

    private static readonly Dictionary<string, List<LockSite>> openCache = new();

    /// <summary>그 구역의 열린 집 — 잠긴 곳과 겹치지 않게, 길 가까이(마을처럼). 판마다 같은 자리.</summary>
    public static IReadOnlyList<LockSite> OpenHouses(ChapterMap map, string zoneId)
    {
        if (map == null || string.IsNullOrEmpty(zoneId))
            return System.Array.Empty<LockSite>();
        string key = $"{map.Chapter}/{zoneId}";
        if (openCache.TryGetValue(key, out List<LockSite> found))
            return found;

        var list = new List<LockSite>();
        ZoneArea? area = map.AreaOf(zoneId);
        if (area.HasValue)
        {
            var taken = new List<LockSite>(For(map, zoneId));
            var rng = new System.Random(StableSeed(zoneId + "/open"));
            int n = OpenHousesPerZone(map.Chapter);
            int type = rng.Next(OpenHouseTypes.Length);
            for (int i = 0; i < n; i++, type++)
            {
                var (art, size, name) = OpenHouseTypes[type % OpenHouseTypes.Length];
                LockSite site = PlaceOpen(map, area.Value, size, name, art, i, rng, taken);
                if (site == null)
                    continue;
                taken.Add(site);
                list.Add(site);
            }
        }
        openCache[key] = list;
        return list;
    }

    private static LockSite PlaceOpen(ChapterMap map, ZoneArea area, Vector2 size, string name, string art, int index, System.Random rng, List<LockSite> taken)
    {
        Rect b = area.Bounds;
        float marginX = size.x * 0.5f + 3f, marginZ = size.y * 0.5f + 3f + DoorClear;
        if (b.width < marginX * 2f || b.height < marginZ * 2f)
            return null;
        LockSite best = null;
        float bestRoad = float.MaxValue;
        for (int tries = 0; tries < 900; tries++)
        {
            float x = Mathf.Round(Mathf.Lerp(b.xMin + marginX, b.xMax - marginX, (float)rng.NextDouble()) * 2f) * 0.5f;
            float z = Mathf.Round(Mathf.Lerp(b.yMin + marginZ, b.yMax - marginZ, (float)rng.NextDouble()) * 2f) * 0.5f;
            var site = new LockSite(area.ZoneId, map.Chapter, LockSiteKind.OpenHouse, new Vector2(x, z), size, name, index, art);
            if (!Fits(map, site, taken))
                continue;
            // 길이 있으면 문 앞이 길에서 4 ~ 14m 인 자리를 좋아한다(마을처럼 길을 따라). 길 위에는 세우지 않는다.
            Vector2[][] paths = PixelDress.Paths(map.Chapter);
            if (paths.Length == 0)
                return site;
            float road = PixelDress.PathDistance(map.Chapter, new Vector2(x, z - size.y * 0.5f - 1f));
            float body = PixelDress.PathDistance(map.Chapter, new Vector2(x, z));
            if (body < size.magnitude * 0.5f + 3f)
                continue;
            float score = Mathf.Abs(road - 8f);
            if (score < bestRoad)
            {
                bestRoad = score;
                best = site;
            }
        }
        return best;
    }

    /// <summary>이 점이 어떤 집(잠긴 곳 · 열린 집) 자리 안인가 — 소품 · 바닥 굽기가 비운다.</summary>
    public static bool InAnySite(ChapterMap map, Vector2 p, float margin)
    {
        foreach (ZoneArea a in map.Areas)
        {
            foreach (LockSite s in For(map, a.ZoneId))
                if (Expand(s, margin).Contains(p)) return true;
            foreach (LockSite s in OpenHouses(map, a.ZoneId))
                if (Expand(s, margin).Contains(p)) return true;
        }
        return false;
    }

    private static Rect Expand(LockSite s, float m)
    {
        Rect r = s.Footprint;
        return Rect.MinMaxRect(r.xMin - m, r.yMin - m - DoorClear, r.xMax + m, r.yMax + m);
    }

    /// <summary>모든 장 · 모든 구역의 잠긴 곳 — 열쇠 에셋 만들기 · 테스트가 쓴다.</summary>
    public static List<LockSite> All()
    {
        var all = new List<LockSite>();
        foreach (ChapterMap map in ZoneMapTable.All)
            foreach (ZoneArea a in map.Areas)
                all.AddRange(For(map, a.ZoneId));
        return all;
    }

    /// <summary>열쇠 id로 자리를 찾는다. 없으면 null.</summary>
    public static LockSite ByKey(string keyId)
    {
        foreach (LockSite s in All())
            if (s.KeyId == keyId)
                return s;
        return null;
    }

    public static void ClearCache()
    {
        cache.Clear();
        openCache.Clear();
    }

    private static bool TryPlace(ChapterMap map, ZoneArea area, LockSiteKind kind, Vector2 size, string name,
                                 System.Random rng, List<LockSite> placed)
    {
        Rect b = area.Bounds;
        float marginX = size.x * 0.5f + 3f;
        float marginZ = size.y * 0.5f + 3f + DoorClear;
        if (b.width < marginX * 2f || b.height < marginZ * 2f)
            return false;

        for (int tries = 0; tries < 1500; tries++)
        {
            float x = Mathf.Lerp(b.xMin + marginX, b.xMax - marginX, (float)rng.NextDouble());
            float z = Mathf.Lerp(b.yMin + marginZ, b.yMax - marginZ, (float)rng.NextDouble());
            // 반 미터 칸에 맞춘다 — 담 조각이 깔끔하게 맞물린다.
            x = Mathf.Round(x * 2f) * 0.5f;
            z = Mathf.Round(z * 2f) * 0.5f;

            var site = new LockSite(area.ZoneId, map.Chapter, kind, new Vector2(x, z), size, name);
            if (Fits(map, site, placed))
            {
                placed.Add(site);
                return true;
            }
        }
        return false;
    }

    /// <summary>이 자리에 세워도 되는가 — 테스트도 같은 판정을 쓴다.</summary>
    public static bool Fits(ChapterMap map, LockSite site, IReadOnlyList<LockSite> others)
    {
        Rect body = site.Footprint;
        Rect withDoor = Rect.MinMaxRect(body.xMin, body.yMin - DoorClear, body.xMax, body.yMax);

        if (!map.InBounds(new Vector3(withDoor.xMin, 0f, withDoor.yMin), 2f) || !map.InBounds(new Vector3(withDoor.xMax, 0f, withDoor.yMax), 2f))
            return false;

        ZoneArea? area = map.AreaOf(site.ZoneId);
        if (!area.HasValue || !area.Value.Bounds.Contains(withDoor.min) || !area.Value.Bounds.Contains(withDoor.max))
            return false;

        foreach (MapBlock block in map.Blocks)
            if (block.Kind != MapBlockKind.Flowerbed && Overlaps(withDoor, block, BlockGap))
                return false;

        foreach (MapGate gate in map.Gates)
            if (Overlaps(withDoor, gate.Block, BlockGap + 2f))
                return false;

        foreach (MapAnchor a in map.Anchors)
        {
            if (a.Kind is MapAnchorKind.BerryTree or MapAnchorKind.Landmark)
                continue;
            if (Distance(withDoor, new Vector2(a.Position.x, a.Position.z)) < AnchorGap)
                return false;
        }

        if (others != null)
            foreach (LockSite o in others)
            {
                Rect r = o.Footprint;
                r = Rect.MinMaxRect(r.xMin - SiteGap, r.yMin - SiteGap - DoorClear, r.xMax + SiteGap, r.yMax + SiteGap);
                if (r.Overlaps(withDoor))
                    return false;
            }

        return true;
    }

    private static bool Overlaps(Rect r, MapBlock block, float gap)
    {
        var b = new Rect(block.Center.x - block.Size.x * 0.5f - gap, block.Center.y - block.Size.z * 0.5f - gap,
            block.Size.x + gap * 2f, block.Size.z + gap * 2f);
        return r.Overlaps(b);
    }

    private static float Distance(Rect r, Vector2 p)
    {
        float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
        float dz = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>판마다 · 기계마다 같은 씨앗 — string.GetHashCode는 실행마다 바뀔 수 있어 쓰지 않는다.</summary>
    public static int StableSeed(string text)
    {
        unchecked
        {
            int h = 17;
            foreach (char c in text)
                h = h * 31 + c;
            return h & 0x7fffffff;
        }
    }

    private static int Mod(int a, int n) => ((a % n) + n) % n;
}
