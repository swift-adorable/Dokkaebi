using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>결정 2-147 — 장 맵 배치 틀(가운데 비우기 · 가장자리 채우기).</summary>
    public class DressPlanTests
    {
        private static ChapterMap Ch0 => ZoneMapTable.Of(0);

        [SetUp]
        public void Fresh() => DressPlan.ClearCache();

        [Test]
        public void 출발_자리는_공터_한가운데다()
        {
            foreach (MapAnchor a in Ch0.Anchors)
                if (a.Kind == MapAnchorKind.Start)
                    Assert.AreEqual(DressPlan.Spot.Clearing, DressPlan.Classify(Ch0, new Vector2(a.Position.x, a.Position.z)), a.ZoneId);
        }

        [Test]
        public void 길이_만나는_곳은_갈림_공터가_된다()
        {
            var junctions = DressPlan.Junctions(PixelDress.Paths(0));
            Assert.GreaterOrEqual(junctions.Count, 3, "고목 앞 · 0-2 갈래 · 방앗간 갈림");
            Assert.IsTrue(junctions.Exists(j => (j - new Vector2(13f, 0f) * ZoneMapTable.Chapter0Scale).sqrMagnitude < 4f), "고목 앞 두 갈래");
            Assert.IsTrue(junctions.Exists(j => (j - new Vector2(32f, 0f) * ZoneMapTable.Chapter0Scale).sqrMagnitude < 4f), "0-2 갈래");
        }

        [Test]
        public void 디딤돌은_공터_안이나_멀리에는_깔지_않는다()
        {
            foreach (Vector2 s in DressPlan.SteppingStones(Ch0))
            {
                float d = DressPlan.ClearingDistance(Ch0, s, out _);
                Assert.LessOrEqual(d, DressPlan.StoneApproach + 1f);
            }
            Assert.Greater(DressPlan.SteppingStones(Ch0).Count, 0);
        }

        [Test]
        public void 물_안과_물가를_가린다()
        {
            MapBlock water = System.Array.Find(Ch0.Blocks, b => b.Kind == MapBlockKind.Water);
            Assert.AreEqual(DressPlan.Spot.Water, DressPlan.Classify(Ch0, water.Center + new Vector2(0f, 10f)));
            Assert.AreEqual(DressPlan.Spot.WaterRim, DressPlan.Classify(Ch0, water.Center + new Vector2(water.Size.x * 0.5f + 0.8f, 10f)));
        }

        [Test]
        public void 같은_장은_늘_같은_틀이다()
        {
            int n = DressPlan.Clearings(Ch0).Count;
            int stones = DressPlan.SteppingStones(Ch0).Count;
            DressPlan.ClearCache();
            Assert.AreEqual(n, DressPlan.Clearings(Ch0).Count);
            Assert.AreEqual(stones, DressPlan.SteppingStones(Ch0).Count);
        }
    }
}
