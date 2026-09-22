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
                                          float noise = 0f, bool los = true,
                                          float hearing = 0f)
        {
            return new PerceptionInput
            {
                viewerPosition = Vector3.zero,
                viewerForward = Vector3.forward,
                visionConeDegrees = cone,
                visionRange = range,
                listenerHearingScale = hearing,
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

        // ── 귀 · 듣는 쪽의 청각 ───────────────────────────────────────
        // 【7-D 결정을 뒤집은 자리다.】
        // 처음에는 「소리 크기는 내는 쪽만 정한다」로 두었다. 듣는 쪽마다 값이
        // 다르면 플레이어가 「이 소리가 어디까지 갔나」를 계산할 수 없다는 이유였다.
        // 덕코프는 듣는 쪽에 청각 능력을 두되 값을 두 개(0.75 · 1.0)에 몰아
        // 그 문제를 피한다 — 59종 중 48종이 그 둘이다.
        // [확인됨 — docs/research/duckov/05_적_AI_실측치.md 3절]

        [Test]
        public void 값을_채우지_않으면_보통_귀로_본다()
        {
            // 구조체를 기본값으로 만든 호출자가 귀머거리가 되면 안 된다.
            Assert.AreEqual(Perception.NormalHearing, Perception.Hearing(0f));
            Assert.AreEqual(Perception.NormalHearing, Perception.Hearing(-1f));
        }

        [Test]
        public void 귀가_밝으면_더_멀리서_듣는다()
        {
            // 소리 반경 8 · 거리 10 → 보통 귀로는 못 듣는다.
            Assert.AreEqual(DetectionKind.None,
                Perception.Detect(At(0f, -10f, noise: 8f, hearing: Perception.NormalHearing)));

            // 같은 소리를 잠복체(2.0)는 듣는다. 「먼저 찾는 쪽」이라는 정체성이다.
            Assert.AreEqual(DetectionKind.Heard,
                Perception.Detect(At(0f, -10f, noise: 8f, hearing: Perception.KeenHearing)));
        }

        [Test]
        public void 귀가_둔하면_가까운_소리도_놓친다()
        {
            // 소리 반경 12 · 거리 10 → 보통 귀는 듣는다.
            Assert.AreEqual(DetectionKind.Heard,
                Perception.Detect(At(0f, -10f, noise: 12f, hearing: Perception.NormalHearing)));

            // 기계형(0.75)은 9m까지만 듣는다 — 눈은 넓지만 귀로는 못 찾는다.
            Assert.AreEqual(DetectionKind.None,
                Perception.Detect(At(0f, -10f, noise: 12f, hearing: Perception.DullHearing)));
        }

        [Test]
        public void 아무리_귀가_밝아도_소리가_없으면_못_듣는다()
        {
            // 「안 뛰는 것만으로 기습을 피한다」는 청각 배율이 아무리 높아도 성립한다.
            Assert.AreEqual(DetectionKind.None,
                Perception.Detect(At(0f, -10f, noise: 0f, hearing: 20f)));
        }

        [Test]
        public void 보는_것이_듣는_것보다_우선한다()
        {
            // 눈으로 봤으면 위치를 안다. 소리로 안 것과 구분되어야
            // 「대략적 위치」와 「정확한 위치」를 나눠 쓸 수 있다.
            Assert.AreEqual(DetectionKind.Seen,
                Perception.Detect(At(0f, 10f, noise: 12f)));
        }

        // ── 유형별 시야 ───────────────────────────────────────────────

        [Test]
        public void 유형마다_시야각이_정해져_있다()
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

        // ── 유형별 청각 · 추적 ────────────────────────────────────────

        [Test]
        public void 청각은_세_값만_쓴다()
        {
            // 값을 거칠게 두는 것이 「예측할 수 없다」는 반대 근거를 지우는 방법이다.
            // 덕코프의 0(귀머거리)과 5·20(전 구역 감지)은 쓰지 않는다 —
            // 0은 「소리로 유인한다」를 없애고, 20은 소리 관리를 무의미하게 만든다.
            float[] allowed = { Perception.DullHearing, Perception.NormalHearing,
                                Perception.KeenHearing };

            foreach (EnemyArchetype a in (EnemyArchetype[])Enum.GetValues(typeof(EnemyArchetype)))
            {
                float hearing = EnemyArchetypeTable.Of(a).hearingScale;

                Assert.IsTrue(allowed.Any(v => Mathf.Approximately(v, hearing)),
                    $"{EnemyArchetypeTable.Name(a)}의 청각이 {hearing}입니다. " +
                    "허용값은 0.75 · 1.0 · 2.0뿐입니다.");
            }
        }

        [Test]
        public void 잠복체가_가장_잘_듣고_기계형이_가장_둔하다()
        {
            float lurker = EnemyArchetypeTable.Of(EnemyArchetype.Lurker).hearingScale;
            float sentry = EnemyArchetypeTable.Of(EnemyArchetype.Sentry).hearingScale;
            float crusher = EnemyArchetypeTable.Of(EnemyArchetype.Crusher).hearingScale;

            // 「먼저 감지하는 쪽」이 잠복체의 답(선제)이다.
            Assert.AreEqual(Perception.KeenHearing, lurker, 0.001f, "잠복체");

            // 기계 눈은 넓지만 귀는 둔하다 — 시야각으로 답하고 소리로 답하지 않는다.
            Assert.AreEqual(Perception.DullHearing, sentry, 0.001f, "보안기");
            Assert.AreEqual(Perception.DullHearing, crusher, 0.001f, "압착기");
        }

        [Test]
        public void 모든_유형이_망각_시간을_갖는다()
        {
            // 0이면 감지를 잃는 즉시 놓는다 — 엄폐물 뒤로 한 걸음에 추적이 끊긴다.
            foreach (EnemyArchetype a in (EnemyArchetype[])Enum.GetValues(typeof(EnemyArchetype)))
            {
                EnemyArchetypeStats stats = EnemyArchetypeTable.Of(a);

                Assert.Greater(stats.forgetTime, 0f, EnemyArchetypeTable.Name(a));
                Assert.LessOrEqual(stats.forgetTime, 30f,
                    $"{EnemyArchetypeTable.Name(a)}의 망각 시간이 30초를 넘습니다. " +
                    "덕코프의 120·180초는 보스급 소수라 쓰지 않기로 했습니다.");
            }
        }

        [Test]
        public void 강제_추적_거리는_덕코프의_세_값만_쓴다()
        {
            // 0 / 15 / 40. 60은 구역 하나와 맞먹어 「구역을 뜬다」 외의 수가 없어진다.
            float[] allowed = { 0f, 15f, 40f };

            foreach (EnemyArchetype a in (EnemyArchetype[])Enum.GetValues(typeof(EnemyArchetype)))
            {
                float range = EnemyArchetypeTable.Of(a).forcedChaseRange;

                Assert.IsTrue(allowed.Any(v => Mathf.Approximately(v, range)),
                    $"{EnemyArchetypeTable.Name(a)}의 강제 추적 거리가 {range}입니다.");
            }
        }

        [Test]
        public void 보안기가_가장_집요하다()
        {
            // 옛 chasesForever를 대신하는 축이다. 「끝까지」가 아니라 「멀리까지」로
            // 바꾼 이유 — 도망이라는 선택지를 없애지 않으려는 것이다.
            EnemyArchetypeStats sentry = EnemyArchetypeTable.Of(EnemyArchetype.Sentry);

            foreach (EnemyArchetype a in (EnemyArchetype[])Enum.GetValues(typeof(EnemyArchetype)))
            {
                if (a == EnemyArchetype.Sentry)
                    continue;

                EnemyArchetypeStats other = EnemyArchetypeTable.Of(a);

                Assert.LessOrEqual(other.forgetTime, sentry.forgetTime,
                    $"{EnemyArchetypeTable.Name(a)}가 보안기보다 오래 기억합니다.");

                Assert.LessOrEqual(other.forcedChaseRange, sentry.forcedChaseRange,
                    $"{EnemyArchetypeTable.Name(a)}가 보안기보다 멀리서 강제 추적합니다.");
            }

            Assert.Greater(sentry.forcedChaseRange, 0f, "보안기가 강제 추적을 잃었습니다.");
        }

        [Test]
        public void 잠복체는_귀가_밝은_대신_오래_쫓지_않는다()
        {
            // 【하나의 유형이 두 개의 답을 요구하게 만들지 않는다.】
            // 잠복체의 답은 「선제」다. 귀가 밝은 것으로 이미 답했으므로
            // 집요함까지 주면 답이 둘이 된다.
            EnemyArchetypeStats lurker = EnemyArchetypeTable.Of(EnemyArchetype.Lurker);
            EnemyArchetypeStats scav = EnemyArchetypeTable.Of(EnemyArchetype.Scav);

            Assert.AreEqual(scav.forgetTime, lurker.forgetTime, 0.001f,
                "잠복체가 귀도 밝고 집요하기까지 합니다. 요구하는 답이 둘이 됩니다.");

            Assert.AreEqual(0f, lurker.forcedChaseRange, 0.001f, "잠복체");
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
