using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 【씬을 닫을 때 새 오브젝트를 만들지 않는다】 (결정 2-128)
    /// 철수로 구역 씬이 닫힐 때 적의 OnDisable이 EnemyManager.EnsureInstance를 불러
    /// 닫히는 씬에 「EnemyManager (Runtime)」이 남았다 — 「Some objects were not cleaned up」.
    /// </summary>
    public class SceneTeardownTests
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

        [SetUp]
        public void NoManager()
        {
            foreach (EnemyManager m in Object.FindObjectsByType<EnemyManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(m.gameObject);
        }

        [Test]
        public void Brain_releasing_its_turn_without_a_manager_creates_nothing()
        {
            var go = new GameObject("brain");
            try
            {
                EnemyBrain brain = go.AddComponent<EnemyBrain>();
                typeof(EnemyBrain).GetMethod("OnDisable", Hidden).Invoke(brain, null);

                Assert.IsNull(Object.FindAnyObjectByType<EnemyManager>(FindObjectsInactive.Include),
                    "차례를 놓으려고 EnemyManager를 새로 만들면 닫히는 씬에 남는다");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Enemy_returning_to_pool_without_a_manager_creates_nothing()
        {
            var go = new GameObject("enemy");
            try
            {
                EnemyController enemy = go.AddComponent<EnemyController>();
                enemy.OnDespawned();

                Assert.IsNull(Object.FindAnyObjectByType<EnemyManager>(FindObjectsInactive.Include));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
