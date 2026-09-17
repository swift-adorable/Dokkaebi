using System.Collections.Generic;
using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// 도감(해금 기록)과 인자 드랍 규칙. (Skill_System.md 11-2 · 11-3절)
    ///
    /// 여기서 지켜야 할 것 —
    ///  · 도감은 보유 목록이 아니라 「무엇이 나올 수 있는가」다.
    ///  · 드랍은 요구 레벨로 거르지 않는다. 주울 수는 있고 끼우지 못할 뿐이다.
    ///  · 첫 Core는 반드시 부여 계열이다. 아니면 투사체 Support가 통째로 죽는다.
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

        // ── 도감 ──────────────────────────────────────────────────────────

        [Test]
        public void 도감에_없는_인자는_드랍_풀에_들어오지_않는다()
        {
            List<SkillDefinition> all = SampleCatalog();
            var codex = new SkillCodex();

            codex.Unlock(all[0]);
            codex.Unlock(all[3]);

            List<SkillDefinition> pool = codex.BuildDropPool(all);

            Assert.AreEqual(2, pool.Count);
            CollectionAssert.Contains(pool, all[0]);
            CollectionAssert.Contains(pool, all[3]);
        }

        [Test]
        public void 같은_인자를_두_번_해금해도_한_번만_기록된다()
        {
            List<SkillDefinition> all = SampleCatalog();
            var codex = new SkillCodex();

            Assert.IsTrue(codex.Unlock(all[0]));
            Assert.IsFalse(codex.Unlock(all[0]));
            Assert.AreEqual(1, codex.Count);
        }

        [Test]
        public void 드랍_풀은_요구_레벨로_거르지_않는다()
        {
            // 11-3절 — "각성 레벨에 미달하는 인자는 주울 수는 있으나 끼울 수 없다".
            // 여기서 걸러 버리면 "레벨을 올려야 끼운다"는 압박이 생기지 않는다.
            var late = SkillTestFactory.CreateSupport("sup_late", SkillTag.Projectile,
                requiredLevel: 13);

            var all = new List<SkillDefinition> { late };
            var codex = new SkillCodex();

            codex.UnlockAll(all);

            CollectionAssert.Contains(codex.BuildDropPool(all), late);
        }

        // ── 드랍 ──────────────────────────────────────────────────────────

        [Test]
        public void 첫_Core는_반드시_부여_계열이다()
        {
            List<SkillDefinition> all = SampleCatalog();
            var codex = new SkillCodex();
            codex.UnlockAll(all);

            List<SkillDefinition> pool = codex.BuildDropPool(all);

            // 시드를 바꿔 가며 전수로 확인한다. 한 번이라도 기폭이 나오면 안 된다.
            for (int seed = 0; seed < 200; seed++)
            {
                SkillDefinition drawn =
                    SkillGemDropTable.DrawFirstCore(pool, new System.Random(seed));

                Assert.IsNotNull(drawn);
                Assert.AreEqual(SkillCategory.Core, drawn.Category);
                Assert.AreEqual(CoreFamily.Ailment, drawn.Family,
                    "기폭 계열만 손에 쥐면 투사체 Support가 통째로 죽습니다.");
            }
        }

        [Test]
        public void 부여_Core가_없으면_첫_드랍은_null이다()
        {
            // 조용히 기폭 Core를 주는 것보다 실패를 드러내는 편이 낫다.
            var pool = new List<SkillDefinition>
            {
                SkillTestFactory.CreateCore("core_shockwave", SkillTag.AreaOfEffect,
                    family: CoreFamily.Detonation)
            };

            Assert.IsNull(SkillGemDropTable.DrawFirstCore(pool, new System.Random(0)));
        }

        [Test]
        public void 같은_시드는_같은_드랍을_낸다()
        {
            List<SkillDefinition> all = SampleCatalog();
            var codex = new SkillCodex();
            codex.UnlockAll(all);

            List<SkillDefinition> pool = codex.BuildDropPool(all);

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
