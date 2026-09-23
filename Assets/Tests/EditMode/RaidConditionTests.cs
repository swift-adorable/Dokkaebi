using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Blob.Tests
{
    /// <summary>
    /// 파밍 조건 계약 테스트. (docs/Blob_Hunting_System.md 5·9절)
    ///
    /// 【여기서 강제하는 것】
    ///   1. 저항은 곱해지지 않는다 — 가장 낮은 배율 하나만
    ///   2. 밀집과 산개는 같이 걸리지 않는다
    ///   3. 저항 특성도 한 판에 하나만
    ///   4. 시설 상태는 양날이다 — 손해만 있는 상태를 만들지 않는다
    /// </summary>
    public class RaidConditionTests
    {
        private static RaidConditions With(FacilityState state, params RaidTrait[] traits)
        {
            return new RaidConditions { facility = state, traits = traits };
        }

        private static EnemyProfile Profile(EnemyArchetype archetype)
        {
            return EnemyProfile.Build(archetype, EnemyRarity.Normal, null);
        }

        // ── 1. 저항 ───────────────────────────────────────────────────

        [Test]
        public void 저항은_곱해지지_않는다()
        {
            // 문서 9절 — "「경화 외피」 레이드(물리 ×0.5) + 「경화」 적(물리 ×0.5) → ×0.5."
            // 곱연산이면 원천 둘로 「이 속성으로는 못 잡는다」가 되고,
            // 상태이상 피해에도 상성이 곱해지므로 방어도 무시라는 우회로까지 막힌다.
            EnemyProfile hardened = EnemyProfile.Build(
                EnemyArchetype.Scav, EnemyRarity.Normal,
                new List<EnemyAffix> { EnemyAffix.Hardened });

            Assert.AreEqual(0.5f, hardened.resistances.physical, 0.001f, "속성만으로 0.5여야 합니다.");

            EnemyProfile both = With(FacilityState.Normal, RaidTrait.Hardened).Apply(hardened);

            Assert.AreEqual(0.5f, both.resistances.physical, 0.001f,
                "레이드 특성이 곱해졌습니다. 0.25가 되면 그 빌드는 파밍을 버려야 합니다.");
        }

        [Test]
        public void 약점이_저항으로_뒤집히지_않는다()
        {
            // 압착기는 전기에 2배로 약하다. 「전기 저항 피막」이 걸려도 약점이 줄 뿐
            // 강점이 되어서는 안 된다 — 유형이 요구하던 답이 지워진다.
            EnemyProfile crusher = Profile(EnemyArchetype.Crusher);

            Assert.Greater(crusher.resistances.lightning, 1f, "압착기의 전기 약점이 없습니다.");

            EnemyProfile insulated = With(FacilityState.Normal, RaidTrait.Insulated).Apply(crusher);

            Assert.Less(insulated.resistances.lightning, crusher.resistances.lightning,
                "약점이 줄지 않았습니다.");

            Assert.GreaterOrEqual(insulated.resistances.lightning,
                RaidTraitTable.ResistanceMultiplier,
                "약점이 저항으로 뒤집혔습니다.");
        }

        [Test]
        public void 저항_특성은_속성마다_하나씩_있다()
        {
            // 「남은 속성을 쓰면 된다」가 성립하려면 다섯 속성이 모두 덮여야 한다.
            var covered = new HashSet<DamageElement>();

            foreach (RaidTrait trait in (RaidTrait[])System.Enum.GetValues(typeof(RaidTrait)))
            {
                RaidTraitEffect effect = RaidTraitTable.Of(trait);

                if (!effect.givesResistance)
                    continue;

                Assert.IsTrue(covered.Add(effect.element),
                    $"{RaidTraitTable.Name(trait)}이 이미 덮인 속성을 또 덮습니다.");
            }

            Assert.AreEqual(5, covered.Count, "저항 특성이 다섯 속성을 다 덮지 않습니다.");
        }

        // ── 2·3. 조합 금지 ────────────────────────────────────────────

        [Test]
        public void 밀집과_산개는_같이_걸리지_않는다()
        {
            // 문서 9절 ※ — 정반대라 같이 걸면 서로를 지운다.
            Assert.IsFalse(RaidTraitTable.CanCombine(RaidTrait.Dense, RaidTrait.Scattered));
            Assert.IsFalse(RaidTraitTable.CanCombine(RaidTrait.Scattered, RaidTrait.Dense));

            Assert.IsTrue(RaidTraitTable.CanCombine(RaidTrait.Dense, RaidTrait.Hardened),
                "배치와 저항은 같이 걸 수 있어야 합니다.");
        }

        [Test]
        public void 저항_특성_둘을_한_판에_걸지_않는다()
        {
            // 맵 전체에 둘을 걸면 남은 속성이 셋뿐이라
            // 「빌드를 바꾸면 된다」가 「이 빌드로는 못 온다」가 된다.
            Assert.IsFalse(RaidTraitTable.CanCombine(RaidTrait.Hardened, RaidTrait.FireProof));
            Assert.IsFalse(RaidTraitTable.CanCombine(RaidTrait.Insulated, RaidTrait.Antibody));
        }

        [Test]
        public void 뽑은_조건은_금지_조합을_만들지_않는다()
        {
            // 씨앗을 바꿔 가며 여러 번 뽑아도 규칙이 깨지지 않아야 한다.
            for (int seed = 0; seed < 300; seed++)
            {
                RaidConditions rolled = RaidConditionRoller.Roll(new System.Random(seed));

                Assert.LessOrEqual(rolled.traits.Length, RaidConditions.MaxTraits, $"seed {seed}");

                if (rolled.traits.Length < 2)
                    continue;

                Assert.IsTrue(RaidTraitTable.CanCombine(rolled.traits[0], rolled.traits[1]),
                    $"seed {seed} — {RaidTraitTable.Name(rolled.traits[0])} + "
                    + RaidTraitTable.Name(rolled.traits[1]));
            }
        }

        [Test]
        public void 같은_씨앗은_같은_판을_낸다()
        {
            // 「이 조건에서 이 빌드가 되는가」를 두 번 확인할 수 있어야 한다.
            RaidConditions a = RaidConditionRoller.Roll(new System.Random(12345));
            RaidConditions b = RaidConditionRoller.Roll(new System.Random(12345));

            Assert.AreEqual(a.facility, b.facility);
            CollectionAssert.AreEqual(a.traits, b.traits);
        }

        // ── 중복 완화 ─────────────────────────────────────────────────

        [Test]
        public void 같은_계열_속성은_덜_나온다()
        {
            // 문서 9절 — 없으면 "그 속성 빌드가 아예 못 싸우는" 구간이 생긴다.
            RaidConditions raid = With(FacilityState.Normal, RaidTrait.Hardened);

            Assert.Less(raid.AffixWeight(EnemyAffix.Hardened), 1f, "경화가 그대로 나옵니다.");
            Assert.AreEqual(1f, raid.AffixWeight(EnemyAffix.FireProof), 0.001f,
                "상관없는 속성까지 줄었습니다.");
        }

        [Test]
        public void 중복_완화도_곱하지_않는다()
        {
            // 가중치를 곱하면 특성이 둘일 때 그 속성이 사실상 사라진다.
            RaidConditions raid = With(FacilityState.Normal,
                RaidTrait.Hardened, RaidTrait.Regenerating);

            Assert.AreEqual(RaidTraitTable.OverlapWeightScale,
                raid.AffixWeight(EnemyAffix.Hardened), 0.001f);
        }

        // ── 재생 ──────────────────────────────────────────────────────

        [Test]
        public void 재생은_속성과_특성이_겹쳐도_더해지지_않는다()
        {
            // 초당 4%가 되면 화력 요구가 두 배로 뛴다.
            EnemyProfile regen = EnemyProfile.Build(
                EnemyArchetype.Scav, EnemyRarity.Normal,
                new List<EnemyAffix> { EnemyAffix.Regenerating });

            Assert.AreEqual(EnemyAffixTable.RegenPerSecond, regen.regenPerSecond, 0.0001f,
                "「재생」 속성이 아무 일도 하지 않습니다.");

            EnemyProfile both = With(FacilityState.Normal, RaidTrait.Regenerating).Apply(regen);

            Assert.AreEqual(EnemyAffixTable.RegenPerSecond, both.regenPerSecond, 0.0001f,
                "재생이 더해졌습니다.");
        }

        // ── 4. 시설 상태 ──────────────────────────────────────────────

        [Test]
        public void 모든_시설_상태가_추가_스폰을_갖는다()
        {
            foreach (FacilityState state in (FacilityState[])System.Enum.GetValues(typeof(FacilityState)))
            {
                if (state == FacilityState.Normal)
                    continue;

                Assert.AreNotEqual(string.Empty, FacilityStateTable.Describe(state),
                    FacilityStateTable.Name(state));
            }
        }

        [Test]
        public void 정전은_시야를_줄이는_대신_보안기를_멈춘다()
        {
            // 문서 5절 — "상태가 양날이어야" 위험하지만 기회가 된다.
            Assert.Less(FacilityStateTable.Of(FacilityState.Blackout).visionScale, 0.5f,
                "「대폭 감소」인데 절반도 줄지 않았습니다.");

            Assert.IsFalse(FacilityStateTable.IsActive(FacilityState.Blackout, EnemyArchetype.Sentry),
                "정전인데 보안기가 움직입니다. 손해만 남습니다.");

            Assert.IsTrue(FacilityStateTable.IsActive(FacilityState.Blackout, EnemyArchetype.Scav),
                "보안기가 아닌 것까지 멈췄습니다.");

            Assert.IsTrue(FacilityStateTable.IsActive(FacilityState.Normal, EnemyArchetype.Sentry));
        }

        [Test]
        public void 정전에서는_보기_전에_듣는다가_뒤집힌다()
        {
            // 정전의 정체가 이것이다 — 시야가 총성보다 짧아진다.
            float sight = FacilityStateTable.VisionRange(FacilityState.Blackout, EnemyArchetype.Scav);

            Assert.Less(sight, EnemyArchetypeTable.DefaultVisionRange,
                "정전인데 시야가 그대로입니다.");
        }

        [Test]
        public void 침수는_느려지는_대신_번개가_두_배다()
        {
            Assert.Less(FacilityStateTable.Of(FacilityState.Flooded).moveScale, 1f);

            RaidConditions flooded = With(FacilityState.Flooded);

            Assert.AreEqual(2f, flooded.DamageMultiplier(DamageElement.Lightning), 0.001f,
                "문서에 적힌 「감전 피해 2배」가 지켜지지 않습니다.");

            Assert.AreEqual(1f, flooded.DamageMultiplier(DamageElement.Fire), 0.001f,
                "상관없는 속성까지 커졌습니다.");
        }

        [Test]
        public void 소독은_깎는_대신_화공체를_강하게_만든다()
        {
            RaidConditions decon = With(FacilityState.Decontamination);

            Assert.Greater(decon.EnvironmentDamagePerSecond, 0f, "환경 피해가 없습니다.");

            EnemyProfile chemic = decon.Apply(Profile(EnemyArchetype.Chemic));
            EnemyProfile scav = decon.Apply(Profile(EnemyArchetype.Scav));

            Assert.Greater(chemic.health, Profile(EnemyArchetype.Chemic).health,
                "화공체가 강해지지 않았습니다. 추가 스폰이 숫자만 느는 것이 됩니다.");

            Assert.AreEqual(Profile(EnemyArchetype.Scav).health, scav.health,
                "상관없는 유형까지 강해졌습니다.");
        }

        [Test]
        public void 평시는_아무것도_바꾸지_않는다()
        {
            EnemyProfile before = Profile(EnemyArchetype.Specimen);
            EnemyProfile after = RaidConditions.None.Apply(before);

            Assert.AreEqual(before.health, after.health);
            Assert.AreEqual(before.damage, after.damage);
            Assert.AreEqual(before.resistances.physical, after.resistances.physical, 0.001f);
            Assert.AreEqual(1f, RaidConditions.None.GroupSizeScale, 0.001f);
            Assert.AreEqual(1f, RaidConditions.None.DurabilityLossScale, 0.001f);
        }

        // ── 표시 ──────────────────────────────────────────────────────

        [Test]
        public void 파밍_전에_걸린_것을_전부_적는다()
        {
            // 문서 5절 — "들어가서 알게 하지 않는다."
            RaidConditions raid = With(FacilityState.Flooded,
                RaidTrait.Dense, RaidTrait.Hardened);

            List<string> lines = raid.Describe();

            Assert.AreEqual(3, lines.Count, "걸린 것 중 적히지 않은 것이 있습니다.");

            foreach (string line in lines)
                Assert.IsNotEmpty(line);
        }

        [Test]
        public void 모든_특성이_이름과_설명을_갖는다()
        {
            foreach (RaidTrait trait in (RaidTrait[])System.Enum.GetValues(typeof(RaidTrait)))
            {
                if (trait == RaidTrait.None)
                    continue;

                Assert.IsNotEmpty(RaidTraitTable.Name(trait), trait.ToString());
                Assert.IsNotEmpty(RaidTraitTable.Describe(trait), trait.ToString());
            }
        }

        [Test]
        public void 특성은_열_종이다()
        {
            int count = 0;

            foreach (RaidTrait trait in (RaidTrait[])System.Enum.GetValues(typeof(RaidTrait)))
            {
                if (trait != RaidTrait.None)
                    count++;
            }

            Assert.AreEqual(RaidTraitTable.Count, count, "문서는 10종이라고 적었습니다.");
        }
    }
}
