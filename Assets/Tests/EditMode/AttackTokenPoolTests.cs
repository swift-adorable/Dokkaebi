using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// 공격 차례표. 「동시에 몇이 덤비는가」를 고정한다.
    ///
    /// 이 수가 조용히 늘어나면 난이도가 아니라 부당함이 늘어난다.
    /// </summary>
    public class AttackTokenPoolTests
    {
        [Test]
        public void 정원만큼만_차례를_쥔다()
        {
            var pool = new AttackTokenPool(2);

            Assert.IsTrue(pool.TryAcquire(1, 0f));
            Assert.IsTrue(pool.TryAcquire(2, 0f));
            Assert.IsFalse(pool.TryAcquire(3, 0f), "정원을 넘겨 덤비면 피할 방법이 없습니다.");

            Assert.AreEqual(2, pool.HeldCount);
        }

        [Test]
        public void 이미_쥔_적은_다시_요청해도_자리를_더_쓰지_않는다()
        {
            var pool = new AttackTokenPool(1);

            Assert.IsTrue(pool.TryAcquire(1, 0f));
            Assert.IsTrue(pool.TryAcquire(1, 0.5f));

            Assert.AreEqual(1, pool.HeldCount);
        }

        [Test]
        public void 놓으면_다음_적이_들어온다()
        {
            var pool = new AttackTokenPool(1);

            pool.TryAcquire(1, 0f);
            Assert.IsFalse(pool.TryAcquire(2, 0f));

            pool.Release(1);

            Assert.IsTrue(pool.TryAcquire(2, 0f));
        }

        [Test]
        public void 임대가_끝나면_자동으로_회수된다()
        {
            // 차례를 쥔 적이 죽거나 끼어서 반납하지 못하면 나머지가 영원히 돌기만 한다.
            var pool = new AttackTokenPool(1);

            pool.TryAcquire(1, 0f, lease: 2f);

            Assert.IsFalse(pool.TryAcquire(2, 1f));
            Assert.IsTrue(pool.TryAcquire(2, 2.5f));
            Assert.IsFalse(pool.Holds(1));
        }

        [Test]
        public void 정원은_1_미만이_될_수_없다()
        {
            var pool = new AttackTokenPool(0);

            Assert.AreEqual(1, pool.Capacity);

            pool.Capacity = -5;

            Assert.AreEqual(1, pool.Capacity);
        }

        [Test]
        public void 정원을_늘리면_즉시_더_들어온다()
        {
            var pool = new AttackTokenPool(1);

            pool.TryAcquire(1, 0f);
            Assert.IsFalse(pool.TryAcquire(2, 0f));

            pool.Capacity = 3;

            Assert.IsTrue(pool.TryAcquire(2, 0f));
            Assert.IsTrue(pool.TryAcquire(3, 0f));
        }

        [Test]
        public void Clear는_전부_비운다()
        {
            var pool = new AttackTokenPool(3);

            pool.TryAcquire(1, 0f);
            pool.TryAcquire(2, 0f);

            pool.Clear();

            Assert.AreEqual(0, pool.HeldCount);
        }
    }
}
