using UnityEngine;

/// <summary>어떻게 알아챘는가. 순서가 곧 우선순위다.</summary>
public enum DetectionKind
{
    /// <summary>알아채지 못했다.</summary>
    None = 0,

    /// <summary>소리로 알아챘다. 방향은 알지만 정확한 위치는 모른다.</summary>
    Heard = 1,

    /// <summary>눈으로 봤다.</summary>
    Seen = 2
}

/// <summary>감지 한 번을 판정하는 데 필요한 값 전부.</summary>
public struct PerceptionInput
{
    public Vector3 viewerPosition;

    /// <summary>보는 쪽이 향한 방향. 정규화되어 있지 않아도 된다.</summary>
    public Vector3 viewerForward;

    /// <summary>시야각 【전체】(도). 90이면 정면 기준 좌우 45도씩이다.</summary>
    public float visionConeDegrees;

    public float visionRange;

    /// <summary>
    /// 듣는 쪽의 귀가 얼마나 밝은가. 소리 반경에 곱한다.
    /// 0 이하면 보통 귀(1.0)로 본다 — 값을 채우지 않은 구조체가 귀머거리가 되면 안 된다.
    /// </summary>
    public float listenerHearingScale;

    public Vector3 targetPosition;

    /// <summary>대상이 지금 내는 소리가 퍼지는 반경(m). 0이면 소리가 없다.</summary>
    public float targetNoiseRadius;

    /// <summary>시선이 가리지 않았는가. 벽 판정은 부르는 쪽이 한다.</summary>
    public bool hasLineOfSight;
}

/// <summary>
/// 감지 — 「먼저 감지당하는가 / 먼저 감지하는가」.
/// (docs/Blob_Hunting_System.md 8절)
///
/// 【지금까지는 거리 하나뿐이었다.】
/// EnemyBrain은 detectDistance 반경 안이면 무조건 알아챘다.
/// 그래서 뒤로 돌아가도, 가만히 서 있어도 결과가 같았다 —
/// 잠입이라는 선택지가 존재하지 않았다.
///
/// 축을 둘로 나눈다.
///   · 눈 — 시야각 안 · 사거리 안 · 시선이 트여 있을 것
///   · 귀 — 대상이 낸 소리의 반경 × 듣는 쪽의 청각 안에 있을 것
///
/// MonoBehaviour 의존이 없는 순수 클래스다. EditMode 테스트 대상.
/// </summary>
public static class Perception
{
    /// <summary>
    /// 시야각의 기본값(도). 문서에 수치가 없다.
    ///
    /// 문서 8절은 「정면 넓고 측·후방 좁다」처럼 말로만 적었지만,
    /// 덕코프 생물 59종의 실측 최빈값이 140도다 (140×27 · 100×14 · 120×5 · 150×2).
    /// [확인됨 — docs/research/duckov/05_적_AI_실측치.md]
    /// 유형별 값은 EnemyArchetypeTable이 갖는다.
    /// </summary>
    public const float DefaultConeDegrees = 140f;

    /// <summary>
    /// 이 거리 안이면 시야각과 무관하게 본다.
    ///
    /// 코앞의 적을 「각도 밖이라」 못 본다면 그건 잠입이 아니라 버그로 읽힌다.
    /// 【불확실】 문서에 수치가 없다.
    /// </summary>
    public const float PointBlankRange = 1.5f;

    // ── 청각 ──────────────────────────────────────────────────────────
    //
    // 【처음 결정을 뒤집었다. (2026-09-22)】
    // 7-D에서는 「소리 크기는 내는 쪽만 정한다」로 두었다. 듣는 쪽마다 값이
    // 다르면 같은 총성이 누구에겐 들리고 누구에겐 안 들려, 플레이어가
    // 「이 소리가 어디까지 갔나」를 계산할 수 없다는 이유였다.
    //
    // 덕코프 생물 59종을 실측해 보니 듣는 쪽에 청각 능력이 있는데,
    // 값이 대부분 두 개에 몰려 있다 — 0.75가 19종, 1.0이 29종.
    // 즉 「듣는 쪽마다 다르다」를 넣으면서도 값을 거칠게 두어
    // 예측 가능성을 지킨다. 내 반대 근거가 회피 가능한 것이었다.
    // [확인됨 — docs/research/duckov/05_적_AI_실측치.md 3절]
    //
    // 세 값만 쓴다. 덕코프의 0(귀머거리)과 5·20(전 구역 감지)은 넣지 않는다 —
    // 0은 「소리로 유인한다」를 없애고, 20은 소리 관리 자체를 무의미하게 만든다.

    /// <summary>둔한 귀 — 기계형. 소리 반경의 4분의 3까지만 듣는다.</summary>
    public const float DullHearing = 0.75f;

    /// <summary>보통 귀. 대부분이 이 값이다.</summary>
    public const float NormalHearing = 1f;

    /// <summary>밝은 귀 — 잠복형. 소리 반경의 두 배까지 듣는다.</summary>
    public const float KeenHearing = 2f;

    /// <summary>
    /// 청각 배율을 안전한 값으로 다듬는다.
    /// 값을 채우지 않은 구조체(0)를 귀머거리로 만들지 않는다.
    /// </summary>
    public static float Hearing(float scale) => scale > 0f ? scale : NormalHearing;

    public static DetectionKind Detect(in PerceptionInput input)
    {
        Vector3 delta = input.targetPosition - input.viewerPosition;
        delta.y = 0f;

        float distance = delta.magnitude;

        // ── 눈 ────────────────────────────────────────────────────────
        if (CanSee(in input, delta, distance))
            return DetectionKind.Seen;

        // ── 귀 ────────────────────────────────────────────────────────
        // 소리는 벽을 넘는다. hasLineOfSight를 보지 않는다.
        if (input.targetNoiseRadius > 0f
            && distance <= input.targetNoiseRadius * Hearing(input.listenerHearingScale))
            return DetectionKind.Heard;

        return DetectionKind.None;
    }

    /// <summary>눈으로 보는가.</summary>
    public static bool CanSee(in PerceptionInput input, Vector3 delta, float distance)
    {
        if (distance > input.visionRange || !input.hasLineOfSight)
            return false;

        // 코앞은 각도를 묻지 않는다.
        if (distance <= PointBlankRange)
            return true;

        Vector3 forward = input.viewerForward;
        forward.y = 0f;

        // 향한 방향이 없으면 각도를 따질 수 없다. 전방위로 본다.
        if (forward.sqrMagnitude < 0.0001f)
            return true;

        float angle = Vector3.Angle(forward.normalized, delta.normalized);

        return angle <= Mathf.Max(0f, input.visionConeDegrees) * 0.5f;
    }
}
