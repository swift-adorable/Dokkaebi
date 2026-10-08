using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 【창이 뜨면 게임이 멈춘다】 (결정 2-92) — 창 바탕이 켜져 있는 동안만 멈추고,
    /// 닫히거나 사라지면 다시 흐른다. 사망(GameOver) 등 GameManager의 멈춤과 함께 본다.
    /// </summary>
    public class GamePauseTests
    {
        private GameObject window;

        [SetUp]
        public void Make() => window = new GameObject("TestWindow");

        [TearDown]
        public void Clean()
        {
            if (window != null)
                Object.DestroyImmediate(window);

            GamePause.Refresh();
            Time.timeScale = 1f;
        }

        [Test]
        public void 창이_켜져_있으면_멈춘다()
        {
            GamePause.Register(window);

            Assert.IsTrue(GamePause.IsHeld);
            Assert.AreEqual(0f, Time.timeScale, "창이 떠 있는데 적이 움직입니다.");
        }

        [Test]
        public void 창을_닫으면_다시_흐른다()
        {
            GamePause.Register(window);
            window.SetActive(false);
            GamePause.Refresh();

            Assert.IsFalse(GamePause.IsHeld);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void 창이_사라지면_다시_흐른다()
        {
            GamePause.Register(window);
            Object.DestroyImmediate(window);
            window = null;
            GamePause.Refresh();

            Assert.IsFalse(GamePause.IsHeld, "부서진 창이 계속 붙잡고 있습니다.");
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void 플레이_중이고_창이_없을_때만_흐른다()
        {
            Assert.IsTrue(GamePause.ShouldRun(true, false));
            Assert.IsFalse(GamePause.ShouldRun(true, true));
            Assert.IsFalse(GamePause.ShouldRun(false, false), "사망 화면에서 시간이 흐릅니다.");
        }
    }
}
