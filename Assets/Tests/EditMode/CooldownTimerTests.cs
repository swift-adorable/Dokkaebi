using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// CooldownTimer 단위 테스트.
    ///
    /// 사격 연사 제한과 대시 쿨다운이 이 구조체 하나에 의존하므로,
    /// 여기서의 경계 조건 오류는 전투 감각에 직접 영향을 준다.
    /// </summary>
    public class CooldownTimerTests
    {
        [Test]
        public void 최초에는_즉시_사용_가능하다()
        {
            var timer = new CooldownTimer();

            Assert.IsTrue(timer.IsReady(0f));
            Assert.AreEqual(0f, timer.RemainingTime(0f));
        }

        [Test]
        public void TryConsume_성공시_true를_반환하고_대기시간을_건다()
        {
            var timer = new CooldownTimer();

            Assert.IsTrue(timer.TryConsume(10f, 2f));
            Assert.IsFalse(timer.IsReady(10f));
            Assert.IsFalse(timer.IsReady(11.9f));
        }

        [Test]
        public void 대기시간이_지나면_다시_사용_가능하다()
        {
            var timer = new CooldownTimer();

            timer.TryConsume(10f, 2f);

            Assert.IsTrue(timer.IsReady(12f), "경계 시점(정확히 쿨다운 종료)에서 사용 가능해야 합니다.");
            Assert.IsTrue(timer.TryConsume(12f, 2f));
        }

        [Test]
        public void 대기중_TryConsume은_false이며_시간을_연장하지_않는다()
        {
            var timer = new CooldownTimer();

            timer.TryConsume(10f, 2f);

            Assert.IsFalse(timer.TryConsume(11f, 2f), "대기 중에는 소비할 수 없습니다.");
            Assert.IsTrue(timer.IsReady(12f),
                "실패한 시도가 쿨다운을 연장하면 연사 중 영구히 막히는 버그가 됩니다.");
        }

        [Test]
        public void RemainingTime은_남은_시간을_정확히_보고한다()
        {
            var timer = new CooldownTimer();

            timer.TryConsume(10f, 2f);

            Assert.AreEqual(2f, timer.RemainingTime(10f), 0.0001f);
            Assert.AreEqual(0.5f, timer.RemainingTime(11.5f), 0.0001f);
        }

        [Test]
        public void Reset은_대기시간을_즉시_해제한다()
        {
            var timer = new CooldownTimer();

            timer.TryConsume(10f, 5f);
            Assert.IsFalse(timer.IsReady(11f));

            timer.Reset();

            Assert.IsTrue(timer.IsReady(11f));
        }

        [Test]
        public void 연사_시나리오_간격만큼만_발사된다()
        {
            var timer = new CooldownTimer();
            const float fireRate = 0.15f;

            int shots = 0;

            // 1초 동안 0.01초 간격으로 발사 시도
            for (int i = 0; i <= 100; i++)
            {
                if (timer.TryConsume(i * 0.01f, fireRate))
                    shots++;
            }

            // 1초 / 0.15초 = 6.67 -> 7발 (0, 0.15, 0.30 ... 0.90)
            Assert.AreEqual(7, shots, "연사 간격이 지켜지지 않으면 DPS 밸런스가 무너집니다.");
        }

        // ── 엣지 케이스 ──────────────────────────────────────────

        [Test]
        public void 엣지_쿨다운_0이면_매번_사용_가능하다()
        {
            var timer = new CooldownTimer();

            Assert.IsTrue(timer.TryConsume(5f, 0f));
            Assert.IsTrue(timer.TryConsume(5f, 0f));
            Assert.IsTrue(timer.TryConsume(5f, 0f));
        }

        [Test]
        public void 엣지_음수_쿨다운은_0으로_취급한다()
        {
            var timer = new CooldownTimer();

            Assert.IsTrue(timer.TryConsume(5f, -10f));
            Assert.IsTrue(timer.IsReady(5f),
                "음수 쿨다운이 과거 시각을 만들면 이후 계산이 꼬입니다.");
            Assert.AreEqual(0f, timer.RemainingTime(5f));
        }

        [Test]
        public void 엣지_시간이_역행해도_RemainingTime은_음수가_되지_않는다()
        {
            var timer = new CooldownTimer();

            timer.TryConsume(100f, 5f);

            // Time.timeScale 변경 등으로 기준 시간이 뒤로 갈 수 있다.
            Assert.GreaterOrEqual(timer.RemainingTime(50f), 0f);
            Assert.IsFalse(timer.IsReady(50f));
        }

        [Test]
        public void 엣지_초기_상태에서_음수_시각도_처리한다()
        {
            var timer = new CooldownTimer();

            Assert.IsTrue(timer.IsReady(-1f));
            Assert.AreEqual(0f, timer.RemainingTime(-1f));
        }
    }
}
