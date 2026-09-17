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
    public class SkillSelectionPoolTests
    {
        private static List<SkillDefinition> Loadout(params SkillDefinition[] entries)
        {
            return new List<SkillDefinition>(entries);
        }

        [Test]
        public void 적재한_것만_후보가_된다()
        {
            SkillDefinition inLoadout = SkillTestFactory.CreateCore("core_in");
            SkillDefinition outOfLoadout = SkillTestFactory.CreateCore("core_out");

            List<SkillDefinition> candidates =
                SkillSelectionPool.Build(Loadout(inLoadout), new RunSkillState(), 10);

            Assert.AreEqual(1, candidates.Count);
            Assert.AreSame(inLoadout, candidates[0]);
            Assert.IsFalse(candidates.Contains(outOfLoadout));
        }

        [Test]
        public void 요구_레벨에_도달하지_않으면_등장하지_않는다()
        {
            SkillDefinition low = SkillTestFactory.CreateCore("core_low", requiredLevel: 1);
            SkillDefinition high = SkillTestFactory.CreateCore("core_high", requiredLevel: 7);

            List<SkillDefinition> atLevel3 =
                SkillSelectionPool.Build(Loadout(low, high), new RunSkillState(), 3);

            Assert.AreEqual(1, atLevel3.Count);
            Assert.AreSame(low, atLevel3[0]);

            List<SkillDefinition> atLevel7 =
                SkillSelectionPool.Build(Loadout(low, high), new RunSkillState(), 7);

            Assert.AreEqual(2, atLevel7.Count, "요구 레벨과 같은 레벨이면 등장해야 합니다.");
        }

        [Test]
        public void 이미_획득한_스킬는_풀에서_제거된다()
        {
            // v5 §10-9: 중복 등장 없음
            SkillDefinition a = SkillTestFactory.CreateCore("core_a");
            SkillDefinition b = SkillTestFactory.CreateCore("core_b");

            var state = new RunSkillState();
            state.TryAcquire(a);

            List<SkillDefinition> candidates =
                SkillSelectionPool.Build(Loadout(a, b), state, 10);

            Assert.AreEqual(1, candidates.Count);
            Assert.AreSame(b, candidates[0]);
        }

        [Test]
        public void 장착할_Core가_없는_Support는_선택창에_등장하지_않는다()
        {
            SkillDefinition zoneCore =
                SkillTestFactory.CreateCore("core_zone", SkillTag.Zone);

            SkillDefinition projectileSupport =
                SkillTestFactory.CreateSupport("sup_projectile", SkillTag.Projectile);

            var state = new RunSkillState();
            state.TryAcquire(zoneCore);

            List<SkillDefinition> candidates =
                SkillSelectionPool.Build(Loadout(zoneCore, projectileSupport), state, 10);

            Assert.AreEqual(0, candidates.Count,
                "죽은 선택지가 등장하면 태그 게이팅이 깨진 것입니다.");
        }

        [Test]
        public void Core를_얻으면_그_Core용_Support가_풀에_들어온다()
        {
            SkillDefinition core = SkillTestFactory.CreateCore("core_p", SkillTag.Projectile);
            SkillDefinition support =
                SkillTestFactory.CreateSupport("sup_p", SkillTag.Projectile);

            var state = new RunSkillState();

            List<SkillDefinition> before =
                SkillSelectionPool.Build(Loadout(core, support), state, 10);

            Assert.AreEqual(1, before.Count, "Core가 없으면 Support는 후보가 아닙니다.");

            state.TryAcquire(core);

            List<SkillDefinition> after =
                SkillSelectionPool.Build(Loadout(core, support), state, 10);

            Assert.AreEqual(1, after.Count);
            Assert.AreSame(support, after[0]);
        }

        [Test]
        public void 전령을_이미_장착했으면_다른_전령은_등장하지_않는다()
        {
            // v8 §8-1: 전령은 동시에 1개. 자리가 없으면 후보로 올리지 않는다.
            SkillDefinition ice = SkillTestFactory.CreatePersistent("herald_ice");
            SkillDefinition ash = SkillTestFactory.CreatePersistent("herald_ash");

            var state = new RunSkillState();
            state.TryAcquire(SkillTestFactory.CreatePersistent("herald_thunder"));

            List<SkillDefinition> candidates =
                SkillSelectionPool.Build(Loadout(ice, ash), state, 13);

            Assert.AreEqual(0, candidates.Count);
        }

        [Test]
        public void 발동_스킬_2개를_채우면_더_등장하지_않는다()
        {
            SkillDefinition third = SkillTestFactory.CreateMeta("meta_3");

            var state = new RunSkillState();
            state.TryAcquire(SkillTestFactory.CreateMeta("meta_1"));
            state.TryAcquire(SkillTestFactory.CreateMeta("meta_2"));

            List<SkillDefinition> candidates =
                SkillSelectionPool.Build(Loadout(third), state, 13);

            Assert.AreEqual(0, candidates.Count);
        }

        [Test]
        public void 상호_배타_스킬는_풀에서_거르지_않는다()
        {
            // v5 §7-4: 획득은 자유롭되 동시 작동이 무의미하다.
            // 여기서 거르면 10-1-2(중복 금지 강제 금지) 위반이다.
            SkillDefinition core = SkillTestFactory.CreateCore("core_p", SkillTag.Projectile);

            SkillDefinition a = SkillTestFactory.CreateSupport(
                "sup_a", SkillTag.Projectile, mutuallyExclusiveIds: new[] { "sup_b" });
            SkillDefinition b = SkillTestFactory.CreateSupport(
                "sup_b", SkillTag.Projectile, mutuallyExclusiveIds: new[] { "sup_a" });

            var state = new RunSkillState();
            state.TryAcquire(core);
            state.TryAcquire(a);

            List<SkillDefinition> candidates =
                SkillSelectionPool.Build(Loadout(core, a, b), state, 10);

            Assert.IsTrue(candidates.Contains(b));
        }

        [Test]
        public void null과_중복_항목은_무시된다()
        {
            SkillDefinition core = SkillTestFactory.CreateCore("core_a");

            List<SkillDefinition> candidates =
                SkillSelectionPool.Build(Loadout(core, null, core), new RunSkillState(), 10);

            Assert.AreEqual(1, candidates.Count);
        }

        [Test]
        public void 적재가_null이면_빈_결과를_돌려준다()
        {
            List<SkillDefinition> candidates =
                SkillSelectionPool.Build(null, new RunSkillState(), 10);

            Assert.IsNotNull(candidates);
            Assert.AreEqual(0, candidates.Count);
        }

        [Test]
        public void 추첨은_요청한_개수만큼_중복_없이_뽑는다()
        {
            var loadout = new List<SkillDefinition>();

            for (int i = 0; i < 8; i++)
                loadout.Add(SkillTestFactory.CreateCore($"core_{i}"));

            List<SkillDefinition> drawn =
                SkillDraft.DrawFrom(loadout, 3, new Random(12345));

            Assert.AreEqual(3, drawn.Count);
            CollectionAssert.AllItemsAreUnique(drawn);
        }

        [Test]
        public void 같은_시드는_같은_결과를_낸다()
        {
            var loadout = new List<SkillDefinition>();

            for (int i = 0; i < 8; i++)
                loadout.Add(SkillTestFactory.CreateCore($"core_{i}"));

            List<SkillDefinition> first = SkillDraft.DrawFrom(loadout, 3, new Random(777));
            List<SkillDefinition> second = SkillDraft.DrawFrom(loadout, 3, new Random(777));

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void 후보가_요청_개수보다_적으면_가능한_만큼만_준다()
        {
            var loadout = new List<SkillDefinition>
            {
                SkillTestFactory.CreateCore("core_0"),
                SkillTestFactory.CreateCore("core_1")
            };

            List<SkillDefinition> drawn = SkillDraft.DrawFrom(loadout, 3, new Random(1));

            Assert.AreEqual(2, drawn.Count);
        }

        [Test]
        public void 후보가_없으면_빈_결과를_돌려준다()
        {
            List<SkillDefinition> drawn =
                SkillDraft.DrawFrom(new List<SkillDefinition>(), 3, new Random(1));

            Assert.IsNotNull(drawn);
            Assert.AreEqual(0, drawn.Count);
        }

        [Test]
        public void Draw는_필터와_추첨을_함께_수행한다()
        {
            SkillDefinition core = SkillTestFactory.CreateCore("core_p", SkillTag.Projectile);
            SkillDefinition lockedSupport =
                SkillTestFactory.CreateSupport("sup_zone", SkillTag.Zone);

            var state = new RunSkillState();
            state.TryAcquire(core);

            List<SkillDefinition> drawn = SkillDraft.Draw(
                Loadout(core, lockedSupport), state, 10, 3, new Random(42));

            Assert.AreEqual(0, drawn.Count,
                "이미 얻은 Core와 붙일 곳 없는 Support만 남으면 후보가 없어야 합니다.");
        }

        [Test]
        public void 추첨_개수가_0_이하면_빈_결과다()
        {
            var loadout = Loadout(SkillTestFactory.CreateCore("core_0"));

            Assert.AreEqual(0, SkillDraft.DrawFrom(loadout, 0, new Random(1)).Count);
            Assert.AreEqual(0, SkillDraft.DrawFrom(loadout, -5, new Random(1)).Count);
        }
    }
}
