using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>【임시】 철수 지점 — 2곳 · 25~35m · 원 안에서 5초 버티기 (결정 2-78).</summary>
    public class ExtractionTests
    {
        private static float FlatDistance(Vector3 a, Vector3 b)
            => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        [Test]
        public void 가운데서_시작하면_두_곳이_25에서_35m_구역_안에_선다()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                Vector3[] points = ExtractionTable.PickPoints(Vector3.zero, new System.Random(seed));

                Assert.AreEqual(ExtractionTable.PointCount, points.Length);
                foreach (Vector3 p in points)
                {
                    float d = FlatDistance(p, Vector3.zero);
                    Assert.That(d, Is.InRange(ExtractionTable.MinDistance - 0.01f, ExtractionTable.MaxDistance + 0.01f), $"seed {seed}");
                    Assert.IsTrue(ExtractionTable.Inside(p), $"seed {seed}: {p}");
                }
            }
        }

        [Test]
        public void 두_곳은_한쪽에_몰리지_않는다()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                Vector3[] points = ExtractionTable.PickPoints(Vector3.zero, new System.Random(seed));
                Assert.That(FlatDistance(points[0], points[1]), Is.GreaterThan(ExtractionTable.MinDistance), $"seed {seed}");
            }
        }

        [Test]
        public void 구석에서_시작해도_구역_밖으로_나가지_않는다()
        {
            var start = new Vector3(40f, 0f, -40f);

            for (int seed = 0; seed < 200; seed++)
                foreach (Vector3 p in ExtractionTable.PickPoints(start, new System.Random(seed)))
                    Assert.IsTrue(ExtractionTable.Inside(p), $"seed {seed}: {p}");
        }

        [Test]
        public void 원_안은_바닥_거리로만_잰다()
        {
            var point = new Vector3(10f, 0f, 10f);

            Assert.IsTrue(ExtractionTable.InCircle(new Vector3(11f, 5f, 11f), point), "높이는 보지 않는다");
            Assert.IsFalse(ExtractionTable.InCircle(new Vector3(13f, 0f, 10f), point));
        }

        [Test]
        public void 가장_가까운_곳을_고른다()
        {
            var points = new[] { new Vector3(30f, 0f, 0f), new Vector3(-10f, 0f, 0f) };

            Assert.AreEqual(1, ExtractionTable.Nearest(Vector3.zero, points));
            Assert.AreEqual(0, ExtractionTable.Nearest(new Vector3(25f, 0f, 0f), points));
            Assert.AreEqual(-1, ExtractionTable.Nearest(Vector3.zero, new Vector3[0]));
        }

        [Test]
        public void 원_안에서_5초를_버티면_철수한다()
        {
            var channel = new ExtractionChannel();

            Assert.AreEqual(ExtractionStep.Started, channel.Tick(true, 1f));
            Assert.AreEqual(ExtractionStep.Holding, channel.Tick(true, 2f));
            Assert.AreEqual(2f, channel.Remaining, 0.001f);
            Assert.AreEqual(ExtractionStep.Completed, channel.Tick(true, 2f));
            Assert.IsTrue(channel.Completed);

            Assert.AreEqual(ExtractionStep.Idle, channel.Tick(true, 1f), "한 번 끝나면 다시 세지 않는다");
        }

        [Test]
        public void 원을_벗어나면_처음부터다()
        {
            var channel = new ExtractionChannel();

            channel.Tick(true, 4f);
            Assert.AreEqual(ExtractionStep.Cancelled, channel.Tick(false, 0.1f));
            Assert.AreEqual(0f, channel.Elapsed);
            Assert.AreEqual(ExtractionStep.Idle, channel.Tick(false, 0.1f));

            Assert.AreEqual(ExtractionStep.Started, channel.Tick(true, 4f));
            Assert.AreEqual(ExtractionStep.Holding, channel.Tick(true, 0.5f), "앞의 4초는 남지 않는다");
        }

        [Test]
        public void 시간이_멈추면_세지_않는다()
        {
            var channel = new ExtractionChannel();

            channel.Tick(true, 0f);
            channel.Tick(true, 0f);
            Assert.AreEqual(0f, channel.Elapsed);
            Assert.IsTrue(channel.Holding);
            Assert.IsFalse(channel.Completed);
        }
    }
}
