using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// 런 중 상태(RunMutationState) 테스트.
    ///
    /// v5 §10-9의 확정 규칙을 그대로 검증한다.
    /// Core 2개 / 소켓 3개 / 기원형 1개 / Nucleus 100 / 중복 등장 없음.
    /// </summary>
    public class RunMutationStateTests
    {
        private static MutationDefinition ProjectileCore(string id) =>
            MutationTestFactory.CreateCore(id, MutationTag.Projectile);

        [Test]
        public void Core는_2개까지만_보유할_수_있다()
        {
            var state = new RunMutationState();

            Assert.IsTrue(state.TryAcquire(ProjectileCore("core_1")));
            Assert.IsTrue(state.TryAcquire(ProjectileCore("core_2")));
            Assert.IsFalse(state.TryAcquire(ProjectileCore("core_3")));

            Assert.AreEqual(RunMutationState.MaxCores, state.Cores.Count);
        }

        [Test]
        public void 같은_변이를_두_번_획득할_수_없다()
        {
            var state = new RunMutationState();
            MutationDefinition core = ProjectileCore("core_pierce");

            Assert.IsTrue(state.TryAcquire(core));
            Assert.IsFalse(state.TryAcquire(core));
            Assert.AreEqual(1, state.AcquiredCount);
        }

        [Test]
        public void Core가_없으면_Support를_획득할_수_없다()
        {
            var state = new RunMutationState();

            MutationDefinition support = MutationTestFactory.CreateSupport("sup_a");

            Assert.IsFalse(state.CanAcquire(support));
            Assert.IsFalse(state.TryAcquire(support));
        }

        [Test]
        public void 태그를_만족하지_않는_Core에는_Support가_붙지_않는다()
        {
            var state = new RunMutationState();

            // 지대 태그만 가진 Core
            state.TryAcquire(MutationTestFactory.CreateCore("core_zone", MutationTag.Zone));

            MutationDefinition projectileSupport =
                MutationTestFactory.CreateSupport("sup_projectile", MutationTag.Projectile);

            Assert.AreEqual(-1, state.FindSocketFor(projectileSupport));
            Assert.IsFalse(state.CanAcquire(projectileSupport));
        }

        [Test]
        public void 태그를_만족하는_Core를_찾아_장착된다()
        {
            var state = new RunMutationState();

            state.TryAcquire(MutationTestFactory.CreateCore("core_zone", MutationTag.Zone));
            state.TryAcquire(ProjectileCore("core_projectile"));

            MutationDefinition support =
                MutationTestFactory.CreateSupport("sup_projectile", MutationTag.Projectile);

            Assert.AreEqual(1, state.FindSocketFor(support));
            Assert.IsTrue(state.TryAcquire(support));

            Assert.AreEqual(0, state.GetSockets(0).Count);
            Assert.AreEqual(1, state.GetSockets(1).Count);
        }

        [Test]
        public void 소켓은_Core당_3개다()
        {
            var state = new RunMutationState();

            state.TryAcquire(ProjectileCore("core_1"));

            for (int i = 0; i < RunMutationState.SocketsPerCore; i++)
            {
                Assert.IsTrue(state.TryAcquire(
                    MutationTestFactory.CreateSupport($"sup_{i}", MutationTag.Projectile)));
            }

            Assert.AreEqual(0, state.FreeSocketCount);
            Assert.IsFalse(state.TryAcquire(
                MutationTestFactory.CreateSupport("sup_overflow", MutationTag.Projectile)));
        }

        [Test]
        public void Core_2개면_최대_6개의_Support를_장착한다()
        {
            var state = new RunMutationState();

            state.TryAcquire(ProjectileCore("core_1"));
            state.TryAcquire(ProjectileCore("core_2"));

            Assert.AreEqual(6, state.FreeSocketCount);

            for (int i = 0; i < 6; i++)
            {
                Assert.IsTrue(state.TryAcquire(
                    MutationTestFactory.CreateSupport($"sup_{i}", MutationTag.Projectile)));
            }

            Assert.AreEqual(0, state.FreeSocketCount);
        }

        [Test]
        public void 기원형은_동시에_1개만_장착된다()
        {
            var state = new RunMutationState();

            MutationDefinition first =
                MutationTestFactory.CreateMeta("meta_origin_1", MetaTriggerKind.Invocation);
            MutationDefinition second =
                MutationTestFactory.CreateMeta("meta_origin_2", MetaTriggerKind.Invocation);

            Assert.IsTrue(state.TryAcquire(first));
            Assert.IsFalse(state.TryAcquire(second));
            Assert.AreSame(first, state.Invocation);
        }

        [Test]
        public void 자동_발동형은_개수_제한이_없다()
        {
            var state = new RunMutationState();

            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(state.TryAcquire(
                    MutationTestFactory.CreateMeta($"meta_auto_{i}", MetaTriggerKind.Automatic)));
            }

            Assert.AreEqual(4, state.Metas.Count);
            Assert.IsNull(state.Invocation);
        }

        [Test]
        public void Nucleus_한도를_넘는_유지형은_획득할_수_없다()
        {
            var state = new RunMutationState();

            // 전령 45 × 2 = 90, 남은 10
            Assert.IsTrue(state.TryAcquire(MutationTestFactory.CreatePersistent("herald_1", 45)));
            Assert.IsTrue(state.TryAcquire(MutationTestFactory.CreatePersistent("herald_2", 45)));

            Assert.AreEqual(90, state.NucleusSpent);
            Assert.AreEqual(10, state.NucleusRemaining);

            // 기동 20은 들어갈 자리가 없다
            Assert.IsFalse(state.TryAcquire(MutationTestFactory.CreatePersistent("mobility", 20)));

            // 감각 확장 10은 정확히 들어간다 (경계값)
            Assert.IsTrue(state.TryAcquire(MutationTestFactory.CreatePersistent("sense", 10)));
            Assert.AreEqual(0, state.NucleusRemaining);
        }

        [Test]
        public void Nucleus_상한을_올리면_더_담을_수_있다()
        {
            var state = new RunMutationState { NucleusCapacity = 120 };

            Assert.IsTrue(state.TryAcquire(MutationTestFactory.CreatePersistent("herald_1", 45)));
            Assert.IsTrue(state.TryAcquire(MutationTestFactory.CreatePersistent("herald_2", 45)));
            Assert.IsTrue(state.TryAcquire(MutationTestFactory.CreatePersistent("mobility", 20)));

            Assert.AreEqual(110, state.NucleusSpent);
        }

        [Test]
        public void 상호_배타_변이는_동시_장착_시_양쪽_모두_무효화된다()
        {
            var state = new RunMutationState();

            state.TryAcquire(ProjectileCore("core_1"));

            MutationDefinition longFuse = MutationTestFactory.CreateSupport(
                "sup_long_fuse", MutationTag.Projectile,
                mutuallyExclusiveIds: new[] { "sup_short_fuse" });

            MutationDefinition shortFuse = MutationTestFactory.CreateSupport(
                "sup_short_fuse", MutationTag.Projectile,
                mutuallyExclusiveIds: new[] { "sup_long_fuse" });

            state.TryAcquire(longFuse);

            Assert.IsFalse(state.IsNullified(longFuse), "혼자일 때는 정상 작동해야 합니다.");

            state.TryAcquire(shortFuse);

            Assert.IsTrue(state.IsNullified(longFuse));
            Assert.IsTrue(state.IsNullified(shortFuse));
        }

        [Test]
        public void 상호_배타여도_획득_자체는_막지_않는다()
        {
            // 10-1-2: 다양성을 "중복 획득 금지"로 강제하지 않는다.
            var state = new RunMutationState();

            state.TryAcquire(ProjectileCore("core_1"));

            MutationDefinition a = MutationTestFactory.CreateSupport(
                "sup_a", MutationTag.Projectile, mutuallyExclusiveIds: new[] { "sup_b" });
            MutationDefinition b = MutationTestFactory.CreateSupport(
                "sup_b", MutationTag.Projectile, mutuallyExclusiveIds: new[] { "sup_a" });

            Assert.IsTrue(state.TryAcquire(a));
            Assert.IsTrue(state.TryAcquire(b));
        }

        [Test]
        public void 기능_배타는_해당_상태의_유발을_차단한다()
        {
            var state = new RunMutationState();

            state.TryAcquire(ProjectileCore("core_1"));

            // 번제 — 점화된 적에게 추가 피해를 주지만 점화를 유발할 수 없다
            state.TryAcquire(MutationTestFactory.CreateSupport(
                "sup_burnt_offering", MutationTag.Projectile,
                cost: CostType.FunctionalExclusion,
                blocksStatusCreation: true,
                blockedStatus: StatusEffectType.Ignite));

            Assert.IsTrue(state.IsStatusBlocked(StatusEffectType.Ignite));
            Assert.IsFalse(state.IsStatusBlocked(StatusEffectType.Freeze));
            Assert.IsFalse(state.IsStatusBlocked(StatusEffectType.None));
        }

        [Test]
        public void 무효화된_변이는_기능_배타도_적용되지_않는다()
        {
            // 엣지 케이스: 상호 배타로 죽은 변이가 페널티만 남기면 안 된다.
            var state = new RunMutationState();

            state.TryAcquire(ProjectileCore("core_1"));

            state.TryAcquire(MutationTestFactory.CreateSupport(
                "sup_a", MutationTag.Projectile,
                mutuallyExclusiveIds: new[] { "sup_b" },
                blocksStatusCreation: true,
                blockedStatus: StatusEffectType.Ignite));

            Assert.IsTrue(state.IsStatusBlocked(StatusEffectType.Ignite));

            state.TryAcquire(MutationTestFactory.CreateSupport(
                "sup_b", MutationTag.Projectile,
                mutuallyExclusiveIds: new[] { "sup_a" }));

            Assert.IsFalse(state.IsStatusBlocked(StatusEffectType.Ignite));
        }

        [Test]
        public void 무효화된_변이는_무기_보정에도_반영되지_않는다()
        {
            var state = new RunMutationState();

            state.TryAcquire(ProjectileCore("core_1"));

            state.TryAcquire(MutationTestFactory.CreateSupport(
                "sup_a", MutationTag.Projectile,
                behaviour: ProjectileBehaviourType.Pierce, behaviourCharges: 2,
                mutuallyExclusiveIds: new[] { "sup_b" }));

            Assert.AreEqual(2, state.GetModifiers().Behaviours.GetRemaining(ProjectileBehaviourType.Pierce));

            state.TryAcquire(MutationTestFactory.CreateSupport(
                "sup_b", MutationTag.Projectile,
                mutuallyExclusiveIds: new[] { "sup_a" }));

            Assert.AreEqual(0, state.GetModifiers().Behaviours.GetRemaining(ProjectileBehaviourType.Pierce));
        }

        [Test]
        public void 무기_보정은_중첩_없이_한_번씩만_합산된다()
        {
            var state = new RunMutationState();

            state.TryAcquire(MutationTestFactory.CreateCore(
                "core_pierce", MutationTag.Projectile,
                behaviour: ProjectileBehaviourType.Pierce, behaviourCharges: 3));

            state.TryAcquire(MutationTestFactory.CreateSupport(
                "sup_dense", MutationTag.Projectile,
                behaviour: ProjectileBehaviourType.Pierce, behaviourCharges: 2,
                fireIntervalMultiplier: 1.15f));

            WeaponModifiers modifiers = state.GetModifiers();

            Assert.AreEqual(5, modifiers.Behaviours.GetRemaining(ProjectileBehaviourType.Pierce));
            Assert.AreEqual(1.15f, modifiers.FireIntervalMultiplier, 0.0001f);
        }

        [Test]
        public void Clear는_런_상태를_전부_초기화한다()
        {
            var state = new RunMutationState();

            state.TryAcquire(ProjectileCore("core_1"));
            state.TryAcquire(MutationTestFactory.CreatePersistent("per_1", 30));

            state.Clear();

            Assert.AreEqual(0, state.AcquiredCount);
            Assert.AreEqual(0, state.Cores.Count);
            Assert.AreEqual(0, state.NucleusSpent);
            Assert.AreEqual(RunMutationState.BaseNucleus, state.NucleusRemaining);
        }

        [Test]
        public void null은_획득되지_않는다()
        {
            var state = new RunMutationState();

            Assert.IsFalse(state.CanAcquire(null));
            Assert.IsFalse(state.TryAcquire(null));
            Assert.IsFalse(state.Has(null));
            Assert.IsFalse(state.IsNullified(null));
        }
    }
}
