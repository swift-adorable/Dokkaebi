using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 아이템 규칙 — 태그 · 내구도 · 난이도. (로드맵 6-L)
    /// </summary>
    public class ItemRuleTests
    {
        private const string ItemRoot = "Assets/Data/ScriptableObjects/Items";

        // ── 아이템 태그 ───────────────────────────────────────────────────

        /// <summary>
        /// 각인은 예외 없이 거래 불가다.
        ///
        /// 사망에도 남는 것을 사고팔 수 있으면 「교환으로만 얻는다」는
        /// 각인의 설계가 돈으로 풀린다.
        /// </summary>
        [Test]
        public void 각인은_전부_거래_불가다()
        {
            var guids = AssetDatabase.FindAssets("t:EquipmentDefinition",
                new[] { $"{ItemRoot}/Imprints" });

            Assert.AreEqual(24, guids.Length, "각인 에셋이 24종이 아닙니다.");

            foreach (string guid in guids)
            {
                var asset = AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));

                Assert.IsTrue(asset.NoTrade, $"{asset.Id}가 거래 가능합니다.");
                Assert.IsTrue(asset.SurvivesDeath, $"{asset.Id}가 사망 비유실이 아닙니다.");
            }
        }

        [Test]
        public void 일반_장비는_거래할_수_있다()
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
                $"{ItemRoot}/Weapons/wpn_t3_acid.asset");

            Assert.IsNotNull(weapon);
            Assert.IsFalse(weapon.NoTrade, "무기까지 거래 불가면 상점이 성립하지 않습니다.");
            Assert.IsFalse(weapon.NoExtract, "무기는 반출할 수 있어야 합니다.");
        }

        // ── 내구도 ────────────────────────────────────────────────────────

        private static ItemStack Stack(string path)
        {
            var def = AssetDatabase.LoadAssetAtPath<EquipmentDefinition>($"{ItemRoot}/{path}");

            Assert.IsNotNull(def, $"{path} 에셋이 없습니다.");

            return new ItemStack(def);
        }

        [Test]
        public void 개체마다_최대_내구도를_따로_가진다()
        {
            ItemStack a = Stack("Armour/arm_body_t4.asset");
            ItemStack b = Stack("Armour/arm_body_t4.asset");

            Assert.AreEqual(a.Definition.MaxDurability, a.MaxDurability);

            // 같은 정의를 쓰지만 상한은 개체마다 따로 움직여야 한다.
            Assert.AreSame(a.Definition, b.Definition);
            Assert.AreNotSame(a, b);
        }

        /// <summary>
        /// 【현재 감소량은 0이다.】 수리 비용과 경제가 8단계에 오므로 그때 값을 정한다.
        /// 이 테스트는 값이 바뀌면 알려주는 역할을 한다.
        /// </summary>
        [Test]
        public void 수리_상한_감소는_아직_꺼져_있다()
        {
            Assert.AreEqual(0f, ItemStack.RepairLossRatio, 0.0001f,
                "수리 상한 감소를 켰습니다. 경제 수치를 함께 정했는지 확인하십시오.");

            ItemStack stack = Stack("Armour/arm_body_t6.asset");

            int before = stack.MaxDurability;

            stack.Damage(30);
            stack.Repair(100);

            Assert.AreEqual(before, stack.MaxDurability, "감소가 꺼져 있는데 상한이 줄었습니다.");
            Assert.AreEqual(before, stack.Durability, "수리 후 상한까지 회복되어야 합니다.");
        }

        [Test]
        public void 마모_판정은_개체_상한을_기준으로_한다()
        {
            ItemStack stack = Stack("Armour/arm_head_t5.asset");

            Assert.IsFalse(stack.IsWorn, "새 장비가 마모 상태입니다.");

            stack.Damage(Mathf.CeilToInt(stack.MaxDurability * 0.8f));

            Assert.IsTrue(stack.IsWorn, "33% 이하인데 마모로 판정되지 않습니다.");
        }

        // ── 난이도 ────────────────────────────────────────────────────────

        /// <summary>
        /// 서바이벌이 모든 기획 수치의 기준값이다. 여기가 1이 아니면
        /// 문서의 모든 숫자가 실제와 어긋난다.
        /// </summary>
        [Test]
        public void 서바이벌_난이도가_기준값_1이다()
        {
            Assert.AreEqual(1f, DifficultyTable.EnemyDamage(DifficultyLevel.Survival), 0.0001f);
            Assert.AreEqual(1f, DifficultyTable.EnemyHealth(DifficultyLevel.Survival), 0.0001f);
        }

        [Test]
        public void 난이도가_오르면_적_피해가_커진다()
        {
            Assert.Less(DifficultyTable.EnemyDamage(DifficultyLevel.Balanced),
                DifficultyTable.EnemyDamage(DifficultyLevel.Survival));

            Assert.Greater(DifficultyTable.EnemyDamage(DifficultyLevel.Extreme),
                DifficultyTable.EnemyDamage(DifficultyLevel.Survival));
        }

        /// <summary>폭주는 피해가 가장 높은 대신 체력이 가장 낮다.</summary>
        [Test]
        public void 폭주는_피해가_최고이고_체력이_최저다()
        {
            foreach (DifficultyLevel level in System.Enum.GetValues(typeof(DifficultyLevel)))
            {
                Assert.LessOrEqual(DifficultyTable.EnemyDamage(level),
                    DifficultyTable.EnemyDamage(DifficultyLevel.Frenzy));

                Assert.GreaterOrEqual(DifficultyTable.EnemyHealth(level),
                    DifficultyTable.EnemyHealth(DifficultyLevel.Frenzy));
            }
        }

        /// <summary>
        /// 인스턴스가 없을 때 1을 돌려줘야 한다.
        /// 테스트와 씬 없는 실행에서 난이도 때문에 수치가 흔들리면 안 된다.
        /// </summary>
        [Test]
        public void 매니저가_없으면_난이도_배율은_1이다()
        {
            Assert.AreEqual(1f, GameManager.EnemyDamageMultiplier, 0.0001f);
            Assert.AreEqual(1f, GameManager.EnemyHealthMultiplier, 0.0001f);
        }
    }
}
