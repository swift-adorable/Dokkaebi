using UnityEngine;

/// <summary>보스 패턴의 갈래 (결정 2-110 · 부록 1182 ~ 1200).</summary>
public enum BossPatternKind
{
    None = 0,
    Thief,        // 야광귀 — 봇짐을 훔쳐 달아난다: 가까이 오면 달아났다가 멈춘다
    Fade,         // 달걀귀신 — 등을 돌리고 섰다가 돌아서 다가온다 · 흐려진다
    GrowWhenSeen, // 어둑시니 — 올려다볼수록 커지고, 눈을 돌리면 줄어든다
    CoilLunge,    // 강철이 — 똬리를 틀고 꼬리로 쓸다가 덮친다
    Pair,         // 처녀귀신 · 몽달귀신 — 서로 곁에 있으면 단단하다
    PlagueKick,   // 두억시니 — 약탕기를 걷어차 역병 김
    Rooted,       // 두두리 — 고목 둥치처럼 서서 버티다가 들이받는다
    Claw,         // 꺼먹살이 — 긁어낸다(출혈)
    Lure,         // 창귀 — 거리를 두며 산군 쪽으로 꾄다
    Pounce,       // 산군 — 덮친다 · 몰리면 그림 속으로 돌아간다
    Dive,         // 삼족오 — 날아올랐다 내리꽂힌다(불)
    IceNeedles,   // 현무 — 빗방울이 언 얼음 바늘
    Thunder,      // 청룡 — 우레
    FireRing,     // 주작 — 가마의 불 고리
    Roar,         // 백호 — 포효로 밀어낸다
    HornCharge,   // 해태 — 뿔로 들이받는다
    Shapeshift    // 구미호 — 둔갑, 넋불이 진짜를 가린다
}

/// <summary>
/// 【보스 17의 고유 패턴】 (결정 2-110) — 부록의 「만나는 까닭 → 물러나는 모습」 · 「보스전의 구조」에 적힌 모습을 싸움으로.
/// 순수 판정 · 수치만 여기 둔다. 실행은 Enemy/BossPattern. 수치는 전부 [임시값].
/// </summary>
public static class BossPatternTable
{
    public static BossPatternKind KindOf(string bossId) => bossId switch
    {
        "yagwanggwi"   => BossPatternKind.Thief,
        "dalgyal"      => BossPatternKind.Fade,
        "eodukssini"   => BossPatternKind.GrowWhenSeen,
        "gangcheori"   => BossPatternKind.CoilLunge,
        "wongwi"       => BossPatternKind.Pair,
        "dueoksini"    => BossPatternKind.PlagueKick,
        "duduri"       => BossPatternKind.Rooted,
        "kkeomeoksari" => BossPatternKind.Claw,
        "changgwi"     => BossPatternKind.Lure,
        "sangun"       => BossPatternKind.Pounce,
        "samjogo"      => BossPatternKind.Dive,
        "hyeonmu"      => BossPatternKind.IceNeedles,
        "cheongnyong"  => BossPatternKind.Thunder,
        "jujak"        => BossPatternKind.FireRing,
        "baekho"       => BossPatternKind.Roar,
        "haetae"       => BossPatternKind.HornCharge,
        "gumiho"       => BossPatternKind.Shapeshift,
        _              => BossPatternKind.None
    };

    /// <summary>패턴 사이의 틈(초).</summary>
    public static float Interval(BossPatternKind kind) => kind switch
    {
        BossPatternKind.Fade => 6f,
        BossPatternKind.CoilLunge => 7f,
        BossPatternKind.PlagueKick => 5f,
        BossPatternKind.Rooted => 8f,
        BossPatternKind.Claw => 4f,
        BossPatternKind.Pounce => 6f,
        BossPatternKind.Dive => 7f,
        BossPatternKind.IceNeedles => 6f,
        BossPatternKind.Thunder => 5f,
        BossPatternKind.FireRing => 7f,
        BossPatternKind.Roar => 8f,
        BossPatternKind.HornCharge => 6f,
        _ => 5f
    };

    /// <summary>위험 표시 뒤 맞기까지(초) — 피할 틈.</summary>
    public const float TelegraphSeconds = 1.0f;

    // ── 어둑시니 ─────────────────────────────────────────────────────

    /// <summary>플레이어가 보고 있는가 — 바라보는 쪽과 어둑시니 쪽이 이만큼 같으면.</summary>
    public const float SeenDot = 0.8f;

    /// <summary>어둑시니 크기 — 보고 있으면 커지고(최대 1.8), 안 보면 줄어든다(최소 0.8). 초당 변화.</summary>
    public static float NextScale(float scale, bool seen, float dt)
        => Mathf.Clamp(scale + (seen ? 0.5f : -0.6f) * dt, 0.8f, 1.8f);

    /// <summary>클수록 단단하다 — 1.8배면 받는 피해 0.5, 0.8배면 1.3.</summary>
    public static float GrowDamageTaken(float scale) => Mathf.Lerp(1.3f, 0.5f, Mathf.InverseLerp(0.8f, 1.8f, scale));

    // ── 처녀귀신 · 몽달귀신 ───────────────────────────────────────────

    public const float PairRange = 6f;

    /// <summary>짝이 곁(6m)에 살아 있으면 받는 피해 0.5, 떨어져 있으면 1.2.</summary>
    public static float PairDamageTaken(bool partnerNear) => partnerNear ? 0.5f : 1.2f;

    // ── 구미호 ───────────────────────────────────────────────────────

    /// <summary>둔갑하는 체력 비율과 그 모습 — 부록 1200 「새색시 → 방앗간지기 → 참봉 → 할머니」.</summary>
    public static readonly (float at, string form)[] Forms =
    {
        (0.80f, "새색시"), (0.60f, "방앗간지기"), (0.40f, "참봉"), (0.20f, "할머니")
    };

    /// <summary>지금 체력(0~1)이면 몇 번째 모습까지 지났는가. 처음엔 0.</summary>
    public static int FormIndex(float normalizedHealth)
    {
        int n = 0;
        foreach ((float at, string _) in Forms)
            if (normalizedHealth <= at)
                n++;
        return n;
    }

    /// <summary>둔갑할 때 함께 서는 가짜 수.</summary>
    public const int Decoys = 2;

    // ── 산군 ─────────────────────────────────────────────────────────

    /// <summary>이 체력 아래로 몰리면 한 번 그림 속으로 돌아간다(잠깐 사라졌다가 조금 회복).</summary>
    public const float RetreatBelow = 0.3f;
    public const float RetreatHeal = 0.1f;
}
