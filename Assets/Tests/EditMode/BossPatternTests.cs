using NUnit.Framework;
using UnityEngine;

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
            Assert.AreEqual(0.75f, BossPatternTable.GrowDamageTaken(s), 0.001f);

            for (int i = 0; i < 20; i++)
                s = BossPatternTable.NextScale(s, seen: false, 0.2f);
            Assert.AreEqual(0.8f, s, 0.001f);
            Assert.AreEqual(1.2f, BossPatternTable.GrowDamageTaken(s), 0.001f);
        }

        [Test]
        public void 처녀_몽달은_함께면_단단하다()
        {
            Assert.Less(BossPatternTable.PairDamageTaken(true), 1f);
            Assert.Greater(BossPatternTable.PairDamageTaken(false), 1f);
        }

        /// <summary>결정 2-113 — 삼족오가 한 번 내리꽂아 쓰러뜨리지 않는다 (6장 방어구 중간값 6.25, 점화는 방어도 무시 · 기준 피해만큼).</summary>
        [Test]
        public void 삼족오_내리꽂기는_한_번에_쓰러뜨리지_않는다()
        {
            int damage = BossDataTable.Of("samjogo").Damage[0];
            float hit = damage * BossPatternTable.DiveDamageMultiplier * DamageResolver.ArmourMultiplier(6.25f, 0f);
            float ignite = damage * BossPatternTable.DiveIgniteMultiplier;
            Assert.Less(hit + ignite, 40f, $"맞는 {hit:0} + 점화 {ignite:0}");
            Assert.Greater(BossPatternTable.DiveTelegraphSeconds, BossPatternTable.TelegraphSeconds);
        }

        /// <summary>결정 2-113 — 어둑시니는 내내 보고 쏘아도 받는 피해가 3/4 아래로 내려가지 않는다(처치 시간이 두 배가 되지 않게).</summary>
        [Test]
        public void 어둑시니는_내내_봐도_처치가_두_배가_되지_않는다()
        {
            Assert.GreaterOrEqual(BossPatternTable.GrowDamageTaken(1.8f), 0.75f);
            Assert.GreaterOrEqual(BossPatternTable.PairDamageTaken(true), 0.75f);
        }

        /// <summary>결정 2-114 — 위험 표시는 범위(테두리)와 남은 시간(차오르는 면)을 보인다.</summary>
        [Test]
        public void 위험_표시는_범위와_남은_시간을_보인다()
        {
            Assert.AreEqual(0f, BossPatternTable.TelegraphProgress(0f, 1f), 0.0001f);
            Assert.AreEqual(0.5f, BossPatternTable.TelegraphProgress(0.75f, 1.5f), 0.0001f);
            Assert.AreEqual(1f, BossPatternTable.TelegraphProgress(3f, 1f), 0.0001f);

            BossTelegraph circle = BossTelegraph.Circle(new Vector3(3f, 0f, 4f), 2.5f, Color.red, 1f);
            BossTelegraph ring = BossTelegraph.Ring(Vector3.zero, 2.5f, 6f, Color.red, 1f);
            BossTelegraph line = BossTelegraph.Line(Vector3.zero, Vector3.right, 13.6f, 3.2f, Color.red, 1f);
            try
            {
                LineRenderer edge = circle.GetComponentInChildren<LineRenderer>();
                Assert.IsNotNull(edge, "테두리");
                for (int i = 0; i < edge.positionCount; i++)
                {
                    Vector3 p = edge.GetPosition(i);
                    Assert.AreEqual(2.5f, new Vector2(p.x - 3f, p.z - 4f).magnitude, 0.01f, "테두리 = 맞는 범위");
                }
                Assert.Less(circle.Fill.localScale.x, 0.01f, "처음엔 비어 있다");
                Assert.AreEqual(5f, ring.Fill.localScale.x, 0.01f, "고리는 안전한 안쪽(2.5)에서 차오르기 시작");
                Assert.AreEqual(2, ring.GetComponentsInChildren<LineRenderer>().Length, "고리는 바깥 · 안쪽 테두리 둘");
                Assert.AreEqual(4, line.GetComponentInChildren<LineRenderer>().positionCount, "줄은 네모 테두리");
                Assert.IsNull(circle.GetComponentInChildren<Collider>(), "표시는 막지 않는다");
                foreach (Renderer r in circle.GetComponentsInChildren<MeshRenderer>())
                {
                    Color c = r.sharedMaterial.color;
                    Assert.AreEqual(BossTelegraph.FillColor.r, c.r, 0.001f, "채우는 색은 빨강");
                    Assert.AreEqual(BossTelegraph.FillColor.g, c.g, 0.001f, "채우는 색은 빨강");
                }
            }
            finally
            {
                Object.DestroyImmediate(circle.gameObject);
                Object.DestroyImmediate(ring.gameObject);
                Object.DestroyImmediate(line.gameObject);
            }
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
