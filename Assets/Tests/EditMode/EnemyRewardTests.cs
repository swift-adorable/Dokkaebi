using System;
using System.Linq;
using NUnit.Framework;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 드롭과 보상의 계약 테스트. (docs/Dokkaebi_Hunting_System.md 6절)
    /// </summary>
    public class EnemyRewardTests
    {
        // ── 경험치 · 골드 ───────────────────────────────────────────

        [Test]
        public void 경험치_배율이_문서와_같다()
        {
            Assert.AreEqual(1f, EnemyRewardTable.ExperienceMultiplier(EnemyRarity.Normal), 0.001f);
            Assert.AreEqual(1.5f, EnemyRewardTable.ExperienceMultiplier(EnemyRarity.Magic), 0.001f);
            Assert.AreEqual(3f, EnemyRewardTable.ExperienceMultiplier(EnemyRarity.Rare), 0.001f);
            Assert.AreEqual(6f, EnemyRewardTable.ExperienceMultiplier(EnemyRarity.Unique), 0.001f);
        }

        [Test]
        public void 경험치와_골드는_서로_다른_축이다()
        {
            // 【6-1절의 요지가 이것이다.】 두 보상이 같은 기울기면
            // 「소켓을 열려면 아래층, 벙커를 지으려면 위층」이 성립하지 않는다.
            // 같은 값을 쓰는 순간 축이 하나로 합쳐진다.
            bool anyDifferent = false;

            foreach (EnemyRarity r in (EnemyRarity[])Enum.GetValues(typeof(EnemyRarity)))
            {
                if (Math.Abs(EnemyRewardTable.ExperienceMultiplier(r)
                             - EnemyRewardTable.GoldMultiplier(r)) > 0.001f)
                    anyDifferent = true;
            }

            Assert.IsTrue(anyDifferent,
                "경험치와 골드 배율이 모든 등급에서 같습니다. 축이 하나로 합쳐졌습니다.");
        }

        [Test]
        public void 강한_적일수록_경험치가_더_가파르게_오른다()
        {
            // 「강한 적을 잡는 것」은 골드보다 경험치에 더 크게 답해야 한다.
            float xp = EnemyRewardTable.ExperienceMultiplier(EnemyRarity.Unique);
            float gold = EnemyRewardTable.GoldMultiplier(EnemyRarity.Unique);

            Assert.Greater(xp, gold,
                $"고유의 경험치 배율 {xp}가 골드 배율 {gold}보다 크지 않습니다.");
        }

        [Test]
        public void 배율이_등급을_따라_단조_증가한다()
        {
            var order = new[]
            {
                EnemyRarity.Normal, EnemyRarity.Magic, EnemyRarity.Rare, EnemyRarity.Unique
            };

            for (int i = 1; i < order.Length; i++)
            {
                Assert.Greater(EnemyRewardTable.ExperienceMultiplier(order[i]),
                    EnemyRewardTable.ExperienceMultiplier(order[i - 1]), $"경험치 {order[i]}");

                Assert.Greater(EnemyRewardTable.GoldMultiplier(order[i]),
                    EnemyRewardTable.GoldMultiplier(order[i - 1]), $"골드 {order[i]}");

                Assert.GreaterOrEqual(EnemyRewardTable.LootRolls(order[i]),
                    EnemyRewardTable.LootRolls(order[i - 1]), $"추첨 {order[i]}");
            }
        }

        [Test]
        public void 반올림은_한_곳에서만_한다()
        {
            // 기본 10 · 마법 1.5배 = 15.
            Assert.AreEqual(15, EnemyRewardTable.Experience(10, EnemyRarity.Magic));

            // 흡수 보너스가 곱해져도 음수나 예외가 나오지 않는다.
            Assert.AreEqual(0, EnemyRewardTable.Experience(10, EnemyRarity.Magic, -5f));
            Assert.AreEqual(0, EnemyRewardTable.Gold(0, EnemyRarity.Unique));
        }

        // ── 드롭 품질 ─────────────────────────────────────────────────

        [Test]
        public void 장비_티어_범위가_문서와_같다()
        {
            var rows = new[]
            {
                (EnemyRarity.Normal, 1, 2),
                (EnemyRarity.Magic,  2, 3),
                (EnemyRarity.Rare,   3, 5),
                (EnemyRarity.Unique, 4, 6)
            };

            foreach (var (rarity, expectMin, expectMax) in rows)
            {
                EnemyRewardTable.EquipmentTierRange(rarity, out int min, out int max);

                Assert.AreEqual(expectMin, min, $"{rarity} 최소 티어");
                Assert.AreEqual(expectMax, max, $"{rarity} 최대 티어");
                Assert.LessOrEqual(min, max, $"{rarity} 범위가 뒤집혔습니다.");
            }
        }

        [Test]
        public void 고유는_젬을_확정으로_준다()
        {
            Assert.AreEqual(1f, EnemyRewardTable.GemChance(EnemyRarity.Unique), 0.001f,
                "고유는 젬 확정 1개입니다. (문서 6절)");
        }

        [Test]
        public void 고유_장비는_온전하지_않다()
        {
            // 티어 4~6짜리를 온전하게 주면 수리 경제가 시작도 전에 무너진다.
            Assert.Greater(EnemyRewardTable.UniqueDurabilityRatio, 0f);
            Assert.Less(EnemyRewardTable.UniqueDurabilityRatio, 1f,
                "고유 장비가 내구도 100%로 나옵니다. (문서 6절 「내구도 손상」)");
        }

        // ── 조건부 드롭 ───────────────────────────────────────────────

        [Test]
        public void 치명타로_죽이면_온전한_신경절이_나오지_않는다()
        {
            var crit = new KillContext { killedByCritical = true };
            var plain = new KillContext { killedByCritical = false };

            Assert.IsFalse(
                ConditionalDropTable.Evaluate(in crit).HasFlag(ConditionalDrop.IntactGanglion));

            Assert.IsTrue(
                ConditionalDropTable.Evaluate(in plain).HasFlag(ConditionalDrop.IntactGanglion));
        }

        [Test]
        public void 동결_상태에서_죽여야_굳지_않은_수액이_나온다()
        {
            var frozen = new KillContext { frozenAtDeath = true };
            var warm = new KillContext { frozenAtDeath = false };

            Assert.IsTrue(
                ConditionalDropTable.Evaluate(in frozen).HasFlag(ConditionalDrop.UnsetSap));

            Assert.IsFalse(
                ConditionalDropTable.Evaluate(in warm).HasFlag(ConditionalDrop.UnsetSap));
        }

        [Test]
        public void 점화는_죽는_순간이_아니라_살아_있는_동안_전체를_본다()
        {
            // 【불을 붙였다가 꺼진 뒤에 죽여도 「걸지 않았다」가 되면 조건이 거짓말이 된다.】
            var burnedEarlier = new KillContext { everIgnited = true };

            Assert.IsFalse(
                ConditionalDropTable.Evaluate(in burnedEarlier).HasFlag(ConditionalDrop.UnburntSpore),
                "한 번 점화됐던 적에게서 미연소 포자가 나옵니다.");
        }

        [Test]
        public void 세_조건이_동시에_성립할_수_있다()
        {
            // 서로 배타가 아니다 — 치명타 없이, 동결시켜, 불을 안 붙이고 죽이면 셋 다 나온다.
            // 배타로 만들면 「무엇을 노릴까」가 아니라 「무엇을 포기할까」가 된다.
            var ideal = new KillContext
            {
                killedByCritical = false, frozenAtDeath = true, everIgnited = false
            };

            ConditionalDrop result = ConditionalDropTable.Evaluate(in ideal);

            foreach (ConditionalDrop drop in ConditionalDropTable.All)
                Assert.IsTrue(result.HasFlag(drop), ConditionalDropTable.Describe(drop));
        }

        [Test]
        public void 세_조건_전부_설명_문구를_갖는다()
        {
            // 사전 고지가 규칙이다. 문구가 없으면 알려줄 방법이 없다.
            Assert.AreEqual(3, ConditionalDropTable.All.Length);

            foreach (ConditionalDrop drop in ConditionalDropTable.All)
            {
                Assert.IsNotEmpty(ConditionalDropTable.Describe(drop), drop.ToString());
                Assert.AreNotEqual(ConditionalDrop.None, drop);
            }

            Assert.AreEqual(3, ConditionalDropTable.All.Distinct().Count(), "중복이 있습니다.");
        }
    }
}
