using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 유형 고유 기믹 셋. (docs/Dokkaebi_Hunting_System.md 1절)
    ///
    /// 문서가 말로만 적은 것을 수치로 옮겼다. 지켜야 할 것 —
    ///   1. 기믹은 셋뿐이다. 유형마다 주면 아홉을 외워야 한다.
    ///   2. 8과 3은 문서에 있는 숫자다. 바꾸면 문서와 갈라진다.
    ///   3. 끌어당기는 세기가 이동 속도를 넘지 않는다 — 도망이 불가능해진다.
    /// </summary>
    public class EnemyGimmickTests
    {
        [Test]
        public void 기믹은_세_유형만_가진다()
        {
            int count = 0;

            for (int i = 0; i < EnemyArchetypeTable.Count; i++)
            {
                if (EnemyGimmickTable.Of((EnemyArchetype)i) != EnemyGimmick.None)
                    count++;
            }

            Assert.AreEqual(3, count, "기믹은 셋뿐이어야 합니다.");
        }

        [Test]
        public void 유형과_기믹이_문서대로_이어진다()
        {
            Assert.AreEqual(EnemyGimmick.RadialSpray,
                EnemyGimmickTable.Of(EnemyArchetype.Chemic), "왕지네 — 8방향");

            Assert.AreEqual(EnemyGimmick.BurstFire,
                EnemyGimmickTable.Of(EnemyArchetype.Sentry), "순라귀 — 3점사");

            Assert.AreEqual(EnemyGimmick.GravityStealth,
                EnemyGimmickTable.Of(EnemyArchetype.Settled), "허깨비 — 중력·은신");
        }

        [Test]
        public void 유형표가_기믹을_들고_있다()
        {
            // 프리팹 연결(EnemyArchetypeWiring)이 이 값을 베껴 넣는다.
            Assert.AreEqual(EnemyGimmick.RadialSpray,
                EnemyArchetypeTable.Of(EnemyArchetype.Chemic).gimmick);

            Assert.AreEqual(EnemyGimmick.None,
                EnemyArchetypeTable.Of(EnemyArchetype.Scav).gimmick);
        }

        // ── 수치 ──────────────────────────────────────────────────────

        [Test]
        public void 화공체는_여덟_방향으로_쏜다()
        {
            GimmickSpec spec = EnemyGimmickTable.Get(EnemyGimmick.RadialSpray);

            Assert.AreEqual(8, spec.ProjectilesPerShot, "문서 1절의 「8방향」");
            Assert.AreEqual(1, spec.BurstCount, "흩뿌리기는 점사가 아닙니다.");
        }

        [Test]
        public void 보안기는_세_발씩_쏜다()
        {
            GimmickSpec spec = EnemyGimmickTable.Get(EnemyGimmick.BurstFire);

            Assert.AreEqual(3, spec.BurstCount, "문서 1절의 「3점사」");
            Assert.AreEqual(1, spec.ProjectilesPerShot, "점사는 한 발씩 세 번입니다.");
            Assert.Greater(spec.BurstInterval, 0f, "간격이 0이면 한 발과 구분되지 않습니다.");
        }

        [Test]
        public void 끌어당기는_세기가_도망을_막지_않는다()
        {
            GimmickSpec spec = EnemyGimmickTable.Get(EnemyGimmick.GravityStealth);

            Assert.Greater(spec.PullStrength, 0f);
            Assert.Less(spec.PullStrength, EnemyArchetypeTable.BaseMoveSpeed,
                "끌어당기는 힘이 이동 속도 이상이면 도망 자체가 불가능해집니다.");
        }

        [Test]
        public void 은신이_풀리는_거리가_시야보다_짧다()
        {
            GimmickSpec spec = EnemyGimmickTable.Get(EnemyGimmick.GravityStealth);

            Assert.Greater(spec.RevealRange, 0f);
            Assert.Less(spec.RevealRange, EnemyArchetypeTable.DefaultVisionRange * 0.5f,
                "시야 절반보다 멀리서 드러나면 「눈앞에서 나타난다」가 아닙니다.");
        }

        [Test]
        public void 기믹이_없으면_평범하게_한_발()
        {
            GimmickSpec spec = EnemyGimmickTable.Get(EnemyGimmick.None);

            Assert.AreEqual(1, spec.ProjectilesPerShot);
            Assert.AreEqual(1, spec.BurstCount);
            Assert.AreEqual(0f, spec.PullStrength);
            Assert.AreEqual(0f, spec.RevealRange);
        }

        // ── 방향 ──────────────────────────────────────────────────────

        [Test]
        public void 첫_방향은_조준한_쪽이다()
        {
            // 조준한 곳에 한 발도 안 가면 「나를 노린 공격」으로 읽히지 않는다.
            var forward = new Vector3(1f, 0f, 0f);

            Vector3 first = EnemyGimmickTable.SprayDirection(forward, 0, 8);

            Assert.AreEqual(forward.normalized.x, first.x, 0.001f);
            Assert.AreEqual(forward.normalized.z, first.z, 0.001f);
        }

        [Test]
        public void 여덟_방향이_고르게_돈다()
        {
            var forward = new Vector3(0f, 0f, 1f);

            for (int i = 0; i < 8; i++)
            {
                Vector3 direction = EnemyGimmickTable.SprayDirection(forward, i, 8);

                Assert.AreEqual(1f, direction.magnitude, 0.001f, "방향은 단위 벡터입니다.");
                Assert.AreEqual(0f, direction.y, 0.001f, "높이를 쓰지 않습니다.");
            }

            // 네 번째(180°)는 정반대여야 한다.
            Vector3 back = EnemyGimmickTable.SprayDirection(forward, 4, 8);

            Assert.AreEqual(-1f, back.z, 0.001f);
        }

        [Test]
        public void 방향이_0이어도_터지지_않는다()
        {
            Vector3 direction = EnemyGimmickTable.SprayDirection(Vector3.zero, 0, 8);

            Assert.AreEqual(1f, direction.magnitude, 0.001f);
        }
    }
}
