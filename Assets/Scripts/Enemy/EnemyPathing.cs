using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 【적 길찾기】 (결정 2-89 · 안 A) — NavMesh에서 경로만 빌려 쓴다.
///
/// NavMeshAgent는 붙이지 않는다 — 이동은 지금처럼 Rigidbody(EnemyMovement)가 하고,
/// 여기서는 「지금 어느 쪽으로 가야 하는가」만 알려 준다. 두뇌 · 밀어내기 · 공격 차례는 그대로다.
///
///   · 0.4초(±20%)마다 경로를 다시 구한다 — 적마다 어긋나게 해서 한 프레임에 몰리지 않는다
///   · 대상까지 NavMesh 위로 곧장 보이면(NavMesh.Raycast) 경로를 구하지 않고 곧장 간다
///   · NavMesh가 없는 씬(SampleScene)이면 언제나 곧장 — 예전 그대로
/// 【바꾸는 법】 다른 길찾기(흐름장 등)로 갈아 끼울 때는 Direction만 바꾼다. EnemyMovement는 이 결과만 본다.
/// </summary>
public sealed class EnemyPathing
{
    public const float RepathInterval = 0.4f;

    /// <summary>꺾임점에 이만큼 다가가면 다음 꺾임점으로 (m).</summary>
    public const float CornerReach = 0.6f;

    /// <summary>NavMesh 위 자리를 찾는 거리 (m) — 적 몸 중심 높이를 넉넉히 덮는다.</summary>
    public const float SampleDistance = 2f;

    /// <summary>처음 쓸 때 만든다 — MonoBehaviour 필드 초기화 중에는 NavMeshPath를 만들 수 없다.</summary>
    private NavMeshPath path;
    private readonly Vector3[] corners = new Vector3[24];
    private int count;
    private int index;
    private float nextRepath;

    /// <summary>다음 프레임에 경로를 다시 구한다 (끼였을 때).</summary>
    public void ForceRepath() => nextRepath = 0f;

    public void Reset()
    {
        count = 0;
        index = 0;
        nextRepath = 0f;
    }

    /// <summary>
    /// 가야 할 방향(XZ, 길이 1). 경로를 따라 돌아가는 중이면 detour = true.
    /// </summary>
    public Vector3 Direction(Vector3 from, Vector3 to, out bool detour)
    {
        detour = false;

        Vector3 straight = to - from;
        straight.y = 0f;
        straight = straight.sqrMagnitude > 0.0001f ? straight.normalized : Vector3.zero;

        if (Time.time >= nextRepath)
        {
            nextRepath = Time.time + RepathInterval * Random.Range(0.8f, 1.2f);
            Repath(from, to);
        }

        if (count < 2)
            return straight;

        while (index < count && Flat(corners[index] - from).sqrMagnitude <= CornerReach * CornerReach)
            index++;

        if (index >= count)
            return straight;

        Vector3 next = Flat(corners[index] - from);
        if (next.sqrMagnitude < 0.0001f)
            return straight;

        detour = true;
        return next.normalized;
    }

    private void Repath(Vector3 from, Vector3 to)
    {
        count = 0;
        index = 0;

        // 땅 높이(0)에서 찾는다 — 몸 중심 높이에서 찾으면 낮은 덩어리(좌판) 지붕에 붙을 수 있다.
        if (!NavMesh.SamplePosition(Ground(from), out NavMeshHit start, SampleDistance, NavMesh.AllAreas)
            || !NavMesh.SamplePosition(Ground(to), out NavMeshHit goal, SampleDistance, NavMesh.AllAreas))
            return;   // NavMesh가 없다 — 곧장

        // 곧장 보이면 경로가 필요 없다.
        if (!NavMesh.Raycast(start.position, goal.position, out _, NavMesh.AllAreas))
            return;

        path ??= new NavMeshPath();

        if (!NavMesh.CalculatePath(start.position, goal.position, NavMesh.AllAreas, path)
            || path.status == NavMeshPathStatus.PathInvalid)
            return;

        count = path.GetCornersNonAlloc(corners);
        index = 1;
    }

    /// <summary>
    /// 걸어서 닿는 자리인가 — 땅 위 NavMesh가 있고 거기서 대상까지 끊기지 않은 길이 있다.
    /// NavMesh가 없는 씬이면 언제나 그렇다고 본다. 덩어리 지붕 위(높이 0.5m 넘음)는 땅이 아니다.
    /// </summary>
    public static bool IsReachable(Vector3 point, Vector3 target, NavMeshPath scratch)
    {
        if (!NavMesh.SamplePosition(Ground(target), out NavMeshHit goal, SampleDistance, NavMesh.AllAreas))
            return true;

        if (!NavMesh.SamplePosition(Ground(point), out NavMeshHit hit, 1f, NavMesh.AllAreas) || hit.position.y > 0.5f)
            return false;

        return NavMesh.CalculatePath(hit.position, goal.position, NavMesh.AllAreas, scratch)
               && scratch.status == NavMeshPathStatus.PathComplete;
    }

    private static Vector3 Ground(Vector3 v) => new(v.x, 0f, v.z);

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
