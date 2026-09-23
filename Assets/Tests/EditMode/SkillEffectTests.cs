using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// 「대가만 있고 효과가 없는 젬은 존재할 수 없다」를 강제한다. (D1)
    ///
    /// 이 테스트가 없던 동안 보조 젬 35종 중 21종이 대가만 적용되고
    /// 효과는 설명 문자열에만 있었다. 끼우면 손해만 보는 젬이었다.
    /// 문서에만 적힌 규칙은 언젠가 무너진다 — 그래서 여기에 고정한다.
    /// </summary>
    public class SkillEffectTests
    {
        /// <summary>
        /// 아직 데이터로 표현할 축이 없는 스킬. 【늘어나면 안 된다.】
        ///
        /// 6-J에서 속성 전환 축을 만들어 비었다.
        /// 다시 채워야 한다면 그것은 새 축이 필요하다는 뜻이다.
        /// </summary>
        private static readonly string[] KnownGaps = new string[0];

        private static SkillCatalog catalog;

        [OneTimeSetUp]
        public void LoadCatalog()
        {
            catalog = SkillCatalog.Load();

            Assert.IsNotNull(catalog,
                "SkillCatalog.asset을 찾지 못했습니다. 「Blob/Skill/카탈로그 다시 만들기」를 실행하십시오.");
        }

        private static List<SkillDefinition> All()
            => catalog.Definitions.Where(d => d != null).ToList();

        [Test]
        public void 모든_Support는_효과를_가진다()
        {
            var missing = All()
                .Where(s => s.Category == SkillCategory.Support)
                .Where(s => !s.HasEffect)
                .Select(s => s.Id)
                .Where(id => !KnownGaps.Contains(id))
                .ToList();

            Assert.IsEmpty(missing,
                "효과가 데이터에 없는 보조 젬입니다. 끼우면 대가만 적용됩니다: "
                + string.Join(", ", missing));
        }

        [Test]
        public void 알려진_공백_목록이_비어_있다()
        {
            var gaps = All()
                .Where(s => s.Category == SkillCategory.Support && !s.HasEffect)
                .Select(s => s.Id)
                .ToList();

            CollectionAssert.AreEquivalent(KnownGaps, gaps,
                "표현할 수 없는 스킬 목록이 바뀌었습니다. 줄었으면 KnownGaps에서 지우고, "
                + "늘었으면 축을 새로 만들어야 합니다.");
        }

        /// <summary>
        /// 【투사체 수명과 상태이상 지속시간은 다른 축이다.】
        ///
        /// 하나로 쓰던 동안 「유지되는 대지」(잔류물 +100%)가
        /// 투사체 사거리를 2배로 만들었다. (docs/Blob_Audit.md D2)
        /// </summary>
        [Test]
        public void 지속시간_Support가_투사체_수명을_건드리지_않는다()
        {
            foreach (SkillDefinition skill in All())
            {
                if (skill.AilmentDurationMultiplier == 1f)
                    continue;

                Assert.AreEqual(1f, skill.LifetimeMultiplier, 0.001f,
                    $"「{skill.DisplayName}」이 상태이상 지속시간과 투사체 수명을 동시에 건드립니다. "
                    + "두 축은 분리되어 있어야 합니다.");
            }
        }

        [Test]
        public void 조건부_Support는_조건과_값을_둘_다_가진다()
        {
            foreach (SkillDefinition skill in All())
            {
                if (skill.ConditionKind == SkillConditionKind.None)
                    continue;

                Assert.AreNotEqual(0f, skill.ConditionalDamageIncrease,
                    $"「{skill.DisplayName}」에 조건은 있는데 값이 0입니다.");

                if (skill.ConditionKind == SkillConditionKind.TargetHasStatus)
                {
                    Assert.AreNotEqual(StatusEffectType.None, skill.ConditionStatus,
                        $"「{skill.DisplayName}」에 볼 상태가 지정되지 않았습니다.");
                }
            }
        }

        /// <summary>
        /// 배타형은 자기가 막는 상태를 조건으로 본다 —
        /// 「점화를 유발할 수 없지만 점화된 적에게 큰 피해」가 그 정체성이다.
        /// </summary>
        [Test]
        public void 배타형_Support는_막는_상태를_조건으로_본다()
        {
            foreach (SkillDefinition skill in All())
            {
                if (!skill.BlocksStatusCreation)
                    continue;

                Assert.AreEqual(SkillConditionKind.TargetHasStatus, skill.ConditionKind,
                    $"「{skill.DisplayName}」은 상태를 막기만 하고 얻는 것이 없습니다.");

                Assert.AreEqual(skill.BlockedStatus, skill.ConditionStatus,
                    $"「{skill.DisplayName}」이 막는 상태와 보는 상태가 다릅니다.");
            }
        }

        // ── 합산 ──────────────────────────────────────────────────────────

        [Test]
        public void 피해_증가율은_가산으로_합쳐진다()
        {
            var modifiers = new WeaponModifiers();

            SkillDefinition a = All().First(s => s.DamageIncrease > 0f);
            SkillDefinition b = All().First(s => s.DamageIncrease > 0f && s.Id != a.Id);

            modifiers.Apply(a);
            modifiers.Apply(b);

            Assert.AreEqual(a.DamageIncrease + b.DamageIncrease, modifiers.DamageIncrease, 0.001f,
                "증가율은 곱이 아니라 합이어야 합니다.");
        }

        [Test]
        public void 상태이상_지속시간_배율은_곱으로_합쳐진다()
        {
            var modifiers = new WeaponModifiers();

            SkillDefinition longer = All().First(s => s.AilmentDurationMultiplier > 1f);
            SkillDefinition shorter = All().First(s => s.AilmentDurationMultiplier < 1f);

            modifiers.Apply(longer);
            modifiers.Apply(shorter);

            Assert.AreEqual(
                longer.AilmentDurationMultiplier * shorter.AilmentDurationMultiplier,
                modifiers.AilmentDurationMultiplier, 0.001f);
        }

        [Test]
        public void Reset하면_효과가_전부_기본값으로_돌아간다()
        {
            var modifiers = new WeaponModifiers();

            modifiers.Apply(All().First(s => s.DamageIncrease != 0f));
            modifiers.Apply(All().First(s => s.ConditionKind != SkillConditionKind.None));

            modifiers.Reset();

            Assert.AreEqual(0f, modifiers.DamageIncrease, 0.001f);
            Assert.AreEqual(0f, modifiers.AilmentPower, 0.001f);
            Assert.AreEqual(1f, modifiers.AilmentDurationMultiplier, 0.001f);
            Assert.AreEqual(1f, modifiers.RangeMultiplier, 0.001f);
            Assert.AreEqual(0, modifiers.Conditions.Count);
        }

        // ── 조건 판정 ─────────────────────────────────────────────────────

        [Test]
        public void 원거리_조건은_사거리_절반을_넘을_때만_붙는다()
        {
            var modifiers = new WeaponModifiers();
            modifiers.Apply(All().First(s => s.Id == "sup_far_shot"));

            // 유효 사거리 10m → 경계는 5m. 사거리 보정이 걸리는 선과 같다.
            float near = modifiers.ConditionalDamageIncrease(3f, 10f, null);
            float far = modifiers.ConditionalDamageIncrease(8f, 10f, null);

            Assert.AreEqual(0f, near, 0.001f, "가까우면 붙지 않아야 합니다.");
            Assert.Greater(far, 0f, "멀면 붙어야 합니다.");
        }

        [Test]
        public void 근접_조건은_원거리_조건과_정확히_반대다()
        {
            var far = new WeaponModifiers();
            far.Apply(All().First(s => s.Id == "sup_far_shot"));

            var near = new WeaponModifiers();
            near.Apply(All().First(s => s.Id == "sup_melee_combat"));

            Assert.Greater(near.ConditionalDamageIncrease(3f, 10f, null), 0f);
            Assert.AreEqual(0f, near.ConditionalDamageIncrease(8f, 10f, null), 0.001f);

            Assert.AreEqual(0f, far.ConditionalDamageIncrease(3f, 10f, null), 0.001f);
            Assert.Greater(far.ConditionalDamageIncrease(8f, 10f, null), 0f);
        }

        [Test]
        public void 상태_조건은_그_상태가_걸린_적에게만_붙는다()
        {
            var modifiers = new WeaponModifiers();
            modifiers.Apply(All().First(s => s.Id == "sup_bloodlust"));

            var clean = new StatusEffectState();

            var bleeding = new StatusEffectState();
            bleeding.Apply(StatusEffectType.Bleed, 10f);

            Assert.AreEqual(0f, modifiers.ConditionalDamageIncrease(3f, 10f, clean), 0.001f);
            Assert.Greater(modifiers.ConditionalDamageIncrease(3f, 10f, bleeding), 0f);
        }

        [Test]
        public void 대상이_없으면_상태_조건은_붙지_않는다()
        {
            var modifiers = new WeaponModifiers();
            modifiers.Apply(All().First(s => s.Id == "sup_bloodlust"));

            Assert.AreEqual(0f, modifiers.ConditionalDamageIncrease(3f, 10f, null), 0.001f);
        }

        // ── 지속시간 배율이 실제로 걸리는가 ───────────────────────────────

        [Test]
        public void 지속시간_배율이_상태이상에_실제로_적용된다()
        {
            var normal = new StatusEffectState();
            normal.Apply(StatusEffectType.Bleed, 10f);

            var doubled = new StatusEffectState();
            doubled.Apply(StatusEffectType.Bleed, 10f, 2f);

            Assert.Greater(doubled.RemainingOf(StatusEffectType.Bleed),
                normal.RemainingOf(StatusEffectType.Bleed),
                "지속시간 배율이 적용되지 않았습니다.");
        }

        // ── 배타형이 실제로 성립하는가 (D3) ───────────────────────────────

        private static SkillDefinition Find(string id)
        {
            SkillDefinition d = All().FirstOrDefault(s => s.Id == id);
            Assert.IsNotNull(d, $"{id} 에셋이 없습니다.");
            return d;
        }

        /// <summary>
        /// 【자기 핵심 젬은 못 걸고, 다른 발생원이 걸어 준 것을 이용한다.】
        ///
        /// 차단이 빌드 전역이던 동안 배타형 4종은 구조적으로 영구 무효였다 —
        /// 「번제」는 화염 태그를 요구해 화염 핵심 젬에만 끼울 수 있는데,
        /// 전역 차단이면 그 화염 핵심 젬의 점화까지 꺼져 조건이 영원히 성립하지 않았다.
        /// </summary>
        [Test]
        public void 번제를_낀_Core는_점화를_걸_수_없다()
        {
            var build = new SocketedBuild();
            build.SetLevel(15);

            Assert.IsTrue(build.TryEquipCore(Find("core_fire"), 0), "화염 핵심 젬 장착");
            Assert.IsTrue(build.TryEquipSupport(Find("sup_burnt_offering"), 0, 0), "번제 장착");

            Assert.AreEqual(StatusEffectType.None, build.EffectiveAilmentOf(0),
                "번제를 낀 핵심 젬은 점화를 걸 수 없어야 합니다.");
        }

        /// <summary>
        /// 다른 핵심 젬이 속성 전환으로 점화를 공급하면 배타형이 성립한다.
        /// 이것이 「단독으로는 작동하지 않는다」의 정확한 뜻이다.
        /// </summary>
        [Test]
        public void 다른_Core가_점화를_공급하면_번제가_성립한다()
        {
            var build = new SocketedBuild();
            build.SetLevel(15);

            build.TryEquipCore(Find("core_fire"), 0);
            build.TryEquipSupport(Find("sup_burnt_offering"), 0, 0);

            // 2번째 핵심 젬에 화염 조율을 끼워 점화를 공급한다.
            Assert.IsTrue(build.TryEquipCore(Find("core_thunder"), 1), "2번째 핵심 젬 장착");
            Assert.IsTrue(build.TryEquipSupport(Find("sup_fire_attunement"), 1, 0), "화염 조율 장착");

            Assert.AreEqual(StatusEffectType.Ignite, build.EffectiveAilmentOf(1),
                "화염 조율이 2번째 핵심 젬의 속성을 점화로 바꾸지 못했습니다.");

            Assert.IsFalse(build.IsStatusUnavailable(StatusEffectType.Ignite),
                "빌드 어딘가에서 점화가 나와야 번제가 일을 합니다.");

            Assert.Contains(StatusEffectType.Ignite,
                build.GetModifiers().Ailments.ToList(),
                "탄에 점화가 실리지 않았습니다.");
        }

        /// <summary>차단은 자기 핵심 젬에만 적용된다. 다른 핵심 젬은 영향을 받지 않는다.</summary>
        [Test]
        public void 차단은_다른_Core에_번지지_않는다()
        {
            var build = new SocketedBuild();
            build.SetLevel(15);

            build.TryEquipCore(Find("core_fire"), 0);
            build.TryEquipSupport(Find("sup_burnt_offering"), 0, 0);
            build.TryEquipCore(Find("core_thunder"), 1);

            Assert.IsTrue(build.IsStatusBlockedForCore(0, StatusEffectType.Ignite),
                "번제를 낀 핵심 젬은 점화가 막혀야 합니다.");

            Assert.IsFalse(build.IsStatusBlockedForCore(1, StatusEffectType.Ignite),
                "다른 핵심 젬까지 막으면 배타형이 영원히 무효가 됩니다.");
        }

        /// <summary>「원소 융합」은 원래 속성을 유지한 채 2차 속성을 더한다.</summary>
        [Test]
        public void 원소_융합은_속성을_추가한다()
        {
            var build = new SocketedBuild();
            build.SetLevel(15);

            build.TryEquipCore(Find("core_fire"), 0);
            build.TryEquipSupport(Find("sup_elemental_fusion"), 0, 0);

            Assert.AreEqual(StatusEffectType.Ignite, build.EffectiveAilmentOf(0),
                "원래 속성이 유지되어야 합니다.");

            Assert.AreNotEqual(StatusEffectType.None, build.AddedAilmentOf(0),
                "2차 속성이 추가되지 않았습니다.");
        }
    }
}
