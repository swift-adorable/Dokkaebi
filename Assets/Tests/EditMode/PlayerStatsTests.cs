using NUnit.Framework;
using UnityEngine;

namespace Blob.Tests
{
    /// <summary>
    /// 각성 레벨 — 이 게임의 유일한 레벨. (결정 2-33)
    /// </summary>
    public class PlayerStatsTests
    {
        private GameObject host;
        private PlayerStats stats;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("PlayerStatsTest");
            stats = host.AddComponent<PlayerStats>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
        }

        [Test]
        public void 되살리면_그_레벨의_필요량이_다시_계산된다()
        {
            stats.Restore(level: 8, experience: 3);

            Assert.AreEqual(8, stats.Level);
            Assert.AreEqual(3, stats.CurrentXP);
            Assert.AreEqual(stats.RequiredXPAt(8), stats.RequiredXP);
        }

        [Test]
        public void 필요량을_넘는_경험치는_잘린다()
        {
            // 곡선을 바꾼 뒤 옛 세이브를 열면 생긴다. 그대로 두면 다음 한 번의
            // 경험치에 레벨이 여러 칸 튄다.
            stats.Restore(level: 2, experience: 99999);

            Assert.Less(stats.CurrentXP, stats.RequiredXP);
        }

        [Test]
        public void 레벨은_1_아래로_내려가지_않는다()
        {
            stats.Restore(level: 0, experience: -5);

            Assert.AreEqual(1, stats.Level);
            Assert.AreEqual(0, stats.CurrentXP);
        }

        [Test]
        public void 되살려도_바뀜을_알린다()
        {
            // 패시브 화면이 이 알림으로 「요구 Lv」 회색 처리를 다시 그린다.
            int raised = 0;
            stats.OnChanged += () => raised++;

            stats.Restore(5, 0);

            Assert.AreEqual(1, raised);
        }
    }
}
