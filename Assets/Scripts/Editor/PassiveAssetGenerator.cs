using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 패시브 트리 에셋을 만드는 에디터 도구. (docs/Dokkaebi_Passive_System.md)
///
/// 【표를 읽을 때 확인할 것 — 전투 수치가 하나도 없다.】
/// 방어도·피해·체력·이동·시야·감지는 전부 장비의 몫이다.
/// 여기에 그것을 넣는 순간 장비를 고를 이유가 사라진다.
///
/// 구조는 덕코프 「스킬」에서 가져왔다 (research/duckov_스킬.md) —
///   · 독립 5계열                     [확인됨]
///   · 필요 재료(아이템) 요구          [확인됨 — 표에 열이 존재]
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
    private const int Rows = 8;   // 4 → 6 (결정 2-139) → 8 (결정 2-141 — 단련 · 무예와 위 단계)

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
    ///   하위 : 골드만            (바로 배운다)
    ///   중간 : + 흔한 재료          (쇠붙이 · 새끼 뭉치)
    ///   상위 : + 귀한 재료          (노리개 · 약재)
    /// 골드만 쓰면 시간을 들이면 전부 열린다. 재료가 들어가야
    /// "이 노리개를 팔까, 패시브에 쓸까"가 생긴다.
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
            PassiveBranch.Adapt, PassiveEffectType.CarryWeight, 6, level: 4, cost: 1800,
            column: 2, row: 1, prereq: new[] { "adapt_slots_1" },
            materials: new[] { ("wire_bundle", 6) }));

        rows.Add(N("adapt_frame", "멜빵",
            "몸 밖에 뼈대를 하나 더 얹었다. 무게와 공간을 함께 번다.",
            PassiveBranch.Adapt, PassiveEffectType.CarryWeight, 9, level: 8, cost: 4200,
            column: 1, row: 2, prereq: new[] { "adapt_slots_2", "adapt_weight_1" },
            materials: new[] { ("cell_battery", 4), ("scrap_metal", 12) }));

        rows.Add(N("adapt_slots_3", "가방 정리 3", "가방 공간 +3.",
            PassiveBranch.Adapt, PassiveEffectType.CarrySlots, 3, level: 12, cost: 7500,
            column: 1, row: 3, prereq: new[] { "adapt_frame" },
            materials: new[] { ("memory_core", 2) }));

        // 결정 2-139 — 위로 두 줄: 자루 꿰매기 · 가방 정리 4 → 봇짐장수 (모두 가방 칸 — 중량은 상한)
        rows.Add(N("adapt_weight_2", "자루 꿰매기", "자루 안쪽에 주머니를 덧댔다. 가방 공간 +2. (소지 중량은 상한 15kg에 이미 닿아 칸으로)",
            PassiveBranch.Adapt, PassiveEffectType.CarrySlots, 2, level: 14, cost: 9000,
            column: 0, row: 4, prereq: new[] { "adapt_slots_3" },
            materials: new[] { ("wire_bundle", 12) }));

        rows.Add(N("adapt_slots_4", "가방 정리 4", "작은 것은 작은 것끼리. 가방 공간 +3.",
            PassiveBranch.Adapt, PassiveEffectType.CarrySlots, 3, level: 16, cost: 12000,
            column: 2, row: 4, prereq: new[] { "adapt_slots_3" },
            materials: new[] { ("memory_core", 3) }));

        rows.Add(N("adapt_master", "봇짐장수", "장터 길을 평생 걸은 사람처럼 챙긴다. 가방 공간 +2.",
            PassiveBranch.Adapt, PassiveEffectType.CarrySlots, 2, level: 20, cost: 20000,
            column: 1, row: 5, prereq: new[] { "adapt_weight_2", "adapt_slots_4" },
            materials: new[] { ("memory_core", 4), ("cell_battery", 6) }));

        // ── 대사 — 얼마나 얻는가 ──────────────────────────────────────
        rows.Add(N("meta_absorb_1", "정기 거두기 1", "허물에서 더 많은 것을 끌어낸다.",
            PassiveBranch.Metabolism, PassiveEffectType.AbsorbAmount, 15, level: 1, cost: 700,
            column: 1, row: 0));

        rows.Add(N("meta_range", "긴 손", "닿는 거리가 늘었다.",
            PassiveBranch.Metabolism, PassiveEffectType.AbsorbRange, 1.5f, level: 3, cost: 1500,
            column: 0, row: 1, prereq: new[] { "meta_absorb_1" },
            materials: new[] { ("bio_sample", 4) }));

        rows.Add(N("meta_scav_1", "수색", "남이 놓친 것을 찾아낸다.",
            PassiveBranch.Metabolism, PassiveEffectType.LootRolls, 1, level: 3, cost: 1700,
            column: 2, row: 1, prereq: new[] { "meta_absorb_1" },
            materials: new[] { ("scrap_metal", 6) }));

        rows.Add(N("meta_absorb_2", "정기 거두기 2", "정기 획득 +20%.",
            PassiveBranch.Metabolism, PassiveEffectType.AbsorbAmount, 20, level: 7, cost: 3600,
            column: 1, row: 2, prereq: new[] { "meta_range", "meta_scav_1" },
            materials: new[] { ("bio_sample", 8) }));

        rows.Add(N("meta_rare", "감정안", "희귀한 것이 더 자주 눈에 띈다.",
            PassiveBranch.Metabolism, PassiveEffectType.RareDropRate, 12, level: 11, cost: 6800,
            column: 1, row: 3, prereq: new[] { "meta_absorb_2" },
            materials: new[] { ("memory_core", 2), ("bio_sample", 6) }));

        // 결정 2-139 — 덕코프 「영양 관리」: 수분 · 기력 소모를 줄인다(양 옆 줄). 위로 두 줄: 샅샅이 · 보는 눈 · 정기 거두기 3 → 소식
        rows.Add(N("meta_water_1", "물 아끼기 1", "목을 축이는 법을 안다. 수분 소모 −10%.",
            PassiveBranch.Metabolism, PassiveEffectType.WaterDrain, 10, level: 2, cost: 1200,
            column: 0, row: 0));

        rows.Add(N("meta_food_1", "허기 참기 1", "배고픔을 오래 참는다. 기력 소모 −10%.",
            PassiveBranch.Metabolism, PassiveEffectType.EnergyDrain, 10, level: 2, cost: 1200,
            column: 2, row: 0));

        rows.Add(N("meta_water_2", "물 아끼기 2", "수분 소모 −10%.",
            PassiveBranch.Metabolism, PassiveEffectType.WaterDrain, 10, level: 6, cost: 3000,
            column: 0, row: 2, prereq: new[] { "meta_water_1", "meta_range" },
            materials: new[] { ("bio_sample", 4) }));

        rows.Add(N("meta_food_2", "허기 참기 2", "기력 소모 −10%.",
            PassiveBranch.Metabolism, PassiveEffectType.EnergyDrain, 10, level: 6, cost: 3000,
            column: 2, row: 2, prereq: new[] { "meta_food_1", "meta_scav_1" },
            materials: new[] { ("bio_sample", 4) }));

        rows.Add(N("meta_scav_2", "샅샅이", "시체를 한 번 더 뒤진다. 전리품 추첨 +1.",
            PassiveBranch.Metabolism, PassiveEffectType.LootRolls, 1, level: 14, cost: 9000,
            column: 0, row: 4, prereq: new[] { "meta_rare" },
            materials: new[] { ("scrap_metal", 12) }));

        rows.Add(N("meta_rare_2", "보는 눈", "값진 것이 먼저 보인다. 희귀 드롭 +15%.",
            PassiveBranch.Metabolism, PassiveEffectType.RareDropRate, 15, level: 17, cost: 14000,
            column: 1, row: 4, prereq: new[] { "meta_rare" },
            materials: new[] { ("memory_core", 3) }));

        rows.Add(N("meta_absorb_3", "정기 거두기 3", "정기 획득 +25%.",
            PassiveBranch.Metabolism, PassiveEffectType.AbsorbAmount, 25, level: 15, cost: 10000,
            column: 2, row: 4, prereq: new[] { "meta_rare" },
            materials: new[] { ("bio_sample", 10) }));

        rows.Add(N("meta_diet", "소식", "적게 먹고 오래 버틴다. 기력 소모 −15%.",
            PassiveBranch.Metabolism, PassiveEffectType.EnergyDrain, 15, level: 18, cost: 15000,
            column: 1, row: 5, prereq: new[] { "meta_water_2", "meta_food_2", "meta_rare_2" },
            materials: new[] { ("memory_core", 3), ("bio_sample", 8) }));

        // ── 회수 — 죽어도 무엇이 남는가 ───────────────────────────────
        // 【사망 규칙의 유일한 예외 계열이다.】 예외를 늘리지 않기 위해 여기 밖에 두지 않는다.
        rows.Add(N("rec_safe_1", "속주머니",
            "안감을 뜯어 만든 자리. 죽어도 한 칸은 남는다.",
            PassiveBranch.Recovery, PassiveEffectType.SafeSlots, 1, level: 5, cost: 2500,
            column: 1, row: 0, materials: new[] { ("wire_bundle", 8) }));

        rows.Add(N("rec_mark", "돌아갈 길목",
            "지도에 길목이 늘 표시된다.",
            PassiveBranch.Recovery, PassiveEffectType.ExtractMark, 1, level: 6, cost: 2200,
            column: 0, row: 1, prereq: new[] { "rec_safe_1" }));

        rows.Add(N("rec_corpse", "회수 계약",
            "쓰러지면 잃은 것이 그 자리에 남는다. 그 장에 다시 가면 한 번 되찾을 수 있다.",
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

        rows.Add(N("brok_stash_1", "창고 정리 1", "소굴 창고를 넓혔다.",
            PassiveBranch.Brokerage, PassiveEffectType.StashSlots, 20, level: 1, cost: 900,
            column: 2, row: 0));

        rows.Add(N("brok_sell_2", "흥정 2", "판매가 +8%.",
            PassiveBranch.Brokerage, PassiveEffectType.SellPrice, 8, level: 1, cost: 1800,
            column: 0, row: 1, prereq: new[] { "brok_sell_1" }));

        rows.Add(N("brok_stash_2", "창고 정리 2", "창고 칸 +20.",
            PassiveBranch.Brokerage, PassiveEffectType.StashSlots, 20, level: 1, cost: 2400,
            column: 2, row: 1, prereq: new[] { "brok_stash_1" }));

        // 거래선 · 단골 — 결정 2-74: 재고가 파밍마다 가득 차서(2-35) 「갱신」 축이 없었다 → 사는 값 · 재고로 바꿨다.
        rows.Add(N("brok_refresh_1", "거래선 1", "가게에서 사는 값 −10%.",
            PassiveBranch.Brokerage, PassiveEffectType.ShopRefresh, 10, level: 1, cost: 3000,
            column: 0, row: 2, prereq: new[] { "brok_sell_2" }));

        rows.Add(N("brok_slots_1", "단골 1", "가게 물건마다 재고 +1.",
            PassiveBranch.Brokerage, PassiveEffectType.ShopSlots, 1, level: 1, cost: 3600,
            column: 2, row: 2, prereq: new[] { "brok_stash_2" }));

        rows.Add(N("brok_refresh_2", "거래선 2", "가게에서 사는 값 −10%.",
            PassiveBranch.Brokerage, PassiveEffectType.ShopRefresh, 10, level: 1, cost: 5400,
            column: 0, row: 3, prereq: new[] { "brok_refresh_1" }));

        rows.Add(N("brok_slots_2", "단골 2", "가게 물건마다 재고 +1.",
            PassiveBranch.Brokerage, PassiveEffectType.ShopSlots, 1, level: 1, cost: 6200,
            column: 2, row: 3, prereq: new[] { "brok_slots_1" }));

        // 결정 2-139 — 덕코프 「창고 확장」 · 블랙마켓처럼 위로 두 줄: 흥정 3 · 창고 정리 3 → 큰 곳간
        rows.Add(N("brok_sell_3", "흥정 3", "판매가 +8%.",
            PassiveBranch.Brokerage, PassiveEffectType.SellPrice, 8, level: 1, cost: 9000,
            column: 0, row: 4, prereq: new[] { "brok_refresh_2" }));

        rows.Add(N("brok_stash_3", "창고 정리 3", "시렁을 하나 더 달았다. 창고 칸 +30.",
            PassiveBranch.Brokerage, PassiveEffectType.StashSlots, 30, level: 1, cost: 8000,
            column: 2, row: 4, prereq: new[] { "brok_slots_2" }));

        rows.Add(N("brok_stash_4", "큰 곳간", "곳간 벽을 헐어 넓혔다. 창고 칸 +40.",
            PassiveBranch.Brokerage, PassiveEffectType.StashSlots, 40, level: 1, cost: 16000,
            column: 1, row: 5, prereq: new[] { "brok_sell_3", "brok_stash_3" },
            materials: new[] { ("scrap_metal", 20), ("wire_bundle", 10) }));

        // ── 역행 — 조우해야 보인다 ────────────────────────────────────
        // 【교차 선행】 다른 계열의 칸을 요구한다. 한 갈래만 파면 닿지 않는다.
        // 「옛 도면」(작업대 역행 물건)은 뺐다 — 만들 물건이 없고 본문에도 없다 (결정 2-127).

        // 「구슬 깨기」(젬 분해)는 뺐다 — 젬은 강화 · 분해 없이 그대로 쓰기만 한다 (결정 2-74).

        // 「한눈에 알아보기」(처치만 해도 도감 등록)는 뺐다 — 구슬 도감이 없어졌다 (결정 2-75).

        rows.Add(N("reg_map", "옛 지도",
            "지도에 봇짐 · 열매 나무 · 쓰러뜨린 적의 시체가 표시된다.",
            PassiveBranch.Regression, PassiveEffectType.MapLoot, 1, level: 1, cost: 8000,
            column: 1, row: 0, prereq: new[] { "meta_absorb_2" },
            materials: new[] { ("memory_core", 4), ("cell_battery", 8) }));

        // ══ 결정 2-141 — 전투 수치(사용자 「전투 수치도 넣기」) · 덕코프 「피지컬 강화 · 스킬 강화」 참고 ══════════

        // ── 단련 — 체력(왼쪽) · 이동/대시(가운데) · 방어(오른쪽). 아래에서 위로 ──
        var T = PassiveBranch.Training;
        rows.Add(N("train_hp_1", "줄넘기", "숨이 덜 찬다. 최대 체력 +5.", T, PassiveEffectType.MaxHealth, 5, level: 1, cost: 800, column: 0, row: 0));
        rows.Add(N("train_run_1", "달리기", "발이 가볍다. 이동 속도 +3%.", T, PassiveEffectType.MoveSpeed, 3, level: 1, cost: 800, column: 1, row: 0));
        rows.Add(N("train_dash_1", "날랜 발 1", "몸을 빼는 데 망설임이 줄었다. 대시 대기 −5%.", T, PassiveEffectType.DashCooldown, 5, level: 3, cost: 1400, column: 1, row: 1, prereq: new[] { "train_run_1" }));
        rows.Add(N("train_hp_2", "씨름", "버티는 몸. 최대 체력 +5.", T, PassiveEffectType.MaxHealth, 5, level: 3, cost: 1400, column: 0, row: 1, prereq: new[] { "train_hp_1" }));
        rows.Add(N("train_arm_1", "굳은살", "맞아도 덜 아프다. 방어도 +1.", T, PassiveEffectType.Armour, 1, level: 4, cost: 1800, column: 2, row: 1, prereq: new[] { "train_hp_1", "train_run_1" }, materials: new[] { ("wire_bundle", 4) }));
        rows.Add(N("train_run_2", "산길 달리기", "비탈도 평지처럼. 이동 속도 +3%.", T, PassiveEffectType.MoveSpeed, 3, level: 6, cost: 3000, column: 1, row: 2, prereq: new[] { "train_dash_1" }, materials: new[] { ("bio_sample", 3) }));
        rows.Add(N("train_hp_3", "지게 버티기", "무거운 것을 지고도 선다. 최대 체력 +8.", T, PassiveEffectType.MaxHealth, 8, level: 7, cost: 3600, column: 0, row: 3, prereq: new[] { "train_hp_2" }, materials: new[] { ("scrap_metal", 8) }));
        rows.Add(N("train_dash_2", "날랜 발 2", "대시 대기 −5%.", T, PassiveEffectType.DashCooldown, 5, level: 8, cost: 4200, column: 1, row: 3, prereq: new[] { "train_run_2" }, materials: new[] { ("wire_bundle", 6) }));
        rows.Add(N("train_arm_2", "쇠가죽", "방어도 +1.", T, PassiveEffectType.Armour, 1, level: 9, cost: 5000, column: 2, row: 3, prereq: new[] { "train_arm_1" }, materials: new[] { ("scrap_metal", 10) }));
        rows.Add(N("train_run_3", "바람걸음", "이동 속도 +4%.", T, PassiveEffectType.MoveSpeed, 4, level: 11, cost: 7000, column: 1, row: 4, prereq: new[] { "train_dash_2" }, materials: new[] { ("cell_battery", 4) }));
        rows.Add(N("train_hp_4", "강골", "뼈가 굵어졌다. 최대 체력 +10.", T, PassiveEffectType.MaxHealth, 10, level: 13, cost: 9000, column: 0, row: 5, prereq: new[] { "train_hp_3" }, materials: new[] { ("bio_sample", 8) }));
        rows.Add(N("train_dash_3", "날랜 발 3", "대시 대기 −8%.", T, PassiveEffectType.DashCooldown, 8, level: 14, cost: 10000, column: 1, row: 5, prereq: new[] { "train_run_3" }, materials: new[] { ("cell_battery", 5) }));
        rows.Add(N("train_arm_3", "돌가죽", "방어도 +2.", T, PassiveEffectType.Armour, 2, level: 15, cost: 12000, column: 2, row: 5, prereq: new[] { "train_arm_2" }, materials: new[] { ("memory_core", 2), ("scrap_metal", 12) }));
        rows.Add(N("train_run_4", "축지", "땅을 접어 걷는다. 이동 속도 +5%.", T, PassiveEffectType.MoveSpeed, 5, level: 18, cost: 16000, column: 1, row: 6, prereq: new[] { "train_dash_3" }, materials: new[] { ("memory_core", 3) }));
        rows.Add(N("train_hp_5", "금강", "쉽게 쓰러지지 않는다. 최대 체력 +12.", T, PassiveEffectType.MaxHealth, 12, level: 20, cost: 20000, column: 0, row: 7, prereq: new[] { "train_hp_4", "train_run_4" }, materials: new[] { ("memory_core", 4), ("bio_sample", 10) }));
        rows.Add(N("train_dash_4", "날랜 발 4", "대시 대기 −8%.", T, PassiveEffectType.DashCooldown, 8, level: 20, cost: 20000, column: 1, row: 7, prereq: new[] { "train_run_4" }, materials: new[] { ("memory_core", 4), ("cell_battery", 6) }));
        rows.Add(N("train_arm_4", "무쇠몸", "방어도 +2.", T, PassiveEffectType.Armour, 2, level: 22, cost: 24000, column: 2, row: 7, prereq: new[] { "train_arm_3", "train_run_4" }, materials: new[] { ("memory_core", 5), ("scrap_metal", 20) }));

        // ── 무예 — 피해(왼쪽) · 재장전(가운데) · 급소(오른쪽, 피해) ──
        var M = PassiveBranch.Martial;
        rows.Add(N("mart_dmg_1", "무기 다루기 1", "손에 익었다. 무기 피해 +3%.", M, PassiveEffectType.WeaponDamage, 3, level: 1, cost: 900, column: 0, row: 0));
        rows.Add(N("mart_reload_1", "빠른 손 1", "재장전 시간 −5%.", M, PassiveEffectType.ReloadSpeed, 5, level: 2, cost: 1100, column: 1, row: 0));
        rows.Add(N("mart_vital_1", "급소 노리기 1", "어디를 쏠지 안다. 무기 피해 +2%.", M, PassiveEffectType.WeaponDamage, 2, level: 4, cost: 1800, column: 2, row: 1, prereq: new[] { "mart_dmg_1", "mart_reload_1" }));
        rows.Add(N("mart_dmg_2", "무기 다루기 2", "무기 피해 +4%.", M, PassiveEffectType.WeaponDamage, 4, level: 5, cost: 2600, column: 0, row: 2, prereq: new[] { "mart_dmg_1" }, materials: new[] { ("scrap_metal", 8) }));
        rows.Add(N("mart_reload_2", "빠른 손 2", "재장전 시간 −8%.", M, PassiveEffectType.ReloadSpeed, 8, level: 6, cost: 3000, column: 1, row: 2, prereq: new[] { "mart_reload_1" }, materials: new[] { ("wire_bundle", 6) }));
        rows.Add(N("mart_vital_2", "급소 노리기 2", "무기 피해 +2%.", M, PassiveEffectType.WeaponDamage, 2, level: 9, cost: 5000, column: 2, row: 3, prereq: new[] { "mart_vital_1" }, materials: new[] { ("bio_sample", 5) }));
        rows.Add(N("mart_dmg_3", "무기 다루기 3", "무기 피해 +5%.", M, PassiveEffectType.WeaponDamage, 5, level: 10, cost: 6500, column: 0, row: 4, prereq: new[] { "mart_dmg_2" }, materials: new[] { ("scrap_metal", 12) }));
        rows.Add(N("mart_reload_3", "빠른 손 3", "재장전 시간 −10%.", M, PassiveEffectType.ReloadSpeed, 10, level: 11, cost: 7000, column: 1, row: 4, prereq: new[] { "mart_reload_2" }, materials: new[] { ("cell_battery", 4) }));
        rows.Add(N("mart_vital_3", "급소 노리기 3", "무기 피해 +3%.", M, PassiveEffectType.WeaponDamage, 3, level: 15, cost: 11000, column: 2, row: 5, prereq: new[] { "mart_vital_2" }, materials: new[] { ("memory_core", 2) }));
        rows.Add(N("mart_dmg_4", "무기 다루기 4", "무기 피해 +6%.", M, PassiveEffectType.WeaponDamage, 6, level: 16, cost: 13000, column: 0, row: 6, prereq: new[] { "mart_dmg_3" }, materials: new[] { ("memory_core", 3) }));
        rows.Add(N("mart_reload_4", "빠른 손 4", "재장전 시간 −12%.", M, PassiveEffectType.ReloadSpeed, 12, level: 17, cost: 14000, column: 1, row: 6, prereq: new[] { "mart_reload_3" }, materials: new[] { ("memory_core", 3), ("wire_bundle", 10) }));
        rows.Add(N("mart_master", "백발백중", "쏘면 맞고, 맞으면 깊다. 무기 피해 +7%.", M, PassiveEffectType.WeaponDamage, 7, level: 22, cost: 26000, column: 1, row: 7, prereq: new[] { "mart_dmg_4", "mart_reload_4", "mart_vital_3" }, materials: new[] { ("memory_core", 5), ("cell_battery", 8) }));

        // ── 기존 갈래 위 단계 (줄 6 · 7) ──
        rows.Add(N("adapt_slots_5", "가방 정리 5", "가방 공간 +3. (칸 상한: 장비 최대치의 절반)",
            PassiveBranch.Adapt, PassiveEffectType.CarrySlots, 3, level: 24, cost: 26000, column: 1, row: 7,
            prereq: new[] { "adapt_master" }, materials: new[] { ("memory_core", 5) }));
        rows.Add(N("meta_water_3", "물 아끼기 3", "수분 소모 −10%.", PassiveBranch.Metabolism, PassiveEffectType.WaterDrain, 10,
            level: 20, cost: 18000, column: 0, row: 6, prereq: new[] { "meta_scav_2" }, materials: new[] { ("bio_sample", 10) }));
        rows.Add(N("meta_range_2", "긴 손 2", "거두는 범위 +1.5m.", PassiveBranch.Metabolism, PassiveEffectType.AbsorbRange, 1.5f,
            level: 20, cost: 18000, column: 2, row: 6, prereq: new[] { "meta_absorb_3" }, materials: new[] { ("cell_battery", 6) }));
        rows.Add(N("meta_absorb_4", "정기 거두기 4", "정기 획득 +30%.", PassiveBranch.Metabolism, PassiveEffectType.AbsorbAmount, 30,
            level: 24, cost: 26000, column: 1, row: 7, prereq: new[] { "meta_diet", "meta_water_3", "meta_range_2" }, materials: new[] { ("memory_core", 5) }));
        rows.Add(N("brok_slots_3", "단골 3", "가게 물건마다 재고 +1.", PassiveBranch.Brokerage, PassiveEffectType.ShopSlots, 1,
            level: 1, cost: 12000, column: 0, row: 6, prereq: new[] { "brok_stash_4" }));
        rows.Add(N("brok_refresh_3", "거래선 3", "가게에서 사는 값 −10%.", PassiveBranch.Brokerage, PassiveEffectType.ShopRefresh, 10,
            level: 1, cost: 14000, column: 2, row: 6, prereq: new[] { "brok_stash_4" }));
        rows.Add(N("brok_stash_5", "광", "광을 하나 더 지었다. 창고 칸 +50.", PassiveBranch.Brokerage, PassiveEffectType.StashSlots, 50,
            level: 1, cost: 30000, column: 1, row: 7, prereq: new[] { "brok_slots_3", "brok_refresh_3" },
            materials: new[] { ("scrap_metal", 30), ("wire_bundle", 20) }));

        return rows;
    }

    [MenuItem("Dokkaebi/Passive/패시브 에셋 생성")]
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
                    "메뉴 Dokkaebi > Loot > 전리품 에셋 생성 을 먼저 실행하십시오.");
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
