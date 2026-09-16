using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// 합성 발사가 RunMutationState → WeaponModifiers 경로에서 실제로 성립하는지 검증한다.
    ///
    /// 전달 Core의 행동과 적재 Core의 상태가 '한 발'에 합쳐져야 한다.
    /// </summary>
    public class CompositeFireIntegrationTests
    {
        private static MutationDefinition DeliveryCore(
            string id, ProjectileBehaviourType behaviour, int charges)
        {
            return MutationTestFactory.CreateCore(id, MutationTag.Projectile,
                family: CoreFamily.Delivery, behaviour: behaviour, behaviourCharges: charges);
        }

        private static MutationDefinition AilmentCore(
            string id, MutationTag tags, StatusEffectType status)
        {
            return MutationTestFactory.CreateCore(id, tags,
                family: CoreFamily.Ailment, createsStatus: status);
        }

        [Test]
        public void 전달_Core와_적재_Core가_한_발에_합쳐진다()
        {
            // 관통 + 화염 = "관통하면서 점화시키는 탄 1발"
            var state = new RunMutationState();

            state.TryAcquire(DeliveryCore("core_pierce", ProjectileBehaviourType.Pierce, 3));
            state.TryAcquire(AilmentCore("core_fire", MutationTag.Fire, StatusEffectType.Ignite));

            WeaponModifiers modifiers = state.GetModifiers();

            Assert.AreEqual(3, modifiers.Behaviours.GetRemaining(ProjectileBehaviourType.Pierce));
            Assert.AreEqual(1, modifiers.Ailments.Count);
            Assert.AreEqual(StatusEffectType.Ignite, modifiers.Ailments[0]);

            // 합성의 핵심: Core가 2개여도 탄 수는 1발 그대로다.
            Assert.AreEqual(1, modifiers.TotalProjectiles);
        }

        [Test]
        public void Core_2개여도_탄_수는_늘지_않는다()
        {
            // 동시 발사였다면 2배가 되었을 상황이다. 모바일 성능의 핵심 차이다.
            var state = new RunMutationState();

            state.TryAcquire(DeliveryCore("core_pierce", ProjectileBehaviourType.Pierce, 3));
            state.TryAcquire(DeliveryCore("core_split", ProjectileBehaviourType.Split, 1));

            Assert.AreEqual(1, state.GetModifiers().TotalProjectiles);
        }

        [Test]
        public void 탄_수는_다중_사격_계열로만_늘어난다()
        {
            var state = new RunMutationState();

            state.TryAcquire(DeliveryCore("core_pierce", ProjectileBehaviourType.Pierce, 3));

            state.TryAcquire(MutationTestFactory.CreateSupport(
                "sup_multishot", MutationTag.Projectile,
                cost: CostType.BarrageDensity,
                extraProjectiles: 2,
                fireIntervalMultiplier: 1.3f));

            WeaponModifiers modifiers = state.GetModifiers();

            Assert.AreEqual(3, modifiers.TotalProjectiles);
            Assert.AreEqual(1.3f, modifiers.FireIntervalMultiplier, 0.0001f);
        }

        [Test]
        public void 적재_Core_2개는_두_상태를_모두_싣는다()
        {
            var state = new RunMutationState();

            state.TryAcquire(AilmentCore("core_fire", MutationTag.Fire, StatusEffectType.Ignite));
            state.TryAcquire(AilmentCore("core_frost", MutationTag.Cold, StatusEffectType.Freeze));

            WeaponModifiers modifiers = state.GetModifiers();

            Assert.AreEqual(2, modifiers.Ailments.Count);
            Assert.Contains(StatusEffectType.Ignite, (System.Collections.ICollection)modifiers.Ailments);
            Assert.Contains(StatusEffectType.Freeze, (System.Collections.ICollection)modifiers.Ailments);
        }

        [Test]
        public void 적재_Core_2개는_발사마다_번갈아_부여된다()
        {
            var state = new RunMutationState();

            state.TryAcquire(AilmentCore("core_fire", MutationTag.Fire, StatusEffectType.Ignite));
            state.TryAcquire(AilmentCore("core_frost", MutationTag.Cold, StatusEffectType.Freeze));

            WeaponModifiers modifiers = state.GetModifiers();
            var composite = new CompositeFireState();

            StatusEffectType first = modifiers.Ailments[composite.NextAilmentIndex(modifiers.Ailments.Count)];
            StatusEffectType second = modifiers.Ailments[composite.NextAilmentIndex(modifiers.Ailments.Count)];
            StatusEffectType third = modifiers.Ailments[composite.NextAilmentIndex(modifiers.Ailments.Count)];

            Assert.AreNotEqual(first, second);
            Assert.AreEqual(first, third);
        }

        [Test]
        public void 기폭_계열_Core는_합성_대상이_아니다()
        {
            // 원소 작렬·충격파는 투사체가 아니므로 탄에 상태를 싣지 않는다.
            var state = new RunMutationState();

            state.TryAcquire(MutationTestFactory.CreateCore(
                "core_shockwave",
                MutationTag.AreaOfEffect | MutationTag.Detonator,
                family: CoreFamily.Detonation));

            Assert.AreEqual(0, state.GetModifiers().Ailments.Count);
        }

        [Test]
        public void 기능_배타로_차단된_상태는_탄에_실리지_않는다()
        {
            // 「번제」는 점화된 적에게 추가 피해를 주지만 점화를 유발할 수 없다.
            var state = new RunMutationState();

            state.TryAcquire(AilmentCore("core_fire", MutationTag.Fire, StatusEffectType.Ignite));

            Assert.AreEqual(1, state.GetModifiers().Ailments.Count);

            state.TryAcquire(MutationTestFactory.CreateSupport(
                "sup_burnt_offering", MutationTag.Fire,
                cost: CostType.FunctionalExclusion,
                blocksStatusCreation: true,
                blockedStatus: StatusEffectType.Ignite));

            Assert.AreEqual(0, state.GetModifiers().Ailments.Count,
                "점화를 유발할 수 없게 되었으므로 탄에 실리면 안 됩니다.");
        }

        [Test]
        public void 같은_상태를_만드는_Core가_겹쳐도_중복_등록되지_않는다()
        {
            var state = new RunMutationState();

            state.TryAcquire(AilmentCore("core_fire_a", MutationTag.Fire, StatusEffectType.Ignite));
            state.TryAcquire(AilmentCore("core_fire_b", MutationTag.Fire, StatusEffectType.Ignite));

            Assert.AreEqual(1, state.GetModifiers().Ailments.Count);
        }

        [Test]
        public void Core가_없으면_상태도_행동도_없다()
        {
            var state = new RunMutationState();

            WeaponModifiers modifiers = state.GetModifiers();

            Assert.AreEqual(0, modifiers.Ailments.Count);
            Assert.IsFalse(modifiers.Behaviours.HasAny);
            Assert.AreEqual(1, modifiers.TotalProjectiles);
        }
    }
}
