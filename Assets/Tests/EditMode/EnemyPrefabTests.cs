using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blob.Tests
{
    /// <summary>
    /// 적 프리팹이 전투 기준의 수치를 쓰고 있는가.
    ///
    /// 【이 테스트가 없어서 놓쳤다】
    /// 감사는 「문서 ↔ 코드」만 비교했다. 프리팹에 직렬화된 값은
    /// 코드도 문서도 아니라서 아무도 보지 않았고, 0~4단계의 시제품 수치
    /// (피해 1 · 체력 3)가 6단계까지 그대로 남아 있었다.
    ///
    /// 그 결과 —
    ///   적 피해 1 vs 플레이어 체력 100 → 100대를 맞아야 죽는다.
    ///   한 대에 1.55초(예비 0.35 + 쿨 1.2), 동시 공격은 2마리 제한.
    ///   즉 가만히 서 있어도 2분 30초를 버틴다. 「죽으면 잃는가」를 확인할 수 없었다.
    ///   적 체력 3 → 어떤 무기로도 한 발. 전투 체감이 전부 무의미했다.
    ///
    /// 수치의 출처는 docs/Blob_Combat_Baseline.md 5절 「적 유형」 표다.
    /// 7단계에서 유형 9종을 데이터로 만들면 이 테스트를 그 표 전체로 넓힌다.
    /// </summary>
    public class EnemyPrefabTests
    {
        private const string EnemyPrefab = "Assets/Prefabs/Enemy.prefab";

        // 스캐브 — Combat_Baseline 5절 1행.
        private const int ScavHealth = 20;
        private const int ScavDamage = 8;
        private const int ScavArmourPenetration = 0;

        private static GameObject LoadEnemy()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefab);

            Assert.IsNotNull(go, $"{EnemyPrefab}을 찾지 못했습니다.");

            return go;
        }

        [Test]
        public void 적_프리팹의_체력이_기준과_같다()
        {
            var health = LoadEnemy().GetComponent<Health>();

            Assert.IsNotNull(health, "Enemy 프리팹에 Health가 없습니다.");

            Assert.AreEqual(ScavHealth, health.Max,
                "적 체력이 기준(스캐브 20)과 다릅니다. "
                + "한 발에 죽으면 무기 티어도 상태이상도 체감되지 않습니다. "
                + "(docs/Blob_Combat_Baseline.md 5절)");
        }

        [Test]
        public void 적_프리팹의_피해가_기준과_같다()
        {
            var attack = LoadEnemy().GetComponent<EnemyAttack>();

            Assert.IsNotNull(attack, "Enemy 프리팹에 EnemyAttack이 없습니다.");

            Assert.AreEqual(ScavDamage, attack.Damage,
                "적 피해가 기준(스캐브 8)과 다릅니다. "
                + "피해가 낮으면 플레이어가 사실상 죽지 않아 "
                + "「죽으면 잃는다」는 이 게임의 뼈대를 확인할 수 없습니다. "
                + "(docs/Blob_Combat_Baseline.md 5절)");

            Assert.AreEqual(ScavArmourPenetration, attack.ArmourPenetration,
                "스캐브는 방어 관통 0입니다. 방어구가 의미를 갖는 첫 적입니다.");
        }

        /// <summary>
        /// 기준의 검증 문장을 그대로 계산한다 —
        /// 「무방어로 1장 적 피해 10을 10대 맞으면 죽는다」(1절).
        /// 스캐브는 8이므로 무방어 13대. 100대와 13대는 전혀 다른 게임이다.
        /// </summary>
        [Test]
        public void 무방어_플레이어가_납득할_횟수에_죽는다()
        {
            var attack = LoadEnemy().GetComponent<EnemyAttack>();

            int hitsToKill = Mathf.CeilToInt(
                CombatConstants.PlayerBaseHealth / (float)attack.Damage);

            Assert.LessOrEqual(hitsToKill, 20,
                $"무방어로 {hitsToKill}대를 맞아야 죽습니다. "
                + "20대를 넘으면 사망을 확인하는 것 자체가 불가능합니다.");

            Assert.GreaterOrEqual(hitsToKill, 5,
                $"무방어로 {hitsToKill}대면 죽습니다. "
                + "5대 미만은 회피를 배울 틈이 없습니다.");
        }

        // ── 원거리 — 자전체 ───────────────────────────────────────────
        // 근접만 있으면 전투가 「붙는다 / 뺀다」 두 동작으로 끝난다.

        private const string RangedPrefab = "Assets/Prefabs/EnemyRanged.prefab";

        private static GameObject LoadRanged()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(RangedPrefab);

            Assert.IsNotNull(go,
                $"{RangedPrefab}이 없습니다. 「Blob/Enemy/원거리 적 프리팹 생성」을 실행하십시오.");

            return go;
        }

        [Test]
        public void 원거리_적이_기준_수치를_쓴다()
        {
            GameObject go = LoadRanged();

            var health = go.GetComponent<Health>();
            var attack = go.GetComponent<EnemyAttack>();

            Assert.AreEqual(EnemyPrefabGenerator.SpitterHealth, health.Max);
            Assert.AreEqual(EnemyPrefabGenerator.SpitterDamage, attack.Damage);
            Assert.AreEqual(EnemyPrefabGenerator.SpitterArmourPenetration,
                attack.ArmourPenetration,
                "자전체는 방어 관통 1입니다. (Combat_Baseline 5절)");
        }

        /// <summary>
        /// 원거리인데 투사체가 비어 있으면 Awake에서 오류만 찍고
        /// 아무것도 쏘지 않는다 — 「적이 공격하지 않는다」로 보인다.
        /// </summary>
        [Test]
        public void 원거리_적은_투사체를_들고_있다()
        {
            var attack = LoadRanged().GetComponent<EnemyAttack>();

            Assert.AreEqual(EnemyAttackKind.Ranged, attack.Kind,
                "원거리로 설정되지 않았습니다.");

            var so = new SerializedObject(attack);

            Assert.IsNotNull(so.FindProperty("projectilePrefab").objectReferenceValue,
                "projectilePrefab이 비어 있으면 예비동작만 하고 쏘지 않습니다.");
        }

        /// <summary>
        /// 유지 거리가 사거리보다 멀면 영원히 사거리에 들어오지 못한다.
        /// 「다가오지도 쏘지도 않는 적」이 되는 전형적인 설정 사고다.
        /// </summary>
        [Test]
        public void 원거리_적의_유지_거리가_사거리_안쪽이다()
        {
            var attack = LoadRanged().GetComponent<EnemyAttack>();

            var so = new SerializedObject(attack);

            float preferred = so.FindProperty("preferredDistance").floatValue;

            Assert.Less(preferred, attack.AttackRange,
                $"유지 거리 {preferred}m가 사거리 {attack.AttackRange}m보다 멉니다. "
                + "적이 영원히 쏘지 못합니다.");
        }
    
        // ── 유형·진영 연결 (7-F) ──────────────────────────────────────
        //
        // 【프리팹과 표가 갈라지지 않게 강제한다.】
        // 런타임 수치는 EnemyIdentity가 EnemyArchetypeTable에서 가져오므로
        // 프리팹에 적힌 값은 사실 쓰이지 않는다. 그래도 검사하는 이유는,
        // 인스펙터를 연 사람이 압착기 프리팹에서 체력 20을 읽으면
        // 틀린 정보를 사실로 믿게 되기 때문이다.

        private static readonly (string path, EnemyArchetype archetype)[] Wired =
        {
            // 이미 있던 둘은 이름이 다르다. 씬과 스포너가 이 경로를 참조하므로 바꾸지 않는다.
            ("Assets/Prefabs/Enemy.prefab",         EnemyArchetype.Scav),
            ("Assets/Prefabs/EnemyRanged.prefab",   EnemyArchetype.Dynamo),

            ("Assets/Prefabs/EnemyCrusher.prefab",  EnemyArchetype.Crusher),
            ("Assets/Prefabs/EnemyLurker.prefab",   EnemyArchetype.Lurker),
            ("Assets/Prefabs/EnemyChemic.prefab",   EnemyArchetype.Chemic),
            ("Assets/Prefabs/EnemySpecimen.prefab", EnemyArchetype.Specimen),
            ("Assets/Prefabs/EnemySettled.prefab",  EnemyArchetype.Settled),
            ("Assets/Prefabs/EnemySentry.prefab",   EnemyArchetype.Sentry),
            ("Assets/Prefabs/EnemyWraith.prefab",   EnemyArchetype.Wraith)
        };

        [Test]
        public void 적_프리팹이_유형과_진영을_갖는다()
        {
            foreach (var row in Wired)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(row.path);

                Assert.IsNotNull(go,
                    $"{row.path}이 없습니다. 「Blob/Enemy/유형·진영 연결」을 실행하십시오.");

                var identity = go.GetComponent<EnemyIdentity>();

                Assert.IsNotNull(identity,
                    $"{row.path}에 EnemyIdentity가 없습니다. "
                    + "유형 수치도 등급도 속성도 하나도 적용되지 않습니다.");

                Assert.AreEqual(row.archetype, identity.Archetype, row.path);

                Assert.IsNotNull(go.GetComponent<EnemyAggro>(),
                    $"{row.path}에 EnemyAggro가 없습니다. "
                    + "진영이 있어도 누구를 노릴지 정하는 쪽이 없습니다.");
            }
        }

        [Test]
        public void 프리팹에_적힌_수치가_유형_표와_같다()
        {
            foreach (var row in Wired)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(row.path);

                Assert.IsNotNull(go, row.path);

                EnemyArchetypeStats stats = EnemyArchetypeTable.Of(row.archetype);
                string who = EnemyArchetypeTable.Name(row.archetype);

                var health = go.GetComponent<Health>();

                Assert.AreEqual(stats.health, health.Max,
                    $"{who} 프리팹의 체력이 표({stats.health})와 다릅니다.");

                var attack = go.GetComponent<EnemyAttack>();

                Assert.AreEqual(stats.damage, attack.Damage,
                    $"{who} 프리팹의 피해가 표({stats.damage})와 다릅니다.");

                Assert.AreEqual(stats.armourPenetration, attack.ArmourPenetration,
                    $"{who} 프리팹의 방어 관통이 표({stats.armourPenetration})와 다릅니다.");

                // 【원거리형을 근접 프리팹에서 만들면 쏘는데 아무것도 안 나간다.】
                // 투사체 참조는 원거리 프리팹에만 들어 있다.
                Assert.AreEqual(
                    stats.ranged ? EnemyAttackKind.Ranged : EnemyAttackKind.Melee,
                    attack.Kind,
                    $"{who} 프리팹의 공격 방식이 표와 다릅니다.");

                var movement = go.GetComponent<EnemyMovement>();

                Assert.AreEqual(stats.moveSpeed, movement.BaseMoveSpeed, 0.001f,
                    $"{who} 프리팹의 이동 속도가 표({stats.moveSpeed})와 다릅니다.");
            }
        }

        [Test]
        public void 유형_아홉이_모두_프리팹을_갖는다()
        {
            Assert.AreEqual(EnemyArchetypeTable.Count, Wired.Length,
                "유형 표는 9종인데 연결된 프리팹 수가 다릅니다.");

            var seen = new System.Collections.Generic.HashSet<EnemyArchetype>();

            foreach (var row in Wired)
            {
                Assert.IsTrue(seen.Add(row.archetype), $"{row.archetype}이 두 번 연결됐습니다.");

                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(row.path), row.path);
            }
        }

        [Test]
        public void 적_프리팹이_높이로_떠오르지_않는다()
        {
            // 【하늘로 올라가던 버그를 고정한다.】
            // 중력이 꺼져 있는데 Y가 자유로우면, 적끼리 부딪혀 한 번 위로
            // 밀린 속도가 영영 남는다. 실제로 적들이 화면 위로 흩어져 떠올랐다.
            // 탑다운이라 높이는 쓰지 않으므로 물리에서 아예 잠근다.
            foreach (var row in Wired)
            {
                var body = AssetDatabase.LoadAssetAtPath<GameObject>(row.path)
                    .GetComponent<Rigidbody>();

                Assert.IsNotNull(body, row.path);

                Assert.IsTrue((body.constraints & RigidbodyConstraints.FreezePositionY) != 0,
                    $"{EnemyArchetypeTable.Name(row.archetype)}의 Y가 잠겨 있지 않습니다. "
                    + "부딪히면 하늘로 올라갑니다.");
            }
        }

        [Test]
        public void 원거리형이_투사체를_들고_있다()
        {
            // 원거리인데 투사체가 비어 있으면 공격이 아무 일도 하지 않는다.
            // 「쏘는 것처럼 보이는데 피해가 안 들어온다」는 눈으로 찾기 가장 어려운 버그다.
            foreach (var row in Wired)
            {
                if (!EnemyArchetypeTable.Of(row.archetype).ranged)
                    continue;

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(row.path);
                var attack = go.GetComponent<EnemyAttack>();

                Assert.IsNotNull(attack.ProjectilePrefab,
                    $"{EnemyArchetypeTable.Name(row.archetype)}이 원거리인데 투사체가 없습니다.");
            }
        }

        [Test]
        public void 연결된_프리팹이_서로_다른_진영을_덮는다()
        {
            // 【세 진영이 없으면 난전을 눈으로 볼 수 없다.】
            // 전부 같은 소속이면 적끼리는 영원히 싸우지 않는다.
            var factions = new System.Collections.Generic.HashSet<Faction>();

            foreach (var row in Wired)
                factions.Add(EnemyArchetypeTable.Of(row.archetype).faction);

            Assert.GreaterOrEqual(factions.Count, 3,
                $"연결된 프리팹이 진영 {factions.Count}종만 덮습니다. "
                + "서로 적대하는 셋이 있어야 난전을 확인할 수 있습니다.");
        }
    }
}
