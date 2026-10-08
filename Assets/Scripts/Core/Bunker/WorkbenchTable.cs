using System;
using System.Collections.Generic;

/// <summary>작업대 제작법 하나 (결정 2-95).</summary>
public sealed class CraftRecipe
{
    public readonly string OutputId;
    public readonly int OutputCount;

    /// <summary>이 제작법이 있는 작업대 단계 (1 ~ 4).</summary>
    public readonly int Stage;

    /// <summary>만들 때마다 드는 것.</summary>
    public readonly MaterialCost[] Inputs;
    public readonly int Gold;

    /// <summary>
    /// 처음 한 번 바쳐야 열린다 — 견본(그 물건 · 하위 무기) · 도면 · 장별 재료. 비어 있으면 단계만 되면 열려 있다(Ⅰ).
    /// 바친 것은 사라진다 (사용자 결정 2026-10-08).
    /// </summary>
    public readonly MaterialCost[] UnlockCost;

    public CraftRecipe(string outputId, int outputCount, int stage, MaterialCost[] inputs, int gold,
                       MaterialCost[] unlockCost = null)
    {
        OutputId = outputId;
        OutputCount = Math.Max(1, outputCount);
        Stage = stage;
        Inputs = inputs ?? Array.Empty<MaterialCost>();
        Gold = Math.Max(0, gold);
        UnlockCost = unlockCost ?? Array.Empty<MaterialCost>();
    }

    public bool NeedsUnlock => UnlockCost.Length > 0;
}

/// <summary>작업대 단계를 올리는 값.</summary>
public readonly struct StageCost
{
    public readonly int Gold;
    public readonly MaterialCost[] Materials;

    public StageCost(int gold, MaterialCost[] materials)
    {
        Gold = gold;
        Materials = materials ?? Array.Empty<MaterialCost>();
    }
}

/// <summary>
/// 【작업대 표】 (결정 2-95 · Bunker 5절 「만들려면 먼저 바쳐야 한다」) — 수치는 전부 [임시값].
///
///   · Ⅰ 기초 — 탄 7 · 횃불 · 면포. 작업대를 지으면 바로 (바칠 것 없음)
///   · Ⅱ 방어구 티어 1~3 — 머리 · 몸통(셋) · 윤도. 그 방어구 견본을 바쳐 하나씩 연다
///   · Ⅲ 방어 관통탄 7 · 방호구(탈 1 · 2) — 견본 + 탄은 그 탄이 처음 나오는 장의 장별 재료 하나
///   · Ⅳ 무기 티어 4~6 — 그 종류의 도면 + 바로 아래 티어 같은 종류 무기 견본
/// 단계를 올리려면 엽전 + 재료. 만들면 바로 나온다(창고 → 자리가 없으면 가방).
/// 재료 이름: 쇠붙이(scrap_metal) · 새끼 뭉치(wire_bundle) · 숯(cell_battery) · 노리개(memory_core) · 약재(bio_sample).
/// </summary>
public static class WorkbenchTable
{
    public const int MaxStage = 4;

    public const string Scrap = "scrap_metal";
    public const string Rope = "wire_bundle";
    public const string Charcoal = "cell_battery";
    public const string Trinket = "memory_core";
    public const string Herb = "bio_sample";

    public static string StageName(int stage) => stage switch
    {
        1 => "Ⅰ",
        2 => "Ⅱ",
        3 => "Ⅲ",
        4 => "Ⅳ",
        _ => "-",
    };

    public static string StageTheme(int stage) => stage switch
    {
        1 => "기초 — 탄 · 횃불 · 면포",
        2 => "방어구 티어 1~3",
        3 => "방어 관통탄 · 방호구",
        4 => "무기 티어 4~6",
        _ => string.Empty,
    };

    private static MaterialCost M(string id, int count) => new(id, count);

    /// <summary>이 단계로 올리는 값 (2 ~ 4). 1은 작업대를 지으면 된다.</summary>
    public static StageCost CostToReach(int stage) => stage switch
    {
        2 => new StageCost(500, new[] { M(Scrap, 6), M(Rope, 4) }),
        3 => new StageCost(1500, new[] { M(Scrap, 10), M(Charcoal, 6), M(Trinket, 1) }),
        4 => new StageCost(4000, new[] { M(Scrap, 16), M(Charcoal, 10), M(Trinket, 3) }),
        _ => new StageCost(0, null),
    };

    // ── 무기 도면 (결정 2-95) ──────────────────────────────────────────

    public const string BlueprintPrefix = "blueprint_";

    public static string BlueprintOf(WeaponKind kind) => BlueprintPrefix + kind.ToString().ToLowerInvariant();

    public static readonly WeaponKind[] BlueprintKinds =
    {
        WeaponKind.Bow, WeaponKind.Pyeonjeon, WeaponKind.Crossbow, WeaponKind.RepeatingCrossbow,
        WeaponKind.Gun, WeaponKind.ScatterGun, WeaponKind.Rocket,
    };

    /// <summary>고유(보스 · 큰 요괴) 시체에 도면이 들 확률 [임시].</summary>
    public const float BlueprintDropChance = 0.4f;

    // ── 무기 (id · 종류 · 티어) — Ⅳ 제작법을 만든다. 에셋과 같은지는 테스트가 본다 ─────────

    public readonly struct WeaponRow
    {
        public readonly string Id;
        public readonly WeaponKind Kind;
        public readonly int Tier;
        public readonly int Value;

        public WeaponRow(string id, WeaponKind kind, int tier, int value)
        {
            Id = id;
            Kind = kind;
            Tier = tier;
            Value = value;
        }
    }

    public static readonly WeaponRow[] Weapons =
    {
        new("wpn_t1_pipe", WeaponKind.Bow, 1, 180),
        new("wpn_t1_sechongtong", WeaponKind.Gun, 1, 180),
        new("wpn_t2_coil", WeaponKind.Bow, 2, 600),
        new("wpn_t2_gwoljangno", WeaponKind.Crossbow, 2, 600),
        new("wpn_t2_seungja", WeaponKind.ScatterGun, 2, 600),
        new("wpn_t3_acid", WeaponKind.Bow, 3, 1600),
        new("wpn_t3_pyeonjeon", WeaponKind.Pyeonjeon, 3, 1600),
        new("wpn_t3_soseungja", WeaponKind.Gun, 3, 1600),
        new("wpn_t3_sunogi", WeaponKind.RepeatingCrossbow, 3, 1600),
        new("wpn_t4_breaker", WeaponKind.Bow, 4, 4200),
        new("wpn_t4_byeolseungja", WeaponKind.Gun, 4, 4200),
        new("wpn_t4_jangpyeonjeon", WeaponKind.Pyeonjeon, 4, 4200),
        new("wpn_t4_sosingijeon", WeaponKind.Rocket, 4, 4200),
        new("wpn_t4_yongdu", WeaponKind.RepeatingCrossbow, 4, 4200),
        new("wpn_t5_cheonbo", WeaponKind.Pyeonjeon, 5, 9000),
        new("wpn_t5_gangno", WeaponKind.Crossbow, 5, 9000),
        new("wpn_t5_jungsingijeon", WeaponKind.Rocket, 5, 9000),
        new("wpn_t5_paljeon", WeaponKind.ScatterGun, 5, 9000),
        new("wpn_t5_rail", WeaponKind.Bow, 5, 9000),
        new("wpn_t6_cheonja", WeaponKind.ScatterGun, 6, 20000),
        new("wpn_t6_eraser", WeaponKind.Bow, 6, 20000),
        new("wpn_t6_sanhwa", WeaponKind.Rocket, 6, 20000),
        new("wpn_t6_sujil", WeaponKind.RepeatingCrossbow, 6, 20000),
    };

    /// <summary>
    /// 그 무기를 열 때 바칠 견본 — 같은 종류에서 티어가 바로 아래인 것.
    /// 같은 종류에 더 낮은 것이 없으면(소신기전) 화약을 쓰는 총통 중 바로 아래 티어 [임시].
    /// </summary>
    public static string SampleFor(WeaponRow target)
    {
        WeaponRow? best = null;

        foreach (WeaponRow w in Weapons)
            if (w.Kind == target.Kind && w.Tier < target.Tier && (best == null || w.Tier > best.Value.Tier))
                best = w;

        if (best != null)
            return best.Value.Id;

        foreach (WeaponRow w in Weapons)
            if (w.Kind == WeaponKind.Gun && w.Tier < target.Tier && (best == null || w.Tier > best.Value.Tier))
                best = w;

        return best?.Id;
    }

    // ── 방어구 (Ⅱ · Ⅲ) ────────────────────────────────────────────────

    private static readonly (string id, int tier, int value)[] Armour =
    {
        ("arm_head_t1", 1, 120), ("arm_head_t2", 2, 420), ("arm_head_t3", 3, 1200),
        ("arm_body_t1", 1, 200), ("arm_body_t1_light", 1, 200), ("arm_body_t1_heavy", 1, 200),
        ("arm_body_t2", 2, 700), ("arm_body_t2_light", 2, 700), ("arm_body_t2_heavy", 2, 700),
        ("arm_body_t3", 3, 1900), ("arm_body_t3_light", 3, 1900), ("arm_body_t3_heavy", 3, 1900),
        ("arm_ears_t1", 1, 90), ("arm_ears_t2", 2, 300), ("arm_ears_t3", 3, 900),
    };

    private static readonly (string id, int tier, int value)[] Masks =
    {
        ("arm_face_t1", 1, 150),
        ("arm_face_physical_t2", 2, 600), ("arm_face_fire_t2", 2, 600), ("arm_face_cold_t2", 2, 600),
        ("arm_face_lightning_t2", 2, 600), ("arm_face_chaos_t2", 2, 600),
    };

    // ── 제작법 ────────────────────────────────────────────────────────

    private static List<CraftRecipe> recipes;

    public static IReadOnlyList<CraftRecipe> All => recipes ??= Build();

    public static CraftRecipe Find(string outputId)
    {
        foreach (CraftRecipe r in All)
            if (r.OutputId == outputId)
                return r;

        return null;
    }

    public static IEnumerable<CraftRecipe> InStage(int stage)
    {
        foreach (CraftRecipe r in All)
            if (r.Stage == stage)
                yield return r;
    }

    private static List<CraftRecipe> Build()
    {
        var list = new List<CraftRecipe>();

        // Ⅰ — 탄 7 · 횃불 · 면포 (덕코프 기본 작업대: 탄 · 간단한 도구) [임시]
        list.Add(new CraftRecipe(AmmoTable.Arrow, 30, 1, new[] { M(Scrap, 1), M(Rope, 1) }, 0));
        list.Add(new CraftRecipe(AmmoTable.Pyeonjeon, 20, 1, new[] { M(Scrap, 1), M(Rope, 1) }, 0));
        list.Add(new CraftRecipe(AmmoTable.Bolt, 15, 1, new[] { M(Scrap, 2) }, 0));
        list.Add(new CraftRecipe(AmmoTable.Dart, 30, 1, new[] { M(Scrap, 1), M(Rope, 1) }, 0));
        list.Add(new CraftRecipe(AmmoTable.Shot, 20, 1, new[] { M(Scrap, 1), M(Charcoal, 1) }, 0));
        list.Add(new CraftRecipe(AmmoTable.Scatter, 8, 1, new[] { M(Scrap, 1), M(Charcoal, 1) }, 0));
        list.Add(new CraftRecipe(AmmoTable.Rocket, 4, 1, new[] { M(Scrap, 2), M(Charcoal, 2) }, 0));
        list.Add(new CraftRecipe("con_guard_light", 1, 1, new[] { M(Rope, 1), M(Charcoal, 1) }, 0));   // 횃불
        list.Add(new CraftRecipe("con_guard_shield", 1, 1, new[] { M(Rope, 2) }, 0));                   // 면포

        // Ⅱ — 방어구 티어 1~3 · 견본(그 방어구)을 바쳐 연다
        foreach ((string id, int tier, int value) in Armour)
            list.Add(new CraftRecipe(id, 1, 2,
                tier >= 2
                    ? new[] { M(Scrap, tier * 2), M(Rope, tier), M(Charcoal, tier - 1) }
                    : new[] { M(Scrap, 2), M(Rope, 1) },
                value * 3 / 10,
                new[] { M(id, 1) }));

        // Ⅲ — 방어 관통탄 7 · 견본 + 그 탄이 처음 나오는 장의 장별 재료 하나 / 방호구(탈 1 · 2) · 견본
        foreach (AmmoInfo a in AmmoTable.All)
        {
            if (!a.IsArmourPiercing)
                continue;

            int batch = Math.Max(4, AmmoTable.CapacityOf(a.Family));
            list.Add(new CraftRecipe(a.Id, batch, 3,
                new[] { M(a.Family, batch), M(Scrap, 2), M(Charcoal, 1) }, 40,
                new[] { M(a.Id, 1), M(ChapterMaterialOf(a.FirstTier), 1) }));
        }

        foreach ((string id, int tier, int value) in Masks)
            list.Add(new CraftRecipe(id, 1, 3,
                new[] { M(Rope, 2 * tier), M(Herb, tier), M(Charcoal, tier) },
                value * 3 / 10,
                new[] { M(id, 1) }));

        // Ⅳ — 무기 티어 4~6 · 도면 + 바로 아래 티어 견본
        foreach (WeaponRow w in Weapons)
        {
            if (w.Tier < 4)
                continue;

            string sample = SampleFor(w);
            var unlock = sample != null
                ? new[] { M(BlueprintOf(w.Kind), 1), M(sample, 1) }
                : new[] { M(BlueprintOf(w.Kind), 1) };

            list.Add(new CraftRecipe(w.Id, 1, 4,
                new[] { M(Scrap, w.Tier * 3), M(Charcoal, w.Tier), M(Trinket, w.Tier - 3), M(Rope, 2) },
                w.Value * 4 / 10,
                unlock));
        }

        return list;
    }

    /// <summary>그 장의 장별 재료 하나(Ⅲ에 바친다) [임시] — 그 장에서만 나는 첫 재료.</summary>
    public static string ChapterMaterialOf(int chapter)
    {
        List<Ingredient> only = IngredientTable.ChapterOnly(chapter);
        return only != null && only.Count > 0 ? only[0].Id : IngredientTable.Rice;
    }
}
