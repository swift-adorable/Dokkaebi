using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>결정 2-149 — 0장 맵 2배 · 열린 집(잠기지 않은 파밍 건물).</summary>
    public class MapScaleTests
    {
        private static ChapterMap Ch0 => ZoneMapTable.Of(0);

        [SetUp]
        public void Fresh() => LockSiteTable.ClearCache();

        [Test]
        public void 영장은_320에_180미터다()
        {
            Assert.AreEqual(160f, Ch0.HalfSize.x, 0.01f);
            Assert.AreEqual(90f, Ch0.HalfSize.y, 0.01f);
        }

        [Test]
        public void 늘려도_자리는_구역_안_덩어리_밖이다()
        {
            foreach (MapAnchor a in Ch0.Anchors)
            {
                Assert.AreEqual(a.ZoneId, Ch0.ZoneAt(a.Position), $"{a.Kind} {a.Position}");
                if (a.Kind != MapAnchorKind.SecretExit)
                    Assert.IsFalse(Ch0.IsBlocked(a.Position, 0.5f), $"{a.Kind} {a.Position}");
            }
        }

        [Test]
        public void 집_한_채_크기는_그대로다()
        {
            foreach (MapBlock b in Ch0.Blocks)
                if (b.Kind == MapBlockKind.Building)
                    Assert.LessOrEqual(Mathf.Max(b.Size.x, b.Size.z), 12.01f, b.Kind.ToString());   // 집 한 채는 그대로
        }

        [Test]
        public void 영장_구역마다_열린_집이_있다()
        {
            foreach (ZoneArea a in Ch0.Areas)
            {
                IReadOnlyList<LockSite> houses = LockSiteTable.OpenHouses(Ch0, a.ZoneId);
                Assert.GreaterOrEqual(houses.Count, 2, a.ZoneId);
                foreach (LockSite h in houses)
                {
                    Assert.AreEqual(LockSiteKind.OpenHouse, h.Kind);
                    Assert.IsFalse(h.Locked);
                    Assert.GreaterOrEqual(h.Size.x, 11f, "덕코프처럼 크게");
                    Assert.IsTrue(LockSiteTable.Fits(Ch0, h, null), h.Id);
                    Assert.Greater(PixelDress.PathDistance(0, h.Center), 3f, "길 위에 세우지 않는다");
                }
            }
        }

        [Test]
        public void 열린_집은_서로_겹치지_않고_판마다_같다()
        {
            var first = new List<Vector2>();
            foreach (ZoneArea a in Ch0.Areas)
                foreach (LockSite h in LockSiteTable.OpenHouses(Ch0, a.ZoneId))
                    first.Add(h.Center);
            for (int i = 0; i < first.Count; i++)
                for (int j = i + 1; j < first.Count; j++)
                    Assert.Greater((first[i] - first[j]).magnitude, 10f);

            LockSiteTable.ClearCache();
            int k = 0;
            foreach (ZoneArea a in Ch0.Areas)
                foreach (LockSite h in LockSiteTable.OpenHouses(Ch0, a.ZoneId))
                    Assert.AreEqual(first[k++], h.Center);
        }

        [Test]
        public void 다른_장은_열린_집이_하나씩이다()
        {
            ChapterMap m = ZoneMapTable.Of(1);
            foreach (ZoneArea a in m.Areas)
                Assert.LessOrEqual(LockSiteTable.OpenHouses(m, a.ZoneId).Count, 1);
        }
    
        [Test]
        public void 넓어진_0장은_적이_넓이만큼_늘어난다()
        {
            Rect r = Ch0.AreaOf("0-1").Value.Bounds;
            int now = PopulationTable.Count(0, r.width * r.height);
            Assert.GreaterOrEqual(now, 45, "넓이 4배 — 제곱근(36)보다 많게");
            Assert.LessOrEqual(now, PopulationTable.Max);
            Assert.AreEqual(PopulationTable.BaseCount, PopulationTable.Count(0, PopulationTable.ReferenceArea), "기준 넓이는 그대로 18");
        }
    }
}
