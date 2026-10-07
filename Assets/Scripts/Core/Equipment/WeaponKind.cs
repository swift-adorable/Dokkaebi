using UnityEngine;

/// <summary>
/// 무기 종류 (결정 2-43 · 2-80). 종류가 발사 간격 비율 · 쓰는 탄 · 채우는 시간 · 성격을 정한다.
/// 이름은 docs/Dokkaebi_Naming.md 1절, 수치는 Combat_Baseline 3-1 · 3-2절.
/// </summary>
public enum WeaponKind
{
    /// <summary>활 — 기준.</summary>
    Bow = 0,
    /// <summary>편전 — 통아에 넣어 쏘는 짧은 화살. 관통 +2.</summary>
    Pyeonjeon = 1,
    /// <summary>단발 쇠뇌 — 쇠뇌살. 조용하다.</summary>
    Crossbow = 2,
    /// <summary>연발 쇠뇌 — 세전. 빠르고 조용하다.</summary>
    RepeatingCrossbow = 3,
    /// <summary>총통 — 철환. 시끄럽고 무겁다.</summary>
    Gun = 4,
    /// <summary>산탄 총통 — 산탄 철환 5갈래. 시끄럽고 무겁다.</summary>
    ScatterGun = 5,
    /// <summary>신기전 — 맞으면 터진다. 시끄럽고 무겁다.</summary>
    Rocket = 6,
}

/// <summary>종류 하나의 성격. 수치는 전부 [임시값] — 플레이로 다시 맞춘다.</summary>
public readonly struct WeaponKindInfo
{
    public readonly WeaponKind Kind;
    public readonly string Name;
    /// <summary>같은 티어 활의 발사 간격에 곱한다 (3-1절 — 활 0.40초 기준 비율).</summary>
    public readonly float IntervalRatio;
    public readonly string AmmoId;
    /// <summary>통이 빈 뒤 가방에서 다시 채우는 시간 (초). 채우는 동안 못 쏜다.</summary>
    public readonly float ReloadSeconds;
    public readonly int PenetrationBonus;
    /// <summary>발사 소리 — 감지 거리(m) 보정. 양수 = 시끄럽다.</summary>
    public readonly float Noise;
    public readonly float WeightScale;
    /// <summary>한 번 쏠 때 나가는 갈래 수 (산탄 5). 피해는 갈래마다 나눈다.</summary>
    public readonly int Pellets;
    public readonly float PelletSpread;
    /// <summary>맞은 자리 폭발 반경 (m) · 둘레가 받는 몫 (신기전).</summary>
    public readonly float ExplosionRadius;
    public readonly float ExplosionShare;

    public WeaponKindInfo(WeaponKind kind, string name, float intervalRatio, string ammoId, float reloadSeconds,
        int penetrationBonus = 0, float noise = 0f, float weightScale = 1f,
        int pellets = 1, float pelletSpread = 0f, float explosionRadius = 0f, float explosionShare = 0f)
    {
        Kind = kind;
        Name = name;
        IntervalRatio = intervalRatio;
        AmmoId = ammoId;
        ReloadSeconds = reloadSeconds;
        PenetrationBonus = penetrationBonus;
        Noise = noise;
        WeightScale = weightScale;
        Pellets = Mathf.Max(1, pellets);
        PelletSpread = pelletSpread;
        ExplosionRadius = explosionRadius;
        ExplosionShare = explosionShare;
    }

    /// <summary>
    /// 초당 피해 배율 (결정 2-80) — 【발사가 느릴수록 · 채우기가 길수록 세다.】
    /// 간격 비율^0.3 × 채우는 시간^0.2. 활 = 1.
    /// </summary>
    public float DpsMultiplier => Mathf.Pow(IntervalRatio, 0.3f) * Mathf.Pow(ReloadSeconds, 0.2f);

    /// <summary>화살통 · 탄창 — 총통 계열만 탄창이다.</summary>
    public string HolderName => Kind == WeaponKind.Gun || Kind == WeaponKind.ScatterGun ? "탄창" : "화살통";
}

/// <summary>무기 종류 표 (결정 2-43 · 2-80).</summary>
public static class WeaponKindTable
{
    private static readonly WeaponKindInfo[] kinds =
    {
        new(WeaponKind.Bow,               "활",       1.00f, AmmoTable.Arrow,   1.0f),
        new(WeaponKind.Pyeonjeon,         "편전",     1.45f, AmmoTable.Pyeonjeon, 1.0f, penetrationBonus: 2),
        new(WeaponKind.Crossbow,          "단발 쇠뇌", 1.45f, AmmoTable.Bolt,    1.5f, noise: -1f),
        new(WeaponKind.RepeatingCrossbow, "연발 쇠뇌", 0.48f, AmmoTable.Dart,    1.5f, noise: -1f),
        new(WeaponKind.Gun,               "총통",     0.75f, AmmoTable.Shot,    2.0f, noise: 2f, weightScale: 1.4f),
        new(WeaponKind.ScatterGun,        "산탄 총통", 1.28f, AmmoTable.Scatter, 2.0f, noise: 2f, weightScale: 1.4f,
            pellets: 5, pelletSpread: 7f),
        new(WeaponKind.Rocket,            "신기전",   2.40f, AmmoTable.Rocket,  2.5f, noise: 2f, weightScale: 1.3f,
            explosionRadius: 2f, explosionShare: 0.5f),
    };

    public static WeaponKindInfo Of(WeaponKind kind) => kinds[(int)kind];

    public static System.Collections.Generic.IReadOnlyList<WeaponKindInfo> All => kinds;

    /// <summary>같은 티어 활(BowTier)에 종류를 얹은 발사 간격.</summary>
    public static float Interval(WeaponKind kind, float bowInterval)
        => bowInterval * Of(kind).IntervalRatio;

    /// <summary>한 번 쏠 때의 피해 (산탄은 갈래 합) — 같은 티어 활의 초당 피해 × 배율 × 간격.</summary>
    public static float ShotDamage(WeaponKind kind, float bowDamage, float bowInterval)
    {
        WeaponKindInfo k = Of(kind);
        float bowDps = bowDamage / Mathf.Max(0.01f, bowInterval);
        return bowDps * k.DpsMultiplier * Interval(kind, bowInterval);
    }
}
