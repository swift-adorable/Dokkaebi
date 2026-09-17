using System.Collections.Generic;
using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// Core 슬롯 개방 시점과 소켓 지정 테스트. (확정 기획)
    ///
    /// 2번째 Core는 Lv7에 열린다. 상한 2개는 그대로이므로 v5 §10-9 위반이 아니다.
    /// 소켓 탈착 비용을 없앤 대신 장착 시점의 선택이 되돌릴 수 없으므로,
    /// 어느 Core에 넣을지 정확히 전달되는지 검증한다.
    /// </summary>
    public class CoreSlotAndSocketTests
    {
        private static SkillDefinition Core(string id, SkillTag tags) =>
            SkillTestFactory.CreateCore(id, tags);

        private static SkillDefinition Support(string id, SkillTag requiredTags) =>
            SkillTestFactory.CreateSupport(id, requiredTags);

        [Test]
        public void Lv7_미만에서는_Core를_1개만_보유한다()
        {
            Assert.AreEqual(1, RunSkillState.GetCoreCapacity(1));
            Assert.AreEqual(1, RunSkillState.GetCoreCapacity(6));
        }

        [Test]
        public void Lv7부터_Core를_2개_보유한다()
        {
            Assert.AreEqual(2, RunSkillState.GetCoreCapacity(7));
            Assert.AreEqual(2, RunSkillState.GetCoreCapacity(20));
            Assert.AreEqual(RunSkillState.MaxCores, RunSkillState.GetCoreCapacity(7));
        }

        [Test]
        public void 슬롯이_1개면_2번째_Core를_획득할_수_없다()
        {
            var state = new RunSkillState { CoreCapacity = 1 };

            Assert.IsTrue(state.TryAcquire(Core("core_1", SkillTag.Projectile)));
            Assert.IsFalse(state.TryAcquire(Core("core_2", SkillTag.Fire)));
            Assert.AreEqual(1, state.Cores.Count);
            Assert.IsTrue(state.IsSecondCoreLocked);
        }

        [Test]
        public void 슬롯이_열리면_2번째_Core를_획득할_수_있다()
        {
            var state = new RunSkillState { CoreCapacity = 1 };

            state.TryAcquire(Core("core_1", SkillTag.Projectile));

            state.CoreCapacity = RunSkillState.GetCoreCapacity(7);

            Assert.IsFalse(state.IsSecondCoreLocked);
            Assert.IsTrue(state.TryAcquire(Core("core_2", SkillTag.Fire)));
            Assert.AreEqual(2, state.Cores.Count);
        }

        [Test]
        public void 슬롯_수는_1과_2_사이로_강제된다()
        {
            var state = new RunSkillState { CoreCapacity = 0 };
            Assert.AreEqual(1, state.CoreCapacity);

            state.CoreCapacity = 99;
            Assert.AreEqual(RunSkillState.MaxCores, state.CoreCapacity);
        }

        [Test]
        public void 잠긴_슬롯은_선택_풀에서도_Core를_제외한다()
        {
            var state = new RunSkillState { CoreCapacity = 1 };

            SkillDefinition first = Core("core_1", SkillTag.Projectile);
            SkillDefinition second = Core("core_2", SkillTag.Fire);

            state.TryAcquire(first);

            List<SkillDefinition> candidates = SkillSelectionPool.Build(
                new List<SkillDefinition> { first, second }, state, 5);

            Assert.AreEqual(0, candidates.Count,
                "슬롯이 잠겨 있으면 2번째 Core가 선택지에 뜨면 안 됩니다.");
        }

        [Test]
        public void 장착_가능한_Core가_하나면_목록에_하나만_나온다()
        {
            var state = new RunSkillState();

            state.TryAcquire(Core("core_projectile", SkillTag.Projectile));
            state.TryAcquire(Core("core_zone", SkillTag.Zone));

            var eligible = state.GetEligibleCoreIndices(Support("sup_p", SkillTag.Projectile));

            Assert.AreEqual(1, eligible.Count);
            Assert.AreEqual(0, eligible[0]);
        }

        [Test]
        public void 두_Core가_모두_조건을_만족하면_둘_다_후보다()
        {
            // 이 경우에만 UI가 유저에게 장착 위치를 묻는다.
            var state = new RunSkillState();

            state.TryAcquire(Core("core_a", SkillTag.Projectile));
            state.TryAcquire(Core("core_b", SkillTag.Projectile | SkillTag.Fire));

            var eligible = state.GetEligibleCoreIndices(Support("sup_p", SkillTag.Projectile));

            Assert.AreEqual(2, eligible.Count);
        }

        [Test]
        public void 소켓이_가득_찬_Core는_후보에서_빠진다()
        {
            var state = new RunSkillState();

            state.TryAcquire(Core("core_a", SkillTag.Projectile));
            state.TryAcquire(Core("core_b", SkillTag.Projectile));

            // core_a의 소켓 3개를 모두 채운다.
            for (int i = 0; i < RunSkillState.SocketsPerCore; i++)
                state.TryAcquire(Support($"sup_fill_{i}", SkillTag.Projectile), 0);

            var eligible = state.GetEligibleCoreIndices(Support("sup_next", SkillTag.Projectile));

            Assert.AreEqual(1, eligible.Count);
            Assert.AreEqual(1, eligible[0]);
        }

        [Test]
        public void 지정한_Core_소켓에_정확히_장착된다()
        {
            var state = new RunSkillState();

            state.TryAcquire(Core("core_a", SkillTag.Projectile));
            state.TryAcquire(Core("core_b", SkillTag.Projectile));

            Assert.IsTrue(state.TryAcquire(Support("sup_p", SkillTag.Projectile), 1));

            Assert.AreEqual(0, state.GetSockets(0).Count);
            Assert.AreEqual(1, state.GetSockets(1).Count);
            Assert.AreEqual("sup_p", state.GetSockets(1)[0].Id);
        }

        [Test]
        public void 지정이_유효하지_않으면_자동으로_배치된다()
        {
            // 엣지 케이스: UI가 낡은 인덱스를 넘겨도 장착 자체가 실패하면 안 된다.
            var state = new RunSkillState();

            state.TryAcquire(Core("core_a", SkillTag.Projectile));

            Assert.IsTrue(state.TryAcquire(Support("sup_a", SkillTag.Projectile), 99));
            Assert.AreEqual(1, state.GetSockets(0).Count);

            Assert.IsTrue(state.TryAcquire(Support("sup_b", SkillTag.Projectile), -1));
            Assert.AreEqual(2, state.GetSockets(0).Count);
        }

        [Test]
        public void 태그를_만족하지_않는_Core를_지정하면_가능한_곳으로_넘어간다()
        {
            var state = new RunSkillState();

            state.TryAcquire(Core("core_zone", SkillTag.Zone));
            state.TryAcquire(Core("core_projectile", SkillTag.Projectile));

            // 0번(지대)은 투사체 Support를 받을 수 없다.
            Assert.IsTrue(state.TryAcquire(Support("sup_p", SkillTag.Projectile), 0));

            Assert.AreEqual(0, state.GetSockets(0).Count);
            Assert.AreEqual(1, state.GetSockets(1).Count);
        }

        [Test]
        public void Core가_아닌_정의에는_후보_목록이_비어_있다()
        {
            var state = new RunSkillState();

            state.TryAcquire(Core("core_a", SkillTag.Projectile));

            Assert.AreEqual(0, state.GetEligibleCoreIndices(null).Count);
            Assert.AreEqual(0,
                state.GetEligibleCoreIndices(SkillTestFactory.CreateCore("core_x")).Count);
        }

        [Test]
        public void Clear는_슬롯_수도_기본값으로_되돌린다()
        {
            var state = new RunSkillState { CoreCapacity = 1 };

            state.TryAcquire(Core("core_a", SkillTag.Projectile));
            state.Clear();

            Assert.AreEqual(RunSkillState.MaxCores, state.CoreCapacity);
        }
    }
}
