using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// HealthPool 단위 테스트.
    ///
    /// 플레이어와 적이 같은 체력 계산을 공유하므로, 여기서의 오류는
    /// 전투 전반(즉사, 무한 체력, 사망 후 부활)에 직접 영향을 준다.
    /// </summary>
    public class HealthPoolTests
    {
        [Test]
        public void 생성시_체력이_가득_찬다()
        {
            var pool = new HealthPool(10);

            Assert.AreEqual(10, pool.Max);
            Assert.AreEqual(10, pool.Current);
            Assert.IsTrue(pool.IsFull);
            Assert.IsFalse(pool.IsDead);
            Assert.AreEqual(1f, pool.Normalized, 0.0001f);
        }

        [Test]
        public void TakeDamage는_실제_피해량을_반환한다()
        {
            var pool = new HealthPool(10);

            Assert.AreEqual(3, pool.TakeDamage(3));
            Assert.AreEqual(7, pool.Current);
        }

        [Test]
        public void 체력이_0이_되면_사망_상태가_된다()
        {
            var pool = new HealthPool(5);

            pool.TakeDamage(5);

            Assert.IsTrue(pool.IsDead);
            Assert.AreEqual(0, pool.Current);
            Assert.AreEqual(0f, pool.Normalized, 0.0001f);
        }

        [Test]
        public void Heal은_실제_회복량을_반환한다()
        {
            var pool = new HealthPool(10);

            pool.TakeDamage(6);   // 10 -> 4

            Assert.AreEqual(4, pool.Heal(4), "부족한 만큼만 회복되어야 합니다.");
            Assert.AreEqual(8, pool.Current);

            Assert.AreEqual(2, pool.Heal(10), "최대치를 넘는 회복분은 버려집니다.");
            Assert.AreEqual(10, pool.Current);
        }

        [Test]
        public void ResetToFull은_체력을_완전히_회복한다()
        {
            var pool = new HealthPool(8);

            pool.TakeDamage(8);
            Assert.IsTrue(pool.IsDead);

            pool.ResetToFull();

            Assert.IsFalse(pool.IsDead, "풀 재사용 시 이게 되지 않으면 적이 스폰 즉시 죽습니다.");
            Assert.AreEqual(8, pool.Current);
        }

        [Test]
        public void SetMax_리필_없이_변경하면_현재_체력이_잘린다()
        {
            var pool = new HealthPool(10);

            pool.SetMax(4, refill: false);

            Assert.AreEqual(4, pool.Max);
            Assert.AreEqual(4, pool.Current, "현재 체력이 최대치를 넘으면 안 됩니다.");
        }

        [Test]
        public void SetMax_리필하면_새_최대치로_가득_찬다()
        {
            var pool = new HealthPool(5);

            pool.TakeDamage(4);
            pool.SetMax(20, refill: true);

            Assert.AreEqual(20, pool.Current);
        }

        // ── 엣지 케이스 ──────────────────────────────────────────

        [Test]
        public void 엣지_남은_체력보다_큰_피해는_남은_만큼만_적용된다()
        {
            var pool = new HealthPool(10);

            pool.TakeDamage(7);

            Assert.AreEqual(3, pool.TakeDamage(999),
                "오버킬이 그대로 반환되면 흡혈/처치보상 계산이 부풀려집니다.");
            Assert.AreEqual(0, pool.Current);
        }

        [Test]
        public void 엣지_사망_후_추가_피해는_0을_반환한다()
        {
            var pool = new HealthPool(3);

            pool.TakeDamage(3);

            Assert.AreEqual(0, pool.TakeDamage(5),
                "사망 후에도 피해가 적용되면 사망 이벤트가 중복 발행됩니다.");
        }

        [Test]
        public void 엣지_사망_상태에서는_회복되지_않는다()
        {
            var pool = new HealthPool(3);

            pool.TakeDamage(3);

            Assert.AreEqual(0, pool.Heal(10), "사망한 대상이 저절로 부활하면 안 됩니다.");
            Assert.IsTrue(pool.IsDead);
        }

        [Test]
        public void 엣지_0이하_피해와_회복은_무시된다()
        {
            var pool = new HealthPool(10);

            Assert.AreEqual(0, pool.TakeDamage(0));
            Assert.AreEqual(0, pool.TakeDamage(-5), "음수 피해가 회복으로 둔갑하면 안 됩니다.");
            Assert.AreEqual(10, pool.Current);

            pool.TakeDamage(5);

            Assert.AreEqual(0, pool.Heal(0));
            Assert.AreEqual(0, pool.Heal(-3));
            Assert.AreEqual(5, pool.Current);
        }

        [Test]
        public void 엣지_최대_체력을_가득_찬_상태에서_회복하면_0이다()
        {
            var pool = new HealthPool(10);

            Assert.AreEqual(0, pool.Heal(5));
            Assert.AreEqual(10, pool.Current);
        }

        [Test]
        public void 엣지_최대_체력_0이하는_1로_보정된다()
        {
            var pool = new HealthPool(0);

            Assert.AreEqual(1, pool.Max, "최대 체력 0이면 생성 즉시 사망 상태가 됩니다.");
            Assert.IsFalse(pool.IsDead);

            pool.SetMax(-10, refill: true);

            Assert.AreEqual(1, pool.Max);
        }
    }
}
