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

    /// <summary>
    /// 지금 이 후보를 감지하고 있는가. (Perception이 판정한 결과)
    ///
    /// 【거리만 보던 것을 대체한다.】
    /// 전에는 감지 거리 안이면 무조건 대상이 됐다. 그래서 뒤로 돌아가도,
    /// 소리를 내지 않아도 결과가 같았다 — 잠입이라는 선택지가 없었다.
    /// 「무엇을 노릴까」(여기)와 「알아챘는가」(Perception)를 나눈다.
    /// </summary>
    public bool isDetected;
}

/// <summary>
/// 이 원형이 얼마나 집요한가. 원형표가 정한다.
///
/// 【세 값이 각각 맡는 일】
///   · forgetTime       — 놓친 뒤 몇 초를 더 찾아다니는가
///   · forcedChaseRange — 이 거리 안에서는 아예 잊지 않는다
///   · safetyRange      — 이 거리를 넘으면 무조건 놓는다 (버그 방지)
/// </summary>
public struct AggroPursuit
{
    /// <summary>감지하지 못한 채 이만큼 흐르면 놓는다(초).</summary>
    public float forgetTime;

    /// <summary>이 거리(m) 안이면 망각 시간이 흐르지 않는다. 0이면 없음.</summary>
    public float forcedChaseRange;

    /// <summary>이 거리(m)를 넘으면 즉시 놓는다. 0 이하면 거리로 놓지 않는다.</summary>
    public float safetyRange;
}

/// <summary>
/// 추적이 기억하는 것. 개체마다 하나씩, 부르는 쪽(EnemyAggro)이 들고 있는다.
///
/// AggroSelector 자체는 여전히 상태를 갖지 않는다 —
/// 기억을 ref로 받아 갱신할 뿐이라 EditMode에서 그대로 검증된다.
/// </summary>
public struct AggroMemory
{
    /// <summary>지금 물고 있는 대상. 없으면 AggroSelector.NoTarget.</summary>
    public int targetId;

    /// <summary>그 대상을 감지하지 못한 채 흐른 시간(초).</summary>
    public float unseenTime;

    /// <summary>아무것도 물지 않은 상태.</summary>
    public static AggroMemory Empty => new AggroMemory
    {
        targetId = AggroSelector.NoTarget,
        unseenTime = 0f
    };
}

/// <summary>
/// 누구를 노릴 것인가. (docs/Blob_Hunting_System.md 4절)
///
/// 【핵심 규칙 — 먼저 문 대상을 계속 문다.】
/// 문서 4절: "먼저 어그로를 끈 대상이 있으면 자기가 공격당해도 그 대상을 우선한다."
/// 맞을 때마다 대상을 바꾸면 세 진영이 얽힌 자리에서 아무도 아무것도 못 죽인다.
/// 난전이 「서로 툭툭 치다 끝나는 장면」이 되면 진영을 넣은 의미가 없다.
///
/// 【추적을 그만두는 조건을 시간으로 바꿨다. (2026-09-22)】
/// 전에는 「감지 거리 × 2를 벗어나면 놓는다」였다. 거리로만 판정하면
/// 플레이어가 적보다 느릴 때 — 즉 짐을 잔뜩 들었을 때 — 교전을 피할
/// 방법이 아예 없다. 추출 루팅에서 가장 중요한 순간에 선택지가 사라진다.
/// 덕코프는 망각 시간(8×35 · 22×16 · 30×4)으로 판정한다.
/// [확인됨 — docs/research/duckov/05_적_AI_실측치.md 2절]
/// 시간으로 바꾸면 「엄폐물 뒤에서 버틴다」가 유효한 수가 된다.
/// 거리는 지형에 낀 적을 떼어내는 최후 방어선으로만 남긴다.
///
/// 【프레임마다 부르지 않는다.】 문서 4절 경고.
/// 언제 부를지는 EnemyAggro가 정하고, 그 간격을 elapsed로 넘긴다.
///
/// MonoBehaviour 의존이 없는 순수 클래스다. EditMode 테스트 대상.
/// </summary>
public static class AggroSelector
{
    /// <summary>플레이어의 후보 식별자. 적 식별자와 겹치지 않는 값이다.</summary>
    public const int PlayerId = 0;

    /// <summary>대상이 없음.</summary>
    public const int NoTarget = -1;

    /// <summary>
    /// 망각 시간의 기본값(초). 덕코프 59종 중 35종이 8초다.
    /// [확인됨 — docs/research/duckov/05_적_AI_실측치.md 2절]
    /// </summary>
    public const float DefaultForgetTime = 8f;

    /// <summary>
    /// 최후 방어선 거리 = 감지 거리 × 이 값.
    ///
    /// 전에 쓰던 2배를 3배로 늘렸다. 이제 포기는 시간이 정하므로,
    /// 거리는 「지형에 끼거나 길을 못 찾은 적이 맵 반대편까지 따라오는 사고」만
    /// 막으면 된다. 2배로 두면 시간 판정이 시작되기도 전에 거리가 먼저 끊는다.
    /// 【불확실】 문서에 수치가 없다.
    /// </summary>
    public const float DefaultSafetyMultiplier = 3f;

    /// <summary>원형 정보가 없을 때 쓰는 기본 추적 성향.</summary>
    public static AggroPursuit DefaultPursuit(float detectRange) => new AggroPursuit
    {
        forgetTime = DefaultForgetTime,
        forcedChaseRange = 0f,
        safetyRange = detectRange * DefaultSafetyMultiplier
    };

    /// <summary>
    /// 노릴 대상을 고른다.
    /// </summary>
    /// <param name="self">고르는 쪽의 소속.</param>
    /// <param name="selfPosition">고르는 쪽의 위치.</param>
    /// <param name="pursuit">이 원형이 얼마나 집요한가.</param>
    /// <param name="memory">이 개체의 기억. 갱신되어 돌아간다.</param>
    /// <param name="elapsed">지난번 호출로부터 흐른 시간(초).</param>
    /// <param name="candidates">후보 전부. 자기 자신이 섞여 있어도 된다.</param>
    /// <param name="selfId">자기 식별자. 자기를 고르지 않으려고 받는다.</param>
    public static int Select(
        Faction self,
        Vector3 selfPosition,
        in AggroPursuit pursuit,
        ref AggroMemory memory,
        float elapsed,
        IReadOnlyList<AggroCandidate> candidates,
        int selfId)
    {
        // 우호는 먼저 싸우지 않는다. 적대만 있으면 구역이 사격장이 된다.
        if (self == Faction.Friendly || candidates == null || candidates.Count == 0)
        {
            memory = AggroMemory.Empty;
            return NoTarget;
        }

        // ── 1. 물고 있던 대상을 먼저 본다 ────────────────────────────
        // 맞았다고 바꾸지 않는다. 이 한 가지가 난전을 성립시킨다.
        if (memory.targetId != NoTarget)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].id != memory.targetId)
                    continue;

                if (KeepHolding(self, candidates[i], selfId, selfPosition,
                                in pursuit, ref memory, elapsed))
                    return memory.targetId;

                break;
            }

            // 놓기로 했거나, 후보 목록에서 아예 사라졌다.
            memory = AggroMemory.Empty;
        }

        // ── 2. 놓쳤으면 【감지된】 적대 중 가장 가까운 것을 문다 ─────
        // 감지 거리는 여기서 보지 않는다. 눈에 보이는가 · 소리가 들리는가는
        // Perception이 이미 판정했고, 그 결과가 isDetected에 들어 있다.
        //
        // 【계약】 isDetected는 거리를 이미 반영한 값이다.
        // 여기서 거리를 한 번 더 자르면 「총성을 듣고 먼 곳에서 찾아온다」가
        // 막힌다 — 소리는 시야보다 멀리 갈 수 있어야 한다.
        // 대신 부르는 쪽이 감지하지도 못한 것을 true로 넘기면,
        // 방금 잊은 대상을 곧바로 다시 무는 일이 생긴다.
        int best = NoTarget;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < candidates.Count; i++)
        {
            AggroCandidate c = candidates[i];

            if (!c.isDetected || !IsEngageable(self, c, selfId))
                continue;

            float distance = Flat(c.position - selfPosition);

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = c.id;
        }

        memory.targetId = best;
        memory.unseenTime = 0f;

        return best;
    }

    /// <summary>
    /// 물고 있던 대상을 계속 물 것인가.
    ///
    /// 순서가 중요하다 — 최후 방어선(거리)이 가장 먼저다.
    /// 강제 추격 거리보다 안전 거리가 뒤에 오면, 지형에 낀 적이
    /// 「강제 추격 거리 안이라」 영원히 물고 있게 된다.
    /// </summary>
    private static bool KeepHolding(
        Faction self, in AggroCandidate c, int selfId, Vector3 selfPosition,
        in AggroPursuit pursuit, ref AggroMemory memory, float elapsed)
    {
        if (!IsEngageable(self, c, selfId))
            return false;

        float distance = Flat(c.position - selfPosition);

        if (pursuit.safetyRange > 0f && distance > pursuit.safetyRange)
            return false;

        // 보고 있거나, 강제 추격 거리 안이면 시간이 흐르지 않는다.
        if (c.isDetected || distance <= pursuit.forcedChaseRange)
        {
            memory.unseenTime = 0f;
            return true;
        }

        memory.unseenTime += Mathf.Max(0f, elapsed);

        return memory.unseenTime < Mathf.Max(0f, pursuit.forgetTime);
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
