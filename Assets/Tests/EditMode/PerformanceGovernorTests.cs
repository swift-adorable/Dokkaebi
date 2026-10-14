using NUnit.Framework;

namespace Dokkaebi.Tests
{
    /// <summary>결정 2-125 — 프레임 지킴이: 느린 창이 두 번 잇달아야 한 단계 내리고, 다시 올리지 않는다.</summary>
    public class PerformanceGovernorTests
    {
        private const float Fast = 1000f / 60f;
        private const float Slow = 1000f / 40f;

        [Test]
        public void 느린_창_한_번은_그대로_두_번_잇달면_내린다()
        {
            int slow = 0;
            Assert.AreEqual(0, PerformanceGovernor.NextTier(0, Slow, ref slow));
            Assert.AreEqual(1, PerformanceGovernor.NextTier(0, Slow, ref slow));
            Assert.AreEqual(0, slow, "내린 뒤에는 다시 센다");
        }

        [Test]
        public void 사이에_빠른_창이_끼면_처음부터_센다()
        {
            int slow = 0;
            PerformanceGovernor.NextTier(0, Slow, ref slow);
            PerformanceGovernor.NextTier(0, Fast, ref slow);
            Assert.AreEqual(0, PerformanceGovernor.NextTier(0, Slow, ref slow));
        }

        [Test]
        public void 빠르면_올라가지_않고_맨_아래에서_멈춘다()
        {
            int slow = 0;
            Assert.AreEqual(2, PerformanceGovernor.NextTier(2, Fast, ref slow), "다시 올리지 않는다");

            int tier = PerformanceGovernor.MaxTier;
            slow = 0;
            PerformanceGovernor.NextTier(tier, Slow, ref slow);
            Assert.AreEqual(PerformanceGovernor.MaxTier, PerformanceGovernor.NextTier(tier, Slow, ref slow));
        }

        [Test]
        public void 단계가_내려갈수록_가벼워진다()
        {
            Assert.AreEqual(1f, PerformanceGovernor.ScaleOf(0));
            Assert.IsTrue(PerformanceGovernor.ShadowsOf(0));
            for (int t = 1; t <= PerformanceGovernor.MaxTier; t++)
            {
                Assert.LessOrEqual(PerformanceGovernor.ScaleOf(t), PerformanceGovernor.ScaleOf(t - 1));
                Assert.IsTrue(!PerformanceGovernor.ShadowsOf(t) || PerformanceGovernor.ShadowsOf(t - 1));
            }
            Assert.IsFalse(PerformanceGovernor.ShadowsOf(PerformanceGovernor.MaxTier));
            Assert.AreEqual(60, PerformanceGovernor.TargetFps);
        }
    }
}
