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

        /// <summary>이미 상태가 걸린 state의 총 피해를 끝까지 뽑아낸다.</summary>
        private int DrainTotal(StatusEffectState state, float duration, int tickCount)
        {
            float tick = duration / tickCount;
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
            // 1중첩 = 10 × 0.05 × 6초 = 3
            Assert.AreEqual(3, TotalOver(StatusEffectType.Poison, 10f, 6f, 1));
            Assert.AreEqual(3, TotalOver(StatusEffectType.Poison, 10f, 0.1f, 60));
            Assert.AreEqual(3, TotalOver(StatusEffectType.Poison, 10f, 1f / 60f, 360));
        }

        /// <summary>
        /// 【중첩이 피해를 늘리는 것은 중독뿐이다.】
        /// 셋 다 중첩하면 "쌓아서 녹인다"는 카오스의 정체성이 사라진다.
        /// </summary>
        [Test]
        public void 중독만_중첩하고_점화와_출혈은_갱신형이다()
        {
            var poison = new StatusEffectState();

            // 9번까지는 쌓인다. 10번째에 임계(부식)로 전이하며 원본이 비워진다.
            for (int i = 0; i < 9; i++)
                poison.Apply(StatusEffectType.Poison, 10f);

            Assert.AreEqual(9, poison.StacksOf(StatusEffectType.Poison), "중독은 중첩합니다.");

            var ignite = new StatusEffectState();
            ignite.Apply(StatusEffectType.Ignite, 10f);
            ignite.Apply(StatusEffectType.Ignite, 10f);

            Assert.AreEqual(1, ignite.StacksOf(StatusEffectType.Ignite), "점화는 갱신형입니다.");

            var bleed = new StatusEffectState();
            bleed.Apply(StatusEffectType.Bleed, 10f);
            bleed.Apply(StatusEffectType.Bleed, 10f);

            Assert.AreEqual(1, bleed.StacksOf(StatusEffectType.Bleed), "출혈도 갱신형입니다.");
        }

        /// <summary>
        /// 중독 10중첩의 총량이 기본 피해의 3배를 넘지 않아야 한다.
        ///
        /// 이전 계수 0.15는 10중첩에서 9배였다. 10번 맞히는 데 4초면 되고
        /// 지속이 6초라 실제로 도달하므로, 도트가 직접 피해의 두 배 가까이 됐다.
        /// </summary>
        [Test]
        public void 중독_최대_중첩의_총량이_기본_피해의_세_배다()
        {
            const float BaseDamage = 10f;

            var state = new StatusEffectState();

            // 임계 전이 직전(9중첩)에서 측정한다. 10번째는 부식으로 넘어간다.
            for (int i = 0; i < 9; i++)
                state.Apply(StatusEffectType.Poison, BaseDamage);

            int total = DrainTotal(state, 6f, 60);

            Assert.LessOrEqual(total, (int)(BaseDamage * 3f),
                "중독 도트가 기본 피해의 3배를 넘습니다. 직접 피해를 압도합니다.");

            Assert.Greater(total, (int)BaseDamage,
                "쌓은 보람이 있어야 합니다. 한 발보다는 세야 합니다.");
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

        /// <summary>
        /// 통제형 상태의 세기는 중첩에 비례하고 최대 중첩에서 문서 수치가 된다.
        /// 1중첩에 전부 주면 한 발만 맞혀도 −40% 감속이 되어 과하다.
        /// </summary>
        [Test]
        public void 감전은_중첩에_비례해_최대_20퍼센트를_올린다()
        {
            var state = new StatusEffectState();
            Assert.AreEqual(1f, state.DamageTakenMultiplier, 0.0001f);

            state.Apply(StatusEffectType.Shock, 10f);

            float one = state.DamageTakenMultiplier;

            Assert.Greater(one, 1f, "1중첩에서도 효과는 있어야 합니다.");
            Assert.Less(one, 1.2f, "1중첩에 전부 주면 안 됩니다.");

            // 5번째까지 쌓는다. 6번째는 마비로 전이하며 원본이 비워진다.
            for (int i = 1; i < 5; i++)
                state.Apply(StatusEffectType.Shock, 10f);

            Assert.Greater(state.DamageTakenMultiplier, one, "쌓을수록 세져야 합니다.");
        }

        [Test]
        public void 냉각은_중첩에_비례해_최대_40퍼센트를_깎는다()
        {
            var state = new StatusEffectState();
            Assert.AreEqual(1f, state.SpeedMultiplier, 0.0001f);

            state.Apply(StatusEffectType.Chill, 10f);

            float one = state.SpeedMultiplier;

            Assert.Less(one, 1f, "냉각은 느리게 만듭니다.");
            Assert.Greater(one, 0.6f, "1중첩에 −40%를 전부 주면 안 됩니다.");

            for (int i = 1; i < 5; i++)
                state.Apply(StatusEffectType.Chill, 10f);

            Assert.Less(state.SpeedMultiplier, one, "쌓을수록 느려져야 합니다.");
        }

        // ── 임계 상태 ─────────────────────────────────────────────────

        /// <summary>
        /// 【최대 중첩 = 임계】 차는 순간 질적으로 다른 것이 된다.
        /// 원본 중첩을 전부 소모하므로 임계가 끝나면 처음부터 다시 쌓아야 한다.
        /// </summary>
        [Test]
        public void 냉각이_최대_중첩에_차면_동결로_전이한다()
        {
            var state = new StatusEffectState();

            for (int i = 0; i < 6; i++)
                state.Apply(StatusEffectType.Chill, 10f);

            Assert.IsTrue(state.Has(StatusEffectType.Freeze), "동결로 전이해야 합니다.");

            Assert.AreEqual(0, state.StacksOf(StatusEffectType.Chill),
                "전이하면 원본 중첩이 전부 소모되어야 합니다.");

            Assert.IsTrue(state.IsIncapacitated, "동결은 행동 불능입니다.");
            Assert.AreEqual(0f, state.SpeedMultiplier, 0.0001f);
        }

        [Test]
        public void 감전이_최대_중첩에_차면_마비로_전이한다()
        {
            var state = new StatusEffectState();

            for (int i = 0; i < 6; i++)
                state.Apply(StatusEffectType.Shock, 10f);

            Assert.IsTrue(state.Has(StatusEffectType.Paralyze));
            Assert.AreEqual(0, state.StacksOf(StatusEffectType.Shock));
            Assert.IsTrue(state.IsIncapacitated, "마비도 행동 불능입니다.");
        }

        [Test]
        public void 중독이_최대_중첩에_차면_부식으로_전이한다()
        {
            var state = new StatusEffectState();

            for (int i = 0; i < 10; i++)
                state.Apply(StatusEffectType.Poison, 10f);

            Assert.IsTrue(state.Has(StatusEffectType.Corrode));
            Assert.AreEqual(0, state.StacksOf(StatusEffectType.Poison));

            Assert.AreEqual(0.5f, state.ArmourMultiplier, 0.0001f, "부식은 방어도를 절반으로 만듭니다.");
            Assert.AreEqual(0.5f, state.HealingMultiplier, 0.0001f, "부식은 회복량도 절반으로 만듭니다.");

            Assert.IsFalse(state.IsIncapacitated, "부식은 행동 불능이 아닙니다.");
        }

        /// <summary>
        /// 임계 상태 중에는 원본을 다시 쌓지 못한다.
        /// 그러지 않으면 행동 불능 중에 게이지가 다시 차 무한 제압이 된다.
        /// </summary>
        [Test]
        public void 임계_상태_중에는_원본을_다시_쌓지_못한다()
        {
            var state = new StatusEffectState();

            for (int i = 0; i < 6; i++)
                state.Apply(StatusEffectType.Chill, 10f);

            Assert.IsTrue(state.Has(StatusEffectType.Freeze));

            for (int i = 0; i < 6; i++)
                state.Apply(StatusEffectType.Chill, 10f);

            Assert.AreEqual(0, state.StacksOf(StatusEffectType.Chill),
                "동결 중에 냉각이 다시 쌓이면 무한 제압이 됩니다.");
        }

        [Test]
        public void 점화는_방어도를_깎는다()
        {
            var state = new StatusEffectState();

            Assert.AreEqual(0f, state.ArmourReduction, 0.0001f);

            state.Apply(StatusEffectType.Ignite, 10f);

            Assert.Greater(state.ArmourReduction, 0f,
                "점화가 방어도를 깎지 않으면 「다음 피해를 키운다」가 성립하지 않습니다.");
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
