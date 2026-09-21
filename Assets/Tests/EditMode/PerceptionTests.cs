using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Blob.Tests
{
    /// <summary>
    /// 감지의 계약 테스트. (docs/Blob_Hunting_System.md 8절)
    ///
    /// 【이 축이 없던 동안 잠입이 존재하지 않았다.】
    /// EnemyBrain은 detectDistance 반경 안이면 무조건 알아챘다.
    /// 뒤로 돌아가도, 가만히 서 있어도 결과가 같았다.
    /// </summary>
    public class PerceptionTests
    {
        private static PerceptionInput At(float x, float z,
                                          float cone = 120f, float range = 18f,
                                          float noise = 0f, bool los = true)
        {
            return new PerceptionInput
            {
                viewerPosition = Vector3.zero,
                viewerForward = Vector3.forward,
                visionConeDegrees = cone,
                visionRange = range,
                targetPosition = new Vector3(x, 0f, z),
                targetNoiseRadius = noise,
                hasLineOfSight = los
            };
        }

        // ── 눈 ────────────────────────────────────────────────────────

        [Test]
        public void 정면의_대상을_본다()
        {
            Assert.AreEqual(DetectionKind.Seen, Perception.Detect(At(0f, 10f)));
        }

        [Test]
        public void 시야각_밖은_보지_못한다()
        {
            // 시야각 120도 → 정면 기준 좌우 60도. 바로 옆(90도)은 밖이다.
            Assert.AreEqual(DetectionKind.None, Perception.Detect(At(10f, 0f)));

            // 뒤는 당연히 밖이다. 【측면 접근이 성립하는 근거다.】
            Assert.AreEqual(DetectionKind.None, Perception.Detect(At(0f, -10f)));
        }

        [Test]
        public void 시야_거리_밖은_보지_못한다()
        {
            Assert.AreEqual(DetectionKind.None, Perception.Detect(At(0f, 30f)));
        }

        [Test]
        public void 시선이_막히면_보지_못한다()
        {
            Assert.AreEqual(DetectionKind.None, Perception.Detect(At(0f, 10f, los: false)));
        }

        [Test]
        public void 코앞은_각도를_묻지_않는다()
        {
            // 코앞의 적을 「각도 밖이라」 못 본다면 잠입이 아니라 버그로 읽힌다.
            Assert.AreEqual(DetectionKind.Seen, Perception.Detect(At(0f, -1f)));
        }

        // ── 귀 ────────────────────────────────────────────────────────

        [Test]
        public void 소리를_내지_않으면_뒤에서는_들키지_않는다()
        {
            // 「안 뛰는 것만으로 기습을 피한다」가 성립하는 자리다.
            Assert.AreEqual(DetectionKind.None,
                Perception.Detect(At(0f, -10f, noise: 0f)));
        }

        [Test]
        public void 소리_반경_안이면_뒤에서도_들킨다()
        {
            Assert.AreEqual(DetectionKind.Heard,
                Perception.Detect(At(0f, -10f, noise: 12f)));
        }

        [Test]
        public void 소리_반경_밖이면_들키지_않는다()
        {
            Assert.AreEqual(DetectionKind.None,
                Perception.Detect(At(0f, -10f, noise: 8f)));
        }

        [Test]
        public void 소리는_벽을_넘는다()
        {
            // 시선이 막혀 있어도 들린다. 그래야 「총성을 듣고 찾아온다」가 성립한다.
            Assert.AreEqual(DetectionKind.Heard,
                Perception.Detect(At(0f, 10f, noise: 12f, los: false)));
        }

        [Test]
        public void 보는_것이_듣는_것보다_우선한다()
        {
            // 눈으로 봤으면 위치를 안다. 소리로 안 것과 구분되어야
            // 「대략적 위치」와 「정확한 위치」를 나눠 쓸 수 있다.
            Assert.AreEqual(DetectionKind.Seen,
                Perception.Detect(At(0f, 10f, noise: 12f)));
        }

        // ── 원형별 시야 ───────────────────────────────────────────────

        [Test]
        public void 원형마다_시야각이_정해져_있다()
        {
            foreach (EnemyArchetype a in (EnemyArchetype[])Enum.GetValues(typeof(EnemyArchetype)))
            {
                EnemyArchetypeStats stats = EnemyArchetypeTable.Of(a);

                Assert.Greater(stats.visionConeDegrees, 0f, EnemyArchetypeTable.Name(a));
                Assert.LessOrEqual(stats.visionConeDegrees, 360f, EnemyArchetypeTable.Name(a));
                Assert.Greater(stats.visionRange, 0f, EnemyArchetypeTable.Name(a));
            }
        }

        [Test]
        public void 문서가_말한_시야_관계를_지킨다()
        {
            float scav = EnemyArchetypeTable.Of(EnemyArchetype.Scav).visionConeDegrees;
            float dynamo = EnemyArchetypeTable.Of(EnemyArchetype.Dynamo).visionConeDegrees;
            float specimen = EnemyArchetypeTable.Of(EnemyArchetype.Specimen).visionConeDegrees;
            float sentry = EnemyArchetypeTable.Of(EnemyArchetype.Sentry).visionConeDegrees;

            // 「스캐브는 시야가 좁다」 (문서 8절)
            Assert.Less(scav, Perception.DefaultConeDegrees, "스캐브가 기본보다 넓습니다.");

            // 「자전체·검체는 정면 넓고 측·후방 좁다」 → 기본보다 좁아야 측면이 성립한다.
            Assert.Less(dynamo, Perception.DefaultConeDegrees, "자전체");
            Assert.AreEqual(dynamo, specimen, 0.01f, "자전체와 검체는 같은 서술을 받았습니다.");

            // 기계 눈이 야생 슬라임보다 좁을 이유가 없다.
            Assert.Greater(sentry, scav, "보안기가 스캐브보다 좁습니다.");
        }

        [Test]
        public void 자전체의_측면으로_돌면_보이지_않는다()
        {
            // 문서 8절이 요구하는 것 — 「측면 접근을 인지하지 못한다」.
            EnemyArchetypeStats dynamo = EnemyArchetypeTable.Of(EnemyArchetype.Dynamo);

            var input = At(9f, 3f, cone: dynamo.visionConeDegrees, range: dynamo.visionRange);

            Assert.AreEqual(DetectionKind.None, Perception.Detect(input),
                "측면으로 돌았는데도 보입니다. 각도라는 답이 죽습니다.");
        }
    }
}
