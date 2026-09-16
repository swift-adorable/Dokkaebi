using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// 합성 발사 테스트. (확정 기획 — Core 2개는 한 발에 합쳐진다)
    ///
    /// 핵심 규약: 적재 계열 Core가 2개여도 탄 수는 늘지 않고, 상태만 번갈아 실린다.
    /// 두 상태를 매 발사마다 동시에 걸면 Support 「이차 주입」이 무가치해진다.
    /// </summary>
    public class CompositeFireStateTests
    {
        [Test]
        public void 적재_Core가_없으면_부여할_상태가_없다()
        {
            var state = new CompositeFireState();

            Assert.AreEqual(-1, state.NextAilmentIndex(0));
            Assert.AreEqual(-1, state.NextAilmentIndex(-3));
        }

        [Test]
        public void 적재_Core가_1개면_항상_그것만_부여한다()
        {
            var state = new CompositeFireState();

            for (int i = 0; i < 5; i++)
                Assert.AreEqual(0, state.NextAilmentIndex(1));
        }

        [Test]
        public void 적재_Core가_2개면_발사마다_번갈아_부여한다()
        {
            var state = new CompositeFireState();

            Assert.AreEqual(0, state.NextAilmentIndex(2));
            Assert.AreEqual(1, state.NextAilmentIndex(2));
            Assert.AreEqual(0, state.NextAilmentIndex(2));
            Assert.AreEqual(1, state.NextAilmentIndex(2));
        }

        [Test]
        public void 각_상태의_적용_빈도는_정확히_절반이다()
        {
            // 「이차 주입」의 대가(적용 빈도 절반)와 같은 수준이어야
            // 해당 Support가 무가치해지지 않는다.
            var state = new CompositeFireState();

            int first = 0;
            int second = 0;

            for (int i = 0; i < 100; i++)
            {
                if (state.NextAilmentIndex(2) == 0)
                    first++;
                else
                    second++;
            }

            Assert.AreEqual(50, first);
            Assert.AreEqual(50, second);
        }

        [Test]
        public void 런_도중_적재_Core가_늘어도_안전하다()
        {
            // 엣지 케이스: 1개로 쏘다가 레벨업으로 2번째 적재 Core를 얻는 경우.
            var state = new CompositeFireState();

            state.NextAilmentIndex(1);
            state.NextAilmentIndex(1);

            Assert.AreEqual(0, state.NextAilmentIndex(2));
            Assert.AreEqual(1, state.NextAilmentIndex(2));
        }

        [Test]
        public void 런_도중_적재_Core가_줄어도_범위를_벗어나지_않는다()
        {
            var state = new CompositeFireState();

            state.NextAilmentIndex(2);
            state.NextAilmentIndex(2);

            int index = state.NextAilmentIndex(1);

            Assert.AreEqual(0, index, "인덱스가 목록 범위를 벗어나면 안 됩니다.");
        }

        [Test]
        public void Reset은_첫_번째_상태부터_다시_시작한다()
        {
            var state = new CompositeFireState();

            state.NextAilmentIndex(2);

            Assert.AreEqual(1, state.Cursor);

            state.Reset();

            Assert.AreEqual(0, state.Cursor);
            Assert.AreEqual(0, state.NextAilmentIndex(2));
        }
    }
}
