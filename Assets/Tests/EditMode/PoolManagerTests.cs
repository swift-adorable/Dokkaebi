using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Blob.Tests
{
    /// <summary>PoolManager 단위 테스트.</summary>
    public class PoolManagerTests
    {
        private GameObject managerObject;
        private PoolManager manager;
        private GameObject prefab;
        private GameObject otherPrefab;

        [SetUp]
        public void SetUp()
        {
            managerObject = new GameObject("PoolManager");
            manager = managerObject.AddComponent<PoolManager>();

            prefab = new GameObject("EnemyPrefab");
            prefab.AddComponent<PoolableProbe>();
            prefab.SetActive(false);

            otherPrefab = new GameObject("BulletPrefab");
            otherPrefab.SetActive(false);

            // 의도적으로 경고/에러 로그를 유발하는 엣지 케이스가 있으므로,
            // 로그 자체가 테스트를 실패시키지 않도록 한다.
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (managerObject != null)
                Object.DestroyImmediate(managerObject);

            if (prefab != null)
                Object.DestroyImmediate(prefab);

            if (otherPrefab != null)
                Object.DestroyImmediate(otherPrefab);

            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void Spawn시_해당_프리팹의_풀이_자동_생성된다()
        {
            Assert.AreEqual(0, manager.PoolCount);

            manager.Spawn(prefab, Vector3.zero, Quaternion.identity);

            Assert.AreEqual(1, manager.PoolCount, "사전 등록 없이 첫 Spawn에서 풀이 만들어져야 합니다.");
        }

        [Test]
        public void 같은_프리팹을_여러_번_Spawn해도_풀은_하나다()
        {
            manager.Spawn(prefab, Vector3.zero, Quaternion.identity);
            manager.Spawn(prefab, Vector3.one, Quaternion.identity);
            manager.Spawn(prefab, Vector3.up, Quaternion.identity);

            Assert.AreEqual(1, manager.PoolCount);
        }

        [Test]
        public void 다른_프리팹은_각각_별도의_풀을_갖는다()
        {
            manager.Spawn(prefab, Vector3.zero, Quaternion.identity);
            manager.Spawn(otherPrefab, Vector3.zero, Quaternion.identity);

            Assert.AreEqual(2, manager.PoolCount);
        }

        [Test]
        public void Despawn은_성공시_true를_반환하고_비활성화한다()
        {
            GameObject instance = manager.Spawn(prefab, Vector3.zero, Quaternion.identity);

            bool result = manager.Despawn(instance);

            Assert.IsTrue(result);
            Assert.IsFalse(instance.activeSelf);
        }

        [Test]
        public void Despawn_후_Spawn하면_같은_인스턴스가_재사용된다()
        {
            GameObject first = manager.Spawn(prefab, Vector3.zero, Quaternion.identity);

            manager.Despawn(first);

            GameObject second = manager.Spawn(prefab, Vector3.one, Quaternion.identity);

            Assert.AreSame(first, second);
        }

        [Test]
        public void PooledObject_Despawn으로_스스로_반납할_수_있다()
        {
            GameObject instance = manager.Spawn(prefab, Vector3.zero, Quaternion.identity);
            var pooled = instance.GetComponent<PooledObject>();

            bool result = pooled.Despawn();

            Assert.IsTrue(result);
            Assert.IsFalse(instance.activeSelf);
        }

        // ── 엣지 케이스 ──────────────────────────────────────────

        [Test]
        public void 엣지_중복_Despawn은_두_번째부터_false를_반환한다()
        {
            GameObject instance = manager.Spawn(prefab, Vector3.zero, Quaternion.identity);

            Assert.IsTrue(manager.Despawn(instance), "첫 반납은 성공해야 합니다.");
            Assert.IsFalse(manager.Despawn(instance),
                "중복 반납이 허용되면 같은 인스턴스가 두 번 배포되어 풀이 오염됩니다.");
            Assert.IsFalse(manager.Despawn(instance));
        }

        [Test]
        public void 엣지_중복_Despawn_후에도_풀_상태가_오염되지_않는다()
        {
            GameObject instance = manager.Spawn(prefab, Vector3.zero, Quaternion.identity);

            manager.Despawn(instance);
            manager.Despawn(instance);

            manager.TryGetPool(prefab, out GameObjectPool pool);

            Assert.AreEqual(1, pool.CountInactive, "중복 반납으로 대기 수가 늘어나면 안 됩니다.");
            Assert.AreEqual(1, pool.CountAll);
        }

        [Test]
        public void 엣지_풀_소속이_아닌_오브젝트_Despawn은_false를_반환한다()
        {
            var stray = new GameObject("NotPooled");

            bool result = manager.Despawn(stray);

            Assert.IsFalse(result);

            Object.DestroyImmediate(stray);
        }

        [Test]
        public void 엣지_null_Despawn은_예외없이_false를_반환한다()
        {
            Assert.IsFalse(manager.Despawn(null));
        }

        [Test]
        public void 엣지_null_프리팹_Spawn은_null을_반환하고_풀을_만들지_않는다()
        {
            // ignoreFailingMessages만으로는 에러 로그가 테스트를 실패시키는 것을 막지 못한다.
            // 예상되는 로그를 명시적으로 선언한다.
            LogAssert.Expect(LogType.Error, "[PoolManager] Spawn에 null 프리팹이 전달되었습니다.");

            GameObject result = manager.Spawn(null, Vector3.zero, Quaternion.identity);

            Assert.IsNull(result);
            Assert.AreEqual(0, manager.PoolCount);
        }

        [Test]
        public void 엣지_Prewarm에_null이나_0을_넣어도_안전하다()
        {
            Assert.DoesNotThrow(() => manager.Prewarm(null, 10));
            Assert.DoesNotThrow(() => manager.Prewarm(prefab, 0));
            Assert.DoesNotThrow(() => manager.Prewarm(prefab, -5));

            Assert.AreEqual(0, manager.PoolCount);
        }

        [Test]
        public void ClearAll은_모든_풀을_제거한다()
        {
            manager.Spawn(prefab, Vector3.zero, Quaternion.identity);
            manager.Spawn(otherPrefab, Vector3.zero, Quaternion.identity);

            manager.ClearAll();

            Assert.AreEqual(0, manager.PoolCount);
        }
    }
}
