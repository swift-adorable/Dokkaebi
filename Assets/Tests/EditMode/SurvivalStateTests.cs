using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// 수분 · 에너지. (docs/Blob_Survival_System.md · 결정 2-32)
    ///
    /// 지켜야 할 것 —
    ///   1. 수분이 에너지보다 먼저 바닥난다. 동시에 비면 게이지가 하나인 것과 같다.
    ///   2. 바닥나도 즉사시키지 않는다. 빠져나갈 시간이 남아야 한다.
    ///   3. 무거우면 빨리 마른다. 이 시스템을 넣는 이유가 그것이다.
    /// </summary>
    public class SurvivalStateTests
    {
        /// <summary>한 번에 큰 시간을 흘리지 않고 잘게 나눈다 — 실제 프레임처럼.</summary>
        private static int Advance(SurvivalState state, float seconds, float multiplier = 1f)
        {
            int damage = 0;

            const float Step = 0.5f;

            for (float t = 0f; t < seconds; t += Step)
                damage += state.Tick(Step, multiplier);

            return damage;
        }

        // ── 소모 ──────────────────────────────────────────────────────

        [Test]
        public void 시작할_때는_가득_차_있다()
        {
            var state = new SurvivalState();

            Assert.AreEqual(SurvivalTable.MaxWater, state.Water, 0.001f);
            Assert.AreEqual(SurvivalTable.MaxEnergy, state.Energy, 0.001f);
            Assert.IsFalse(state.IsDehydrated);
            Assert.IsFalse(state.IsStarving);
        }

        [Test]
        public void 시간이_지나면_줄어든다()
        {
            var state = new SurvivalState();

            Advance(state, 60f);

            Assert.AreEqual(100f - 6f, state.Water, 0.01f, "초당 0.10 × 60초");
            Assert.AreEqual(100f - 3.6f, state.Energy, 0.01f, "초당 0.06 × 60초");
        }

        [Test]
        public void 수분이_에너지보다_먼저_바닥난다()
        {
            // 둘이 같은 순간에 비면 게이지를 두 개 둘 이유가 없다.
            var state = new SurvivalState();

            Advance(state, 17f * 60f);

            Assert.IsTrue(state.IsDehydrated, "16분 40초면 수분이 비어야 합니다.");
            Assert.IsFalse(state.IsStarving, "에너지는 아직 남아 있어야 합니다.");
        }

        [Test]
        public void 음수로_내려가지_않는다()
        {
            var state = new SurvivalState();

            Advance(state, 60f * 60f);

            Assert.AreEqual(0f, state.Water, 0.001f);
            Assert.AreEqual(0f, state.Energy, 0.001f);
        }

        // ── 소지 중량 ─────────────────────────────────────────────────

        [Test]
        public void 무거우면_빨리_마른다()
        {
            // 【이 시스템을 넣는 이유다.】 과중량이 이동만 깎으면
            // 「일단 다 들고 간다」가 늘 정답에 가깝다.
            var light = new SurvivalState();
            var heavy = new SurvivalState();

            Advance(light, 300f, SurvivalTable.DrainMultiplier(EncumbranceLevel.Normal));
            Advance(heavy, 300f, SurvivalTable.DrainMultiplier(EncumbranceLevel.Immobile));

            Assert.Less(heavy.Water, light.Water, "무거운 쪽이 물을 더 썼어야 합니다.");

            float lightUsed = 100f - light.Water;
            float heavyUsed = 100f - heavy.Water;

            Assert.AreEqual(1.40f, heavyUsed / lightUsed, 0.01f, "움직일 수 없음 = ×1.40");
        }

        [Test]
        public void 과중량_단계마다_배율이_오른다()
        {
            Assert.AreEqual(1.00f, SurvivalTable.DrainMultiplier(EncumbranceLevel.Normal), 0.001f);
            Assert.AreEqual(1.10f, SurvivalTable.DrainMultiplier(EncumbranceLevel.Heavy), 0.001f);
            Assert.AreEqual(1.25f, SurvivalTable.DrainMultiplier(EncumbranceLevel.Overloaded), 0.001f);
            Assert.AreEqual(1.40f, SurvivalTable.DrainMultiplier(EncumbranceLevel.Immobile), 0.001f);
        }

        // ── 페널티 ────────────────────────────────────────────────────

        [Test]
        public void 바닥나기_전에는_페널티가_없다()
        {
            var state = new SurvivalState();

            Advance(state, 60f);

            Assert.AreEqual(1f, state.MoveMultiplier, 0.001f);
            Assert.AreEqual(1f, state.EnergyRestoreMultiplier, 0.001f);
        }

        [Test]
        public void 탈수는_이동을_깎고_음식을_덜_차게_한다()
        {
            // 덕코프 「체력(스태미나) 회복 −70%」 그대로. Blob은 스태미나를
            // 에너지에 합쳤으므로 에너지가 차는 양에 붙는다.
            var state = new SurvivalState();

            state.Drain(SurvivalTable.MaxWater, 0f);

            Assert.IsTrue(state.IsDehydrated);
            Assert.IsFalse(state.IsStarving);
            Assert.AreEqual(SurvivalTable.DehydratedMoveScale, state.MoveMultiplier, 0.001f);
            Assert.AreEqual(0.30f, state.EnergyRestoreMultiplier, 0.001f);
        }

        [Test]
        public void 목마르면_먹어도_덜_찬다()
        {
            // 【물부터 마셔야 음식이 제값을 한다.】 두 게이지가 엮이는 자리다.
            var state = new SurvivalState();

            state.Drain(SurvivalTable.MaxWater, 40f);

            float before = state.Energy;

            state.Restore(0f, 20f);

            Assert.AreEqual(before + 6f, state.Energy, 0.01f, "20의 30%인 6만 찹니다.");
        }

        [Test]
        public void 물을_먼저_마시면_음식이_제값을_한다()
        {
            var state = new SurvivalState();

            state.Drain(SurvivalTable.MaxWater, 40f);

            float before = state.Energy;

            state.Restore(50f, 0f);   // 먼저 마신다
            state.Restore(0f, 20f);   // 그 다음 먹는다

            Assert.AreEqual(before + 20f, state.Energy, 0.01f, "온전히 20이 찹니다.");
        }

        [Test]
        public void 배율은_부르기_전의_상태로_정한다()
        {
            // 한 번의 호출 안에서 물을 먼저 더하고 재면, 물과 음식이 한 아이템에
            // 들어 있을 때와 따로 먹을 때의 결과가 달라진다.
            var state = new SurvivalState();

            state.Drain(SurvivalTable.MaxWater, 40f);

            float before = state.Energy;

            state.Restore(50f, 20f);   // 한 번에

            Assert.AreEqual(before + 6f, state.Energy, 0.01f,
                "목이 마른 채로 부른 것이므로 30%만 찹니다.");
        }

        [Test]
        public void 체력_회복에는_손대지_않는다()
        {
            // 이 축의 존재 이유는 추출 압박이다. 회복약을 덜 듣게 만드는 것은
            // 전투 페널티라 방침에서 벗어난다. (Survival_System 6절)
            var state = new SurvivalState();

            state.Drain(SurvivalTable.MaxWater, SurvivalTable.MaxEnergy);

            Assert.IsTrue(state.IsDehydrated && state.IsStarving);

            // 굶주려도 에너지 회복 배율은 탈수분만 걸린다 — 자기 자신을 막지 않는다.
            Assert.AreEqual(SurvivalTable.DehydratedEnergyRestoreScale,
                state.EnergyRestoreMultiplier, 0.001f);
        }

        [Test]
        public void 굶주림_자체는_회복을_막지_않는다()
        {
            // 덕코프의 배고픔은 스태미나 회복을 깎지만, Blob은 그 축이 에너지라
            // 그대로 옮기면 자기 자신을 가리켜 빠져나올 수 없는 나선이 된다.
            var state = new SurvivalState();

            state.Drain(0f, SurvivalTable.MaxEnergy);

            Assert.IsTrue(state.IsStarving);
            Assert.IsFalse(state.IsDehydrated);
            Assert.AreEqual(1f, state.EnergyRestoreMultiplier, 0.001f);

            state.Restore(0f, 30f);

            Assert.AreEqual(30f, state.Energy, 0.01f, "온전히 찹니다.");
        }

        [Test]
        public void 둘_다_바닥나면_곱한다()
        {
            // 더하면 페널티가 하나 더 생겼을 때 0 이하로 떨어진다. 곱은 0이 되지 않는다.
            var state = new SurvivalState();

            state.Drain(SurvivalTable.MaxWater, SurvivalTable.MaxEnergy);

            Assert.AreEqual(
                SurvivalTable.DehydratedMoveScale * SurvivalTable.StarvingMoveScale,
                state.MoveMultiplier, 0.001f);

            Assert.Greater(state.MoveMultiplier, 0f, "곱은 0이 되지 않습니다.");
        }

        // ── 허기 ──────────────────────────────────────────────────────

        [Test]
        public void 허기는_중첩이_천천히_오른다()
        {
            var state = new SurvivalState();

            state.Drain(0f, SurvivalTable.MaxEnergy);

            Assert.AreEqual(1, state.StarvingStacks, "굶자마자 1중첩");

            Advance(state, SurvivalTable.StarvingStackInterval + 1f);
            Assert.AreEqual(2, state.StarvingStacks);

            Advance(state, SurvivalTable.StarvingStackInterval);
            Assert.AreEqual(3, state.StarvingStacks);

            Advance(state, SurvivalTable.StarvingStackInterval * 5f);
            Assert.AreEqual(SurvivalTable.StarvingMaxStacks, state.StarvingStacks, "상한을 넘지 않습니다.");
        }

        [Test]
        public void 허기는_이초마다_중첩만큼_아프다()
        {
            var state = new SurvivalState();

            state.Drain(0f, SurvivalTable.MaxEnergy);

            // 1중첩 구간 10초 = 5회 × 1
            int damage = Advance(state, 10f);

            Assert.AreEqual(5, damage);
        }

        [Test]
        public void 굶어도_빠져나갈_시간이_남는다()
        {
            // 【바닥나도 즉사시키지 않는다.】 (Survival_System 6절)
            var state = new SurvivalState();

            state.Drain(0f, SurvivalTable.MaxEnergy);

            int damage = Advance(state, 60f);

            Assert.Less(damage, 100, "1분 만에 체력 100이 사라지면 안 됩니다.");
        }

        [Test]
        public void 먹으면_중첩이_풀린다()
        {
            var state = new SurvivalState();

            state.Drain(0f, SurvivalTable.MaxEnergy);
            Advance(state, SurvivalTable.StarvingStackInterval * 3f);

            Assert.AreEqual(3, state.StarvingStacks);

            state.Restore(0f, 50f);

            Assert.IsFalse(state.IsStarving);
            Assert.AreEqual(0, state.StarvingStacks);

            // 다시 굶으면 1중첩부터 시작한다.
            state.Drain(0f, 50f);
            Assert.AreEqual(1, state.StarvingStacks);
        }

        [Test]
        public void 프레임이_튀어도_밀린_피해를_전부_준다()
        {
            // 「버벅이면 안 아프다」가 되면 안 된다.
            var state = new SurvivalState();

            state.Drain(0f, SurvivalTable.MaxEnergy);

            int damage = state.Tick(10f);

            Assert.AreEqual(5, damage, "10초 = 2초 간격 5회 × 1중첩");
        }

        // ── 채우기 ────────────────────────────────────────────────────

        [Test]
        public void 최대치를_넘겨_채우지_못한다()
        {
            var state = new SurvivalState();

            Advance(state, 60f);
            state.Restore(999f, 999f);

            Assert.AreEqual(SurvivalTable.MaxWater, state.Water, 0.001f);
            Assert.AreEqual(SurvivalTable.MaxEnergy, state.Energy, 0.001f);
        }

        [Test]
        public void 출격하면_다시_가득_찬다()
        {
            var state = new SurvivalState();

            state.Drain(100f, 100f);
            Advance(state, 90f);

            state.Refill();

            Assert.AreEqual(SurvivalTable.MaxWater, state.Water, 0.001f);
            Assert.AreEqual(0, state.StarvingStacks, "중첩도 같이 풀려야 합니다.");
        }

        [Test]
        public void 최대치를_줄이면_지금_값도_잘린다()
        {
            var state = new SurvivalState();

            state.SetMax(40f, 40f);

            Assert.AreEqual(40f, state.Water, 0.001f);
            Assert.AreEqual(40f, state.MaxWater, 0.001f);
        }
    }
}
