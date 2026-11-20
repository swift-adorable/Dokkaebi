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
        public void 영장은_제로존만큼_크다()
        {
            // 결정 2-150 — 덕코프 제로존 걷는 땅 298×212m. 판은 340×230m(가장자리 숲 포함)
            Assert.AreEqual(170f, Ch0.HalfSize.x, 0.01f);
            Assert.AreEqual(115f, Ch0.HalfSize.y, 0.01f);
        }

        [Test]
        public void 영장은_섬_모양_숲으로_둘러싸였다()
        {
            int edgeForests = 0;
            foreach (MapBlock b in Ch0.Blocks)
                if (b.Kind == MapBlockKind.Forest && (Mathf.Abs(b.Center.x) + b.Size.x * 0.5f > 165f || Mathf.Abs(b.Center.y) + b.Size.z * 0.5f > 110f))
                    edgeForests++;
            Assert.GreaterOrEqual(edgeForests, 12, "가장자리를 숲 덩어리가 감싼다");
        }

        [Test]
        public void 열린_집은_마을_둘레에_모인다()
        {
            foreach (ZoneArea a in Ch0.Areas)
                foreach (LockSite h in LockSiteTable.OpenHouses(Ch0, a.ZoneId))
                {
                    float best = float.MaxValue;
                    foreach (MapAnchor v in Ch0.AnchorsOf(MapAnchorKind.Landmark, a.ZoneId))
                        best = Mathf.Min(best, Vector2.Distance(h.Center, new Vector2(v.Position.x, v.Position.z)));
                    // v7 — 집터 1.4배: 마을 둘레(50m 안)에 모이고, 자리가 없으면 길가 외딴 집(문 앞 길이 16m 안)
                    float road = PixelDress.PathDistance(0, new Vector2(h.Center.x, h.Center.y - h.Size.y * 0.5f - 1f));
                    Assert.IsTrue(best < 50f || road < 16f, $"{h.Id} 마을 {best:0} · 길 {road:0}");
                }
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
                    Assert.GreaterOrEqual(h.Size.x, 10f, "덕코프처럼 크게 (작은 ㅡ자 3칸이 10m)");
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
        public void 영장_집터_자리는_모두_비어_있다()   // v8 — 길은 집터 자리를 돌아가고, 자리는 숲 · 덩어리 · 앵커를 피한다
        {
            foreach (var (z, c, sz) in LockSiteTable.Chapter0Plots)
            {
                Assert.IsNull(LockSiteTable.WhyNot(Ch0, new LockSite(z, 0, LockSiteKind.OpenHouse, c, sz, "t"), null), $"{c}");
                Assert.IsTrue(LockSiteTable.ClearOfPathsForTest(0, new Rect(c - sz * 0.5f, sz)), $"길이 집터 자리 {c} 를 지난다");
            }
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
    
        [Test]
        public void 영장_집은_짜임이_서로_다르고_ㅁ자_ㄷ자가_있다()
        {
            var seen = new HashSet<string>();
            var all = new List<string>();
            foreach (ZoneArea a in Ch0.Areas)
                foreach (LockSite h in LockSiteTable.OpenHouses(Ch0, a.ZoneId))
                    all.Add($"{a.ZoneId}:{h.Variant}@{h.Center}");
            foreach (ZoneArea a in Ch0.Areas)
                foreach (LockSite h in LockSiteTable.OpenHouses(Ch0, a.ZoneId))
                    Assert.IsTrue(seen.Add(h.Variant), $"짜임이 겹친다: {h.Variant} — {string.Join(", ", all)}");
            Assert.Contains("ㅁ자-통", new List<string>(seen));
            Assert.IsTrue(seen.Contains("ㄷ자+행랑") || seen.Contains("ㄷ자-안채"), $"ㄷ자 집터 — {string.Join(", ", all)}");   // v7 — 양반가(36×34m)는 0장 마을엔 자리가 없을 수 있다
            Assert.GreaterOrEqual(seen.Count, 8);
        }
    }
}
