using System;
using NUnit.Framework;
using UnityEngine;

namespace Blob.Tests
{
    /// <summary>GameObjectPool 단위 테스트.</summary>
    public class GameObjectPoolTests
    {
        private GameObject prefab;
        private Transform parent;

        [SetUp]
        public void SetUp()
        {
            prefab = new GameObject("TestPrefab");
            prefab.AddComponent<PoolableProbe>();
            prefab.SetActive(false);

            parent = new GameObject("PoolParent").transform;
        }

        [TearDown]
        public void TearDown()
        {
            if (parent != null)
                UnityEngine.Object.DestroyImmediate(parent.gameObject);

            if (prefab != null)
                UnityEngine.Object.DestroyImmediate(prefab);
        }

        private GameObjectPool CreatePool(int capacity = 4, int maxSize = 16)
        {
            return new GameObjectPool(prefab, parent, capacity, maxSize);
        }

        [Test]
        public void Get_활성화된_인스턴스를_반환한다()
        {
            GameObjectPool pool = CreatePool();

            GameObject instance = pool.Get(new Vector3(1f, 2f, 3f), Quaternion.identity);

            Assert.IsNotNull(instance);
            Assert.IsTrue(instance.activeSelf, "풀에서 꺼낸 인스턴스는 활성 상태여야 합니다.");
            Assert.AreEqual(new Vector3(1f, 2f, 3f), instance.transform.position);
            Assert.AreEqual(1, pool.CountActive);
        }

        [Test]
        public void Get_PooledObject를_원본_프리팹에_연결한다()
        {
            GameObjectPool pool = CreatePool();

            GameObject instance = pool.Get(Vector3.zero, Quaternion.identity);
            var pooled = instance.GetComponent<PooledObject>();

            Assert.IsNotNull(pooled, "풀 인스턴스에는 PooledObject가 자동으로 붙어야 합니다.");
            Assert.AreEqual(prefab, pooled.SourcePrefab);
            Assert.IsTrue(pooled.IsSpawned);
        }

        [Test]
        public void Release_후_Get하면_같은_인스턴스를_재사용한다()
        {
            GameObjectPool pool = CreatePool();

            GameObject first = pool.Get(Vector3.zero, Quaternion.identity);
            int firstId = first.GetInstanceID();

            pool.Release(first);

            GameObject second = pool.Get(Vector3.one, Quaternion.identity);

            Assert.AreEqual(firstId, second.GetInstanceID(), "풀링의 핵심은 재사용입니다.");
            Assert.AreEqual(1, pool.CountAll, "새로 Instantiate되지 않아야 합니다.");
        }

        [Test]
        public void Release시_비활성화되고_카운트가_이동한다()
        {
            GameObjectPool pool = CreatePool();

            GameObject instance = pool.Get(Vector3.zero, Quaternion.identity);
            pool.Release(instance);

            Assert.IsFalse(instance.activeSelf);
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(1, pool.CountInactive);
        }

        [Test]
        public void IPoolable_생명주기가_정확히_한_번씩_통지된다()
        {
            GameObjectPool pool = CreatePool();

            GameObject instance = pool.Get(Vector3.zero, Quaternion.identity);
            var probe = instance.GetComponent<PoolableProbe>();

            Assert.AreEqual(1, probe.SpawnedCount);
            Assert.AreEqual(0, probe.DespawnedCount);

            pool.Release(instance);

            Assert.AreEqual(1, probe.SpawnedCount);
            Assert.AreEqual(1, probe.DespawnedCount);

            pool.Get(Vector3.zero, Quaternion.identity);

            Assert.AreEqual(2, probe.SpawnedCount, "재사용 시 OnSpawned가 다시 호출되어야 합니다.");
        }

        [Test]
        public void Prewarm은_미리_생성하고_비활성_상태로_대기시킨다()
        {
            GameObjectPool pool = CreatePool();

            pool.Prewarm(5);

            Assert.AreEqual(5, pool.CountInactive);
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(5, pool.CountAll);
        }

        // ── 엣지 케이스 ──────────────────────────────────────────

        [Test]
        public void 엣지_null_프리팹은_생성자에서_예외를_던진다()
        {
            Assert.Throws<ArgumentNullException>(
                () => new GameObjectPool(null, parent, 4, 16));
        }

        [Test]
        public void 엣지_maxSize가_0이면_예외를_던진다()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new GameObjectPool(prefab, parent, 4, 0));
        }

        [Test]
        public void 엣지_음수_capacity는_예외를_던진다()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new GameObjectPool(prefab, parent, -1, 16));
        }

        [Test]
        public void 엣지_null_Release는_예외없이_무시된다()
        {
            GameObjectPool pool = CreatePool();

            Assert.DoesNotThrow(() => pool.Release(null));
            Assert.AreEqual(0, pool.CountInactive);
        }

        [Test]
        public void 엣지_Prewarm에_0이하를_넣으면_아무것도_하지_않는다()
        {
            GameObjectPool pool = CreatePool();

            pool.Prewarm(0);
            pool.Prewarm(-3);

            Assert.AreEqual(0, pool.CountAll);
        }

        [Test]
        public void 엣지_maxSize를_초과한_반납분은_보관하지_않는다()
        {
            GameObjectPool pool = CreatePool(capacity: 1, maxSize: 2);

            GameObject a = pool.Get(Vector3.zero, Quaternion.identity);
            GameObject b = pool.Get(Vector3.zero, Quaternion.identity);
            GameObject c = pool.Get(Vector3.zero, Quaternion.identity);

            pool.Release(a);
            pool.Release(b);
            pool.Release(c);

            Assert.AreEqual(2, pool.CountInactive,
                "maxSize를 넘는 반납분은 파괴되어 메모리가 무한히 늘지 않아야 합니다.");
        }
    }
}
