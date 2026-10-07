using UnityEngine;

/// <summary>
/// 【임시】 철수 지점 규칙 (결정 2-78). 구역 맵(9단계)이 생기면 자리만 맵이 정하고 버티기 규칙은 그대로 쓴다.
///
///   · 판마다 2곳 · 출발 자리에서 25~35m · 처음부터 보인다
///   · 원(반경 2.5m) 안에서 5초 버티면 철수 — 원을 벗어나면 처음부터
///   · 맞아도 끊기지 않는다 — 「버티기」다. 쓰러지면 끝
/// 수치는 [임시값].
/// </summary>
public static class ExtractionTable
{
    public const int PointCount = 2;
    public const float MinDistance = 25f;
    public const float MaxDistance = 35f;
    public const float Radius = 2.5f;
    public const float ChannelSeconds = 5f;

    /// <summary>파밍 구역 바닥의 반 폭 — 바닥은 100m × 100m (Floor 배율 10). 가장자리에서 5m 안쪽까지만 쓴다.</summary>
    public const float AreaHalfExtent = 45f;

    private const int Tries = 16;

    /// <summary>
    /// 철수 지점 자리를 고른다 — 서로 반대쪽(±30°)으로 갈라 두 길목이 한쪽에 몰리지 않게.
    /// 구역 밖으로 나가는 방향은 다시 고르고, 끝내 못 고르면 구역 안으로 당긴다.
    /// </summary>
    public static Vector3[] PickPoints(Vector3 start, System.Random rng,
        int count = PointCount, float halfExtent = AreaHalfExtent)
    {
        var points = new Vector3[count];
        float baseAngle = (float)(rng.NextDouble() * Mathf.PI * 2f);

        for (int i = 0; i < count; i++)
        {
            float slice = Mathf.PI * 2f / count;
            Vector3 at = start;
            bool found = false;

            for (int t = 0; t < Tries && !found; t++)
            {
                float jitter = (float)(rng.NextDouble() - 0.5) * Mathf.Deg2Rad * 60f;
                // 몇 번 실패하면 각도를 크게 흔든다 — 구석에서 시작해도 안쪽 방향을 찾는다.
                if (t >= Tries / 2)
                    jitter = (float)rng.NextDouble() * Mathf.PI * 2f;

                float angle = baseAngle + i * slice + jitter;
                float distance = Mathf.Lerp(MinDistance, MaxDistance, (float)rng.NextDouble());
                at = start + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                found = Inside(at, halfExtent);
            }

            if (!found)
                at = Clamp(at, halfExtent);

            at.y = start.y;
            points[i] = at;
        }

        return points;
    }

    public static bool Inside(Vector3 at, float halfExtent = AreaHalfExtent)
        => Mathf.Abs(at.x) <= halfExtent && Mathf.Abs(at.z) <= halfExtent;

    public static Vector3 Clamp(Vector3 at, float halfExtent = AreaHalfExtent)
        => new(Mathf.Clamp(at.x, -halfExtent, halfExtent), at.y, Mathf.Clamp(at.z, -halfExtent, halfExtent));

    /// <summary>바닥(XZ) 거리로 원 안에 있는가. 높이는 보지 않는다.</summary>
    public static bool InCircle(Vector3 player, Vector3 point, float radius = Radius)
    {
        float dx = player.x - point.x;
        float dz = player.z - point.z;
        return dx * dx + dz * dz <= radius * radius;
    }

    /// <summary>가장 가까운 철수 지점의 번호. 없으면 −1.</summary>
    public static int Nearest(Vector3 player, Vector3[] points)
    {
        int best = -1;
        float bestSq = float.MaxValue;

        for (int i = 0; points != null && i < points.Length; i++)
        {
            float dx = player.x - points[i].x;
            float dz = player.z - points[i].z;
            float sq = dx * dx + dz * dz;

            if (sq < bestSq)
            {
                bestSq = sq;
                best = i;
            }
        }

        return best;
    }
}

public enum ExtractionStep
{
    /// <summary>원 밖 · 버티는 중이 아님.</summary>
    Idle,
    /// <summary>이번 틱에 원에 들어와 세기 시작했다.</summary>
    Started,
    /// <summary>세는 중.</summary>
    Holding,
    /// <summary>이번 틱에 원을 벗어나 처음부터가 됐다.</summary>
    Cancelled,
    /// <summary>다 버텼다 — 철수.</summary>
    Completed,
}

/// <summary>원 안에서 버티는 시간을 센다. 벗어나면 처음부터. 한 번 끝나면 다시 세지 않는다.</summary>
public sealed class ExtractionChannel
{
    private readonly float seconds;

    public ExtractionChannel(float seconds = ExtractionTable.ChannelSeconds)
    {
        this.seconds = Mathf.Max(0.01f, seconds);
    }

    public float Elapsed { get; private set; }
    public bool Holding { get; private set; }
    public bool Completed { get; private set; }

    public float Remaining => Mathf.Max(0f, seconds - Elapsed);
    public float Progress => Mathf.Clamp01(Elapsed / seconds);

    public ExtractionStep Tick(bool inside, float deltaTime)
    {
        if (Completed)
            return ExtractionStep.Idle;

        if (!inside)
        {
            bool was = Holding;
            Holding = false;
            Elapsed = 0f;
            return was ? ExtractionStep.Cancelled : ExtractionStep.Idle;
        }

        bool started = !Holding;
        Holding = true;
        Elapsed += Mathf.Max(0f, deltaTime);

        if (Elapsed >= seconds)
        {
            Elapsed = seconds;
            Holding = false;
            Completed = true;
            return ExtractionStep.Completed;
        }

        return started ? ExtractionStep.Started : ExtractionStep.Holding;
    }

    /// <summary>쓰러졌을 때 · 씬이 바뀔 때 — 처음 상태로.</summary>
    public void Reset()
    {
        Elapsed = 0f;
        Holding = false;
        Completed = false;
    }
}
