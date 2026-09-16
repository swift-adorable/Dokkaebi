using System.Collections.Generic;
using NUnit.Framework;
using Random = System.Random;

namespace Blob.Tests
{
    /// <summary>
    /// 선택 풀 필터와 추첨 테스트.
    ///
    /// "장착할 대상이 없어 버려지는 선택지"가 생기지 않는 것이 태그 게이팅의 목적이므로
    /// (마스터 프롬프트 10-2 [1]) 필터가 그 약속을 지키는지 검증한다.
    /// </summary>
    public class MutationSelectionPoolTests
    {
        private static List<MutationDefinition> Loadout(params MutationDefinition[] entries)
        {
            return new List<MutationDefinition>(entries);
        }

        [Test]
        public void 적재한_것만_후보가_된다()
        {
            MutationDefinition inLoadout = MutationTestFactory.CreateCore("core_in");
            MutationDefinition outOfLoadout = MutationTestFactory.CreateCore("core_out");

            List<MutationDefinition> candidates =
                MutationSelectionPool.Build(Loadout(inLoadout), new RunMutationState(), 10);

            Assert.AreEqual(1, candidates.Count);
            Assert.AreSame(inLoadout, candidates[0]);
            Assert.IsFalse(candidates.Contains(outOfLoadout));
        }

        [Test]
        public void 요구_레벨에_도달하지_않으면_등장하지_않는다()
        {
            MutationDefinition low = MutationTestFactory.CreateCore("core_low", requiredLevel: 1);
            MutationDefinition high = MutationTestFactory.CreateCore("core_high", requiredLevel: 7);

            List<MutationDefinition> atLevel3 =
                MutationSelectionPool.Build(Loadout(low, high), new RunMutationState(), 3);

            Assert.AreEqual(1, atLevel3.Count);
            Assert.AreSame(low, atLevel3[0]);

            List<MutationDefinition> atLevel7 =
                MutationSelectionPool.Build(Loadout(low, high), new RunMutationState(), 7);

            Assert.AreEqual(2, atLevel7.Count, "요구 레벨과 같은 레벨이면 등장해야 합니다.");
        }

        [Test]
        public void 이미_획득한_변이는_풀에서_제거된다()
        {
            // v5 §10-9: 중복 등장 없음
            MutationDefinition a = MutationTestFactory.CreateCore("core_a");
            MutationDefinition b = MutationTestFactory.CreateCore("core_b");

            var state = new RunMutationState();
            state.TryAcquire(a);

            List<MutationDefinition> candidates =
                MutationSelectionPool.Build(Loadout(a, b), state, 10);

            Assert.AreEqual(1, candidates.Count);
            Assert.AreSame(b, candidates[0]);
        }

        [Test]
        public void 장착할_Core가_없는_Support는_선택창에_등장하지_않는다()
        {
            MutationDefinition zoneCore =
                MutationTestFactory.CreateCore("core_zone", MutationTag.Zone);

            MutationDefinition projectileSupport =
                MutationTestFactory.CreateSupport("sup_projectile", MutationTag.Projectile);

            var state = new RunMutationState();
            state.TryAcquire(zoneCore);

            List<MutationDefinition> candidates =
                MutationSelectionPool.Build(Loadout(zoneCore, projectileSupport), state, 10);

            Assert.AreEqual(0, candidates.Count,
                "죽은 선택지가 등장하면 태그 게이팅이 깨진 것입니다.");
        }

        [Test]
        public void Core를_얻으면_그_Core용_Support가_풀에_들어온다()
        {
            MutationDefinition core = MutationTestFactory.CreateCore("core_p", MutationTag.Projectile);
            MutationDefinition support =
                MutationTestFactory.CreateSupport("sup_p", MutationTag.Projectile);

            var state = new RunMutationState();

            List<MutationDefinition> before =
                MutationSelectionPool.Build(Loadout(core, support), state, 10);

            Assert.AreEqual(1, before.Count, "Core가 없으면 Support는 후보가 아닙니다.");

            state.TryAcquire(core);

            List<MutationDefinition> after =
                MutationSelectionPool.Build(Loadout(core, support), state, 10);

            Assert.AreEqual(1, after.Count);
            Assert.AreSame(support, after[0]);
        }

        [Test]
        public void Nucleus가_모자란_유지형은_등장하지_않는다()
        {
            MutationDefinition heavy = MutationTestFactory.CreatePersistent("per_heavy", 45);
            MutationDefinition light = MutationTestFactory.CreatePersistent("per_light", 10);

            var state = new RunMutationState();
            state.TryAcquire(MutationTestFactory.CreatePersistent("per_a", 45));
            state.TryAcquire(MutationTestFactory.CreatePersistent("per_b", 45));

            List<MutationDefinition> candidates =
                MutationSelectionPool.Build(Loadout(heavy, light), state, 13);

            Assert.AreEqual(1, candidates.Count);
            Assert.AreSame(light, candidates[0]);
        }

        [Test]
        public void 상호_배타_변이는_풀에서_거르지_않는다()
        {
            // v5 §7-4: 획득은 자유롭되 동시 작동이 무의미하다.
            // 여기서 거르면 10-1-2(중복 금지 강제 금지) 위반이다.
            MutationDefinition core = MutationTestFactory.CreateCore("core_p", MutationTag.Projectile);

            MutationDefinition a = MutationTestFactory.CreateSupport(
                "sup_a", MutationTag.Projectile, mutuallyExclusiveIds: new[] { "sup_b" });
            MutationDefinition b = MutationTestFactory.CreateSupport(
                "sup_b", MutationTag.Projectile, mutuallyExclusiveIds: new[] { "sup_a" });

            var state = new RunMutationState();
            state.TryAcquire(core);
            state.TryAcquire(a);

            List<MutationDefinition> candidates =
                MutationSelectionPool.Build(Loadout(core, a, b), state, 10);

            Assert.IsTrue(candidates.Contains(b));
        }

        [Test]
        public void null과_중복_항목은_무시된다()
        {
            MutationDefinition core = MutationTestFactory.CreateCore("core_a");

            List<MutationDefinition> candidates =
                MutationSelectionPool.Build(Loadout(core, null, core), new RunMutationState(), 10);

            Assert.AreEqual(1, candidates.Count);
        }

        [Test]
        public void 적재가_null이면_빈_결과를_돌려준다()
        {
            List<MutationDefinition> candidates =
                MutationSelectionPool.Build(null, new RunMutationState(), 10);

            Assert.IsNotNull(candidates);
            Assert.AreEqual(0, candidates.Count);
        }

        [Test]
        public void 추첨은_요청한_개수만큼_중복_없이_뽑는다()
        {
            var loadout = new List<MutationDefinition>();

            for (int i = 0; i < 8; i++)
                loadout.Add(MutationTestFactory.CreateCore($"core_{i}"));

            List<MutationDefinition> drawn =
                MutationDraft.DrawFrom(loadout, 3, new Random(12345));

            Assert.AreEqual(3, drawn.Count);
            CollectionAssert.AllItemsAreUnique(drawn);
        }

        [Test]
        public void 같은_시드는_같은_결과를_낸다()
        {
            var loadout = new List<MutationDefinition>();

            for (int i = 0; i < 8; i++)
                loadout.Add(MutationTestFactory.CreateCore($"core_{i}"));

            List<MutationDefinition> first = MutationDraft.DrawFrom(loadout, 3, new Random(777));
            List<MutationDefinition> second = MutationDraft.DrawFrom(loadout, 3, new Random(777));

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void 후보가_요청_개수보다_적으면_가능한_만큼만_준다()
        {
            var loadout = new List<MutationDefinition>
            {
                MutationTestFactory.CreateCore("core_0"),
                MutationTestFactory.CreateCore("core_1")
            };

            List<MutationDefinition> drawn = MutationDraft.DrawFrom(loadout, 3, new Random(1));

            Assert.AreEqual(2, drawn.Count);
        }

        [Test]
        public void 후보가_없으면_빈_결과를_돌려준다()
        {
            List<MutationDefinition> drawn =
                MutationDraft.DrawFrom(new List<MutationDefinition>(), 3, new Random(1));

            Assert.IsNotNull(drawn);
            Assert.AreEqual(0, drawn.Count);
        }

        [Test]
        public void Draw는_필터와_추첨을_함께_수행한다()
        {
            MutationDefinition core = MutationTestFactory.CreateCore("core_p", MutationTag.Projectile);
            MutationDefinition lockedSupport =
                MutationTestFactory.CreateSupport("sup_zone", MutationTag.Zone);

            var state = new RunMutationState();
            state.TryAcquire(core);

            List<MutationDefinition> drawn = MutationDraft.Draw(
                Loadout(core, lockedSupport), state, 10, 3, new Random(42));

            Assert.AreEqual(0, drawn.Count,
                "이미 얻은 Core와 붙일 곳 없는 Support만 남으면 후보가 없어야 합니다.");
        }

        [Test]
        public void 추첨_개수가_0_이하면_빈_결과다()
        {
            var loadout = Loadout(MutationTestFactory.CreateCore("core_0"));

            Assert.AreEqual(0, MutationDraft.DrawFrom(loadout, 0, new Random(1)).Count);
            Assert.AreEqual(0, MutationDraft.DrawFrom(loadout, -5, new Random(1)).Count);
        }
    }
}
