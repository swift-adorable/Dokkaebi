using NUnit.Framework;
using UnityEngine;

namespace Blob.Tests
{
    /// <summary>
    /// 스포너가 유형 9종과 파밍 조건을 실제로 읽는지. (로드맵 7-G)
    ///
    /// 【표를 만들어 놓고 연결을 안 하는 것이 이 프로젝트의 반복된 실패다.】
    /// 7-B의 등급·속성, 7-G의 밤 상태·레이드 특성이 모두 계산은 있는데
    /// 스폰 경로에 닿지 않아 「스폰이 전부 Normal이었다」가 되었다.
    /// 여기서는 「닿는가」만 본다 — 수치는 각자의 테스트가 본다.
    /// </summary>
    public class RaidWiringTests
    {
        private static EnemyPrefabCatalog LoadCatalog()
        {
            EnemyPrefabCatalog catalog = EnemyPrefabCatalog.Load();

            Assert.IsNotNull(catalog,
                "Resources/EnemyPrefabCatalog.asset이 없습니다. "
                + "「Blob/Enemy/유형 프리팹 카탈로그 생성」을 실행하십시오.");

            return catalog;
        }

        [Test]
        public void 유형_아홉이_모두_카탈로그에_있다()
        {
            EnemyPrefabCatalog catalog = LoadCatalog();

            Assert.AreEqual(EnemyArchetypeTable.Count, catalog.FilledCount);

            for (int i = 0; i < EnemyArchetypeTable.Count; i++)
            {
                var archetype = (EnemyArchetype)i;

                Assert.IsNotNull(catalog.Get(archetype),
                    $"{EnemyArchetypeTable.Name(archetype)} 프리팹이 없습니다.");
            }
        }

        [Test]
        public void 카탈로그의_프리팹이_제_유형으로_연결되어_있다()
        {
            EnemyPrefabCatalog catalog = LoadCatalog();

            for (int i = 0; i < EnemyArchetypeTable.Count; i++)
            {
                var archetype = (EnemyArchetype)i;

                GameObject prefab = catalog.Get(archetype);

                var identity = prefab.GetComponent<EnemyIdentity>();

                Assert.IsNotNull(identity,
                    $"{EnemyArchetypeTable.Name(archetype)}에 EnemyIdentity가 없습니다.");

                Assert.AreEqual(archetype, identity.Archetype,
                    $"{prefab.name}이 다른 유형으로 연결되어 있습니다.");
            }
        }

        [Test]
        public void 기믹이_프리팹에_실려_있다()
        {
            EnemyPrefabCatalog catalog = LoadCatalog();

            for (int i = 0; i < EnemyArchetypeTable.Count; i++)
            {
                var archetype = (EnemyArchetype)i;

                var attack = catalog.Get(archetype).GetComponent<EnemyAttack>();

                Assert.IsNotNull(attack);

                Assert.AreEqual(EnemyGimmickTable.Of(archetype), attack.Gimmick,
                    $"{EnemyArchetypeTable.Name(archetype)}의 기믹이 표와 다릅니다.");
            }
        }

        [Test]
        public void 정착체만_중력_은신_컴포넌트를_가진다()
        {
            EnemyPrefabCatalog catalog = LoadCatalog();

            for (int i = 0; i < EnemyArchetypeTable.Count; i++)
            {
                var archetype = (EnemyArchetype)i;

                bool has = catalog.Get(archetype)
                    .GetComponent<EnemyGravityStealth>() != null;

                Assert.AreEqual(archetype == EnemyArchetype.Settled, has,
                    $"{EnemyArchetypeTable.Name(archetype)}의 중력·은신 연결이 틀렸습니다.");
            }
        }

        // ── 파밍 조건이 개체에 닿는가 ─────────────────────────────────

        [Test]
        public void 조건이_없으면_개체가_그대로다()
        {
            EnemyProfile built = EnemyProfile.Build(
                EnemyArchetype.Scav, EnemyRarity.Normal, null);

            EnemyProfile applied = RaidConditions.None.Apply(built);

            Assert.AreEqual(built.health, applied.health);
            Assert.AreEqual(built.damage, applied.damage);
        }

        [Test]
        public void 소독_사이클이_화공체를_강하게_만든다()
        {
            // 「그 상태에서 만나면 다르다」가 없으면 추가 스폰은 숫자만 느는 것이다.
            var conditions = new RaidConditions
            {
                facility = FacilityState.Decontamination,
                traits = new RaidTrait[0]
            };

            EnemyProfile plain = EnemyProfile.Build(
                EnemyArchetype.Chemic, EnemyRarity.Normal, null);

            EnemyProfile empowered = conditions.Apply(plain);

            Assert.Greater(empowered.health, plain.health);
            Assert.Greater(empowered.damage, plain.damage);
        }

        [Test]
        public void 소독_사이클이_다른_유형은_건드리지_않는다()
        {
            var conditions = new RaidConditions
            {
                facility = FacilityState.Decontamination,
                traits = new RaidTrait[0]
            };

            EnemyProfile plain = EnemyProfile.Build(
                EnemyArchetype.Scav, EnemyRarity.Normal, null);

            EnemyProfile applied = conditions.Apply(plain);

            Assert.AreEqual(plain.health, applied.health);
            Assert.AreEqual(plain.damage, applied.damage);
        }

        [Test]
        public void 무리_배율이_특성마다_다르다()
        {
            var dense = new RaidConditions
            {
                facility = FacilityState.Normal,
                traits = new[] { RaidTrait.Dense }
            };

            var scattered = new RaidConditions
            {
                facility = FacilityState.Normal,
                traits = new[] { RaidTrait.Scattered }
            };

            Assert.Greater(dense.GroupSizeScale, 1f, "밀집은 한 번에 더 나옵니다.");
            Assert.Less(scattered.GroupSizeScale, 1f, "산개는 더 적게 나옵니다.");
        }
    }
}
