using NUnit.Framework;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 【각시탈】 (결정 2-103) — 3-2 두억시니 다음 새색시 장면에서 줍고(쓰러져도 남는다),
    /// 구미호가 가까이 있다는 것만 알린다.
    /// </summary>
    public class KaksitalTests
    {
        [Test]
        public void 두억시니를_쓰러뜨리면_각시탈을_가진다()
        {
            var progress = new StoryProgress();
            Assert.IsFalse(KaksitalRule.Has(progress));
            Assert.IsFalse(KaksitalRule.Has(null));

            progress.Defeat(KaksitalRule.FoundAfterBossId);
            Assert.IsTrue(KaksitalRule.Has(progress));
        }

        [Test]
        public void 구미호가_가까이_있을_때만_알린다()
        {
            Assert.IsTrue(KaksitalRule.ShouldNotice(true, true, 10f));
            Assert.IsTrue(KaksitalRule.ShouldNotice(true, true, KaksitalRule.NoticeRange));
            Assert.IsFalse(KaksitalRule.ShouldNotice(true, true, KaksitalRule.NoticeRange + 1f));
            Assert.IsFalse(KaksitalRule.ShouldNotice(false, true, 5f));   // 탈이 없다
            Assert.IsFalse(KaksitalRule.ShouldNotice(true, false, 5f));   // 구미호가 없다
        }

        [Test]
        public void 각시탈이_가리키는_보스는_본문대로()
        {
            Assert.IsNotNull(StoryTable.Boss(KaksitalRule.FoundAfterBossId));
            Assert.AreEqual("3-2", StoryTable.Boss(KaksitalRule.FoundAfterBossId).ZoneId);
            Assert.AreEqual("6-2", StoryTable.Boss(KaksitalRule.GumihoId).ZoneId);
        }
    }
}
