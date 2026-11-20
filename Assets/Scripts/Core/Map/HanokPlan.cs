using System.Collections.Generic;
using UnityEngine;

/// <summary>방 성격 — 부엌 · 방 · 대청 · 곳간 · 대문간 · 누마루 · 외양간 · 정주간 · 사당.</summary>
public enum HanokRoom { Kitchen, Room, Hall, Storage, Gate, Pavilion, Stable, Jeongju, Shrine }

/// <summary>담 — 없음 · 기와담 · 흙돌담 · 싸리울 · 막돌 돌담.</summary>
public enum HanokFence { None, Giwa, Earth, Brush, Stone }

public enum HanokRunKind { Outer, Inner, Rail, Fence }
public enum HanokOpenKind { Door, Window }

public readonly struct HanokRoomPlan
{
    public readonly Rect R;
    public readonly HanokRoom Kind;
    /// <summary>마당을 보는 쪽 — 0 동 · 1 북 · 2 서 · 3 남. 문 · 툇마루 · 트인 대청 앞이 이쪽.</summary>
    public readonly int Facing;
    public readonly int Building;
    public HanokRoomPlan(Rect r, HanokRoom kind, int facing, int building) { R = r; Kind = kind; Facing = facing; Building = building; }
}

public struct HanokBuilding
{
    public Rect R;
    public string Name;
    public bool Thatch;
    public float WallHeight;
}

public readonly struct HanokOpening
{
    public readonly float At;      // 벽 가운데부터 거리(m)
    public readonly float Width;
    public readonly HanokOpenKind Kind;
    public HanokOpening(float at, float width, HanokOpenKind kind) { At = at; Width = width; Kind = kind; }
}

public sealed class HanokRun
{
    public Vector2 A, B;
    public HanokRunKind Kind;
    public readonly List<HanokOpening> Openings = new();
    /// <summary>이 벽 너머(북쪽)가 집 밖인 뒷벽 — 카메라를 가리지 않으니 자르지 않는다.</summary>
    public bool BackWall;
    public int Building = -1;
    /// <summary>문짝이 열려 들어가는 쪽(방 안) — 바깥벽은 방 쪽, 안벽은 이웃 방 쪽.</summary>
    public Vector2 Inward;
    public float Length => (B - A).magnitude;
    public bool AlongX => Mathf.Abs(B.x - A.x) > Mathf.Abs(B.y - A.y);
}

public readonly struct HanokYard
{
    public readonly string Kind;   // jangdok · well · pyeongsang · woodpile · stepping · tree · jars · cart
    public readonly Vector2 At;
    public HanokYard(string kind, Vector2 at) { Kind = kind; At = at; }
}

/// <summary>
/// 【한옥 집터 짜임】 (결정 2-151 v6 · 2026-11-19 사용자 「참고전용_한옥내부구조 사진 하나도 참고 안 하고 만드냐 · 처음부터 다시」)
/// 175장을 보고 가장 크게 달랐던 것: 한옥은 「한 채」가 아니라 「집터」다 —
///   담 안에 안채 · 사랑채 · 행랑채(대문간) · 곳간채 · 사당이 마당을 둘러싼다 (양반가 배치도 · 튼ㅁ자 · ㄷ자+행랑 · 농가+곳간채).
///   방은 지방형 순서를 따른다 (중부 ㄱ: 안방-대청-건넌방 + 부엌 날개 · 남부 ㅡ: 부엌-방-대청-방 · 관북 겹집: 두 줄 + 정주간 · 제주 안거리/밖거리).
///   문 · 툇마루 · 트인 대청은 마당 쪽(Facing)에 — ㄷ · ㅁ 날개는 안쪽을 본다. 부엌은 안방 옆 끝, 흙바닥.
/// 이 클래스는 짜임만 만든다(방 · 채 · 벽 줄 · 문 · 창 · 담 · 마당 살림). 3D 는 LockedSite 가 이것대로 세운다.
/// 정리 노트: ~/Dokkaebi-art/ref_hanok_notes.md (깃에 넣지 않음).
/// </summary>
public sealed class HanokPlan
{
    public const float Depth = 3.2f;      // 방 깊이 한 칸 (v8 — 덕코프 방은 캐릭터 폭의 6 ~ 8배 · 「방을 좀 더 넓혀봐」)
    public const float Porch = 1.5f;      // 툇마루 폭
    /// <summary>
    /// 집터 키움 (v7 · 2026-11-19 사용자 「한옥 건물 자체가 너무 작으니까 좀 더 키워 봐」).
    /// 짜임 좌표(아래 Lay 의 미터)를 통째로 이만큼 키운다 — 방 · 마당 · 담 · 집터가 같이 커지고, 가구는 그대로라 방이 넉넉해진다.
    /// 벽 높이는 캐릭터 키에 맞춰 조금만(WallScale).
    /// </summary>
    public const float Scale = 1.7f;   // v8 1.4 → 1.7
    public const float WallScale = 1.1f;
    public const float DoorWidth = 1.6f;   // v7 — 1.1m 는 몸 · 내비가 겨우 빠졌다(플레이 점검)

    public string Variant;
    public Vector2 Size;
    public bool Ruin;
    public bool NoPorch;
    public HanokFence Fence;
    public readonly List<HanokRoomPlan> Rooms = new();
    public readonly List<HanokBuilding> Buildings = new();
    public readonly List<HanokRun> Runs = new();
    public readonly List<Rect> Porches = new();
    public readonly List<HanokYard> Yard = new();
    /// <summary>담 줄과 대문 틈 (담 위 거리 at · 폭).</summary>
    private readonly List<(Vector2 a, Vector2 b, List<(float at, float w)> gates)> fences = new();

    // ── 짜임 표 ─────────────────────────────────────────────────────

    /// <summary>짜임 이름 → 집터 크기(m). LockSiteTable 이 이 순서로 장 안의 집에 나눠 준다.</summary>
    /// <summary>키우기 전 크기 — Lay 의 좌표가 이 크기 기준.</summary>
    private static readonly (string Variant, string Art, Vector2 Size, string Name)[] RawTable =
    {
        ("ㅁ자-통", "giwa", new Vector2(20f, 19f), "ㅁ자 기와집"),
        ("ㄷ자-안채", "giwa", new Vector2(20f, 17f), "ㄷ자 기와집 (누마루)"),
        ("ㄷ자+행랑", "giwa", new Vector2(26f, 24f), "양반가 (ㄷ자 안채 · 사랑채 · 행랑채)"),
        ("ㅁ자-튼", "giwa", new Vector2(22f, 20f), "튼ㅁ자집 (안채 ㄱ · 사랑채 ㄱ)"),
        ("ㄱ자+곳간채", "choga", new Vector2(20f, 17f), "ㄱ자 초가 · 곳간채"),
        ("ㅡ자+아래채", "choga", new Vector2(19f, 15f), "남부 일자집 · 아래채"),
        ("겹집", "choga", new Vector2(16f, 14f), "관북 겹집"),
        ("안밖거리", "choga", new Vector2(18f, 18f), "안거리 · 밖거리 (돌담)"),
        ("주막-ㄱ자", "jumak", new Vector2(18f, 14f), "주막"),
        ("폐가-ㄱ자", "ruin", new Vector2(16f, 13f), "무너진 ㄱ자 초가"),
        ("곳간채", "choga", new Vector2(13f, 9f), "곳간채"),
        ("ㅡ자-초가", "choga", new Vector2(13f, 10f), "세 칸 초가"),
    };

    /// <summary>짜임 표 — 키운 크기(Scale). 정적 초기화 순서 때문에 RawTable 뒤에 둔다.</summary>
    public static readonly (string Variant, string Art, Vector2 Size, string Name)[] Table =
        System.Array.ConvertAll(RawTable, e => (e.Variant, e.Art, e.Size * Scale, e.Name));

    public static (string Variant, string Art, Vector2 Size, string Name) Entry(string variant)
    {
        foreach (var e in Table) if (e.Variant == variant) return e;
        return Table[Table.Length - 1];
    }

    // ── 만들기 ──────────────────────────────────────────────────────

    private System.Random rng;
    private float hx, hz;

    public static HanokPlan Make(string variant, Vector2 size, System.Random rng)
    {
        var p = new HanokPlan { Variant = variant, Size = size, rng = rng, hx = size.x * 0.5f, hz = size.y * 0.5f };
        p.Lay(variant);
        p.ScaleUp(Scale);
        if (rng.Next(2) == 0) p.Mirror();
        p.MakeRuns();
        p.ResolveYard();
        return p;
    }

    /// <summary>한 줄로 이은 채 — (x0, z0) 모서리부터 alongX 면 +x, 아니면 +z 로 칸을 놓는다. 칸 폭은 조금씩 흔들고 total 에 맞춘다.</summary>
    private int Bar(float x0, float z0, bool alongX, float total, float depth, int facing, string name, bool thatch, float wallH, int building, params (HanokRoom k, float w)[] bays)
    {
        var w = new float[bays.Length];
        float sum = 0f;
        for (int i = 0; i < bays.Length; i++) { w[i] = bays[i].w * (0.9f + (float)rng.NextDouble() * 0.2f); sum += w[i]; }
        if (building < 0)
        {
            Buildings.Add(new HanokBuilding { Name = name, Thatch = thatch, WallHeight = wallH, R = alongX ? new Rect(x0, z0, total, depth) : new Rect(x0, z0, depth, total) });
            building = Buildings.Count - 1;
        }
        else
        {
            HanokBuilding b = Buildings[building];
            Rect add = alongX ? new Rect(x0, z0, total, depth) : new Rect(x0, z0, depth, total);
            b.R = Rect.MinMaxRect(Mathf.Min(b.R.xMin, add.xMin), Mathf.Min(b.R.yMin, add.yMin), Mathf.Max(b.R.xMax, add.xMax), Mathf.Max(b.R.yMax, add.yMax));
            Buildings[building] = b;
        }
        float cur = 0f;
        for (int i = 0; i < bays.Length; i++)
        {
            float len = i == bays.Length - 1 ? total - cur : Mathf.Round(total * w[i] / sum * 10f) / 10f;
            Rect r = alongX ? new Rect(x0 + cur, z0, len, depth) : new Rect(x0, z0 + cur, depth, len);
            Rooms.Add(new HanokRoomPlan(r, bays[i].k, facing, building));
            cur += len;
        }
        return building;
    }

    private void Wall(float ax, float az, float bx, float bz, params (float at, float w)[] gates)
        => fences.Add((new Vector2(ax, az), new Vector2(bx, bz), new List<(float, float)>(gates)));

    /// <summary>담 네모 — 남쪽 담에 대문 틈(gateX 가운데).</summary>
    private void FenceBox(float x0, float z0, float x1, float z1, float gateX, float gateW, bool south = true, bool north = true)
    {
        if (south) Wall(x0, z0, x1, z0, (gateX - (x0 + x1) * 0.5f, gateW));
        if (north) Wall(x0, z1, x1, z1);
        Wall(x0, z0, x0, z1);
        Wall(x1, z0, x1, z1);
    }

    private static (HanokRoom, float) K(float w) => (HanokRoom.Kitchen, w);
    private static (HanokRoom, float) R(float w) => (HanokRoom.Room, w);
    private static (HanokRoom, float) H(float w) => (HanokRoom.Hall, w);
    private static (HanokRoom, float) S(float w) => (HanokRoom.Storage, w);
    private static (HanokRoom, float) G(float w) => (HanokRoom.Gate, w);
    private static (HanokRoom, float) P(float w) => (HanokRoom.Pavilion, w);
    private static (HanokRoom, float) M(float w) => (HanokRoom.Stable, w);   // 외양간(마구)
    private static (HanokRoom, float) J(float w) => (HanokRoom.Jeongju, w);
    private static (HanokRoom, float) T(float w) => (HanokRoom.Shrine, w);   // 사당

    private const int E = 0, N = 1, W = 2, Sx = 3;

    private void Lay(string variant)
    {
        const float D = Depth;
        switch (variant)
        {
            case "ㅁ자-통":   // 한 덩어리 고리 · 가운데 중정 · 남쪽 대문간으로만 (22 · 29 · 121)
            {
                float x0 = -8.5f, x1 = 8.5f, z0 = -8f, z1 = 8f, L = x1 - x0;
                Bar(x0, z1 - D, true, L, D, Sx, "안채", false, 2.7f, -1, R(3.2f), H(3f), H(3f), R(3.4f), K(4.4f));          // 건넌방 · 대청 · 안방 · 부엌
                Bar(x0, z0, true, L, D, N, "행랑", false, 2.5f, -1, S(2.8f), R(3.2f), H(2.8f), G(2.6f), R(2.8f), S(2.8f));  // 곳간 · 사랑방 · 사랑대청 · 대문간 · 방 · 광
                Bar(x0, z0 + D, false, z1 - z0 - 2f * D, D, E, "서익랑", false, 2.6f, -1, R(3.6f), S(3.4f), R(3.6f));
                Bar(x1 - D, z0 + D, false, z1 - z0 - 2f * D, D, W, "동익랑", false, 2.6f, -1, R(3.4f), H(3.6f), R(3.6f));
                NoPorchOnOuter = true;
                Yard.Add(new HanokYard("jangdok", new Vector2(5.5f, 8.9f)));
                Yard.Add(new HanokYard("well", new Vector2(-2.2f, 1.6f)));
                Yard.Add(new HanokYard("tree", new Vector2(9.2f, -8.6f)));
                Yard.Add(new HanokYard("stepping", new Vector2(Rooms[8].R.center.x, -5f)));
                break;
            }
            case "ㄷ자+행랑":   // 양반가 — ㄷ자 안채 · 동쪽 사랑채(누마루) · 남쪽 긴 행랑채(솟을대문) · 중문 담 · 사당 (84 · 100 · 63)
            {
                float X0 = -12.5f, X1 = 12.5f, Z0 = -11.5f, Z1 = 11.5f;
                Bar(X0, Z0, true, X1 - X0, D, N, "행랑채", false, 2.4f, -1, S(2.8f), M(3f), R(2.8f), G(3f), R(2.8f), S(2.8f), S(2.8f), R(2.8f), S(2.2f));
                Wall(X0, Z0 + D, X0, Z1); Wall(X1, Z0 + D, X1, Z1); Wall(X0, Z1, X1, Z1);                       // 담 (남쪽은 행랑채가 담)
                float nz = 7.3f;
                Bar(-11f, nz, true, 16f, D, Sx, "안채", false, 2.8f, -1, K(3f), R(3.4f), H(3f), H(3f), R(3.6f));    // 부엌 · 안방 · 대청 · 건넌방
                Bar(-11f, 1.3f, false, nz - 1.3f, D, E, "안채", false, 2.8f, -1, S(3f), R(3f));                       // 서쪽 날개
                Bar(2.3f, 1.3f, false, nz - 1.3f, D, W, "안채", false, 2.8f, -1, R(3f), H(3f));                       // 동쪽 날개
                Wall(-12.5f, -1.5f, 5f, -1.5f, (-3.4f - (-12.5f + 5f) * 0.5f, 2f));                                   // 중문 담
                Wall(5f, -1.5f, 5f, 1.3f);
                Bar(12.5f - 1f - D, -4f, false, 12f, D, W, "사랑채", false, 2.8f, -1, P(2.8f), R(3.4f), H(3f), R(2.8f));
                Bar(9.2f, 8.7f, true, 2.6f, 2.4f, Sx, "사당", false, 2.3f, -1, T(2.6f));
                Yard.Add(new HanokYard("jangdok", new Vector2(-9.6f, 10.4f)));
                Yard.Add(new HanokYard("well", new Vector2(-5f, 2.6f)));
                Yard.Add(new HanokYard("tree", new Vector2(6.8f, -6.4f)));
                Yard.Add(new HanokYard("tree", new Vector2(6.2f, 9.6f)));
                Yard.Add(new HanokYard("stepping", new Vector2(-2.4f, -5f)));
                Yard.Add(new HanokYard("pyeongsang", new Vector2(-8.6f, -5.6f)));
                break;
            }
            case "ㅁ자-튼":   // 안채 ㄱ(북서) + 사랑채 ㄱ(남동)이 마주봐 마당을 감싼다 · 모서리가 트였다 (36 · 148)
            {
                FenceBox(-10.5f, -9.5f, 10.5f, 9.5f, -6.8f, 2.4f);
                float nz = 5.4f;
                Bar(-9.5f, nz, true, 13f, D, Sx, "안채", false, 2.8f, -1, R(3.6f), H(3f), H(3f), R(3.4f));             // 안방 · 대청 · 건넌방
                Bar(-9.5f, -0.6f, false, nz + 0.6f, D, E, "안채", false, 2.8f, -1, R(2.8f), K(3.2f));                   // 부엌이 안방 아래
                Bar(-3f, -8f, true, 12.5f, D, N, "사랑채", false, 2.6f, -1, R(3f), H(3f), R(3f), S(3.5f));
                Bar(9.5f - D, -8f + D, false, 6.3f, D, W, "사랑채", false, 2.6f, -1, M(3f), R(3.3f));
                Yard.Add(new HanokYard("jangdok", new Vector2(-6f, 8.85f)));
                Yard.Add(new HanokYard("well", new Vector2(6.8f, 5.6f)));
                Yard.Add(new HanokYard("tree", new Vector2(8.6f, 7.8f)));
                Yard.Add(new HanokYard("stepping", new Vector2(-1.5f, -1.5f)));
                break;
            }
            case "ㄱ자+곳간채":   // 중부형 ㄱ 안채 + 따로 선 곳간채 · 싸리울 · 사립문 (23 · 85/179)
            {
                Fence = HanokFence.Brush;
                FenceBox(-9.5f, -8f, 9.5f, 8f, 2f, 2.2f);
                float nz = 8f - 1.6f - D;
                Bar(-8.5f, nz, true, 9.6f, D, Sx, "안채", true, 2.5f, -1, R(3.3f), H(2.9f), R(2.8f));
                Bar(-8.5f, nz - 5.6f, false, 5.6f, D, E, "안채", true, 2.5f, -1, R(2.6f), K(3f));
                Bar(9.5f - 1.2f - D, -5f, false, 8.4f, D, W, "곳간채", true, 2.3f, -1, M(2.8f), S(2.8f), S(2.8f));
                Yard.Add(new HanokYard("jangdok", new Vector2(-4f, 7.1f)));
                Yard.Add(new HanokYard("well", new Vector2(-3f, -5.6f)));
                Yard.Add(new HanokYard("woodpile", new Vector2(3.6f, 6.6f)));
                Yard.Add(new HanokYard("pyeongsang", new Vector2(0.6f, -2.6f)));
                break;
            }
            case "ㅡ자+아래채":   // 남부형 — 부엌 · 큰방 · 대청 · 작은방 · 광 + 툇마루, 아래채(외양간 · 헛간) · 흙돌담 (46 · 23)
            {
                Fence = HanokFence.Earth;
                FenceBox(-9f, -7f, 9f, 7f, 1.6f, 2.2f);
                Bar(-7.5f, 7f - 1.4f - D, true, 15f, D, Sx, "안채", true, 2.6f, -1, K(3f), R(3.2f), H(3.4f), R(2.8f), S(2.6f));
                Bar(-8f, -5.8f, false, 6.6f, D, E, "아래채", true, 2.3f, -1, M(3f), S(3.6f));
                Yard.Add(new HanokYard("jangdok", new Vector2(7.2f, -4.6f)));
                Yard.Add(new HanokYard("woodpile", new Vector2(-3.8f, -5.8f)));
                Yard.Add(new HanokYard("stepping", new Vector2(0.6f, -2.4f)));
                break;
            }
            case "겹집":   // 관북 — 방 두 줄 + 정주간 · 부엌 · 외양간이 한 지붕 · 툇마루 없음 · 돌담 (46)
            {
                Fence = HanokFence.Stone;
                NoPorch = true;
                FenceBox(-7.5f, -6.5f, 7.5f, 6.5f, -0.6f, 2.2f);
                int b = Bar(-6f, 0.6f, true, 12f, 3f, N, "겹집", true, 2.5f, -1, R(3f), R(3f), R(3f), S(3f));
                Bar(-6f, -2.4f, true, 12f, 3f, Sx, "겹집", true, 2.5f, b, R(2.6f), J(3.2f), K(3.2f), M(3f));
                Yard.Add(new HanokYard("woodpile", new Vector2(6.6f, -4.6f)));
                Yard.Add(new HanokYard("jars", new Vector2(-5f, -4.8f)));
                break;
            }
            case "안밖거리":   // 제주 — 안거리(정지 · 상방 · 구들 · 고방) · 밖거리가 마주봄 · 돌담 · 옆으로 드는 올레 (46)
            {
                Fence = HanokFence.Stone;
                Wall(-8.5f, -8.5f, 8.5f, -8.5f); Wall(-8.5f, 8.5f, 8.5f, 8.5f); Wall(-8.5f, -8.5f, -8.5f, 8.5f);
                Wall(8.5f, -8.5f, 8.5f, 8.5f, (-1f, 2.2f));                                                         // 올레 — 동쪽 담 틈
                Bar(-7f, 8.5f - 1.2f - D, true, 12f, D, Sx, "안거리", true, 2.4f, -1, K(3.2f), H(3.4f), R(2.8f), S(2.6f));
                Bar(-5f, -8.5f + 1.2f, true, 10f, D, N, "밖거리", true, 2.4f, -1, R(2.8f), H(3.4f), K(3.8f));
                Yard.Add(new HanokYard("jars", new Vector2(6.6f, 6.8f)));
                Yard.Add(new HanokYard("woodpile", new Vector2(-7.2f, 0f)));
                Yard.Add(new HanokYard("tree", new Vector2(6.8f, -6.8f)));
                break;
            }
            case "주막-ㄱ자":   // 주막 — 방 둘 · 넓은 마루가 길을 보고, 동쪽 날개에 큰 부엌 · 담 없이 평상 여럿
            {
                float nz = 7f - 1f - D;
                Bar(-8f, nz, true, 12f, D, Sx, "주막", true, 2.6f, -1, R(2.7f), R(2.7f), H(3.3f), H(3.3f));
                Bar(4f - 3.2f, nz - 6.6f, false, 6.6f, 3.2f, W, "주막", true, 2.6f, -1, S(3f), K(3.6f));
                Yard.Add(new HanokYard("pyeongsang", new Vector2(-6f, -1.6f)));
                Yard.Add(new HanokYard("pyeongsang", new Vector2(-2.6f, -4.6f)));
                Yard.Add(new HanokYard("pyeongsang", new Vector2(-6.2f, -5.4f)));
                Yard.Add(new HanokYard("jangdok", new Vector2(6.4f, 5.2f)));
                Yard.Add(new HanokYard("cart", new Vector2(6.6f, -4.6f)));
                break;
            }
            case "ㄷ자-안채":   // ㄷ자 — 서쪽 부엌 날개 · 북쪽 방-대청-방 · 동쪽 날개 끝 누마루 · 기와담 · 일각문 (104 · 14 · 51)
            {
                FenceBox(-9.5f, -8f, 9.5f, 8f, 0f, 2.4f);
                float nz = 8f - 1.3f - D;
                Bar(-8f, nz, true, 16f, D, Sx, "안채", false, 2.8f, -1, R(3.6f), H(3.2f), H(3.2f), R(3.4f), S(2.6f));
                Bar(-8f, -3.5f, false, nz + 3.5f, D, E, "안채", false, 2.8f, -1, R(3f), K(4.5f));
                Bar(8f - D, -3.5f, false, nz + 3.5f, D, W, "안채", false, 2.8f, -1, P(2.8f), R(4.7f));
                Yard.Add(new HanokYard("jangdok", new Vector2(-4.6f, 7.35f)));
                Yard.Add(new HanokYard("tree", new Vector2(-7.6f, -6.4f)));
                Yard.Add(new HanokYard("stepping", new Vector2(0f, -3f)));
                break;
            }
            case "폐가-ㄱ자":   // 무너진 ㄱ자 초가 — 벽 군데군데 · 싸리울 반쯤 쓰러짐
            {
                Ruin = true;
                Fence = HanokFence.Brush;
                FenceBox(-7.5f, -6.5f, 7.5f, 6.5f, -2f, 2.4f);
                float nz = 6.5f - 1.2f - D;
                Bar(-7f, nz, true, 11f, D, Sx, "폐가", true, 2.4f, -1, K(3f), R(2.8f), H(2.6f), R(2.6f));
                Bar(4f - D, nz - 6.4f, false, 6.4f, D, W, "폐가", true, 2.4f, -1, S(3.2f), R(3.2f));
                Yard.Add(new HanokYard("cart", new Vector2(-4.6f, -4.4f)));
                Yard.Add(new HanokYard("woodpile", new Vector2(6.2f, 4.6f)));
                break;
            }
            case "곳간채":   // 마을 곳간 — 지킴이방 + 곳간 둘 · 담 없음
            {
                Bar(-6f, -1.35f, true, 9.6f, D, Sx, "곳간채", true, 2.4f, -1, R(2.8f), S(3.4f), S(3.4f));
                Yard.Add(new HanokYard("woodpile", new Vector2(5f, 0.6f)));
                Yard.Add(new HanokYard("jars", new Vector2(5f, -2.8f)));
                Yard.Add(new HanokYard("cart", new Vector2(-3.4f, -3.6f)));
                break;
            }
            default:   // ㅡ자-초가 — 부엌 · 방 · 방 + 툇마루 · 싸리울
            {
                Variant = "ㅡ자-초가";
                Fence = HanokFence.Brush;
                FenceBox(-6f, -4.5f, 6f, 4.5f, 1.4f, 1.8f);
                Bar(-4.5f, 4.5f - 1f - D, true, 9f, D, Sx, "초가", true, 2.4f, -1, K(3f), R(3f), R(3f));
                Yard.Add(new HanokYard("jangdok", new Vector2(-4.4f, -2.6f)));
                Yard.Add(new HanokYard("woodpile", new Vector2(4.8f, -2.8f)));
                break;
            }
        }
        if (Fence == HanokFence.None && fences.Count > 0) Fence = HanokFence.Giwa;
    }

    /// <summary>ㅁ자 통집 — 바깥쪽 면엔 툇마루가 없다(마당은 안쪽).</summary>
    private bool NoPorchOnOuter;

    private void ScaleUp(float k)
    {
        static Rect Sc(Rect r, float k) => Rect.MinMaxRect(r.xMin * k, r.yMin * k, r.xMax * k, r.yMax * k);
        for (int i = 0; i < Rooms.Count; i++) Rooms[i] = new HanokRoomPlan(Sc(Rooms[i].R, k), Rooms[i].Kind, Rooms[i].Facing, Rooms[i].Building);
        for (int i = 0; i < Buildings.Count; i++) { HanokBuilding b = Buildings[i]; b.R = Sc(b.R, k); b.WallHeight *= WallScale; Buildings[i] = b; }
        for (int i = 0; i < Yard.Count; i++) Yard[i] = new HanokYard(Yard[i].Kind, Yard[i].At * k);
        for (int i = 0; i < fences.Count; i++)
        {
            var (a, b, gates) = fences[i];
            var g = new List<(float, float)>();
            foreach (var (at, w) in gates) g.Add((at * k, w * k));
            fences[i] = (a * k, b * k, g);
        }
    }

    private void Mirror()
    {
        static Rect F(Rect r) => Rect.MinMaxRect(-r.xMax, r.yMin, -r.xMin, r.yMax);
        static int Fd(int d) => d == 0 ? 2 : d == 2 ? 0 : d;
        for (int i = 0; i < Rooms.Count; i++) Rooms[i] = new HanokRoomPlan(F(Rooms[i].R), Rooms[i].Kind, Fd(Rooms[i].Facing), Rooms[i].Building);
        for (int i = 0; i < Buildings.Count; i++) { HanokBuilding b = Buildings[i]; b.R = F(b.R); Buildings[i] = b; }
        for (int i = 0; i < Yard.Count; i++) Yard[i] = new HanokYard(Yard[i].Kind, new Vector2(-Yard[i].At.x, Yard[i].At.y));
        for (int i = 0; i < fences.Count; i++)
        {
            var (a, b, gates) = fences[i];
            var g = new List<(float, float)>();
            bool alongX = Mathf.Abs(b.x - a.x) > 0.01f;
            foreach (var (at, w) in gates) g.Add((alongX ? -at : at, w));
            fences[i] = (new Vector2(-a.x, a.y), new Vector2(-b.x, b.y), g);
        }
    }

    // ── 벽 줄 · 문 · 창 ─────────────────────────────────────────────

    private static void Side(Rect r, int s, out Vector2 a, out Vector2 b)
    {
        switch (s)
        {
            case 0: a = new Vector2(r.xMax, r.yMin); b = new Vector2(r.xMax, r.yMax); break;   // 동
            case 1: a = new Vector2(r.xMin, r.yMax); b = new Vector2(r.xMax, r.yMax); break;   // 북
            case 2: a = new Vector2(r.xMin, r.yMin); b = new Vector2(r.xMin, r.yMax); break;   // 서
            default: a = new Vector2(r.xMin, r.yMin); b = new Vector2(r.xMax, r.yMin); break;  // 남
        }
    }

    /// <summary>방 r 의 s 쪽 변과 다른 방 o 가 맞닿는 구간(변 위 0 … 길이).</summary>
    private static bool Shared(Rect r, int s, Rect o, out float t0, out float t1)
    {
        t0 = t1 = 0f;
        const float e = 0.02f;
        bool touch = s switch
        {
            0 => Mathf.Abs(r.xMax - o.xMin) < e,
            1 => Mathf.Abs(r.yMax - o.yMin) < e,
            2 => Mathf.Abs(r.xMin - o.xMax) < e,
            _ => Mathf.Abs(r.yMin - o.yMax) < e,
        };
        if (!touch) return false;
        if (s == 0 || s == 2) { t0 = Mathf.Max(r.yMin, o.yMin) - r.yMin; t1 = Mathf.Min(r.yMax, o.yMax) - r.yMin; }
        else { t0 = Mathf.Max(r.xMin, o.xMin) - r.xMin; t1 = Mathf.Min(r.xMax, o.xMax) - r.xMin; }
        return t1 - t0 > e;
    }

    private static bool InnerDoor(HanokRoom a, HanokRoom b)
    {
        if (a == HanokRoom.Kitchen && b == HanokRoom.Room || a == HanokRoom.Room && b == HanokRoom.Kitchen) return false;   // 아궁이 벽
        if (a == HanokRoom.Gate || b == HanokRoom.Gate || a == HanokRoom.Shrine || b == HanokRoom.Shrine) return false;
        if (a == HanokRoom.Pavilion || b == HanokRoom.Pavilion) return a is HanokRoom.Room or HanokRoom.Hall || b is HanokRoom.Room or HanokRoom.Hall;
        return true;
    }

    private void MakeRuns()
    {
        for (int i = 0; i < Rooms.Count; i++)
        {
            HanokRoomPlan room = Rooms[i];
            for (int s = 0; s < 4; s++)
            {
                Side(room.R, s, out Vector2 a, out Vector2 b);
                float len = (b - a).magnitude;
                var shared = new List<(float t0, float t1, int other)>();
                for (int j = 0; j < Rooms.Count; j++)
                    if (j != i && Shared(room.R, s, Rooms[j].R, out float t0, out float t1))
                        shared.Add((t0, t1, j));
                shared.Sort((p, q) => p.t0.CompareTo(q.t0));

                // 맞닿은 구간 — 안벽(동 · 북 변에서만 한 번)
                if (s == 0 || s == 1)
                    foreach (var (t0, t1, other) in shared)
                    {
                        HanokRoom ok = Rooms[other].Kind;
                        if (room.Kind == HanokRoom.Hall && ok == HanokRoom.Hall) continue;                                     // 대청 두 칸 — 트였다
                        if (room.Kind == HanokRoom.Kitchen && ok == HanokRoom.Jeongju || room.Kind == HanokRoom.Jeongju && ok == HanokRoom.Kitchen) continue;   // 정주간 — 부엌과 트였다
                        var run = new HanokRun { A = Vector2.Lerp(a, b, t0 / len), B = Vector2.Lerp(a, b, t1 / len), Kind = HanokRunKind.Inner, Building = room.Building, Inward = s == 0 ? Vector2.right : Vector2.up };
                        if (t1 - t0 >= 2.0f && InnerDoor(room.Kind, ok))
                            run.Openings.Add(new HanokOpening(0f, 1.5f, HanokOpenKind.Door));
                        Runs.Add(run);
                    }

                // 바깥 구간 — 맞닿지 않은 나머지
                float cur = 0f;
                var outer = new List<(float, float)>();
                foreach (var (t0, t1, _) in shared) { if (t0 > cur + 0.05f) outer.Add((cur, t0)); cur = Mathf.Max(cur, t1); }
                if (len > cur + 0.05f) outer.Add((cur, len));
                foreach (var (t0, t1) in outer)
                    OuterRun(room, s, Vector2.Lerp(a, b, t0 / len), Vector2.Lerp(a, b, t1 / len));
            }
        }

        // 담
        foreach (var (a, b, gates) in fences)
        {
            var run = new HanokRun { A = a, B = b, Kind = HanokRunKind.Fence };
            foreach (var (at, w) in gates) run.Openings.Add(new HanokOpening(at, w, HanokOpenKind.Door));
            Runs.Add(run);
        }
    }

    private void OuterRun(HanokRoomPlan room, int s, Vector2 a, Vector2 b)
    {
        float len = (b - a).magnitude;
        bool front = s == room.Facing;
        bool back = s == (room.Facing + 2) % 4;
        var run = new HanokRun { A = a, B = b, Kind = HanokRunKind.Outer, Building = room.Building, BackWall = s == 1, Inward = s switch { 0 => Vector2.left, 1 => Vector2.down, 2 => Vector2.right, _ => Vector2.up } };
        switch (room.Kind)
        {
            case HanokRoom.Gate:
                if (front || back) return;   // 대문간 — 앞뒤로 트였다
                break;
            case HanokRoom.Hall:
                if (front) { AddPorch(room, s, a, b); return; }   // 대청 앞은 통째로 트였다 (분합문을 들어 올림)
                run.Openings.Add(new HanokOpening(0f, Mathf.Min(1.4f, len - 0.6f), HanokOpenKind.Window));   // 바라지창
                break;
            case HanokRoom.Pavilion:
                if (front) return;                               // 누마루 — 마당 쪽으로 오른다
                run.Kind = HanokRunKind.Rail;
                break;
            case HanokRoom.Stable:
                if (front) return;                               // 외양간 — 앞이 트였다
                break;
            case HanokRoom.Room:
            case HanokRoom.Jeongju:
            case HanokRoom.Shrine:
                if (front)
                {
                    if (len >= 2.2f) run.Openings.Add(new HanokOpening(0f, DoorWidth, HanokOpenKind.Door));
                    if (room.Kind == HanokRoom.Room) AddPorch(room, s, a, b);
                }
                else Windows(run, len);
                break;
            case HanokRoom.Kitchen:
                if (front || back) { if (len >= 2.2f) run.Openings.Add(new HanokOpening(front ? 0f : len * 0.2f, front ? 1.8f : 1.4f, HanokOpenKind.Door)); }   // 앞문 · 뒷문(장독대로)
                else if (len >= 2f) run.Openings.Add(new HanokOpening(0f, 0.7f, HanokOpenKind.Window));                                                          // 살창
                break;
            case HanokRoom.Storage:
                if (front && len >= 2.4f) run.Openings.Add(new HanokOpening(0f, 2.0f, HanokOpenKind.Door));
                break;
        }
        Runs.Add(run);
    }

    private static void Windows(HanokRun run, float len)
    {
        int n = Mathf.Max(0, Mathf.FloorToInt(len / 3.4f));
        for (int i = 0; i < n; i++)
            run.Openings.Add(new HanokOpening(-len * 0.5f + len * (i + 0.5f) / n, 1.3f, HanokOpenKind.Window));   // v8 — 큰 방에 맞춰 창도 크게
    }

    private void AddPorch(HanokRoomPlan room, int s, Vector2 a, Vector2 b)
    {
        if (NoPorch) return;
        Rect r = s switch
        {
            0 => Rect.MinMaxRect(a.x, a.y, a.x + Porch, b.y),
            1 => Rect.MinMaxRect(a.x, a.y, b.x, a.y + Porch),
            2 => Rect.MinMaxRect(a.x - Porch, a.y, a.x, b.y),
            _ => Rect.MinMaxRect(a.x, a.y - Porch, b.x, a.y),
        };
        if (NoPorchOnOuter && (Mathf.Abs(r.xMin) > hx - 2.2f * Scale || Mathf.Abs(r.xMax) > hx - 2.2f * Scale || Mathf.Abs(r.yMin) > hz - 2.2f * Scale || Mathf.Abs(r.yMax) > hz - 2.2f * Scale)) return;
        foreach (HanokRoomPlan o in Rooms)
            if (o.R.Overlaps(Shrink(r, 0.05f))) return;
        Porches.Add(r);
    }

    private static Rect Shrink(Rect r, float m) => Rect.MinMaxRect(r.xMin + m, r.yMin + m, r.xMax - m, r.yMax - m);

    /// <summary>
    /// 마당 살림이 방 · 툇마루에 붙으면 가까운 빈 마당으로 옮긴다 — 방 깊이를 바꿔도(v8) 짜임 좌표를 하나하나 고치지 않게.
    /// 집터 안 · 방 · 툇마루에서 0.8m 떨어진 가장 가까운 자리를 나선으로 찾는다. 박석(stepping)은 길이라 그대로.
    /// </summary>
    private void ResolveYard()
    {
        bool Free(Vector2 q)
        {
            if (Mathf.Abs(q.x) > hx - 1.5f || Mathf.Abs(q.y) > hz - 1.5f) return false;
            foreach (HanokRoomPlan r in Rooms) if (Rect.MinMaxRect(r.R.xMin - 1.4f, r.R.yMin - 1.4f, r.R.xMax + 1.4f, r.R.yMax + 1.4f).Contains(q)) return false;
            foreach (Rect pr in Porches) if (Rect.MinMaxRect(pr.xMin - 1.4f, pr.yMin - 1.4f, pr.xMax + 1.4f, pr.yMax + 1.4f).Contains(q)) return false;
            foreach (HanokRun run in Runs)
                if (run.Kind == HanokRunKind.Fence)
                {
                    Vector2 d = run.B - run.A; float t = Mathf.Clamp01(Vector2.Dot(q - run.A, d) / d.sqrMagnitude);
                    if ((run.A + d * t - q).magnitude < 1.4f) return false;
                }
            return true;
        }
        for (int i = 0; i < Yard.Count; i++)
        {
            HanokYard y = Yard[i];
            if (y.Kind == "stepping" || Free(y.At)) continue;
            Vector2 best = y.At; bool found = false;
            for (float rad = 0.5f; rad < 12f && !found; rad += 0.5f)
                for (int k = 0; k < 16 && !found; k++)
                {
                    float a = k * Mathf.PI / 8f;
                    Vector2 q = y.At + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
                    if (Free(q)) { best = q; found = true; }
                }
            if (found) Yard[i] = new HanokYard(y.Kind, best);
            else { Yard.RemoveAt(i); i--; }   // 둘 데가 없으면 뺀다
        }
    }

    // ── 검사 (테스트용) ──────────────────────────────────────────────

    /// <summary>
    /// 집터 바깥에서 걸어 모든 방에 들어갈 수 있는가 — 0.25m 칸에 벽(문 · 트인 곳 빼고)을 그려 넣고 바깥부터 채워 본다.
    /// 들어가지 못하는 방 이름을 bad 에.
    /// </summary>
    public bool AllRoomsReachable(out string bad)
    {
        const float c = 0.25f;
        float ox = -hx - 2f, oz = -hz - 2f;
        int nx = Mathf.CeilToInt((Size.x + 4f) / c), nz = Mathf.CeilToInt((Size.y + 4f) / c);
        var wall = new bool[nx, nz];
        void Mark(Vector2 p) { int i = Mathf.FloorToInt((p.x - ox) / c), j = Mathf.FloorToInt((p.y - oz) / c); if (i >= 0 && j >= 0 && i < nx && j < nz) wall[i, j] = true; }
        foreach (HanokRun run in Runs)
        {
            float len = run.Length;
            Vector2 dir = (run.B - run.A).normalized;
            for (float t = 0f; t <= len; t += c * 0.5f)
            {
                bool open = false;
                foreach (HanokOpening o in run.Openings)
                    if (o.Kind == HanokOpenKind.Door && Mathf.Abs(t - (len * 0.5f + o.At)) < o.Width * 0.5f - 0.05f) open = true;
                if (!open) Mark(run.A + dir * t);
            }
        }
        var seen = new bool[nx, nz];
        var q = new Queue<(int, int)>();
        q.Enqueue((0, 0)); seen[0, 0] = true;
        while (q.Count > 0)
        {
            var (i, j) = q.Dequeue();
            foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int a = i + di, b = j + dj;
                if (a < 0 || b < 0 || a >= nx || b >= nz || seen[a, b] || wall[a, b]) continue;
                seen[a, b] = true; q.Enqueue((a, b));
            }
        }
        bad = null;
        foreach (HanokRoomPlan r in Rooms)
        {
            int i = Mathf.FloorToInt((r.R.center.x - ox) / c), j = Mathf.FloorToInt((r.R.center.y - oz) / c);
            if (!seen[i, j]) { bad = $"{Variant}: {Buildings[r.Building].Name} {r.Kind} {r.R}"; return false; }
        }
        return true;
    }
}
