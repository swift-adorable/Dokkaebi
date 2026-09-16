using NUnit.Framework;
using UnityEngine;

namespace Blob.Tests
{
    /// <summary>
    /// 튕겨 쏘기(지형 충돌) 테스트. (v5 §6-1, §10)
    ///
    /// 핵심 규약: 튕겨 쏘기는 충돌 우선순위 큐와 별개다.
    /// ProjectileBehaviourType에 Ricochet이 들어가면 관통과 서로를 막게 되므로
    /// 타입 정의 자체를 검증에 포함한다.
    /// </summary>
    public class ProjectileRicochetStateTests
    {
        [Test]
        public void 기본_튕김은_3회다()
        {
            Assert.AreEqual(3, ProjectileRicochetState.DefaultBounces);
        }

        [Test]
        public void 튕겨_쏘기는_충돌_우선순위_큐에_들어가지_않는다()
        {
            // v5 §10: "튕겨 쏘기는 이 큐와 별개입니다"
            foreach (ProjectileBehaviourType type in
                System.Enum.GetValues(typeof(ProjectileBehaviourType)))
            {
                Assert.AreNotEqual("Ricochet", type.ToString(),
                    "튕겨 쏘기를 큐에 넣으면 관통과 서로를 막게 됩니다.");
            }
        }

        [Test]
        public void 설정한_횟수만큼만_튕긴다()
        {
            var state = new ProjectileRicochetState();
            state.Set(ProjectileRicochetState.DefaultBounces);

            Assert.IsTrue(state.TryConsume());
            Assert.IsTrue(state.TryConsume());
            Assert.IsTrue(state.TryConsume());

            Assert.IsFalse(state.TryConsume(), "4번째 튕김은 실패하고 투사체가 소멸해야 합니다.");
            Assert.IsFalse(state.HasAny);
        }

        [Test]
        public void 추가_튕김은_횟수를_더한다()
        {
            // 「추가 튕김」 Support: 튕김 횟수 +3
            var state = new ProjectileRicochetState();
            state.Set(3);
            state.Add(3);

            Assert.AreEqual(6, state.Remaining);
        }

        [Test]
        public void 음수는_0으로_보정된다()
        {
            var state = new ProjectileRicochetState();

            state.Set(-5);
            Assert.AreEqual(0, state.Remaining);
            Assert.IsFalse(state.HasAny);

            state.Set(2);
            state.Add(-10);
            Assert.AreEqual(0, state.Remaining);
        }

        [Test]
        public void Clear는_잔여를_비운다()
        {
            var state = new ProjectileRicochetState();
            state.Set(3);
            state.Clear();

            Assert.AreEqual(0, state.Remaining);
            Assert.IsFalse(state.TryConsume());
        }

        [Test]
        public void 정면_벽에_부딪히면_정반대로_튕긴다()
        {
            Vector3 reflected = ProjectileRicochetState.Reflect(Vector3.forward, Vector3.back);

            Assert.AreEqual(-1f, reflected.z, 0.001f);
            Assert.AreEqual(0f, reflected.x, 0.001f);
        }

        [Test]
        public void 비스듬한_벽에서는_입사각과_반사각이_같다()
        {
            // 45도 벽에 정면으로 들어가면 옆으로 90도 꺾인다.
            Vector3 normal = new Vector3(-1f, 0f, -1f).normalized;

            Vector3 reflected = ProjectileRicochetState.Reflect(Vector3.forward, normal);

            Assert.AreEqual(-1f, reflected.x, 0.001f);
            Assert.AreEqual(0f, reflected.z, 0.001f);
        }

        [Test]
        public void 반사_결과의_높이_성분은_항상_0이다()
        {
            // Top-Down이므로 y가 섞이면 탄이 바닥이나 하늘로 빠진다.
            Vector3 reflected = ProjectileRicochetState.Reflect(
                new Vector3(0f, 0.5f, 1f), new Vector3(0f, 0.8f, -1f));

            Assert.AreEqual(0f, reflected.y, 0.0001f);
            Assert.AreEqual(1f, reflected.magnitude, 0.001f);
        }

        [Test]
        public void 법선이_없으면_입사_방향을_유지한다()
        {
            // 엣지 케이스: 모서리에서 법선이 0이면 방향이 NaN이 되어 탄이 사라진다.
            Vector3 reflected = ProjectileRicochetState.Reflect(Vector3.forward, Vector3.zero);

            Assert.AreEqual(1f, reflected.z, 0.001f);
            Assert.AreEqual(1f, reflected.magnitude, 0.001f);
        }

        [Test]
        public void 입사_방향이_0이어도_유효한_방향을_돌려준다()
        {
            Vector3 reflected = ProjectileRicochetState.Reflect(Vector3.zero, Vector3.back);

            Assert.AreEqual(1f, reflected.magnitude, 0.001f);
        }
    }
}
