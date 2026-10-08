using System.Collections.Generic;

/// <summary>탄 한 종류 (결정 2-43 · 2-80). 수치는 [임시값].</summary>
public readonly struct AmmoInfo
{
    public readonly string Id;
    public readonly string Name;
    public readonly string Description;
    /// <summary>한 발 무게 (kg) — Combat_Baseline 3-2절.</summary>
    public readonly float Weight;
    /// <summary>가방 한 칸에 드는 수.</summary>
    public readonly int StackMax;
    /// <summary>한 발 값 (냥).</summary>
    public readonly int Value;
    /// <summary>화살통 · 탄창에 담기는 수 — 탄마다 고정.</summary>
    public readonly int Capacity;
    /// <summary>이 탄을 쓰는 무기가 처음 나오는 티어(= 장). 시체에서는 이 장부터 나온다.</summary>
    public readonly int FirstTier;

    /// <summary>
    /// 이 탄이 속한 계열 = 무기가 정하는 탄 id (결정 2-95). 일반 탄은 자기 자신,
    /// 방어 관통탄은 같은 무기의 일반 탄 — 같은 통에 넣고 같은 무기로 쏜다.
    /// </summary>
    public readonly string Family;

    /// <summary>방어 관통 + (방어 관통탄 · 결정 2-95).</summary>
    public readonly int PenetrationBonus;

    /// <summary>피해 배율 (방어 관통탄은 조금 약하다 [임시]).</summary>
    public readonly float DamageScale;

    /// <summary>시체 전리품 표의 비중 · 개수.</summary>
    public readonly int CorpseWeight;
    public readonly int CorpseMin;
    public readonly int CorpseMax;

    public bool IsArmourPiercing => PenetrationBonus > 0;

    public AmmoInfo(string id, string name, string description, float weight, int stackMax, int value,
        int capacity, int firstTier, string family = null, int penetrationBonus = 0, float damageScale = 1f,
        int corpseWeight = AmmoTable.CorpseWeight, int corpseMin = AmmoTable.CorpseMin, int corpseMax = AmmoTable.CorpseMax)
    {
        Family = family ?? id;
        PenetrationBonus = penetrationBonus;
        DamageScale = damageScale;
        CorpseWeight = corpseWeight;
        CorpseMin = corpseMin;
        CorpseMax = corpseMax;
        Id = id;
        Name = name;
        Description = description;
        Weight = weight;
        StackMax = stackMax;
        Value = value;
        Capacity = capacity;
        FirstTier = firstTier;
    }
}

/// <summary>
/// 탄 7종 (결정 2-43 · 2-80). 탄은 다시 주울 수 없다 — 쏜 것은 사라진다.
///   · 가게 — 화살 · 철환은 잡화 가게(0장부터), 전부는 대장간
///   · 시체 — 그 장에서 쓰는 무기의 탄이 10~30발
///   · 제작(작업대) — 작업대 제작 단계에서
/// </summary>
public static class AmmoTable
{
    public const string Arrow = "ammo_arrow";
    public const string Pyeonjeon = "ammo_pyeonjeon";
    public const string Bolt = "ammo_bolt";
    public const string Dart = "ammo_dart";
    public const string Shot = "ammo_shot";
    public const string Scatter = "ammo_scatter";
    public const string Rocket = "ammo_rocket";

    /// <summary>방어 관통탄 id = 일반 탄 id + 이것 (결정 2-95).</summary>
    public const string PiercingSuffix = "_ap";

    // 방어 관통탄 [임시값] — 방어 관통 +1 · 피해 ×0.9 · 값 4배 · 작업대 Ⅲ에서만 만든다(가게에 없다).
    // 시체에서는 드물게(비중 2 · 4~10발) — 처음 하나가 작업대에 바칠 견본이 된다.
    public const int PiercingBonus = 1;
    public const float PiercingDamageScale = 0.9f;
    public const int PiercingCorpseWeight = 2;
    public const int PiercingCorpseMin = 4;
    public const int PiercingCorpseMax = 10;
    /// <summary>방어 관통탄이 시체에서 나오기 시작하는 장 [임시].</summary>
    public const int PiercingFirstChapter = 3;

    public static string PiercingOf(string baseId) => baseId + PiercingSuffix;

    public const int CorpseMin = 10;
    public const int CorpseMax = 30;
    /// <summary>시체 전리품 표의 비중 (공용 표 — 쇠붙이 30 · 새끼 뭉치 22).</summary>
    public const int CorpseWeight = 14;

    private static readonly AmmoInfo[] all =
    {
        new(Arrow,     "화살",     "활에 메기는 장전. 쏜 화살은 다시 주울 수 없다.",          0.007f, 400, 1, 30, 1),
        new(Pyeonjeon, "편전",     "통아에 넣어 쏘는 짧은 화살. 멀리 가고 깊이 박힌다.",      0.006f, 250, 2, 20, 3),
        new(Bolt,      "쇠뇌살",   "단발 쇠뇌에 거는 굵은 살.",                               0.011f, 300, 2, 15, 2),
        new(Dart,      "세전",     "연발 쇠뇌에 채우는 가는 살.",                             0.004f, 600, 1, 30, 3),
        new(Shot,      "철환",     "총통에 화약과 함께 재는 쇠구슬.",                          0.003f, 1000, 1, 20, 1),
        new(Scatter,   "산탄 철환", "작은 쇠구슬 한 줌. 한 번에 흩어져 나간다.",               0.005f, 600, 2, 8, 2),
        new(Rocket,    "신기전",   "화약 통을 단 화살. 맞으면 터진다.",                        0.028f, 100, 8, 4, 4),

        // ── 방어 관통탄 【임시】 이름 · 수치 (결정 2-95) — 보조 구슬 「관통」(적을 뚫는다)과 다르다: 방어도를 덜 받는다.
        AP(Arrow,     "쇠촉 화살",    "촉을 쇠로 바꾼 화살. 갑옷을 덜 탄다. 【임시】",     0.008f, 400, 1, 30, 1),
        AP(Pyeonjeon, "쇠촉 편전",    "쇠촉을 박은 편전. 갑옷을 덜 탄다. 【임시】",       0.007f, 250, 2, 20, 3),
        AP(Bolt,      "쇠촉 쇠뇌살",  "쇠촉을 박은 쇠뇌살. 갑옷을 덜 탄다. 【임시】",     0.012f, 300, 2, 15, 2),
        AP(Dart,      "쇠촉 세전",    "쇠촉을 박은 세전. 갑옷을 덜 탄다. 【임시】",       0.005f, 600, 1, 30, 3),
        AP(Shot,      "단철환",       "두드려 단단히 한 쇠구슬. 갑옷을 덜 탄다. 【임시】", 0.003f, 1000, 1, 20, 1),
        AP(Scatter,   "단철 산탄",    "단철 알갱이 한 줌. 갑옷을 덜 탄다. 【임시】",       0.005f, 600, 2, 8, 2),
        AP(Rocket,    "쇠촉 신기전",  "쇠촉을 단 신기전. 갑옷을 덜 탄다. 【임시】",       0.029f, 100, 8, 4, 4),
    };

    private static AmmoInfo AP(string family, string name, string description, float weight, int stackMax,
                               int baseValue, int capacity, int firstTier)
        => new(PiercingOf(family), name, description, weight, stackMax, baseValue * 4, capacity,
            UnityEngine.Mathf.Max(firstTier, PiercingFirstChapter), family, PiercingBonus, PiercingDamageScale,
            PiercingCorpseWeight, PiercingCorpseMin, PiercingCorpseMax);

    /// <summary>그 무기(탄 계열 id)가 이 탄을 받는가 — 일반 탄 · 방어 관통탄 둘 다.</summary>
    public static bool Accepts(string familyId, string ammoId)
    {
        if (string.IsNullOrEmpty(familyId) || string.IsNullOrEmpty(ammoId))
            return false;

        if (ammoId == familyId)
            return true;

        AmmoInfo? a = Find(ammoId);
        return a != null && a.Value.Family == familyId;
    }

    /// <summary>계열의 탄 id들 — 일반 탄이 먼저(채울 때 먼저 쓴다).</summary>
    public static IEnumerable<string> FamilyMembers(string familyId)
    {
        if (string.IsNullOrEmpty(familyId))
            yield break;

        yield return familyId;

        foreach (AmmoInfo a in all)
            if (a.Family == familyId && a.Id != familyId)
                yield return a.Id;
    }

    public static IReadOnlyList<AmmoInfo> All => all;

    public static bool IsAmmo(string id) => Find(id) != null;

    public static AmmoInfo? Find(string id)
    {
        foreach (AmmoInfo a in all)
            if (a.Id == id)
                return a;

        return null;
    }

    public static int CapacityOf(string id) => Find(id)?.Capacity ?? 0;

    public static int PenetrationBonusOf(string id) => Find(id)?.PenetrationBonus ?? 0;

    public static float DamageScaleOf(string id) => Find(id)?.DamageScale ?? 1f;

    /// <summary>이 장의 시체에서 나오는가. 탄이 아니면 언제나 true (다른 물건은 막지 않는다).</summary>
    public static bool DropsIn(string id, int chapter)
    {
        AmmoInfo? a = Find(id);
        return a == null || a.Value.FirstTier <= UnityEngine.Mathf.Max(1, chapter);
    }

    /// <summary>
    /// 통에 채울 수 — 빈 자리만큼, 가방에 있는 만큼.
    /// </summary>
    public static int RefillAmount(int loaded, int capacity, int inBag)
        => UnityEngine.Mathf.Clamp(capacity - loaded, 0, UnityEngine.Mathf.Max(0, inBag));

    /// <summary>미리 채우기 버튼이 뜨는 남은 양 — 담는 수의 25% 이하 (결정 2-81) [임시값].</summary>
    public const float OfferReloadRatio = 0.25f;

    /// <summary>
    /// 미리 채우기 버튼을 띄울까 — 통이 25% 이하로 남았고, 덜 찼고, 가방에 탄이 있다.
    /// 화살 30 → 7발 · 쇠뇌살 15 → 3 · 산탄 8 → 2 · 신기전 4 → 1발부터.
    /// </summary>
    public static bool ShouldOfferReload(int loaded, int capacity, int inBag)
        => capacity > 0 && inBag > 0 && loaded < capacity
           && loaded <= UnityEngine.Mathf.FloorToInt(capacity * OfferReloadRatio);
}
