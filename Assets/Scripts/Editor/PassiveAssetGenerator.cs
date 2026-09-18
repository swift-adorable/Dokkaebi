using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 패시브 트리 에셋을 만드는 에디터 도구. (로드맵 6-G)
///
/// 【전투 수치가 하나도 없다는 점을 확인하며 읽을 것.】
/// 방어도·피해·체력·이동·감지는 전부 장비의 몫이다.
/// 여기에 그것을 넣는 순간 장비를 고를 이유가 사라진다.
///
/// 배치는 스크린샷의 「스킬 강화」와 같다 — 아래에서 위로 자라고,
/// 가로선(LEVEL n)이 계정 레벨 구간을 나눈다.
/// </summary>
public static class PassiveAssetGenerator
{
    private const string TreeAssetPath = "Assets/Resources/PassiveTree.asset";

    private const int Columns = 4;
    private const int Rows = 4;

    private struct Row
    {
        public string id;
        public string name;
        public string desc;
        public PassiveEffectType effect;
        public float value;
        public int level;
        public int cost;
        public string[] prerequisites;
        public int column;
        public int row;
    }

    /// <summary>
    /// 표. 행(row)이 0이 맨 아래다.
    ///
    /// 뿌리 3개를 아래에 두고 위로 좁아지게 했다 —
    /// 초반에는 무엇을 먼저 배울지 고르는 폭이 넓고,
    /// 위로 갈수록 이미 고른 길에 묶인다.
    /// </summary>
    private static List<Row> BuildTable()
    {
        return new List<Row>
        {
            // ── 0행 : 뿌리 3개 ──────────────────────────────────────────
            New("carry_1", "가방 전문가 1", "가방에 물건을 더 효율적으로 넣는 법을 익혔다.",
                PassiveEffectType.CarrySlots, 2, level: 1, cost: 800,
                prereq: null, column: 0, row: 0),

            New("absorb_1", "포식 본능 1", "시체에서 더 많은 것을 끌어낸다.",
                PassiveEffectType.AbsorbAmount, 15, level: 1, cost: 800,
                prereq: null, column: 2, row: 0),

            New("trade_1", "흥정 1", "상인이 조금 더 쳐 준다.",
                PassiveEffectType.SellPrice, 10, level: 1, cost: 600,
                prereq: null, column: 3, row: 0),

            // ── 1행 ────────────────────────────────────────────────────
            New("weight_1", "등짐 1", "같은 무게를 덜 무겁게 진다.",
                PassiveEffectType.CarryWeight, 8, level: 3, cost: 1400,
                prereq: new[] { "carry_1" }, column: 0, row: 1),

            New("scavenger_1", "수색 1", "남이 놓친 것을 찾아낸다.",
                PassiveEffectType.LootRolls, 1, level: 3, cost: 1600,
                prereq: new[] { "absorb_1" }, column: 2, row: 1),

            New("stash_1", "창고 정리 1", "벙커 창고를 넓혔다.",
                PassiveEffectType.StashSlots, 20, level: 3, cost: 1200,
                prereq: new[] { "trade_1" }, column: 3, row: 1),

            // ── 2행 : LEVEL 5 가로선 위 ─────────────────────────────────
            New("carry_2", "가방 전문가 2", "가방 공간 +4.",
                PassiveEffectType.CarrySlots, 4, level: 5, cost: 2600,
                prereq: new[] { "weight_1" }, column: 0, row: 2),

            New("craft_bench", "제작대", "벙커에서 물건을 만들 수 있게 된다.",
                PassiveEffectType.CraftBench, 1, level: 5, cost: 3000,
                prereq: new[] { "stash_1" }, column: 3, row: 2),

            New("rare_1", "감정안", "희귀한 것이 더 자주 눈에 띈다.",
                PassiveEffectType.RareDropRate, 12, level: 5, cost: 2800,
                prereq: new[] { "scavenger_1" }, column: 2, row: 2),

            // ── 3행 : 정점 3개 ──────────────────────────────────────────
            New("weight_2", "등짐 2", "소지 중량 +14 kg.",
                PassiveEffectType.CarryWeight, 14, level: 8, cost: 4800,
                prereq: new[] { "carry_2" }, column: 0, row: 3),

            New("corpse_recovery", "회수 계약",
                "추출에 실패해도 시체에서 한 번은 되찾아 올 수 있다.",
                PassiveEffectType.CorpseRecovery, 1, level: 8, cost: 6000,
                prereq: new[] { "rare_1" }, column: 2, row: 3),

            New("map_loot", "지도 표식", "지도에 전리품 위치가 표시된다.",
                PassiveEffectType.MapLoot, 1, level: 8, cost: 4200,
                prereq: new[] { "craft_bench" }, column: 3, row: 3)
        };
    }

    private static Row New(string id, string name, string desc,
                           PassiveEffectType effect, float value,
                           int level, int cost, string[] prereq, int column, int row)
    {
        return new Row
        {
            id = id, name = name, desc = desc,
            effect = effect, value = value,
            level = level, cost = cost,
            prerequisites = prereq, column = column, row = row
        };
    }

    [MenuItem("Blob/Passive/패시브 에셋 생성")]
    public static void Generate()
    {
        EnsureFolder("Assets/Data");
        EnsureFolder("Assets/Data/ScriptableObjects");
        EnsureFolder(PassiveTree.NodeFolder);
        EnsureFolder("Assets/Resources");

        List<Row> rows = BuildTable();
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

            node.EditorSet(row.id, row.name, row.desc, row.effect, row.value,
                row.level, row.cost, row.prerequisites, row.column, row.row);

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

        Debug.Log($"[PassiveAssetGenerator] 패시브 {nodes.Count}칸 생성 완료");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int split = path.LastIndexOf('/');

        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }
}
