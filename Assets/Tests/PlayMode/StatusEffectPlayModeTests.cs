using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Blob.Tests
{
    /// <summary>
    /// 상태이상이 실제 런타임에서 걸리고, 틱하고, 죽음으로 이어지는지 검증한다. (로드맵 5-F)
    ///
    /// EditMode가 검증하는 것은 "공식이 맞는가"이고,
    /// 여기서 검증하는 것은 "배선이 이어져 있는가"다.
    /// StatusEffectSystem이 자동 생성되는지, Health가 스스로 등록하는지,
    /// 풀 재사용 시 상태가 새는지 — 순수 클래스로는 잡을 수 없는 것들이다.
    /// </summary>
    public class StatusEffectPlayModeTests
    {
        /// <summary>
        /// 테스트를 배속으로 돌린다.
        ///
        /// 도트 검증은 실제로 초 단위 시간이 흘러야 하는데, 그대로 두면
        /// 한 클래스가 12초를 넘겨 MCP 응답 한도에 걸린다.
        /// deltaTime이 함께 스케일되므로 도트 총량은 배속과 무관하게 같다.
        /// (StatusEffectStateTests가 "총량은 틱 간격과 무관"을 이미 고정했다)
        /// </summary>
        private const float TimeScale = 10f;

        private readonly System.Collections.Generic.List<GameObject> spawned = new();

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = TimeScale;
        }

        private Health CreateTarget(int maxHealth = 100, float invulnerable = 0f)
        {
            var go = new GameObject("Target");
            spawned.Add(go);

            var health = go.AddComponent<Health>();

            // 무적 시간이 있으면 도트 검증이 흔들리므로 테스트에서는 0으로 둔다.
            health.SetMaxHealth(maxHealth, refill: true);
            health.ConfigureForTest(invulnerable);

            return health;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;

            foreach (GameObject go in spawned)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }

            spawned.Clear();

            if (StatusEffectSystem.HasInstance)
                StatusEffectSystem.Instance.Clear();
        }

        [UnityTest]
        public IEnumerator 상태를_걸면_시스템이_자동으로_생성되고_추적한다()
        {
            // 씬에 StatusEffectSystem을 배치하지 않아도 동작해야 한다.
            // "씬에 넣는 것을 잊어 런타임에 터지는" 실패 지점을 만들지 않는다.
            Health target = CreateTarget();

            target.ApplyStatus(StatusEffectType.Ignite, 10f);

            yield return null;

            Assert.IsTrue(StatusEffectSystem.HasInstance, "시스템이 자동 생성되지 않았습니다.");
            Assert.IsTrue(target.Status.Has(StatusEffectType.Ignite));
        }

        [UnityTest]
        public IEnumerator 점화가_시간에_따라_실제로_체력을_깎는다()
        {
            Health target = CreateTarget();

            target.ApplyStatus(StatusEffectType.Ignite, 10f);

            int before = target.Current;

            // 점화 지속시간은 4초다. 절반만 지나도 체력이 줄어 있어야 한다.
            yield return new WaitForSeconds(2f);

            Assert.Less(target.Current, before, "점화가 체력을 깎지 않았습니다.");
        }

        [UnityTest]
        public IEnumerator 점화가_끝나면_추적_목록에서_빠진다()
        {
            // 목록에 계속 남으면 적이 쌓일수록 매 프레임 비용이 늘어난다.
            Health target = CreateTarget();

            target.ApplyStatus(StatusEffectType.Ignite, 10f);

            yield return null;

            Assert.AreEqual(1, StatusEffectSystem.Instance.TrackedCount);

            yield return new WaitForSeconds(4.5f);

            Assert.IsFalse(target.Status.Has(StatusEffectType.Ignite));
            Assert.AreEqual(0, StatusEffectSystem.Instance.TrackedCount, "만료 후에도 추적 중입니다.");
        }

        [UnityTest]
        public IEnumerator 도트만으로도_죽는다()
        {
            // 체력을 도트 총량보다 낮게 잡는다. 점화 총량은 기본 피해와 같다.
            Health target = CreateTarget(maxHealth: 6);

            target.ApplyStatus(StatusEffectType.Ignite, 10f);

            yield return new WaitForSeconds(4.5f);

            Assert.IsTrue(target.IsDead, "도트만으로 죽지 않았습니다.");
        }

        [UnityTest]
        public IEnumerator 도트는_무적을_무시하지만_무적을_갱신하지_않는다()
        {
            // 무적 중에 도트가 멈추면 "맞고 굴렀더니 화상이 사라지는" 규칙이 되고,
            // 도트가 무적을 갱신하면 도트만으로 영구 무적이 된다. 둘 다 막아야 한다.
            Health target = CreateTarget(maxHealth: 100, invulnerable: 10f);

            target.ApplyStatus(StatusEffectType.Ignite, 100f);

            int before = target.Current;

            yield return new WaitForSeconds(1f);

            Assert.Less(target.Current, before, "무적 중에 도트가 멈췄습니다.");
            Assert.IsFalse(target.IsInvulnerable, "도트가 무적을 갱신했습니다.");
        }

        [UnityTest]
        public IEnumerator 면역_대상에게는_상태가_걸리지_않는다()
        {
            // 막는 것은 장비의 몫이다. 판정 지점이 이어져 있는지만 확인한다.
            // (장비 시스템은 6단계이므로 현재 IsImmuneTo는 항상 false다)
            Health target = CreateTarget();

            Assert.IsFalse(target.IsImmuneTo(StatusEffectType.Ignite));

            yield return null;
        }

        [UnityTest]
        public IEnumerator 풀_재사용_시_이전_상태가_새지_않는다()
        {
            // 재사용된 개체가 이전 런의 점화를 들고 나오면 밸런스가 조용히 무너진다.
            Health target = CreateTarget();

            target.ApplyStatus(StatusEffectType.Ignite, 10f);

            yield return null;

            Assert.IsTrue(target.Status.HasAny);

            target.OnSpawned();

            Assert.IsFalse(target.Status.HasAny, "재스폰 후에도 상태가 남아 있습니다.");
            Assert.AreEqual(0, StatusEffectSystem.Instance.TrackedCount, "재스폰 후에도 추적 중입니다.");
        }

        [UnityTest]
        public IEnumerator 감전은_이후_피해를_증폭시킨다()
        {
            Health plain = CreateTarget();
            Health shocked = CreateTarget();

            shocked.ApplyStatus(StatusEffectType.Shock, 10f);

            yield return null;

            var hit = DamageRequest.FromWeapon(100, 0f);
            hit.distance = -1f;

            int plainDamage = plain.TakeDamage(hit);
            int shockedDamage = shocked.TakeDamage(hit);

            Assert.AreEqual(100, plainDamage);
            Assert.AreEqual(120, shockedDamage, "감전이 받는 피해를 20% 올려야 합니다.");
        }

        [UnityTest]
        public IEnumerator 여러_대상의_도트가_서로_간섭하지_않는다()
        {
            Health a = CreateTarget();
            Health b = CreateTarget();

            a.ApplyStatus(StatusEffectType.Ignite, 10f);
            b.ApplyStatus(StatusEffectType.Poison, 10f);

            yield return null;

            Assert.AreEqual(2, StatusEffectSystem.Instance.TrackedCount);
            Assert.IsTrue(a.Status.Has(StatusEffectType.Ignite));
            Assert.IsFalse(a.Status.Has(StatusEffectType.Poison));
            Assert.IsTrue(b.Status.Has(StatusEffectType.Poison));
            Assert.IsFalse(b.Status.Has(StatusEffectType.Ignite));
        }
    }
}
