using System.Collections.Generic;
using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// 상태이상 상태 기계 테스트. (docs/Blob_Combat_Baseline.md 7절)
    ///
    /// 시간을 주입받는 순수 클래스이므로 EditMode에서 전수 검증한다.
    /// </summary>
    public class StatusEffectStateTests
    {
        private readonly List<DamageRequest> buffer = new List<DamageRequest>();

        [SetUp]
        public void SetUp() => buffer.Clear();

        [Test]
        public void 상태를_걸면_지속시간만큼_유지된다()
        {
            var state = new StatusEffectState();
            state.Apply(StatusEffectType.Ignite, 10f);

            Assert.IsTrue(state.Has(StatusEffectType.Ignite));
            Assert.AreEqual(4f, state.RemainingOf(StatusEffectType.Ignite), 0.001f);

            state.Tick(3.9f, false, buffer);
            Assert.IsTrue(state.Has(StatusEffectType.Ignite));

            state.Tick(0.2f, false, buffer);
            Assert.IsFalse(state.Has(StatusEffectType.Ignite));
        }

        /// <summary>지속시간 전체를 지정한 틱 간격으로 굴려 총 피해를 센다.</summary>
        private int TotalOver(StatusEffectType type, float baseDamage, float tick, int tickCount)
        {
            var state = new StatusEffectState();
            state.Apply(type, baseDamage);

            int total = 0;

            for (int i = 0; i < tickCount; i++)
            {
                state.Tick(tick, false, buffer);

                foreach (var r in buffer)
                    total += r.baseDamage;
            }

            return total;
        }

        [Test]
        public void 점화의_총_피해는_기본_피해_한_발과_같다()
        {
            // 즉발과 도트의 총량을 맞추고, 차이는 방어도 무시로 낸다.
            Assert.AreEqual(10, TotalOver(StatusEffectType.Ignite, 10f, 0.5f, 8));
        }

        [Test]
        public void 도트_총량은_틱_간격과_무관하다()
        {
            // 회귀 테스트 — 틱마다 올림하면 잘게 쪼갤수록 총 피해가 부풀어 오른다.
            // 실제로 0.5초 틱에서 점화 10이 16이 되는 버그가 있었다.
            Assert.AreEqual(10, TotalOver(StatusEffectType.Ignite, 10f, 4f, 1), "1틱");
            Assert.AreEqual(10, TotalOver(StatusEffectType.Ignite, 10f, 1f, 4), "4틱");
            Assert.AreEqual(10, TotalOver(StatusEffectType.Ignite, 10f, 0.5f, 8), "8틱");
            Assert.AreEqual(10, TotalOver(StatusEffectType.Ignite, 10f, 0.1f, 40), "40틱");
            Assert.AreEqual(10, TotalOver(StatusEffectType.Ignite, 10f, 1f / 60f, 240), "60fps");
        }

        [Test]
        public void 중독_총량도_틱_간격과_무관하다()
        {
            // 10 × 0.15 × 6초 = 9
            Assert.AreEqual(9, TotalOver(StatusEffectType.Poison, 10f, 6f, 1));
            Assert.AreEqual(9, TotalOver(StatusEffectType.Poison, 10f, 0.1f, 60));
            Assert.AreEqual(9, TotalOver(StatusEffectType.Poison, 10f, 1f / 60f, 360));
        }

        [Test]
        public void 중독만_크게_중첩한다()
        {
            var state = new StatusEffectState();

            for (int i = 0; i < 15; i++)
                state.Apply(StatusEffectType.Poison, 10f);

            Assert.AreEqual(10, state.StacksOf(StatusEffectType.Poison), "최대 중첩은 10입니다.");

            var ignite = new StatusEffectState();
            ignite.Apply(StatusEffectType.Ignite, 10f);
            ignite.Apply(StatusEffectType.Ignite, 10f);

            Assert.AreEqual(1, ignite.StacksOf(StatusEffectType.Ignite), "점화는 갱신형입니다.");
        }

        [Test]
        public void 다시_걸면_지속시간이_갱신된다()
        {
            var state = new StatusEffectState();
            state.Apply(StatusEffectType.Ignite, 10f);
            state.Tick(3f, false, buffer);

            Assert.AreEqual(1f, state.RemainingOf(StatusEffectType.Ignite), 0.001f);

            state.Apply(StatusEffectType.Ignite, 10f);

            Assert.AreEqual(4f, state.RemainingOf(StatusEffectType.Ignite), 0.001f);
        }

        [Test]
        public void 낮은_피해로_덮어써서_도트를_약화시킬_수_없다()
        {
            // 엣지 케이스 — 약한 공격이 강한 도트를 덮어쓰면
            // "약한 무기를 섞으면 손해"라는 비직관적 규칙이 생긴다.
            var state = new StatusEffectState();
            state.Apply(StatusEffectType.Ignite, 100f);
            state.Apply(StatusEffectType.Ignite, 1f);

            state.Tick(1f, false, buffer);

            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(25, buffer[0].baseDamage, "100 × 0.25 = 25가 유지되어야 합니다.");
        }

        [Test]
        public void 출혈은_이동_중인_대상에게_두_배가_된다()
        {
            var still = new StatusEffectState();
            still.Apply(StatusEffectType.Bleed, 10f);
            still.Tick(1f, false, buffer);
            int stillDamage = buffer[0].baseDamage;

            var moving = new StatusEffectState();
            moving.Apply(StatusEffectType.Bleed, 10f);
            moving.Tick(1f, true, buffer);
            int movingDamage = buffer[0].baseDamage;

            Assert.AreEqual(stillDamage * 2, movingDamage);
        }

        [Test]
        public void 상태이상_피해는_방어도를_무시하는_요청으로_나간다()
        {
            var state = new StatusEffectState();
            state.Apply(StatusEffectType.Ignite, 10f);
            state.Tick(1f, false, buffer);

            Assert.AreEqual(1, buffer.Count);
            Assert.IsTrue(buffer[0].bypassArmour, "「갑각」의 대응 수단이 되려면 방어도를 무시해야 합니다.");
            Assert.AreEqual(DamageElement.Fire, buffer[0].element);
        }

        [Test]
        public void 감전_동결_응집은_피해가_없다()
        {
            foreach (var type in new[] { StatusEffectType.Shock, StatusEffectType.Freeze, StatusEffectType.Congeal })
            {
                var state = new StatusEffectState();
                state.Apply(type, 100f);
                state.Tick(1f, false, buffer);

                Assert.AreEqual(0, buffer.Count, $"{type}은 피해가 없어야 합니다.");
            }
        }

        [Test]
        public void 감전은_받는_피해를_20퍼센트_올린다()
        {
            var state = new StatusEffectState();
            Assert.AreEqual(1f, state.DamageTakenMultiplier, 0.0001f);

            state.Apply(StatusEffectType.Shock, 10f);
            Assert.AreEqual(1.2f, state.DamageTakenMultiplier, 0.0001f);
        }

        [Test]
        public void 동결은_속도를_40퍼센트_깎는다()
        {
            var state = new StatusEffectState();
            Assert.AreEqual(1f, state.SpeedMultiplier, 0.0001f);

            state.Apply(StatusEffectType.Freeze, 10f);
            Assert.AreEqual(0.6f, state.SpeedMultiplier, 0.0001f);
        }

        [Test]
        public void 응집은_상태_전이_범위를_두_배로_만든다()
        {
            var state = new StatusEffectState();
            Assert.AreEqual(1f, state.SpreadMultiplier, 0.0001f);

            state.Apply(StatusEffectType.Congeal, 10f);
            Assert.AreEqual(2f, state.SpreadMultiplier, 0.0001f);
        }

        [Test]
        public void 기폭으로_상태를_제거할_수_있다()
        {
            var state = new StatusEffectState();
            state.Apply(StatusEffectType.Ignite, 10f);

            state.Clear(StatusEffectType.Ignite);

            Assert.IsFalse(state.Has(StatusEffectType.Ignite));
            Assert.AreEqual(0, state.StacksOf(StatusEffectType.Ignite));
        }

        [Test]
        public void ClearAll은_전부_초기화한다()
        {
            // 풀 재사용 시 이전 런의 상태가 남으면 안 된다.
            var state = new StatusEffectState();
            state.Apply(StatusEffectType.Ignite, 10f);
            state.Apply(StatusEffectType.Shock, 10f);

            Assert.IsTrue(state.HasAny);

            state.ClearAll();

            Assert.IsFalse(state.HasAny);
            Assert.AreEqual(1f, state.DamageTakenMultiplier, 0.0001f);
        }

        [Test]
        public void 남은_시간보다_큰_deltaTime은_남은_만큼만_피해를_준다()
        {
            // 엣지 케이스 — 프레임 드랍 시 도트가 과다 피해를 주면 안 된다.
            var state = new StatusEffectState();
            state.Apply(StatusEffectType.Ignite, 10f);

            state.Tick(100f, false, buffer);

            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(10, buffer[0].baseDamage, "4초치(=10)를 넘으면 안 됩니다.");
            Assert.IsFalse(state.Has(StatusEffectType.Ignite));
        }

        [Test]
        public void None과_0_이하_deltaTime은_무시된다()
        {
            var state = new StatusEffectState();

            state.Apply(StatusEffectType.None, 10f);
            Assert.IsFalse(state.HasAny);

            state.Apply(StatusEffectType.Ignite, 10f);
            state.Tick(0f, false, buffer);

            Assert.AreEqual(0, buffer.Count);
            Assert.AreEqual(4f, state.RemainingOf(StatusEffectType.Ignite), 0.001f);
        }

        [Test]
        public void 중첩된_중독은_총_피해가_비례해서_커진다()
        {
            // 한 틱만 보면 소수 잔여분 때문에 정확히 비례하지 않는다.
            // 지속시간 전체의 총합으로 비교해야 의미 있는 검증이 된다.
            int one = TotalOver(StatusEffectType.Poison, 10f, 0.1f, 60);

            var triple = new StatusEffectState();
            for (int i = 0; i < 3; i++)
                triple.Apply(StatusEffectType.Poison, 10f);

            int three = 0;

            for (int i = 0; i < 60; i++)
            {
                triple.Tick(0.1f, false, buffer);

                foreach (var r in buffer)
                    three += r.baseDamage;
            }

            Assert.AreEqual(one * 3, three);
        }
    }
}
