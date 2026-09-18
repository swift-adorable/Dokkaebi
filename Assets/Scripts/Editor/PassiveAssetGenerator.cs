using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 패시브 트리 에셋을 만드는 에디터 도구. (docs/Blob_Passive_System.md)
///
/// 【표를 읽을 때 확인할 것 — 전투 수치가 하나도 없다.】
/// 방어도·피해·체력·이동·시야·감지는 전부 장비의 몫이다.
/// 여기에 그것을 넣는 순간 장비를 고를 이유가 사라진다.
///
/// 구조는 덕코프 「스킬」에서 가져왔다 (research/duckov_스킬.md) —
///   · 독립 5계열                     [확인됨]
///   · 필요물품(아이템) 요구          [확인됨 — 표에 열이 존재]
///   · 다이아몬드 (갈라졌다 합류)     [확인됨 — 영양 관리 3→4·5→6]
///   · 교차 선행 (계열을 넘는 선행)   [확인됨 — 식이요법 = 낚시3 + 영양3]
///   · 계단형 단순 계열               [확인됨 — 블랙마켓 통신 I~V]
/// </summary>
public static class PassiveAssetGenerator
{
    private const string TreeAssetPath = "Assets/Resources/PassiveTree.asset";
    private const string LootRoot = "Assets/Data/ScriptableObjects/Items/Loot";

    /// <summary>한 계열의 격자. 계열마다 같은 크기를 쓴다.</summary>
    private const int Columns = 3;
    private const int Rows = 4;

    private struct Row
    {
        public string id;
        public string name;
        public string desc;
        public PassiveBranch branch;
        public PassiveEffectType effect;
        public float value;
        public int level;
        public int cost;
        public (string item, int count)[] materials;
        public string[] prerequisites;
        public int column;
        public int row;
    }

    private static Row N(string id, string name, string desc, PassiveBranch branch,
                         PassiveEffectType effect, float value, int level, int cost,
                         int column, int row,
                         string[] prereq = null, (string, int)[] materials = null)
    {
        return new Row
        {
            id = id, name = name, desc = desc, branch = branch,
            effect = effect, value = value, level = level, cost = cost,
            materials = materials, prerequisites = prereq,
            column = column, row = row
        };
    }

    /// <summary>
    /// 표. row 0이 맨 아래다.
    ///
    /// 비용 설계 —
    ///   하위 : 크레딧만            (바로 배운다)
    ///   중간 : + 흔한 재료          (고철 · 전선 뭉치)
    ///   상위 : + 귀한 재료          (메모리 코어 · 조직 샘플)
    /// 크레딧만 쓰면 시간을 들이면 전부 열린다. 재료가 들어가야
    /// "이 메모리 코어를 팔까, 패시브에 쓸까"가 생긴다.
    /// </summary>
    private static List<Row> BuildTable()
    {
        var rows = new List<Row>();

        // ── 적응 — 얼마나 들고 나가는가 ───────────────────────────────
        // 뿌리 하나에서 둘로 갈라졌다가 위에서 합류하는 다이아몬드.
        rows.Add(N("adapt_slots_1", "가방 정리 1", "물건을 넣는 순서를 익혔다.",
            PassiveBranch.Adapt, PassiveEffectType.CarrySlots, 2, level: 1, cost: 700,
            column: 1, row: 0));

        rows.Add(N("adapt_slots_2", "가방 정리 2", "빈틈을 남기지 않는다.",
            PassiveBranch.Adapt, PassiveEffectType.CarrySlots, 3, level: 4, cost: 1800,
            column: 0, row: 1, prereq: new[] { "adapt_slots_1" },
            materials: new[] { ("scrap_metal", 8) }));

        rows.Add(N("adapt_weight_1", "등짐", "같은 무게를 덜 무겁게 진다.",
            PassiveBranch.Adapt, PassiveEffectType.CarryWeight, 8, level: 4, cost: 1800,
            column: 2, row: 1, prereq: new[] { "adapt_slots_1" },
            materials: new[] { ("wire_bundle", 6) }));

        rows.Add(N("adapt_frame", "외골격 프레임",
            "몸 밖에 뼈대를 하나 더 얹었다. 무게와 공간을 함께 번다.",
            PassiveBranch.Adapt, PassiveEffectType.CarryWeight, 12, level: 8, cost: 4200,
            column: 1, row: 2, prereq: new[] { "adapt_slots_2", "adapt_weight_1" },
            materials: new[] { ("cell_battery", 4), ("scrap_metal", 12) }));

        rows.Add(N("adapt_slots_3", "가방 정리 3", "가방 공간 +3.",
            PassiveBranch.Adapt, PassiveEffectType.CarrySlots, 3, level: 12, cost: 7500,
            column: 1, row: 3, prereq: new[] { "adapt_frame" },
            materials: new[] { ("memory_core", 2) }));

        // ── 대사 — 얼마나 얻는가 ──────────────────────────────────────
        rows.Add(N("meta_absorb_1", "포식 본능 1", "시체에서 더 많은 것을 끌어낸다.",
            PassiveBranch.Metabolism, PassiveEffectType.AbsorbAmount, 15, level: 1, cost: 700,
            column: 1, row: 0));

        rows.Add(N("meta_range", "촉수 연장", "닿는 거리가 늘었다.",
            PassiveBranch.Metabolism, PassiveEffectType.AbsorbRange, 1.5f, level: 3, cost: 1500,
            column: 0, row: 1, prereq: new[] { "meta_absorb_1" },
            materials: new[] { ("bio_sample", 4) }));

        rows.Add(N("meta_scav_1", "수색", "남이 놓친 것을 찾아낸다.",
            PassiveBranch.Metabolism, PassiveEffectType.LootRolls, 1, level: 3, cost: 1700,
            column: 2, row: 1, prereq: new[] { "meta_absorb_1" },
            materials: new[] { ("scrap_metal", 6) }));

        rows.Add(N("meta_absorb_2", "포식 본능 2", "경험치 획득 +20%.",
            PassiveBranch.Metabolism, PassiveEffectType.AbsorbAmount, 20, level: 7, cost: 3600,
            column: 1, row: 2, prereq: new[] { "meta_range", "meta_scav_1" },
            materials: new[] { ("bio_sample", 8) }));

        rows.Add(N("meta_rare", "감정안", "희귀한 것이 더 자주 눈에 띈다.",
            PassiveBranch.Metabolism, PassiveEffectType.RareDropRate, 12, level: 11, cost: 6800,
            column: 1, row: 3, prereq: new[] { "meta_absorb_2" },
            materials: new[] { ("memory_core", 2), ("bio_sample", 6) }));

        // ── 회수 — 죽어도 무엇이 남는가 ───────────────────────────────
        // 【사망 규칙의 유일한 예외 계열이다.】 예외를 늘리지 않기 위해 여기 밖에 두지 않는다.
        rows.Add(N("rec_safe_1", "속주머니",
            "안감을 뜯어 만든 자리. 죽어도 한 칸은 남는다.",
            PassiveBranch.Recovery, PassiveEffectType.SafeSlots, 1, level: 5, cost: 2500,
            column: 1, row: 0, materials: new[] { ("wire_bundle", 8) }));

        rows.Add(N("rec_mark", "추출 좌표",
            "지도에 추출 지점이 상시 표시된다.",
            PassiveBranch.Recovery, PassiveEffectType.ExtractMark, 1, level: 6, cost: 2200,
            column: 0, row: 1, prereq: new[] { "rec_safe_1" }));

        rows.Add(N("rec_corpse", "회수 계약",
            "추출에 실패해도 내 시체에서 한 번은 되찾아 올 수 있다.",
            PassiveBranch.Recovery, PassiveEffectType.CorpseRecovery, 1, level: 9, cost: 5500,
            column: 2, row: 1, prereq: new[] { "rec_safe_1" },
            materials: new[] { ("cell_battery", 6) }));

        rows.Add(N("rec_safe_2", "이중 속주머니", "죽어도 지키는 칸 +1. (총 2칸)",
            PassiveBranch.Recovery, PassiveEffectType.SafeSlots, 1, level: 13, cost: 9000,
            column: 1, row: 2, prereq: new[] { "rec_mark", "rec_corpse" },
            materials: new[] { ("memory_core", 3) }));

        // ── 중개 — 벙커 경제 (계단형. 레벨을 보지 않는다) ─────────────
        // 전부 결정이면 피로하다. 고민 없이 쌓는 갈래가 하나쯤 있어야 한다.
        rows.Add(N("brok_sell_1", "흥정 1", "상인이 조금 더 쳐 준다.",
            PassiveBranch.Brokerage, PassiveEffectType.SellPrice, 8, level: 1, cost: 600,
            column: 0, row: 0));

        rows.Add(N("brok_stash_1", "창고 정리 1", "벙커 창고를 넓혔다.",
            PassiveBranch.Brokerage, PassiveEffectType.StashSlots, 20, level: 1, cost: 900,
            column: 2, row: 0));

        rows.Add(N("brok_sell_2", "흥정 2", "판매가 +8%.",
            PassiveBranch.Brokerage, PassiveEffectType.SellPrice, 8, level: 1, cost: 1800,
            column: 0, row: 1, prereq: new[] { "brok_sell_1" }));

        rows.Add(N("brok_stash_2", "창고 정리 2", "창고 칸 +20.",
            PassiveBranch.Brokerage, PassiveEffectType.StashSlots, 20, level: 1, cost: 2400,
            column: 2, row: 1, prereq: new[] { "brok_stash_1" }));

        rows.Add(N("brok_refresh_1", "거래선 1", "상점 갱신 쿨타임 −10%.",
            PassiveBranch.Brokerage, PassiveEffectType.ShopRefresh, 10, level: 1, cost: 3000,
            column: 0, row: 2, prereq: new[] { "brok_sell_2" }));

        rows.Add(N("brok_slots_1", "정보용량 1", "상점 갱신 횟수 +1.",
            PassiveBranch.Brokerage, PassiveEffectType.ShopSlots, 1, level: 1, cost: 3600,
            column: 2, row: 2, prereq: new[] { "brok_stash_2" }));

        rows.Add(N("brok_refresh_2", "거래선 2", "상점 갱신 쿨타임 −10%.",
            PassiveBranch.Brokerage, PassiveEffectType.ShopRefresh, 10, level: 1, cost: 5400,
            column: 0, row: 3, prereq: new[] { "brok_refresh_1" }));

        rows.Add(N("brok_slots_2", "정보용량 2", "상점 갱신 횟수 +1.",
            PassiveBranch.Brokerage, PassiveEffectType.ShopSlots, 1, level: 1, cost: 6200,
            column: 2, row: 3, prereq: new[] { "brok_slots_1" }));

        // ── 역행 — 조우해야 보인다 ────────────────────────────────────
        // 【교차 선행】 다른 계열의 칸을 요구한다. 한 갈래만 파면 닿지 않는다.
        rows.Add(N("reg_bench", "제작대",
            "역행자가 남긴 도면. 벙커에서 물건을 만들 수 있게 된다.",
            PassiveBranch.Regression, PassiveEffectType.CraftBench, 1, level: 1, cost: 3000,
            column: 1, row: 0, materials: new[] { ("memory_core", 1) }));

        rows.Add(N("reg_salvage", "역분해",
            "인자를 도로 풀어 재료로 되돌린다. 쓸모없는 인자가 사라진다.",
            PassiveBranch.Regression, PassiveEffectType.GemSalvage, 1, level: 1, cost: 4500,
            column: 0, row: 1, prereq: new[] { "reg_bench" },
            materials: new[] { ("bio_sample", 6) }));

        rows.Add(N("reg_codex", "기록 역산",
            "처치만 해도 도감에 등록된다. 변이 샘플을 흡수할 필요가 없어진다.",
            PassiveBranch.Regression, PassiveEffectType.CodexAuto, 1, level: 1, cost: 6500,
            column: 2, row: 1, prereq: new[] { "reg_bench", "meta_absorb_2" },
            materials: new[] { ("memory_core", 3) }));

        rows.Add(N("reg_map", "누락된 지도",
            "지도에 전리품 위치가 표시된다. 원래 없던 표식이다.",
            PassiveBranch.Regression, PassiveEffectType.MapLoot, 1, level: 1, cost: 8000,
            column: 1, row: 2, prereq: new[] { "reg_salvage", "reg_codex" },
            materials: new[] { ("memory_core", 4), ("cell_battery", 8) }));

        return rows;
    }

    [MenuItem("Blob/Passive/패시브 에셋 생성")]
    public static void Generate()
    {
        EnsureFolder("Assets/Data");
        EnsureFolder("Assets/Data/ScriptableObjects");
        EnsureFolder(PassiveTree.NodeFolder);
        EnsureFolder("Assets/Resources");

        List<Row> rows = BuildTable();

        if (!Validate(rows))
            return;

        var nodes = new List<PassiveNode>(rows.Count);

        foreach (Row row in rows)
        {
            string path = $"{PassiveTree.NodeFolder}/{row.id}.asset";

            var node = AssetDatabase.LoadAssetAtPath<PassiveNode>(path);

            if (node == null)
            {
                node = ScriptableObject.CreateInstance<PassiveNode>();
                AssetDatabase.CreateAsset(node, path);
            }

            node.EditorSet(row.id, row.name, row.desc, row.branch,
                row.effect, row.value, row.level, row.cost,
                ResolveMaterials(row.materials), row.prerequisites,
                row.column, row.row);

            EditorUtility.SetDirty(node);

            nodes.Add(node);
        }

        var tree = AssetDatabase.LoadAssetAtPath<PassiveTree>(TreeAssetPath);

        if (tree == null)
        {
            tree = ScriptableObject.CreateInstance<PassiveTree>();
            AssetDatabase.CreateAsset(tree, TreeAssetPath);
        }

        tree.EditorSetNodes(nodes, Columns, Rows);

        EditorUtility.SetDirty(tree);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PassiveAssetGenerator] 패시브 {nodes.Count}칸 / {PassiveBranchInfo.Count}계열 생성 완료");
    }

    /// <summary>표 자체의 실수를 생성 전에 잡는다. 에셋을 만든 뒤에는 찾기 어렵다.</summary>
    private static bool Validate(List<Row> rows)
    {
        var ids = new HashSet<string>();
        bool ok = true;

        foreach (Row row in rows)
        {
            if (!ids.Add(row.id))
            {
                Debug.LogError($"[PassiveAssetGenerator] id 중복: {row.id}");
                ok = false;
            }

            if (row.column >= Columns || row.row >= Rows)
            {
                Debug.LogError(
                    $"[PassiveAssetGenerator] {row.id}의 자리가 격자를 벗어납니다 " +
                    $"({row.column},{row.row}) / {Columns}×{Rows}");
                ok = false;
            }
        }

        foreach (Row row in rows)
        {
            if (row.prerequisites == null)
                continue;

            foreach (string prerequisite in row.prerequisites)
            {
                if (!ids.Contains(prerequisite))
                {
                    Debug.LogError(
                        $"[PassiveAssetGenerator] {row.id}의 선행 '{prerequisite}'이 표에 없습니다.");
                    ok = false;
                }
            }
        }

        // 같은 계열 · 같은 자리에 둘이 놓이면 하나가 화면에서 사라진다.
        var taken = new HashSet<(PassiveBranch, int, int)>();

        foreach (Row row in rows)
        {
            if (!taken.Add((row.branch, row.column, row.row)))
            {
                Debug.LogError(
                    $"[PassiveAssetGenerator] {row.branch} 계열의 ({row.column},{row.row})가 겹칩니다.");
                ok = false;
            }
        }

        return ok;
    }

    private static PassiveMaterial[] ResolveMaterials((string item, int count)[] source)
    {
        if (source == null || source.Length == 0)
            return new PassiveMaterial[0];

        var result = new List<PassiveMaterial>(source.Length);

        foreach ((string itemId, int count) in source)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{LootRoot}/{itemId}.asset");

            if (item == null)
            {
                Debug.LogWarning(
                    $"[PassiveAssetGenerator] 재료 '{itemId}'를 찾지 못했습니다. " +
                    "메뉴 Blob > Loot > 전리품 에셋 생성 을 먼저 실행하십시오.");
                continue;
            }

            result.Add(new PassiveMaterial(item, count));
        }

        return result.ToArray();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int split = path.LastIndexOf('/');

        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }
}
