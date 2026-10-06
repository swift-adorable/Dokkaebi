using System.Collections.Generic;

/// <summary>이야기 보스 하나의 수치 — 몸마다 체력 · 한 대 피해, 바꾼 방어도 · 화염 배율.</summary>
public sealed class BossStats
{
    public readonly string Id;

    /// <summary>몸마다 체력 — StoryTable 보스의 Bodies와 같은 순서 · 같은 수.</summary>
    public readonly int[] Health;

    /// <summary>몸마다 한 대 피해.</summary>
    public readonly int[] Damage;

    /// <summary>방어도를 따로 둘 때만 값이 있다. 없으면 유형 방어도 + 등급 보너스.</summary>
    public readonly float? Armour;

    /// <summary>화염 배율을 따로 둘 때만 값이 있다. 없으면 유형 저항 그대로.</summary>
    public readonly float? Fire;

    public BossStats(string id, int[] health, int[] damage, float? armour = null, float? fire = null)
    {
        Id = id;
        Health = health;
        Damage = damage;
        Armour = armour;
        Fire = fire;
    }
}

/// <summary>
/// 【Boss Data.】 이야기 보스 17의 체력 · 피해 (결정 2-68).
/// **원본은 docs/Dokkaebi_Combat_Baseline.md 5-1절 표다** — 이 표는 그것을 옮긴 것이고 BossDataTests가 대조한다.
/// 표는 docs/research/sim/boss_data.py가 만든다 (처치 목표 중간 20초 · 장 40초 · 최종 60초, 빌드 배율 가정).
///
/// 등급 배율(고유 ×15)을 쓰지 않는다 — 그러면 6장이 1장보다 쉬워진다. 일반 적은 여전히 유형 × 등급이다.
/// </summary>
public static class BossDataTable
{
    public const float MidBossSeconds = 20f;
    public const float ChapterBossSeconds = 40f;
    public const float FinalBossSeconds = 60f;

    private static BossStats S(string id, int[] health, int[] damage, float? armour = null, float? fire = null)
        => new(id, health, damage, armour, fire);

    private static readonly BossStats[] all =
    {
        S("yagwanggwi", new[] { 200 }, new[] { 22 }),
        S("dalgyal", new[] { 200 }, new[] { 12 }),
        S("hyeonmu", new[] { 500 }, new[] { 21 }, armour: 2f),     // 1장 무기는 관통 0
        S("eodukssini", new[] { 360 }, new[] { 28 }),
        S("gangcheori", new[] { 240 }, new[] { 16 }),
        S("cheongnyong", new[] { 480 }, new[] { 29 }),
        S("wongwi", new[] { 280, 420 }, new[] { 12, 33 }),
        S("dueoksini", new[] { 560 }, new[] { 33 }),
        S("jujak", new[] { 640 }, new[] { 27 }),
        S("duduri", new[] { 510 }, new[] { 33 }),
        S("kkeomeoksari", new[] { 1200 }, new[] { 39 }),
        S("baekho", new[] { 2400 }, new[] { 52 }),
        S("changgwi", new[] { 1080, 1080, 1080 }, new[] { 45, 45, 45 }),
        S("sangun", new[] { 720 }, new[] { 39 }),
        S("haetae", new[] { 1720 }, new[] { 44 }),
        S("samjogo", new[] { 2910 }, new[] { 45 }),
        S("gumiho", new[] { 8740 }, new[] { 82 }, fire: 1.0f),       // 첫 구슬(불)이 최종 보스의 약점이 되지 않게
    };

    public static IReadOnlyList<BossStats> All => all;

    public static BossStats Of(string bossId)
    {
        foreach (BossStats s in all)
            if (s.Id == bossId)
                return s;

        return null;
    }
}
