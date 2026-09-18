using NUnit.Framework;
using UnityEngine;

namespace Blob.Tests
{
    /// <summary>
    /// 교전 판단. 「적이 무작정 달라붙지 않는다」를 거리별로 고정한다.
    ///
    /// 이 규칙이 무너지면 전투가 조용히 예전으로 돌아간다 —
    /// 화면으로는 "좀 이상한데?" 정도로만 보이고 원인을 찾기 어렵다.
    /// 그래서 눈이 아니라 테스트로 잡는다.
    /// </summary>
    public class EngagementPlannerTests
    {
        private static EngagementInput Ranged(float distance, bool token = true,
                                              bool reloading = false, bool sight = true)
        {
            return new EngagementInput
            {
                distance = distance,
                preferredDistance = 7f,
                band = 1.2f,
                attackRange = 9f,
                detectDistance = 18f,
                hasAttackToken = token,
                isReloading = reloading,
                hasLineOfSight = sight
            };
        }

        [Test]
        public void 인지_범위_밖이면_아무것도_하지_않는다()
        {
            EngagementPlan plan = EngagementPlanner.Plan(Ranged(30f));

            Assert.AreEqual(EngagementPhase.Idle, plan.phase);
            Assert.IsFalse(plan.mayAttack);
            Assert.IsFalse(plan.IsMoving);
        }

        [Test]
        public void 멀면_붙는다()
        {
            EngagementPlan plan = EngagementPlanner.Plan(Ranged(15f));

            Assert.AreEqual(EngagementPhase.Advance, plan.phase);
            Assert.Greater(plan.approach, 0f);
        }

        [Test]
        public void 적정_거리에서는_붙지도_물러나지도_않고_돈다()
        {
            EngagementPlan plan = EngagementPlanner.Plan(Ranged(7f));

            Assert.AreEqual(EngagementPhase.Hold, plan.phase);
            Assert.AreEqual(0f, plan.approach, 0.0001f);
            Assert.Greater(plan.strafe, 0f, "제자리에 서 있으면 사람처럼 보이지 않습니다.");
            Assert.IsTrue(plan.mayAttack);
        }

        [Test]
        public void 너무_붙으면_쏘면서_물러난다()
        {
            EngagementPlan plan = EngagementPlanner.Plan(Ranged(2f));

            Assert.AreEqual(EngagementPhase.Back, plan.phase);
            Assert.Less(plan.approach, 0f);
            Assert.IsTrue(plan.mayAttack, "물러나는 동안에도 쏜다. 멈춰서 물러나기만 하면 무해합니다.");
        }

        [Test]
        public void 차례가_아니면_쏘지_않고_더_바깥에서_돈다()
        {
            // 이것이 없으면 다섯 마리가 동시에 달려들어 피할 방법이 사라진다.
            EngagementPlan withToken = EngagementPlanner.Plan(Ranged(7f));
            EngagementPlan without = EngagementPlanner.Plan(Ranged(7f, token: false));

            Assert.IsTrue(withToken.mayAttack);

            Assert.AreEqual(EngagementPhase.Reposition, without.phase);
            Assert.IsFalse(without.mayAttack);
            Assert.Greater(without.strafe, 0f);
        }

        [Test]
        public void 차례가_아니면_유지_거리보다_더_멀리_선다()
        {
            // 적정 거리(7)에 있으면 대기 거리(7×1.25=8.75)보다 안쪽이므로 물러나야 한다.
            EngagementPlan plan = EngagementPlanner.Plan(Ranged(7f, token: false));

            Assert.Less(plan.approach, 0f);
        }

        [Test]
        public void 재장전_중에는_쏘지_않고_거리를_벌린다()
        {
            EngagementPlan plan = EngagementPlanner.Plan(Ranged(7f, reloading: true));

            Assert.AreEqual(EngagementPhase.Reload, plan.phase);
            Assert.IsFalse(plan.mayAttack);
            Assert.Less(plan.approach, 0f);
        }

        [Test]
        public void 재장전이_차례보다_우선한다()
        {
            // 차례를 쥔 채로 재장전에 들어가도 쏘면 안 된다.
            EngagementPlan plan = EngagementPlanner.Plan(Ranged(7f, token: true, reloading: true));

            Assert.AreEqual(EngagementPhase.Reload, plan.phase);
            Assert.IsFalse(plan.mayAttack);
        }

        [Test]
        public void 보이지_않으면_각을_잡으러_붙는다()
        {
            EngagementPlan plan = EngagementPlanner.Plan(Ranged(10f, sight: false));

            Assert.AreEqual(EngagementPhase.Advance, plan.phase);
            Assert.Greater(plan.approach, 0f);
            Assert.IsFalse(plan.mayAttack, "보이지도 않는데 쏘면 벽을 향해 쏘게 됩니다.");
        }

        [Test]
        public void 붙는_도중이라도_사거리에_닿으면_쏜다()
        {
            // 사거리 9, 유지 거리 7+밴드 1.2 = 8.2. 그 사이(8.5)는 붙으면서 쏘는 구간이다.
            EngagementPlan plan = EngagementPlanner.Plan(Ranged(8.5f));

            Assert.AreEqual(EngagementPhase.Advance, plan.phase);
            Assert.IsTrue(plan.mayAttack, "멈춰 설 이유가 없습니다.");
        }

        [Test]
        public void 근접_적도_옆으로_돈다()
        {
            var input = new EngagementInput
            {
                distance = 1.2f,
                preferredDistance = 1.2f,
                band = 0.4f,
                attackRange = 1.6f,
                detectDistance = 14f,
                hasAttackToken = true,
                isReloading = false,
                hasLineOfSight = true
            };

            EngagementPlan plan = EngagementPlanner.Plan(in input);

            Assert.AreEqual(EngagementPhase.Hold, plan.phase);
            Assert.Greater(plan.strafe, 0f);
        }

        // ── 방향 합성 ─────────────────────────────────────────────────────

        [Test]
        public void 접근_성분만_있으면_대상_방향으로_간다()
        {
            var plan = new EngagementPlan { approach = 1f, strafe = 0f };

            Vector3 direction = EngagementPlanner.ToDirection(in plan, Vector3.forward, 1f);

            Assert.AreEqual(Vector3.forward, direction);
        }

        [Test]
        public void 후퇴_성분은_반대_방향이다()
        {
            var plan = new EngagementPlan { approach = -1f, strafe = 0f };

            Vector3 direction = EngagementPlanner.ToDirection(in plan, Vector3.forward, 1f);

            Assert.AreEqual(Vector3.back, direction);
        }

        [Test]
        public void 도는_방향_부호가_바뀌면_반대로_돈다()
        {
            var plan = new EngagementPlan { approach = 0f, strafe = 1f };

            Vector3 right = EngagementPlanner.ToDirection(in plan, Vector3.forward, 1f);
            Vector3 left = EngagementPlanner.ToDirection(in plan, Vector3.forward, -1f);

            Assert.AreEqual(-right.x, left.x, 0.0001f);
            Assert.AreNotEqual(right.x, 0f);
        }

        [Test]
        public void 이동_성분이_전부_0이면_방향도_0이다()
        {
            var plan = new EngagementPlan { approach = 0f, strafe = 0f };

            Assert.AreEqual(Vector3.zero,
                EngagementPlanner.ToDirection(in plan, Vector3.forward, 1f));
        }
    }
}
