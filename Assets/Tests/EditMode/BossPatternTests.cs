using NUnit.Framework;

namespace Dokkaebi.Tests
{
    /// <summary>【보스 17의 고유 패턴】 (결정 2-110 · 부록 1182 ~ 1200)</summary>
    public class BossPatternTests
    {
        [Test]
        public void 보스_열일곱은_모두_패턴이_있고_서로_다르다()
        {
            var kinds = new System.Collections.Generic.HashSet<BossPatternKind>();
            foreach (BossDefinition boss in StoryTable.Bosses)
            {
                BossPatternKind k = BossPatternTable.KindOf(boss.Id);
                Assert.AreNotEqual(BossPatternKind.None, k, boss.Id);
                Assert.IsTrue(kinds.Add(k), $"{boss.Id} — {k} 겹침");
            }
            Assert.AreEqual(17, kinds.Count);
        }

        [Test]
        public void 어둑시니는_보면_커지고_단단해진다()
        {
            float s = 1f;
            for (int i = 0; i < 20; i++)
                s = BossPatternTable.NextScale(s, seen: true, 0.2f);
            Assert.AreEqual(1.8f, s, 0.001f);
            Assert.AreEqual(0.5f, BossPatternTable.GrowDamageTaken(s), 0.001f);

            for (int i = 0; i < 20; i++)
                s = BossPatternTable.NextScale(s, seen: false, 0.2f);
            Assert.AreEqual(0.8f, s, 0.001f);
            Assert.AreEqual(1.3f, BossPatternTable.GrowDamageTaken(s), 0.001f);
        }

        [Test]
        public void 처녀_몽달은_함께면_단단하다()
        {
            Assert.Less(BossPatternTable.PairDamageTaken(true), 1f);
            Assert.Greater(BossPatternTable.PairDamageTaken(false), 1f);
        }

        [Test]
        public void 구미호는_본문_순서로_둔갑한다()
        {
            CollectionAssert.AreEqual(new[] { "새색시", "방앗간지기", "참봉", "할머니" },
                System.Array.ConvertAll(BossPatternTable.Forms, f => f.form));
            Assert.AreEqual(0, BossPatternTable.FormIndex(1f));
            Assert.AreEqual(1, BossPatternTable.FormIndex(0.8f));
            Assert.AreEqual(4, BossPatternTable.FormIndex(0.1f));
        }
    }
}
