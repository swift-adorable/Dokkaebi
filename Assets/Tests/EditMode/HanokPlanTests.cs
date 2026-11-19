using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>결정 2-151 v6 — 한옥 집터 짜임 (참고전용_한옥내부구조 175장).</summary>
    public class HanokPlanTests
    {
        private static IEnumerable<HanokPlan> All()
        {
            foreach (var e in HanokPlan.Table)
                for (int seed = 0; seed < 6; seed++)   // 거울 뒤집기 · 칸 폭 흔들림까지
                    yield return HanokPlan.Make(e.Variant, e.Size, new System.Random(seed));
        }

        [Test]
        public void 짜임_이름은_서로_다르다()
        {
            var seen = new HashSet<string>();
            foreach (var e in HanokPlan.Table) Assert.IsTrue(seen.Add(e.Variant), e.Variant);
            Assert.GreaterOrEqual(seen.Count, 10);
        }

        [Test]
        public void 방은_겹치지_않고_집터_안에_있다()
        {
            foreach (HanokPlan p in All())
            {
                var lot = new Rect(-p.Size.x * 0.5f, -p.Size.y * 0.5f, p.Size.x, p.Size.y);
                for (int i = 0; i < p.Rooms.Count; i++)
                {
                    Rect a = p.Rooms[i].R;
                    Assert.IsTrue(lot.Contains(a.min) && lot.Contains(a.max), $"{p.Variant} {a}");
                    Assert.GreaterOrEqual(Mathf.Min(a.width, a.height), 1.9f, $"{p.Variant} 방이 너무 좁다 {a}");
                    for (int j = i + 1; j < p.Rooms.Count; j++)
                    {
                        Rect b = p.Rooms[j].R;
                        Rect s = Rect.MinMaxRect(b.xMin + 0.05f, b.yMin + 0.05f, b.xMax - 0.05f, b.yMax - 0.05f);
                        Assert.IsFalse(a.Overlaps(s), $"{p.Variant} {a} · {b}");
                    }
                }
            }
        }

        [Test]
        public void 바깥에서_모든_방에_걸어_들어간다()
        {
            foreach (HanokPlan p in All())
                Assert.IsTrue(p.AllRoomsReachable(out string bad), bad);
        }

        [Test]
        public void 살림집엔_부엌이_있고_곳간채만_없다()
        {
            foreach (HanokPlan p in All())
            {
                bool kitchen = p.Rooms.Exists(r => r.Kind == HanokRoom.Kitchen);
                Assert.AreEqual(p.Variant != "곳간채", kitchen, p.Variant);
            }
        }

        [Test]
        public void 큰_집터는_여러_채가_마당을_둘러싼다()
        {
            foreach (string v in new[] { "ㅁ자-통", "ㄷ자+행랑", "ㅁ자-튼", "ㄱ자+곳간채", "안밖거리" })
            {
                var e = HanokPlan.Entry(v);
                HanokPlan p = HanokPlan.Make(v, e.Size, new System.Random(1));
                Assert.GreaterOrEqual(p.Buildings.Count, 2, v);
            }
            var y = HanokPlan.Entry("ㄷ자+행랑");
            HanokPlan yb = HanokPlan.Make("ㄷ자+행랑", y.Size, new System.Random(2));
            Assert.IsTrue(yb.Rooms.Exists(r => r.Kind == HanokRoom.Gate), "행랑채 대문간");
            Assert.IsTrue(yb.Rooms.Exists(r => r.Kind == HanokRoom.Pavilion), "사랑채 누마루");
            Assert.IsTrue(yb.Rooms.Exists(r => r.Kind == HanokRoom.Shrine), "사당");
        }

        [Test]
        public void 같은_씨앗이면_같은_집이다()
        {
            var e = HanokPlan.Entry("ㅁ자-튼");
            HanokPlan a = HanokPlan.Make(e.Variant, e.Size, new System.Random(9)), b = HanokPlan.Make(e.Variant, e.Size, new System.Random(9));
            Assert.AreEqual(a.Rooms.Count, b.Rooms.Count);
            for (int i = 0; i < a.Rooms.Count; i++) Assert.AreEqual(a.Rooms[i].R, b.Rooms[i].R);
        }
    }
}
