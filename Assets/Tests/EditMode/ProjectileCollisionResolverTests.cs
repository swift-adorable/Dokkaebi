using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// 충돌 우선순위 큐 테스트. (v5 §10)
    ///
    /// "한 번의 충돌에는 단 하나만 해결된다"가 깨지면 곱연산 인플레가 발생하고
    /// Skill 조합 설계 전체가 무너진다. 그 약속을 전수 검증한다.
    /// </summary>
    public class ProjectileCollisionResolverTests
    {
        private static ProjectileBehaviourState State(
            int split = 0, int pierce = 0, int fork = 0, int chain = 0, int ret = 0)
        {
            return new ProjectileBehaviourState
            {
                SplitRemaining = split,
                PierceRemaining = pierce,
                ForkRemaining = fork,
                ChainRemaining = chain,
                ReturnRemaining = ret
            };
        }

        [Test]
        public void 분열은_3갈래로_갈라지고_자신은_소멸한다()
        {
            // v5 §6-1: "최초 적중 시 탄환이 3갈래로 갈라집니다"
            ProjectileBehaviourState state = State(split: 1);

            ProjectileCollisionResult result =
                ProjectileCollisionResolver.Resolve(ref state, false);

            Assert.AreEqual(ProjectileBehaviourType.Split, result.Behaviour);
            Assert.AreEqual(3, result.ChildCount);
            Assert.AreEqual(ProjectileCollisionResolver.SplitChildCount, result.ChildCount);
            Assert.IsFalse(result.KeepAlive);
        }

        [Test]
        public void Fork는_2갈래로_갈라진다()
        {
            ProjectileBehaviourState state = State(fork: 1);

            ProjectileCollisionResult result =
                ProjectileCollisionResolver.Resolve(ref state, false);

            Assert.AreEqual(ProjectileBehaviourType.Fork, result.Behaviour);
            Assert.AreEqual(2, result.ChildCount);
            Assert.IsFalse(result.KeepAlive);
        }

        [Test]
        public void 분열과_Fork의_갈래_수는_서로_다르다()
        {
            // 이전 구현은 Split과 Fork를 같은 코드로 처리해 둘 다 2갈래였다. 회귀 방지.
            Assert.AreNotEqual(
                ProjectileCollisionResolver.SplitChildCount,
                ProjectileCollisionResolver.ForkChildCount);
        }

        [Test]
        public void 관통은_궤도를_유지한_채_살아남는다()
        {
            ProjectileBehaviourState state = State(pierce: 3);

            ProjectileCollisionResult result =
                ProjectileCollisionResolver.Resolve(ref state, false);

            Assert.AreEqual(ProjectileBehaviourType.Pierce, result.Behaviour);
            Assert.AreEqual(0, result.ChildCount);
            Assert.IsTrue(result.KeepAlive);
            Assert.IsFalse(result.SeekNextTarget);
            Assert.AreEqual(2, state.PierceRemaining);
        }

        [Test]
        public void 관통은_기본_3체까지만_통한다()
        {
            ProjectileBehaviourState state = State(pierce: 3);

            for (int i = 0; i < 3; i++)
            {
                ProjectileCollisionResult hit =
                    ProjectileCollisionResolver.Resolve(ref state, false);

                Assert.IsTrue(hit.KeepAlive, $"{i + 1}번째 적중에서 소멸하면 안 됩니다.");
            }

            ProjectileCollisionResult fourth =
                ProjectileCollisionResolver.Resolve(ref state, false);

            Assert.AreEqual(ProjectileBehaviourType.None, fourth.Behaviour);
            Assert.IsFalse(fourth.KeepAlive);
        }

        [Test]
        public void Chain은_다음_대상을_찾도록_요청한다()
        {
            ProjectileBehaviourState state = State(chain: 2);

            ProjectileCollisionResult result =
                ProjectileCollisionResolver.Resolve(ref state, false);

            Assert.AreEqual(ProjectileBehaviourType.Chain, result.Behaviour);
            Assert.IsTrue(result.SeekNextTarget);
            Assert.IsTrue(result.KeepAlive);
        }

        [Test]
        public void Return은_귀환을_시작시킨다()
        {
            ProjectileBehaviourState state = State(ret: 1);

            ProjectileCollisionResult result =
                ProjectileCollisionResolver.Resolve(ref state, false);

            Assert.AreEqual(ProjectileBehaviourType.Return, result.Behaviour);
            Assert.IsTrue(result.BeginReturn);
            Assert.IsTrue(result.KeepAlive);
        }

        [Test]
        public void 남은_행동이_없으면_소멸한다()
        {
            ProjectileBehaviourState state = State();

            ProjectileCollisionResult result =
                ProjectileCollisionResolver.Resolve(ref state, false);

            Assert.AreEqual(ProjectileBehaviourType.None, result.Behaviour);
            Assert.IsFalse(result.KeepAlive);
            Assert.AreEqual(0, result.ChildCount);
        }

        [Test]
        public void 우선순위는_Split_Pierce_Fork_Chain_Return_순이다()
        {
            ProjectileBehaviourState state = State(1, 1, 1, 1, 1);

            Assert.AreEqual(ProjectileBehaviourType.Split,
                ProjectileCollisionResolver.Resolve(ref state, false).Behaviour);
            Assert.AreEqual(ProjectileBehaviourType.Pierce,
                ProjectileCollisionResolver.Resolve(ref state, false).Behaviour);
            Assert.AreEqual(ProjectileBehaviourType.Fork,
                ProjectileCollisionResolver.Resolve(ref state, false).Behaviour);
            Assert.AreEqual(ProjectileBehaviourType.Chain,
                ProjectileCollisionResolver.Resolve(ref state, false).Behaviour);
            Assert.AreEqual(ProjectileBehaviourType.Return,
                ProjectileCollisionResolver.Resolve(ref state, false).Behaviour);
            Assert.AreEqual(ProjectileBehaviourType.None,
                ProjectileCollisionResolver.Resolve(ref state, false).Behaviour);
        }

        [Test]
        public void 관통과_Fork를_둘_다_가져도_한_충돌에는_관통만_발동한다()
        {
            // v5 §10: "곱연산이 일어나지 않습니다"
            ProjectileBehaviourState state = State(pierce: 1, fork: 1);

            ProjectileCollisionResult result =
                ProjectileCollisionResolver.Resolve(ref state, false);

            Assert.AreEqual(ProjectileBehaviourType.Pierce, result.Behaviour);
            Assert.AreEqual(0, result.ChildCount, "같은 충돌에서 Fork가 발동하면 안 됩니다.");
            Assert.AreEqual(1, state.ForkRemaining, "Fork는 소비되지 않고 남아 있어야 합니다.");
        }

        [Test]
        public void Fork와_Chain은_순차_단계로_체인화된다()
        {
            // 첫 충돌에서 Fork만, 분열된 자식이 다음 충돌에서 Chain을 쓴다.
            ProjectileBehaviourState parent = State(fork: 1, chain: 1);

            ProjectileCollisionResult first =
                ProjectileCollisionResolver.Resolve(ref parent, false);

            Assert.AreEqual(ProjectileBehaviourType.Fork, first.Behaviour);

            ProjectileBehaviourState child = parent.CreateChildState();

            ProjectileCollisionResult second =
                ProjectileCollisionResolver.Resolve(ref child, false);

            Assert.AreEqual(ProjectileBehaviourType.Chain, second.Behaviour);
        }

        [Test]
        public void 귀환_중에는_큐를_더_소비하지_않는다()
        {
            // 엣지 케이스: 귀환 중에 Chain이 남아 있다고 방향을 틀면 Return이 성립하지 않는다.
            ProjectileBehaviourState state = State(chain: 2, ret: 1);

            ProjectileCollisionResult result =
                ProjectileCollisionResolver.Resolve(ref state, true);

            Assert.AreEqual(ProjectileBehaviourType.Return, result.Behaviour);
            Assert.IsTrue(result.KeepAlive);
            Assert.IsFalse(result.SeekNextTarget);
            Assert.AreEqual(2, state.ChainRemaining, "귀환 중 Chain이 소비되면 안 됩니다.");
            Assert.AreEqual(1, state.ReturnRemaining);
        }

        [Test]
        public void 자식_각도는_좌우_대칭으로_균등_분배된다()
        {
            Assert.AreEqual(0f, ProjectileCollisionResolver.GetChildAngle(0, 1, 60f), 0.001f);

            Assert.AreEqual(-60f, ProjectileCollisionResolver.GetChildAngle(0, 2, 60f), 0.001f);
            Assert.AreEqual(60f, ProjectileCollisionResolver.GetChildAngle(1, 2, 60f), 0.001f);

            Assert.AreEqual(-60f, ProjectileCollisionResolver.GetChildAngle(0, 3, 60f), 0.001f);
            Assert.AreEqual(0f, ProjectileCollisionResolver.GetChildAngle(1, 3, 60f), 0.001f);
            Assert.AreEqual(60f, ProjectileCollisionResolver.GetChildAngle(2, 3, 60f), 0.001f);
        }

        [Test]
        public void 자식_각도_인덱스가_범위를_벗어나도_안전하다()
        {
            Assert.AreEqual(-60f, ProjectileCollisionResolver.GetChildAngle(-5, 3, 60f), 0.001f);
            Assert.AreEqual(60f, ProjectileCollisionResolver.GetChildAngle(99, 3, 60f), 0.001f);
            Assert.AreEqual(0f, ProjectileCollisionResolver.GetChildAngle(0, 0, 60f), 0.001f);
        }
    }
}
