using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 6-C 장비 에셋의 계약 테스트.
    ///
    /// 문서에만 적힌 규칙은 언젠가 "방어력 +5 하나쯤이야"로 무너진다.
    /// 그래서 규칙을 테스트로 고정한다 — 특히 【각인 순수 증가 금지】.
    /// (docs/Dokkaebi_Imprint_System.md 0절 / Dokkaebi_Equipment_System.md 6절)
    /// </summary>
    public class EquipmentAssetTests
    {
        private const string ItemRoot = "Assets/Data/ScriptableObjects/Items";

        private static List<T> Load<T>(string folder) where T : UnityEngine.Object
        {
            return AssetDatabase
                .FindAssets($"t:{typeof(T).Name}", new[] { $"{ItemRoot}/{folder}" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(a => a != null)
                .ToList();
        }

        // ── 각인 — 이 게임의 뼈대를 지키는 테스트 ─────────────────────────

        /// <summary>
        /// 각인은 사망해도 남는 유일한 장착품이다. 순수 증가를 주면
        /// "죽어도 사라지지 않는 전투력"이 되어 「죽으면 들고 있던 것 전부」가 물러진다.
        /// </summary>
        [Test]
        public void 각인은_전부_대가를_가진다()
        {
            List<ImprintAssetGenerator.Spec> table = ImprintAssetGenerator.Table();

            Assert.AreEqual(24, table.Count, "각인은 8계열 × 3단계 = 24종입니다.");

            foreach (ImprintAssetGenerator.Spec spec in table)
            {
                bool hasCost = spec.stats.Any(EquipmentStatMeta.IsDrawback);

                Assert.IsTrue(hasCost,
                    $"「{spec.familyLabel} {spec.tier}」에 음수 옵션이 없습니다. " +
                    "각인은 전부 「A를 깎아 B를 얻는다」이며 예외가 없습니다.");
            }
        }

        /// <summary>대가만 있고 이득이 없는 각인도 존재할 수 없다.</summary>
        [Test]
        public void 각인은_전부_이득을_가진다()
        {
            foreach (ImprintAssetGenerator.Spec spec in ImprintAssetGenerator.Table())
            {
                bool hasGain = spec.stats.Any(EquipmentStatMeta.IsGain)
                               || spec.immunity != StatusEffectType.None
                               || (spec.extraImmunities?.Length ?? 0) > 0;

                Assert.IsTrue(hasGain,
                    $"「{spec.familyLabel} {spec.tier}」에 이득이 없습니다.");
            }
        }

        /// <summary>생성된 에셋도 같은 규칙을 지켜야 한다. 표만 맞고 에셋이 틀리면 의미가 없다.</summary>
        [Test]
        public void 생성된_각인_에셋도_순수_증가를_주지_않는다()
        {
            List<EquipmentDefinition> assets = Load<EquipmentDefinition>("Imprints");

            Assert.AreEqual(24, assets.Count,
                "각인 에셋이 24종이 아닙니다. 「Dokkaebi/Equipment/각인 에셋 생성」을 실행하십시오.");

            foreach (EquipmentDefinition asset in assets)
            {
                Assert.AreEqual(ItemKind.Imprint, asset.Kind, $"{asset.Id}의 종류가 각인이 아닙니다.");

                Assert.IsTrue(asset.SurvivesDeath, $"{asset.Id}가 사망 비유실이 아닙니다.");

                Assert.IsTrue(asset.HasDrawback,
                    $"{asset.Id}에 음수 옵션이 없습니다 — 순수 증가 각인입니다.");

                Assert.IsTrue(asset.HasGain, $"{asset.Id}에 이득이 없습니다.");
            }
        }

        /// <summary>
        /// 각인의 무게는 고정이다. 각인은 무게 결정을 만들지 않는다 — 슬롯이 결정이다.
        /// </summary>
        [Test]
        public void 각인의_무게와_내구도는_전부_같다()
        {
            foreach (EquipmentDefinition asset in Load<EquipmentDefinition>("Imprints"))
            {
                Assert.AreEqual(0.3f, asset.Weight, 0.001f, $"{asset.Id}의 무게가 0.3이 아닙니다.");
                Assert.AreEqual(0, asset.MaxDurability, $"{asset.Id}에 내구도가 있습니다.");
            }
        }

        /// <summary>같은 계열 + 같은 단계가 두 개 있으면 중복 장착 판정이 무의미해진다.</summary>
        [Test]
        public void 각인은_계열과_단계_조합이_중복되지_않는다()
        {
            var seen = new HashSet<string>();

            foreach (ImprintAssetGenerator.Spec spec in ImprintAssetGenerator.Table())
            {
                string key = $"{spec.family}_{spec.tier}";

                Assert.IsTrue(seen.Add(key), $"{key}가 중복되었습니다.");
            }
        }

        /// <summary>
        /// 각인은 패시브의 축(가방·창고·판매가·해금)을 건드리지 않는다.
        /// 경계가 무너지면 성장 축 넷 중 하나가 죽는다.
        /// </summary>
        [Test]
        public void 각인은_칸_축을_건드리지_않는다()
        {
            foreach (ImprintAssetGenerator.Spec spec in ImprintAssetGenerator.Table())
            {
                foreach (EquipmentStat stat in spec.stats)
                {
                    Assert.AreNotEqual(EquipmentStatType.SlotCapacity, stat.type,
                        $"「{spec.familyLabel} {spec.tier}」가 칸을 줍니다. 그것은 가방과 패시브의 몫입니다.");

                    Assert.AreNotEqual(EquipmentStatType.MaxCarryWeight, stat.type,
                        $"「{spec.familyLabel} {spec.tier}」가 소지 중량을 줍니다.");
                }
            }
        }

        // ── 무기 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 문서 표(Combat_Baseline 3절)와 에셋이 일치하는지.
        /// 이것이 문서와 코드의 드리프트를 막는 유일한 장치다.
        /// </summary>
        [Test]
        public void 무기_에셋이_문서_표와_일치한다()
        {
            List<WeaponAssetGenerator.Spec> table = WeaponAssetGenerator.Table();

            Assert.AreEqual(6, table.Count, "무기는 티어 1~6 = 6종입니다.");

            foreach (WeaponAssetGenerator.Spec spec in table)
            {
                var asset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
                    $"{ItemRoot}/Weapons/{spec.id}.asset");

                Assert.IsNotNull(asset,
                    $"{spec.id} 에셋이 없습니다. 「Dokkaebi/Equipment/무기 에셋 생성」을 실행하십시오.");

                Assert.AreEqual(spec.tier, asset.Tier, $"{spec.id} 티어");
                Assert.AreEqual(spec.damage, asset.BaseDamage, 0.001f, $"{spec.id} 기본 피해");
                Assert.AreEqual(spec.interval, asset.FireInterval, 0.001f, $"{spec.id} 발사 간격");
                Assert.AreEqual(spec.range, asset.EffectiveRange, 0.001f, $"{spec.id} 유효 사거리");

                Assert.AreEqual(spec.penetration,
                    asset.GetStat(EquipmentStatType.ArmourPenetration), 0.001f,
                    $"{spec.id} 방어 관통");
            }
        }

        /// <summary>티어 1 무기는 전투 기준점이므로 상수와 어긋나면 안 된다.</summary>
        [Test]
        public void 티어_1_무기가_전투_기준점과_같다()
        {
            WeaponAssetGenerator.Spec t1 = WeaponAssetGenerator.Table().First(s => s.tier == 1);

            Assert.AreEqual(CombatConstants.BaseWeaponDamage, t1.damage, 0.001f);
            Assert.AreEqual(CombatConstants.BaseFireInterval, t1.interval, 0.001f);
            Assert.AreEqual(CombatConstants.BaseEffectiveRange, t1.range, 0.001f);
        }

        /// <summary>
        /// 【티어는 DPS 단조 증가가 아니다】 4·5티어는 발사 간격이 오히려 늘어난다.
        /// "세게 한 방" ↔ "빠르게 여러 발"의 선택이 유지되어야 다탄 빌드가 후반에 죽지 않는다.
        /// </summary>
        [Test]
        public void 무기_티어가_올라도_발사_간격은_단조_감소하지_않는다()
        {
            List<WeaponAssetGenerator.Spec> table = WeaponAssetGenerator.Table()
                .OrderBy(s => s.tier).ToList();

            bool foundSlower = false;

            for (int i = 1; i < table.Count; i++)
            {
                if (table[i].interval > table[i - 1].interval)
                {
                    foundSlower = true;
                    break;
                }
            }

            Assert.IsTrue(foundSlower,
                "모든 티어가 더 빨라지기만 합니다. 상위 티어 중 하나는 느려져야 " +
                "「세게 한 방 ↔ 빠르게 여러 발」의 선택이 남습니다.");
        }

        /// <summary>피해는 티어와 함께 반드시 올라야 한다. 여기가 뒤집히면 진행이 화력 성장이 아니게 된다.</summary>
        [Test]
        public void 무기_피해는_티어와_함께_오른다()
        {
            List<WeaponAssetGenerator.Spec> table = WeaponAssetGenerator.Table()
                .OrderBy(s => s.tier).ToList();

            for (int i = 1; i < table.Count; i++)
            {
                Assert.Greater(table[i].damage, table[i - 1].damage,
                    $"티어 {table[i].tier}의 피해가 티어 {table[i - 1].tier}보다 낮습니다.");
            }
        }

        /// <summary>방어 관통은 상한을 넘을 수 없다. 넘으면 초과 관통에 보상이 생겨 공식의 의도가 깨진다.</summary>
        [Test]
        public void 무기_방어_관통이_상한을_넘지_않는다()
        {
            foreach (WeaponAssetGenerator.Spec spec in WeaponAssetGenerator.Table())
            {
                Assert.LessOrEqual(spec.penetration, CombatConstants.MaxArmour,
                    $"{spec.id}의 방어 관통이 상한을 넘습니다.");
            }
        }

        /// <summary>성능 상단(티어 4 이상)에는 음수 옵션이 하나 이상 붙는다.</summary>
        [Test]
        public void 상위_티어_무기에는_대가가_붙는다()
        {
            foreach (WeaponAssetGenerator.Spec spec in WeaponAssetGenerator.Table())
            {
                if (spec.tier < 4)
                    continue;

                bool hasCost = spec.extra != null && spec.extra.Any(EquipmentStatMeta.IsDrawback);

                Assert.IsTrue(hasCost,
                    $"{spec.id}에 대가가 없습니다. 페널티 없는 장비는 성능 하단이나 드롭 전용이어야 합니다.");
            }
        }

        // ── 방어구 · 가방 ─────────────────────────────────────────────────

        [Test]
        public void 방어구_에셋이_부위별로_전부_있다()
        {
            List<EquipmentDefinition> armour = Load<EquipmentDefinition>("Armour");

            Assert.AreEqual(6, armour.Count(a => a.Slot == EquipmentSlot.Head), "머리 6종");
            Assert.AreEqual(18, armour.Count(a => a.Slot == EquipmentSlot.Body), "몸통 6티어 × 3변형");
            Assert.AreEqual(6, armour.Count(a => a.Slot == EquipmentSlot.Ears), "청각 6종");
            Assert.AreEqual(16, armour.Count(a => a.Slot == EquipmentSlot.Face), "얼굴 1 + 5속성 × 3단계");
        }

        /// <summary>
        /// 【최고 티어일수록 칸이 준다】 — "방어 최대화 = 파밍량 최소화"가
        /// 슬롯 구조에 박혀 있어야 방어구 선택이 결정이 된다.
        /// </summary>
        [Test]
        public void 몸통_방어구는_티어가_오르면_칸이_준다()
        {
            List<EquipmentDefinition> normal = Load<EquipmentDefinition>("Armour")
                .Where(a => a.Slot == EquipmentSlot.Body && !a.Id.Contains("_light") && !a.Id.Contains("_heavy"))
                .OrderBy(a => a.Tier)
                .ToList();

            Assert.AreEqual(6, normal.Count, "몸통 일반 변형이 6종이 아닙니다.");

            float first = normal.First().GetStat(EquipmentStatType.SlotCapacity);
            float last = normal.Last().GetStat(EquipmentStatType.SlotCapacity);

            Assert.Less(last, first,
                "티어가 올라도 칸이 줄지 않습니다. 「방어 최대화 = 파밍량 최소화」가 성립하지 않습니다.");

            float bodyArmourFirst = normal.First().GetStat(EquipmentStatType.BodyArmour);
            float bodyArmourLast = normal.Last().GetStat(EquipmentStatType.BodyArmour);

            Assert.Greater(bodyArmourLast, bodyArmourFirst, "티어가 올라도 방어도가 오르지 않습니다.");
        }

        /// <summary>같은 티어에서 경량은 칸을, 중갑은 방어를 가져가야 선택이 생긴다.</summary>
        [Test]
        public void 몸통_3변형이_서로_다른_것을_준다()
        {
            List<EquipmentDefinition> body = Load<EquipmentDefinition>("Armour")
                .Where(a => a.Slot == EquipmentSlot.Body && a.Tier == 3)
                .ToList();

            EquipmentDefinition light = body.First(a => a.Id.EndsWith("_light"));
            EquipmentDefinition heavy = body.First(a => a.Id.EndsWith("_heavy"));
            EquipmentDefinition normal = body.First(a => !a.Id.Contains("_light") && !a.Id.Contains("_heavy"));

            Assert.Greater(light.GetStat(EquipmentStatType.SlotCapacity),
                normal.GetStat(EquipmentStatType.SlotCapacity), "경량이 칸을 더 주지 않습니다.");

            Assert.AreEqual(0f, heavy.GetStat(EquipmentStatType.SlotCapacity), 0.001f,
                "중갑의 칸이 0이 아닙니다.");

            Assert.Greater(heavy.GetStat(EquipmentStatType.BodyArmour),
                normal.GetStat(EquipmentStatType.BodyArmour), "중갑이 방어도를 더 주지 않습니다.");

            Assert.Greater(heavy.Weight, light.Weight, "중갑이 경량보다 무겁지 않습니다.");
        }

        /// <summary>
        /// 얼굴 슬롯은 속성 대응 전담이다. 방어도를 주면 머리·몸통과 구분이 사라진다.
        /// 티어 4 이상에서 수치가 아니라 면역으로 넘어가는 것이 의도된 질적 도약이다.
        /// </summary>
        [Test]
        public void 얼굴_방어구는_방어도를_주지_않고_티어_4부터_면역을_준다()
        {
            List<EquipmentDefinition> face = Load<EquipmentDefinition>("Armour")
                .Where(a => a.Slot == EquipmentSlot.Face)
                .ToList();

            foreach (EquipmentDefinition f in face)
            {
                Assert.AreEqual(0f, f.GetStat(EquipmentStatType.HeadArmour), 0.001f,
                    $"{f.Id}가 머리 방어도를 줍니다.");

                Assert.AreEqual(0f, f.GetStat(EquipmentStatType.BodyArmour), 0.001f,
                    $"{f.Id}가 몸통 방어도를 줍니다.");

                if (f.Tier >= 4)
                {
                    Assert.AreNotEqual(StatusEffectType.None, f.Immunity,
                        $"{f.Id}(티어 {f.Tier})에 면역이 없습니다.");
                }
            }
        }

        /// <summary>청각 슬롯은 내구도가 없다. (docs/Dokkaebi_Equipment_System.md 1절)</summary>
        [Test]
        public void 청각과_가방은_내구도가_없다()
        {
            foreach (EquipmentDefinition ears in Load<EquipmentDefinition>("Armour")
                         .Where(a => a.Slot == EquipmentSlot.Ears))
            {
                Assert.AreEqual(0, ears.MaxDurability, $"{ears.Id}에 내구도가 있습니다.");
            }

            foreach (EquipmentDefinition bag in Load<EquipmentDefinition>("Backpacks"))
            {
                Assert.AreEqual(0, bag.MaxDurability, $"{bag.Id}에 내구도가 있습니다.");
            }
        }

        /// <summary>
        /// 가방이 주는 칸은 덕코프 곡선(+8 ~ +30)을 따른다.
        /// 기본 20칸에 최상위 가방을 끼면 50칸이 된다 — 덕코프와 같은 대역이다.
        /// (docs/research/duckov/08_전투_실측과_교전.md 6절)
        /// </summary>
        [Test]
        public void 가방_곡선이_덕코프_대역_안에_있다()
        {
            List<EquipmentDefinition> bags = Load<EquipmentDefinition>("Backpacks")
                .OrderBy(b => b.Tier).ToList();

            Assert.AreEqual(6, bags.Count);

            float first = bags.First().GetStat(EquipmentStatType.SlotCapacity);
            float last = bags.Last().GetStat(EquipmentStatType.SlotCapacity);

            Assert.AreEqual(8f, first, 0.001f, "가장 작은 가방은 +8칸입니다.");
            Assert.AreEqual(30f, last, 0.001f, "가장 큰 가방은 +30칸입니다.");

            // 맨몸으로도 한 판 돌 수 있어야 한다. 12칸은 너무 작았다.
            Assert.GreaterOrEqual(PlayerInventory.BaseSlots, 20,
                "기본 칸이 20칸 미만이면 가방을 찾기 전에 아무것도 못 줍습니다.");
        }

        /// <summary>
        /// 【최고 티어일수록 칸이 준다.】 단 0까지는 가지 않는다 —
        /// 0으로 만들면 「최고 방어구를 입으면 아예 못 줍는다」가 되어
        /// 선택이 아니라 금지가 된다. 덕코프도 최상위에서 +2를 남긴다.
        /// </summary>
        [Test]
        public void 몸통_일반_변형은_최상위에서도_칸이_0이_아니다()
        {
            EquipmentDefinition top = Load<EquipmentDefinition>("Armour")
                .Where(a => a.Slot == EquipmentSlot.Body
                            && !a.Id.Contains("_light") && !a.Id.Contains("_heavy"))
                .OrderByDescending(a => a.Tier)
                .First();

            Assert.Greater(top.GetStat(EquipmentStatType.SlotCapacity), 0f,
                "최고 티어 일반 몸통의 칸이 0입니다. 그것은 중갑 변형의 몫입니다.");
        }

        /// <summary>
        /// 「최대 소지 중량」과 「가방 칸」은 별도 자원이다.
        /// 가방마다 배분이 달라야 "초반 병목은 칸, 후반 병목은 무게"가 선택으로 나타난다.
        /// </summary>
        [Test]
        public void 가방은_중량과_칸의_배분이_서로_다르다()
        {
            List<EquipmentDefinition> bags = Load<EquipmentDefinition>("Backpacks");

            Assert.AreEqual(6, bags.Count, "가방은 6종입니다.");

            var ratios = bags
                .Select(b => b.GetStat(EquipmentStatType.MaxCarryWeight)
                             / UnityEngine.Mathf.Max(1f, b.GetStat(EquipmentStatType.SlotCapacity)))
                .ToList();

            Assert.Greater(ratios.Max() / ratios.Min(), 2f,
                "가방들의 중량:칸 비율이 비슷합니다. 배분이 다르지 않으면 늘 최신 가방이 정답이 됩니다.");
        }

        // ── WeaponProfile — 무기와 각인이 실제로 합쳐지는지 ────────────────

        [Test]
        public void 무기가_없으면_맨몸_성능을_쓴다()
        {
            WeaponProfile profile = WeaponProfile.Create(null, null);

            Assert.Greater(profile.Damage, 0f, "맨몸 피해가 0이면 시체 회수를 시도할 수 없습니다.");
            Assert.Less(profile.Damage, CombatConstants.BaseWeaponDamage, "맨몸이 티어 1보다 강합니다.");
        }

        [Test]
        public void 진격_각인을_끼우면_피해가_오르고_사거리가_준다()
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
                $"{ItemRoot}/Weapons/wpn_t1_pipe.asset");

            var imprint = AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(
                $"{ItemRoot}/Imprints/imp_charge_t2.asset");

            Assert.IsNotNull(weapon);
            Assert.IsNotNull(imprint);

            var bare = new EquipmentModifiers();
            WeaponProfile before = WeaponProfile.Create(weapon, bare);

            var withImprint = new EquipmentModifiers();
            withImprint.Add(imprint);
            WeaponProfile after = WeaponProfile.Create(weapon, withImprint);

            Assert.Greater(after.Damage, before.Damage, "진격 각인이 피해를 올리지 않습니다.");
            Assert.Less(after.EffectiveRange, before.EffectiveRange, "진격 각인이 사거리를 깎지 않습니다.");
        }

        /// <summary>
        /// 경량 Ⅲ의 「방어도 0 취급」이 실제로 0이 되는지.
        /// 큰 음수를 주고 합산 후 0에서 자르는 방식이므로 경로를 검증해야 한다.
        /// </summary>
        [Test]
        public void 경량_3단_각인은_방어도를_0으로_만든다()
        {
            var body = AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(
                $"{ItemRoot}/Armour/arm_body_t6_heavy.asset");

            var imprint = AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(
                $"{ItemRoot}/Imprints/imp_feather_t3.asset");

            Assert.IsNotNull(body);
            Assert.IsNotNull(imprint);

            var modifiers = new EquipmentModifiers();
            modifiers.Add(body);
            modifiers.Add(imprint);

            DefenceProfile defence = modifiers.ToDefenceProfile();

            Assert.AreEqual(0, defence.bodyArmour,
                "최상급 중갑을 입고도 경량 Ⅲ이면 몸통 방어도는 0이어야 합니다.");
        }

        /// <summary>역치 Ⅲ은 상태 3종을 막고 그 속성 피해를 두 배로 받는다.</summary>
        [Test]
        public void 역치_3단_각인은_세_상태를_막고_피해를_두_배로_받는다()
        {
            var imprint = AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(
                $"{ItemRoot}/Imprints/imp_threshold_t3.asset");

            Assert.IsNotNull(imprint);

            var modifiers = new EquipmentModifiers();
            modifiers.Add(imprint);

            // 면역은 【걸리는 상태】를 막는다. 위험 상태(동결)가 아니라 원본(냉각)이다.
            // 동결만 막으면 "냉기 저항 장비를 꼈는데 여전히 느려진다"가 되어 플레이어가 혼란스럽다.
            Assert.IsTrue(modifiers.IsImmuneTo(StatusEffectType.Ignite), "점화 면역");
            Assert.IsTrue(modifiers.IsImmuneTo(StatusEffectType.Chill), "냉각 면역");
            Assert.IsTrue(modifiers.IsImmuneTo(StatusEffectType.Shock), "감전 면역");
            Assert.IsFalse(modifiers.IsImmuneTo(StatusEffectType.Poison), "중독까지 막으면 대가가 없습니다.");

            DefenceProfile defence = modifiers.ToDefenceProfile();

            Assert.AreEqual(2f, defence.resistances.fire, 0.001f, "화염 피해가 두 배가 아닙니다.");
        }
    }
}
