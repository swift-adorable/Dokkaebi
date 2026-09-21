using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 어그로 대상 후보 하나. MonoBehaviour를 참조하지 않는 값이다.
/// </summary>
public struct AggroCandidate
{
    /// <summary>대상을 다시 찾을 때 쓰는 식별자. 플레이어는 PlayerId다.</summary>
    public int id;

    public Faction faction;
    public Vector3 position;

    /// <summary>플레이어인가. 우호가 아닌 모든 진영이 이쪽을 노린다.</summary>
    public bool isPlayer;

    public bool isAlive;
}

/// <summary>
/// 누구를 노릴 것인가. (docs/Blob_Hunting_System.md 4절)
///
/// 【핵심 규칙 — 먼저 문 대상을 계속 문다.】
/// 문서 4절: "먼저 어그로를 끈 대상이 있으면 자기가 공격당해도 그 대상을 우선한다."
/// 맞을 때마다 대상을 바꾸면 세 진영이 얽힌 자리에서 아무도 아무것도 못 죽인다.
/// 난전이 「서로 툭툭 치다 끝나는 장면」이 되면 진영을 넣은 의미가 없다.
///
/// 【프레임마다 부르지 않는다.】 문서 4절 경고.
/// 이 클래스는 상태를 갖지 않는다. 언제 부를지는 EnemyAggro가 정한다.
///
/// MonoBehaviour 의존이 없는 순수 클래스다. EditMode 테스트 대상.
/// </summary>
public static class AggroSelector
{
    /// <summary>플레이어의 후보 식별자. 적 instanceId와 겹치지 않는 값이다.</summary>
    public const int PlayerId = 0;

    /// <summary>대상이 없음.</summary>
    public const int NoTarget = -1;

    /// <summary>
    /// 물고 있던 대상을 놓는 거리 배수.
    ///
    /// 【불확실】 문서에 수치가 없다. 감지 거리의 2배로 둔다 —
    /// 감지 거리와 같게 두면 경계에서 물었다 놨다를 반복하고,
    /// 무한이면 모든 적이 보안기가 된다. 보안기의 「끝까지 쫓는다」는
    /// infiniteLeash로 따로 표현한다. (문서 8절)
    /// </summary>
    public const float DefaultLeashMultiplier = 2f;

    /// <summary>
    /// 노릴 대상을 고른다.
    /// </summary>
    /// <param name="self">고르는 쪽의 소속.</param>
    /// <param name="selfPosition">고르는 쪽의 위치.</param>
    /// <param name="detectRange">새 대상을 찾는 거리.</param>
    /// <param name="currentTargetId">지금 물고 있는 대상. 없으면 NoTarget.</param>
    /// <param name="infiniteLeash">한 번 물면 거리와 무관하게 놓지 않는가. (보안기)</param>
    /// <param name="candidates">후보 전부. 자기 자신이 섞여 있어도 된다.</param>
    /// <param name="selfId">자기 식별자. 자기를 고르지 않으려고 받는다.</param>
    public static int Select(
        Faction self,
        Vector3 selfPosition,
        float detectRange,
        int currentTargetId,
        bool infiniteLeash,
        IReadOnlyList<AggroCandidate> candidates,
        int selfId)
    {
        if (candidates == null || candidates.Count == 0)
            return NoTarget;

        // 우호는 먼저 싸우지 않는다. 적대만 있으면 구역이 사격장이 된다.
        if (self == Faction.Friendly)
            return NoTarget;

        float leash = infiniteLeash
            ? float.PositiveInfinity
            : detectRange * DefaultLeashMultiplier;

        // ── 1. 물고 있던 대상을 먼저 본다 ────────────────────────────
        // 맞았다고 바꾸지 않는다. 이 한 가지가 난전을 성립시킨다.
        if (currentTargetId != NoTarget)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                AggroCandidate c = candidates[i];

                if (c.id != currentTargetId)
                    continue;

                if (IsEngageable(self, c, selfId)
                    && Flat(c.position - selfPosition) <= leash)
                    return currentTargetId;

                break;
            }
        }

        // ── 2. 놓쳤으면 감지 거리 안에서 가장 가까운 적대를 문다 ─────
        int best = NoTarget;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < candidates.Count; i++)
        {
            AggroCandidate c = candidates[i];

            if (!IsEngageable(self, c, selfId))
                continue;

            float distance = Flat(c.position - selfPosition);

            if (distance > detectRange || distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = c.id;
        }

        return best;
    }

    /// <summary>이 후보를 노릴 수 있는가.</summary>
    private static bool IsEngageable(Faction self, in AggroCandidate c, int selfId)
    {
        if (!c.isAlive || c.id == selfId)
            return false;

        return c.isPlayer
            ? FactionTable.IsHostileToPlayer(self)
            : FactionTable.IsHostile(self, c.faction);
    }

    /// <summary>수평 거리. 높이는 보지 않는다 — 탑다운이다.</summary>
    private static float Flat(Vector3 delta)
    {
        delta.y = 0f;
        return delta.magnitude;
    }
}
