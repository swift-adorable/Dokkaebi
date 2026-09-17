using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// 투사체 행동 우선순위 엔진 테스트.
    ///
    /// 이 우선순위가 Skill 조합 설계의 근간이다.
    /// 순서가 틀리거나 한 충돌에 둘 이상이 발동하면 수치 인플레가 발생한다.
    /// </summary>
    public class ProjectileBehaviourStateTests
    {
        [Test]
        public void 아무_행동도_없으면_None을_반환한다()
        {
            var state = new ProjectileBehaviourState();

            Assert.IsFalse(state.HasAny);
            Assert.AreEqual(ProjectileBehaviourType.None, state.ConsumeNext());
        }

        [Test]
        public void 우선순위대로_하나씩_소비된다()
        {
            var state = new ProjectileBehaviourState
            {
                SplitRemaining = 1,
                PierceRemaining = 1,
                ForkRemaining = 1,
                ChainRemaining = 1,
                ReturnRemaining = 1
            };

            Assert.AreEqual(ProjectileBehaviourType.Split, state.ConsumeNext());
            Assert.AreEqual(ProjectileBehaviourType.Pierce, state.ConsumeNext());
            Assert.AreEqual(ProjectileBehaviourType.Fork, state.ConsumeNext());
            Assert.AreEqual(ProjectileBehaviourType.Chain, state.ConsumeNext());
            Assert.AreEqual(ProjectileBehaviourType.Return, state.ConsumeNext());
            Assert.AreEqual(ProjectileBehaviourType.None, state.ConsumeNext());
        }

        [Test]
        public void 충돌_한_번에_행동_하나만_소비된다()
        {
            var state = new ProjectileBehaviourState
            {
                ForkRemaining = 2,
                ChainRemaining = 3
            };

            state.ConsumeNext();

            Assert.AreEqual(1, state.ForkRemaining, "Fork만 1 소비되어야 합니다.");
            Assert.AreEqual(3, state.ChainRemaining, "Chain은 손대지 않아야 합니다.");
        }

        [Test]
        public void Fork와_Chain을_함께_가지면_Fork가_먼저다()
        {
            var state = new ProjectileBehaviourState
            {
                ForkRemaining = 1,
                ChainRemaining = 1
            };

            Assert.AreEqual(ProjectileBehaviourType.Fork, state.ConsumeNext(),
                "곱해지는 것이 아니라 순차 단계가 되어야 합니다.");
            Assert.AreEqual(ProjectileBehaviourType.Chain, state.ConsumeNext());
        }

        [Test]
        public void Pierce는_Fork보다_먼저_판정된다()
        {
            var state = new ProjectileBehaviourState
            {
                PierceRemaining = 1,
                ForkRemaining = 1
            };

            Assert.AreEqual(ProjectileBehaviourType.Pierce, state.ConsumeNext());
        }

        [Test]
        public void 자식_상태는_남은_행동을_그대로_물려받는다()
        {
            var state = new ProjectileBehaviourState
            {
                ForkRemaining = 2,
                ChainRemaining = 1
            };

            state.ConsumeNext();   // Fork 1 소비

            ProjectileBehaviourState child = state.CreateChildState();

            Assert.AreEqual(1, child.ForkRemaining);
            Assert.AreEqual(1, child.ChainRemaining);
        }

        [Test]
        public void 자식_상태는_부모와_독립적이다()
        {
            var state = new ProjectileBehaviourState { ChainRemaining = 2 };

            ProjectileBehaviourState child = state.CreateChildState();
            child.ConsumeNext();

            Assert.AreEqual(2, state.ChainRemaining,
                "구조체 복사가 아니라 참조가 되면 형제 투사체끼리 횟수를 나눠 갖게 됩니다.");
            Assert.AreEqual(1, child.ChainRemaining);
        }

        [Test]
        public void GetSet으로_행동_횟수를_조작할_수_있다()
        {
            var state = new ProjectileBehaviourState();

            state.SetRemaining(ProjectileBehaviourType.Chain, 4);

            Assert.AreEqual(4, state.GetRemaining(ProjectileBehaviourType.Chain));
            Assert.AreEqual(4, state.TotalRemaining);
        }

        // ── 엣지 케이스 ──────────────────────────────────────────

        [Test]
        public void 엣지_음수_설정은_0으로_보정된다()
        {
            var state = new ProjectileBehaviourState();

            state.SetRemaining(ProjectileBehaviourType.Fork, -5);

            Assert.AreEqual(0, state.GetRemaining(ProjectileBehaviourType.Fork));
            Assert.IsFalse(state.HasAny);
        }

        [Test]
        public void 엣지_None에_대한_조작은_무시된다()
        {
            var state = new ProjectileBehaviourState();

            Assert.DoesNotThrow(() => state.SetRemaining(ProjectileBehaviourType.None, 3));
            Assert.AreEqual(0, state.GetRemaining(ProjectileBehaviourType.None));
            Assert.AreEqual(0, state.TotalRemaining);
        }

        [Test]
        public void 엣지_소진_후_반복_호출해도_음수가_되지_않는다()
        {
            var state = new ProjectileBehaviourState { PierceRemaining = 1 };

            state.ConsumeNext();

            for (int i = 0; i < 5; i++)
                Assert.AreEqual(ProjectileBehaviourType.None, state.ConsumeNext());

            Assert.AreEqual(0, state.PierceRemaining);
            Assert.AreEqual(0, state.TotalRemaining);
        }

        [Test]
        public void 엣지_Clear는_모든_행동을_제거한다()
        {
            var state = new ProjectileBehaviourState
            {
                SplitRemaining = 3,
                ForkRemaining = 2,
                ReturnRemaining = 1
            };

            state.Clear();

            Assert.IsFalse(state.HasAny);
            Assert.AreEqual(ProjectileBehaviourType.None, state.ConsumeNext());
        }
    }
}
