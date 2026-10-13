using UnityEngine;

/// <summary>보조 구슬 중 수치 합산만으로는 안 되는 셋 — 탄이 살아 움직이거나, 플레이어의 지금 상태를 본다.</summary>
public enum SupportMechanic
{
    None = 0,
    Homing,     // 추격 — 탄이 적을 따라간다. 상태이상에 걸린 적을 먼저
    Momentum,   // 기세 — 일정 거리 넘게 움직이는 동안 피해 증가, 멈추면 바로 사라짐
    Cadence     // 운율 — 이어 쏠수록 빨라지고, 상한에 닿으면 1초 과열
}

/// <summary>
/// 【보조 구슬 고유 효과 — 추격 · 기세 · 운율】 (결정 2-116) — 구슬 설명(SkillAssetGenerator)에 적힌 대로.
/// 순수 판정 · 수치만 여기 둔다. 실행은 PlayerWeapon(기세 · 운율)과 BulletController(추격). 수치는 [임시값].
/// 이전에는 셋 다 이름 · 대가만 있고 효과가 없었다 — 기세는 늘 +20%, 운율은 늘 발사 간격 ×0.85로 합산되던 것을 고친다.
/// </summary>
public static class SupportMechanics
{
    public static SupportMechanic Of(string skillId) => skillId switch
    {
        "sup_pursuit" => SupportMechanic.Homing,
        "sup_momentum" => SupportMechanic.Momentum,
        "sup_cadence" => SupportMechanic.Cadence,
        _ => SupportMechanic.None
    };

    // ── 추격 ─────────────────────────────────────────────────────────

    /// <summary>이 거리 안의 적을 쫓는다(m).</summary>
    public const float HomingRadius = 8f;

    /// <summary>초당 꺾는 각도 — 너무 크면 탄이 적 둘레를 돈다.</summary>
    public const float HomingTurnDegrees = 270f;

    /// <summary>상태이상에 걸린 적은 이만큼(m) 더 가까운 것으로 친다 — 「상태이상에 걸린 적을 우선」.</summary>
    public const float HomingStatusPreference = 4f;

    /// <summary>표적을 다시 고르는 간격(초).</summary>
    public const float HomingRetargetSeconds = 0.15f;

    /// <summary>낮을수록 먼저 쫓는다.</summary>
    public static float HomingScore(float distance, bool hasStatus) => distance - (hasStatus ? HomingStatusPreference : 0f);

    /// <summary>탄이 나아가는 쪽 앞(반구)에 있는가 — 뒤로 돌아가 쫓지 않는다.</summary>
    public static bool InFront(Vector3 forward, Vector3 toTarget)
        => Vector3.Dot(new Vector3(forward.x, 0f, forward.z), new Vector3(toTarget.x, 0f, toTarget.z)) > 0f;

    // ── 기세 ─────────────────────────────────────────────────────────

    /// <summary>쉬지 않고 이만큼(m) 움직이면 기세가 붙는다.</summary>
    public const float MomentumArmDistance = 3f;

    /// <summary>이보다 느리면(m/s) 멈춘 것으로 본다 — 멈추면 기세는 바로 사라진다.</summary>
    public const float MomentumStopSpeed = 0.5f;

    public struct MomentumState
    {
        public float Distance;

        public bool Active => Distance >= MomentumArmDistance;

        /// <summary>이번 프레임 움직인 거리 · 걸린 시간.</summary>
        public void Tick(float moved, float deltaTime)
        {
            if (deltaTime <= 0f)
                return;
            if (moved / deltaTime < MomentumStopSpeed)
                Distance = 0f;
            else
                Distance += moved;
        }
    }

    // ── 운율 ─────────────────────────────────────────────────────────

    /// <summary>이어 쏠 때마다 발사 간격이 이만큼씩 준다.</summary>
    public const float CadenceStep = 0.03f;

    /// <summary>이만큼 이어 쏘면 상한 — 그 순간 과열.</summary>
    public const int CadenceMaxStacks = 20;

    /// <summary>과열로 못 쏘는 시간(초) — 구슬 대가 「1초간 강제 과열 정지」.</summary>
    public const float CadenceOverheatSeconds = 1f;

    /// <summary>앞 발사에서 (지금 간격 × 이 값)보다 오래 쉬면 이어 쏜 것이 아니다.</summary>
    public const float CadenceChainWindow = 1.6f;

    public struct CadenceState
    {
        public int Stacks;
        public float LastShot;
        public float OverheatedUntil;
        public bool Started;

        public float IntervalMultiplier => 1f - CadenceStep * Mathf.Clamp(Stacks, 0, CadenceMaxStacks - 1);

        public bool Overheated(float now) => now < OverheatedUntil;

        /// <summary>쏜 뒤에 부른다. baseInterval = 운율을 뺀 발사 간격.</summary>
        public void OnShot(float now, float baseInterval)
        {
            if (!Started || now - LastShot > baseInterval * IntervalMultiplier * CadenceChainWindow + 0.05f)
                Stacks = 0;

            Started = true;
            LastShot = now;
            Stacks++;

            if (Stacks >= CadenceMaxStacks)
            {
                OverheatedUntil = now + CadenceOverheatSeconds;
                Stacks = 0;
                Started = false;
            }
        }
    }

    /// <summary>
    /// 쉬지 않고 쏠 때 한 바퀴(상한까지 쏘고 과열이 풀릴 때까지) 평균 발사 간격 배율 — 테스트 · 문서용.
    /// 1보다 작으면 운율이 이득이다.
    /// </summary>
    public static float CadenceAverageMultiplier(float baseInterval)
    {
        float time = 0f;
        for (int k = 0; k < CadenceMaxStacks - 1; k++)
            time += baseInterval * (1f - CadenceStep * k);
        time += Mathf.Max(baseInterval, CadenceOverheatSeconds);
        return time / (baseInterval * CadenceMaxStacks);
    }
}
