using System.Collections.Generic;
using NUnit.Framework;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 구슬 드롭 · 고르기 규칙 (결정 2-75 — 도감을 없애고 고르지 않은 구슬 3종).
    ///
    /// 여기서 지켜야 할 것 —
    ///  · 떨어지는 것은 종류(핵심 · 보조 · 정신력)뿐이고, 무엇이 될지는 플레이어가 고른다.
    ///  · 핵심 · 보조는 단계(떨어진 장)가 고를 수 있는 요구 레벨을 정한다. 정신력은 단계가 없다.
    ///  · 첫 핵심 구슬은 반드시 부여 계열이다. 아니면 투사체 보조 구슬이 통째로 죽는다.
    /// </summary>
    public class SkillGemDropTests
    {
        private static List<SkillDefinition> SampleCatalog()
        {
            return new List<SkillDefinition>
            {
                SkillTestFactory.CreateCore("core_fire", SkillTag.Projectile | SkillTag.Fire,
                    family: CoreFamily.Ailment),
                SkillTestFactory.CreateCore("core_frost", SkillTag.Projectile | SkillTag.Cold,
                    family: CoreFamily.Ailment),
                SkillTestFactory.CreateCore("core_shockwave", SkillTag.AreaOfEffect,
                    family: CoreFamily.Detonation),
                SkillTestFactory.CreateSupport("sup_pierce", SkillTag.Projectile),
                SkillTestFactory.CreateMeta("meta_a"),
                SkillTestFactory.CreatePersistent("herald_a")
            };
        }

        // ── 고르지 않은 구슬 (결정 2-75 — 도감을 없앴다) ─────────────────

        [Test]
        public void 단계가_고를_수_있는_요구_레벨을_정한다()
        {
            Assert.AreEqual(1, GemCutting.LevelCap(1));
            Assert.AreEqual(3, GemCutting.LevelCap(2));
            Assert.AreEqual(9, GemCutting.LevelCap(5));
            Assert.AreEqual(int.MaxValue, GemCutting.LevelCap(6));
            Assert.AreEqual(1, GemCutting.TierOf(0), "0장은 1단계");
            Assert.AreEqual(6, GemCutting.TierOf(9));

            var late = SkillTestFactory.CreateSupport("sup_late", SkillTag.Projectile, requiredLevel: 5);
            Assert.IsFalse(GemCutting.Allows(BlankGemKind.Support, 2, late));
            Assert.IsTrue(GemCutting.Allows(BlankGemKind.Support, 3, late));
            Assert.IsFalse(GemCutting.Allows(BlankGemKind.Core, 6, late), "종류가 다르면 못 고른다");
        }

        [Test]
        public void 정신력_구슬은_단계_없이_발동_전령_중에서()
        {
            List<SkillDefinition> all = SampleCatalog();
            List<SkillDefinition> spirit = GemCutting.Candidates(BlankGemKind.Spirit, 1, all);

            Assert.AreEqual(2, spirit.Count);
            Assert.IsTrue(spirit.TrueForAll(s => s.Category == SkillCategory.Meta || s.Category == SkillCategory.Persistent));
        }

        [Test]
        public void 떨어지는_비율은_핵심25_보조60_정신력15()
        {
            int core = 0, support = 0, spirit = 0;
            for (int roll = 0; roll < 100; roll++)
            {
                switch (GemCutting.RollKind(roll))
                {
                    case BlankGemKind.Core: core++; break;
                    case BlankGemKind.Support: support++; break;
                    case BlankGemKind.Spirit: spirit++; break;
                }
            }

            Assert.AreEqual(25, core);
            Assert.AreEqual(60, support);
            Assert.AreEqual(15, spirit);
            Assert.AreEqual("gem_blank_core_3", GemCutting.BlankId(BlankGemKind.Core, 3));
            Assert.AreEqual("gem_blank_spirit", GemCutting.BlankId(BlankGemKind.Spirit, 4));
        }

        [Test]
        public void 고르지_않은_구슬_에셋이_다_있고_고르면_바뀐다()
        {
            ItemCatalog items = ItemCatalog.Load();
            SkillGemCatalog gems = SkillGemCatalog.Load();

            for (int t = 1; t <= GemCutting.MaxTier; t++)
            {
                Assert.IsTrue(items.Find(GemCutting.BlankId(BlankGemKind.Core, t))?.IsBlankGem == true, $"핵심 {t}");
                Assert.IsTrue(items.Find(GemCutting.BlankId(BlankGemKind.Support, t))?.IsBlankGem == true, $"보조 {t}");
            }

            ItemDefinition blank = items.Find(GemCutting.BlankId(BlankGemKind.Core, 1));
            Assert.AreEqual(BlankGemKind.Core, blank.BlankGem);
            Assert.AreEqual(0, blank.SlotCost, "구슬은 칸을 먹지 않는다");

            SkillDefinition fire = SkillCatalog.Load().Find("core_fire");
            SkillDefinition burst = SkillCatalog.Load().Find("core_elemental_burst");

            var bag = new Inventory(5, 30f);
            bag.TryAdd(blank, 1);

            Assert.AreEqual(GemCutError.NotAllowed, GemCutting.Cut(bag, blank, SkillCatalog.Load().Find("core_frost"), gems.Find(SkillCatalog.Load().Find("core_frost"))),
                "서리는 Lv3 — 1단계로는 못 고른다");
            Assert.AreEqual(GemCutError.None, GemCutting.Cut(bag, blank, fire, gems.Find(fire)));
            Assert.AreEqual(0, bag.CountOf(blank));
            Assert.AreEqual(1, bag.CountOf(gems.Find(fire)));
            Assert.AreEqual(GemCutError.NotBlank, GemCutting.Cut(bag, blank, burst, gems.Find(burst)));
        }

        // ── 드롭 ──────────────────────────────────────────────────────────

        [Test]
        public void 첫_Core는_반드시_부여_계열이다()
        {
            List<SkillDefinition> pool = SampleCatalog();

            // 시드를 바꿔 가며 전수로 확인한다. 한 번이라도 기폭이 나오면 안 된다.
            for (int seed = 0; seed < 200; seed++)
            {
                SkillDefinition drawn =
                    SkillGemDropTable.DrawFirstCore(pool, new System.Random(seed));

                Assert.IsNotNull(drawn);
                Assert.AreEqual(SkillCategory.Core, drawn.Category);
                Assert.AreEqual(CoreFamily.Ailment, drawn.Family,
                    "기폭 계열만 손에 쥐면 투사체 보조 젬이 통째로 죽습니다.");
            }
        }

        [Test]
        public void 부여_Core가_없으면_첫_드롭은_null이다()
        {
            // 조용히 기폭 핵심 젬을 주는 것보다 실패를 드러내는 편이 낫다.
            var pool = new List<SkillDefinition>
            {
                SkillTestFactory.CreateCore("core_shockwave", SkillTag.AreaOfEffect,
                    family: CoreFamily.Detonation)
            };

            Assert.IsNull(SkillGemDropTable.DrawFirstCore(pool, new System.Random(0)));
        }

        [Test]
        public void 같은_시드는_같은_드롭을_낸다()
        {
            List<SkillDefinition> pool = SampleCatalog();

            SkillDefinition a = SkillGemDropTable.Draw(pool, new System.Random(12345));
            SkillDefinition b = SkillGemDropTable.Draw(pool, new System.Random(12345));

            Assert.AreSame(a, b);
        }

        [Test]
        public void 빈_풀에서는_아무것도_나오지_않는다()
        {
            Assert.IsNull(SkillGemDropTable.Draw(new List<SkillDefinition>(), new System.Random(0)));
            Assert.IsNull(SkillGemDropTable.Draw(null, new System.Random(0)));
        }

        [Test]
        public void 분류_필터가_정확히_걸러낸다()
        {
            List<SkillDefinition> all = SampleCatalog();

            Assert.AreEqual(3, SkillGemDropTable.Filter(all, SkillCategory.Core).Count);
            Assert.AreEqual(2, SkillGemDropTable.Filter(all, SkillCategory.Core, CoreFamily.Ailment).Count);
            Assert.AreEqual(1, SkillGemDropTable.Filter(all, SkillCategory.Support).Count);
            Assert.AreEqual(1, SkillGemDropTable.Filter(all, SkillCategory.Persistent).Count);
        }
    }
}
