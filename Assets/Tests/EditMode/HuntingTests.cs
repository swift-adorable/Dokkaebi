using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 7단계 사냥 데이터 골격의 계약 테스트.
    ///
    /// 【여기서 강제하는 것】
    ///   1. 유형 수치가 전투 기준(Combat_Baseline 5절) 표와 한 글자도 다르지 않다
    ///   2. 조합 금지 규칙이 실제로 막힌다
    ///   3. 저항이 곱해지지 않는다
    ///   4. 설계 규칙 — 한 유형이 두 개의 답을 요구하지 않는다
    ///
    /// 문서와 코드의 드리프트를 테스트로 막는다. (마스터 프롬프트 7-8)
    /// </summary>
    public class HuntingTests
    {
        // ── 1. 유형 수치가 문서 표와 같다 ─────────────────────────────

        /// <summary>docs/Dokkaebi_Combat_Baseline.md 5절 표를 그대로 옮긴 것.</summary>
        private static readonly (EnemyArchetype archetype,
                                 int health, int damage, int penetration, float armour)[] Baseline =
        {
            (EnemyArchetype.Scav,      20,  8, 0, 0f),
            (EnemyArchetype.Crusher,   90, 14, 1, 4f),
            (EnemyArchetype.Dynamo,    30, 10, 1, 0f),
            (EnemyArchetype.Lurker,    25, 16, 2, 0f),
            (EnemyArchetype.Chemic,    45,  6, 0, 0f),
            (EnemyArchetype.Specimen,  60, 14, 3, 1f),
            (EnemyArchetype.Settled,  120, 20, 0, 0f),
            (EnemyArchetype.Sentry,    70, 12, 2, 3f),
            (EnemyArchetype.Wraith,    80, 16, 5, 0f)
        };

        [Test]
        public void 유형_수치가_전투_기준과_같다()
        {
            foreach (var row in Baseline)
            {
                EnemyArchetypeStats stats = EnemyArchetypeTable.Of(row.archetype);
                string who = EnemyArchetypeTable.Name(row.archetype);

                Assert.AreEqual(row.health, stats.health, $"{who} 체력");
                Assert.AreEqual(row.damage, stats.damage, $"{who} 피해");
                Assert.AreEqual(row.penetration, stats.armourPenetration, $"{who} 방어 관통");
                Assert.AreEqual(row.armour, stats.armour, 0.001f, $"{who} 방어도");
            }
        }

        [Test]
        public void 유형은_아홉_종이고_표에_빠진_것이_없다()
        {
            var all = (EnemyArchetype[])Enum.GetValues(typeof(EnemyArchetype));

            Assert.AreEqual(EnemyArchetypeTable.Count, all.Length,
                "enum과 EnemyArchetypeTable.Count가 어긋났습니다.");

            Assert.AreEqual(all.Length, Baseline.Length,
                "유형을 추가했는데 이 테스트의 기준 표를 갱신하지 않았습니다.");

            foreach (EnemyArchetype archetype in all)
            {
                Assert.IsNotEmpty(EnemyArchetypeTable.Name(archetype),
                    $"{archetype}의 표시 이름이 없습니다.");
            }
        }

        [Test]
        public void 유형_저항이_문서와_같다()
        {
            // 절굿공이귀 · 순라귀 — 기계형. 전기 2배 / 카오스 면역.
            foreach (EnemyArchetype mech in new[] { EnemyArchetype.Crusher, EnemyArchetype.Sentry })
            {
                ElementalResistances r = EnemyArchetypeTable.Of(mech).resistances;

                Assert.AreEqual(2f, r.lightning, 0.001f, $"{EnemyArchetypeTable.Name(mech)} 전기");
                Assert.AreEqual(0f, r.chaos, 0.001f, $"{EnemyArchetypeTable.Name(mech)} 카오스");
            }

            // 허깨비 — 물리 0.66 / 화염 1.5.
            ElementalResistances settled = EnemyArchetypeTable.Of(EnemyArchetype.Settled).resistances;

            Assert.AreEqual(0.66f, settled.physical, 0.001f, "허깨비 물리");
            Assert.AreEqual(1.5f, settled.fire, 0.001f, "허깨비 화염");

            // 무주귀 — 물리 0.66만. 【화염 1.5를 주지 않는다.】
            // 문서 1절은 무주귀에 화염 배율을 적지 않았다.
            ElementalResistances wraith = EnemyArchetypeTable.Of(EnemyArchetype.Wraith).resistances;

            Assert.AreEqual(0.66f, wraith.physical, 0.001f, "무주귀 물리");
            Assert.AreEqual(1f, wraith.fire, 0.001f,
                "무주귀에 화염 1.5가 붙었습니다. 문서 1절에 없는 값입니다.");
        }

        [Test]
        public void 발소리가_없는_유형은_잠복체와_데이터체뿐이다()
        {
            var silent = ((EnemyArchetype[])Enum.GetValues(typeof(EnemyArchetype)))
                .Where(a => !EnemyArchetypeTable.Of(a).makesFootsteps)
                .ToArray();

            CollectionAssert.AreEquivalent(
                new[] { EnemyArchetype.Lurker, EnemyArchetype.Wraith }, silent,
                "발소리 없는 유형이 문서 8절과 다릅니다. " +
                "「먼저 감지당하는가」가 이 게임의 핵심 긴장입니다.");
        }

        // ── 2. 설계 규칙 ──────────────────────────────────────────────

        /// <summary>
        /// docs/Dokkaebi_Combat_Baseline.md 5절 표의 「요구하는 답」 열.
        ///
        /// 【이전 판을 폐기했다 — 정정】
        /// 처음에는 「체력 60 이상 + 방어도 4 이상 금지」라는 기준을 내가 만들어
        /// 넣었고, 절굿공이귀(90 / 4)에서 실패했다. 그런데 절굿공이귀는 문서가
        /// 「느리고 단단」하라고 직접 지정한 유형이다. 원문에 없는 한계치를
        /// 지어내 문서를 틀렸다고 판정한 셈이다. 문서의 열을 그대로 쓴다.
        /// </summary>
        private static readonly (EnemyArchetype archetype, EnemyAnswer answer)[] Answers =
        {
            (EnemyArchetype.Scav,     EnemyAnswer.Timing),
            (EnemyArchetype.Crusher,  EnemyAnswer.Penetration),
            (EnemyArchetype.Dynamo,   EnemyAnswer.Angle),
            (EnemyArchetype.Lurker,   EnemyAnswer.Initiative),
            (EnemyArchetype.Chemic,   EnemyAnswer.Distance),
            (EnemyArchetype.Specimen, EnemyAnswer.Cover),
            (EnemyArchetype.Settled,  EnemyAnswer.Element),
            (EnemyArchetype.Sentry,   EnemyAnswer.Dodge),
            (EnemyArchetype.Wraith,   EnemyAnswer.Element)
        };

        [Test]
        public void 유형이_요구하는_답이_문서와_같다()
        {
            foreach (var row in Answers)
            {
                Assert.AreEqual(row.answer, EnemyArchetypeTable.Of(row.archetype).answer,
                    $"{EnemyArchetypeTable.Name(row.archetype)}가 요구하는 답");
            }
        }

        [Test]
        public void 요구하는_답이_한쪽으로_몰리지_않는다()
        {
            // 【하나의 유형이 두 개의 답을 요구하게 만들지 않는다】를 뒤집어 읽으면,
            // 아홉 유형이 서로 다른 답을 고르게 나눠 가져야 한다는 뜻이다.
            // 절반이 같은 답이면 나머지 답을 주는 장비·젬이 죽은 선택지가 된다.
            var counts = Answers
                .GroupBy(row => row.answer)
                .ToDictionary(g => g.Key, g => g.Count());

            Assert.GreaterOrEqual(counts.Count, 6,
                "아홉 유형이 쓰는 답이 6종 미만입니다. 대응 수단이 놀게 됩니다.");

            foreach (var pair in counts)
            {
                Assert.LessOrEqual(pair.Value, 2,
                    $"답 「{pair.Key}」를 요구하는 유형이 {pair.Value}종입니다. " +
                    "한 가지 대응만 갖추면 되는 구간이 생깁니다.");
            }
        }

        // ── 3. 등급 ───────────────────────────────────────────────────

        [Test]
        public void 등급_배율이_문서와_같다()
        {
            Assert.AreEqual(1f, EnemyRarityTable.HealthMultiplier(EnemyRarity.Normal), 0.001f);
            Assert.AreEqual(2f, EnemyRarityTable.HealthMultiplier(EnemyRarity.Magic), 0.001f);
            Assert.AreEqual(4.5f, EnemyRarityTable.HealthMultiplier(EnemyRarity.Rare), 0.001f);
            Assert.AreEqual(15f, EnemyRarityTable.HealthMultiplier(EnemyRarity.Unique), 0.001f);

            Assert.AreEqual(1f, EnemyRarityTable.DamageMultiplier(EnemyRarity.Normal), 0.001f);
            Assert.AreEqual(1.2f, EnemyRarityTable.DamageMultiplier(EnemyRarity.Magic), 0.001f);
            Assert.AreEqual(1.4f, EnemyRarityTable.DamageMultiplier(EnemyRarity.Rare), 0.001f);
            Assert.AreEqual(1.8f, EnemyRarityTable.DamageMultiplier(EnemyRarity.Unique), 0.001f);
        }

        [Test]
        public void 체력_배율이_피해_배율보다_훨씬_가파르다()
        {
            // 【의도된 비대칭이다.】 보스는 아프게가 아니라 단단하게 위협한다.
            // 이 관계가 뒤집히면 「파밍 안에서 젬 / 파밍 사이에 장비」 분업이 무너진다.
            float health = EnemyRarityTable.HealthMultiplier(EnemyRarity.Unique);
            float damage = EnemyRarityTable.DamageMultiplier(EnemyRarity.Unique);

            Assert.Greater(health, damage * 5f,
                $"고유 체력 배율 {health}이 피해 배율 {damage}의 5배에 못 미칩니다.");
        }

        // ── 4. 조합 금지 ──────────────────────────────────────────────

        [Test]
        public void 갑각과_경화를_동시에_붙이지_않는다()
        {
            var chosen = new List<EnemyAffix> { EnemyAffix.Carapace };

            Assert.IsFalse(EnemyAffixTable.CanAdd(chosen, EnemyAffix.Hardened),
                "갑각 위에 경화가 붙었습니다. 방어 관통과 속성 전환 두 답을 모두 막습니다.");

            chosen = new List<EnemyAffix> { EnemyAffix.Hardened };

            Assert.IsFalse(EnemyAffixTable.CanAdd(chosen, EnemyAffix.Carapace),
                "경화 위에 갑각이 붙었습니다. 순서만 바꿔도 막혀야 합니다.");
        }

        [Test]
        public void 방어형은_두_개까지만_붙는다()
        {
            var chosen = new List<EnemyAffix> { EnemyAffix.FireProof, EnemyAffix.Insulated };

            Assert.IsFalse(EnemyAffixTable.CanAdd(chosen, EnemyAffix.Antibody),
                "방어형이 3개가 됐습니다.");

            // 방어형이 찼어도 다른 분류는 계속 붙는다.
            Assert.IsTrue(EnemyAffixTable.CanAdd(chosen, EnemyAffix.Swift));
        }

        [Test]
        public void 같은_속성을_두_번_붙이지_않는다()
        {
            var chosen = new List<EnemyAffix> { EnemyAffix.Swift };

            Assert.IsFalse(EnemyAffixTable.CanAdd(chosen, EnemyAffix.Swift));
        }

        [Test]
        public void 과민은_시각_신호가_없으므로_붙지_않는다()
        {
            Assert.IsFalse(EnemyAffixTable.IsEnabled(EnemyAffix.Hair),
                "「과민」이 켜졌습니다. 예비동작 단축을 알리는 시각 신호가 먼저입니다.");

            Assert.IsFalse(EnemyAffixTable.CanAdd(new List<EnemyAffix>(), EnemyAffix.Hair));
        }

        [Test]
        public void 속성은_열네_종이고_전부_분류와_이름이_있다()
        {
            Assert.AreEqual(EnemyAffixTable.Count, EnemyAffixTable.Every.Count);

            foreach (EnemyAffix affix in EnemyAffixTable.Every)
            {
                Assert.AreNotEqual(EnemyAffix.None, affix);
                Assert.IsNotEmpty(EnemyAffixTable.Name(affix), $"{affix}의 이름이 없습니다.");
            }

            Assert.AreEqual(6,
                EnemyAffixTable.Every.Count(a => EnemyAffixTable.GroupOf(a) == EnemyAffixGroup.Defence),
                "방어형은 6종입니다. (문서 5종 + 내한성)");

            Assert.AreEqual(3,
                EnemyAffixTable.Every.Count(a => EnemyAffixTable.GroupOf(a) == EnemyAffixGroup.Survival),
                "생존형은 3종입니다.");

            Assert.AreEqual(3,
                EnemyAffixTable.Every.Count(a => EnemyAffixTable.GroupOf(a) == EnemyAffixGroup.Offence),
                "공격형은 3종입니다.");

            Assert.AreEqual(2,
                EnemyAffixTable.Every.Count(a => EnemyAffixTable.GroupOf(a) == EnemyAffixGroup.Support),
                "지유형은 2종입니다.");
        }

        // ── 5. 롤 ─────────────────────────────────────────────────────

        [Test]
        public void 등급별_속성_개수가_범위_안이다()
        {
            var rolled = new List<EnemyAffix>();

            // 씨앗을 바꿔 가며 여러 번 돌린다. 한 번만 보면 운으로 통과한다.
            for (int seed = 0; seed < 200; seed++)
            {
                var random = new Random(seed);

                EnemyAffixRoller.Roll(EnemyRarity.Normal, random, rolled);
                Assert.AreEqual(0, rolled.Count, "일반은 속성을 갖지 않습니다.");

                EnemyAffixRoller.Roll(EnemyRarity.Magic, random, rolled);
                Assert.That(rolled.Count, Is.InRange(1, 2), "마법은 1~2개입니다.");

                EnemyAffixRoller.Roll(EnemyRarity.Rare, random, rolled);
                Assert.That(rolled.Count, Is.InRange(3, 4), "희귀는 3~4개입니다.");
            }
        }

        [Test]
        public void 뽑힌_조합은_언제나_금지_규칙을_지킨다()
        {
            var rolled = new List<EnemyAffix>();

            for (int seed = 0; seed < 500; seed++)
            {
                EnemyAffixRoller.Roll(EnemyRarity.Rare, new Random(seed), rolled);

                int defence = rolled.Count(a =>
                    EnemyAffixTable.GroupOf(a) == EnemyAffixGroup.Defence);

                Assert.LessOrEqual(defence, EnemyAffixTable.MaxDefenceAffixes,
                    $"씨앗 {seed}: 방어형이 {defence}개입니다.");

                Assert.IsFalse(
                    rolled.Contains(EnemyAffix.Carapace) && rolled.Contains(EnemyAffix.Hardened),
                    $"씨앗 {seed}: 갑각과 경화가 함께 나왔습니다.");

                Assert.IsFalse(rolled.Contains(EnemyAffix.Hair),
                    $"씨앗 {seed}: 꺼 둔 「과민」이 나왔습니다.");

                CollectionAssert.AllItemsAreUnique(rolled, $"씨앗 {seed}: 중복이 있습니다.");
            }
        }

        [Test]
        public void 같은_씨앗은_같은_결과를_낸다()
        {
            var first = new List<EnemyAffix>();
            var second = new List<EnemyAffix>();

            EnemyAffixRoller.Roll(EnemyRarity.Rare, new Random(1234), first);
            EnemyAffixRoller.Roll(EnemyRarity.Rare, new Random(1234), second);

            CollectionAssert.AreEqual(first, second,
                "재현되지 않으면 실패를 따라갈 수 없습니다.");
        }

        // ── 6. 최종 수치 합성 ─────────────────────────────────────────

        [Test]
        public void 등급이_체력과_피해와_방어도를_올린다()
        {
            EnemyProfile normal = EnemyProfile.Build(EnemyArchetype.Sentry, EnemyRarity.Normal, null);
            EnemyProfile rare = EnemyProfile.Build(EnemyArchetype.Sentry, EnemyRarity.Rare, null);

            Assert.AreEqual(70, normal.health);
            Assert.AreEqual(315, rare.health, "70 × 4.5 = 315");

            Assert.AreEqual(12, normal.damage);
            Assert.AreEqual(17, rare.damage, "12 × 1.4 = 16.8 → 17");

            Assert.AreEqual(3f, normal.armour, 0.001f);
            Assert.AreEqual(5f, rare.armour, 0.001f, "순라귀 3 + 희귀 보너스 2");
        }

        [Test]
        public void 갑각이_방어도를_삼_더한다()
        {
            EnemyProfile plain = EnemyProfile.Build(EnemyArchetype.Scav, EnemyRarity.Magic, null);

            EnemyProfile armoured = EnemyProfile.Build(EnemyArchetype.Scav, EnemyRarity.Magic,
                new[] { EnemyAffix.Carapace });

            Assert.AreEqual(plain.armour + EnemyAffixTable.CarapaceArmour, armoured.armour, 0.001f);
        }

        [Test]
        public void 저항은_곱해지지_않고_가장_낮은_것_하나만_남는다()
        {
            // 허깨비는 물리 0.66. 여기에 「경화」(물리 0.5)를 얹는다.
            // 곱하면 0.33 — 물리 빌드는 파밍을 버려야 한다. 그래서 곱하지 않는다.
            EnemyProfile profile = EnemyProfile.Build(
                EnemyArchetype.Settled, EnemyRarity.Magic, new[] { EnemyAffix.Hardened });

            Assert.AreEqual(0.5f, profile.resistances.physical, 0.001f,
                "물리 저항이 0.5가 아닙니다. 곱연산이 들어갔다면 0.33이 됩니다. " +
                "(docs/Dokkaebi_Hunting_System.md 9절)");

            // 화염은 건드리지 않았으므로 유형 값이 그대로다.
            Assert.AreEqual(1.5f, profile.resistances.fire, 0.001f);
        }

        [Test]
        public void 저항_속성이_유형의_약점을_뒤집지_않는다()
        {
            // 【절굿공이귀는 「전기로 잡아라」가 정체성이다.】
            // 절연성이 붙었다고 전기가 오히려 덜 아프게 되면
            // 전기 빌드에게는 공략법이 사라진다.
            EnemyProfile insulated = EnemyProfile.Build(
                EnemyArchetype.Crusher, EnemyRarity.Magic, new[] { EnemyAffix.Insulated });

            Assert.AreEqual(1f, insulated.resistances.lightning, 0.001f,
                "절굿공이귀 + 절연성의 전기 배율이 1.0이 아닙니다. " +
                "2.0이면 속성이 안 먹은 것이고, 0.5면 약점이 뒤집힌 것입니다.");

            // 손대지 않은 속성은 그대로다.
            Assert.AreEqual(0f, insulated.resistances.chaos, 0.001f, "카오스 면역이 사라졌습니다.");
        }

        [Test]
        public void 이미_저항이_높은_적이_속성_때문에_물러지지_않는다()
        {
            // 허깨비의 물리 0.66에 경화(0.5)를 얹으면 0.5다.
            // 0.33(곱연산)도 아니고, 0.66보다 커져서도 안 된다.
            EnemyProfile hardened = EnemyProfile.Build(
                EnemyArchetype.Settled, EnemyRarity.Magic, new[] { EnemyAffix.Hardened });

            Assert.AreEqual(0.5f, hardened.resistances.physical, 0.001f);

            Assert.LessOrEqual(hardened.resistances.physical,
                EnemyArchetypeTable.Of(EnemyArchetype.Settled).resistances.physical,
                "속성이 붙었는데 오히려 물리에 약해졌습니다.");
        }

        [Test]
        public void 신속과_육중이_이동_배율에_들어간다()
        {
            EnemyProfile swift = EnemyProfile.Build(
                EnemyArchetype.Scav, EnemyRarity.Magic, new[] { EnemyAffix.Swift });

            EnemyProfile hulking = EnemyProfile.Build(
                EnemyArchetype.Scav, EnemyRarity.Magic, new[] { EnemyAffix.Hulking });

            Assert.AreEqual(1.4f, swift.moveScale, 0.001f);
            Assert.AreEqual(0.8f, hulking.moveScale, 0.001f);
            Assert.IsTrue(hulking.immuneToControl, "육중은 밀어내기·기절에 면역입니다.");
            Assert.IsFalse(swift.immuneToControl);
        }

        [Test]
        public void 꺼진_속성은_데이터로_들어와도_적용되지_않는다()
        {
            // 고유 개체는 속성을 손으로 지정한다. 뽑는 쪽만 막으면 새어 들어온다.
            EnemyProfile profile = EnemyProfile.Build(
                EnemyArchetype.Scav, EnemyRarity.Unique, new[] { EnemyAffix.Hair, EnemyAffix.Swift });

            CollectionAssert.DoesNotContain(profile.affixes, EnemyAffix.Hair,
                "꺼 둔 「과민」이 고유 개체를 통해 들어왔습니다.");

            CollectionAssert.Contains(profile.affixes, EnemyAffix.Swift);
        }

        [Test]
        public void 모든_피해_속성에_저항하는_적이_존재한다()
        {
            // 【한 속성만 저항을 만나지 않으면 그 빌드만 판단을 요구받지 않는다.】
            // 냉기가 그랬다 — 물리·화염·번개·카오스는 막는 속성이 있는데
            // 냉기만 없었다. 문서에 의도라는 서술이 없어 누락으로 보고 채웠다.
            // 속성을 추가·삭제할 때 이 균형이 깨지면 여기서 걸린다.
            var covered = new HashSet<DamageElement>();

            foreach (EnemyAffix affix in EnemyAffixTable.Every)
            {
                if (EnemyAffixTable.TryGetResistance(affix, out DamageElement element))
                    covered.Add(element);
            }

            foreach (DamageElement element in (DamageElement[])Enum.GetValues(typeof(DamageElement)))
            {
                Assert.IsTrue(covered.Contains(element),
                    $"{element} 피해에 저항하는 몬스터 속성이 없습니다. " +
                    "그 속성으로 빌드한 플레이어만 「답을 바꾸는」 경험을 하지 않습니다.");
            }
        }

        [Test]
        public void 재생_수치가_비어_있지_않다()
        {
            // 문서 3절이 「초당 회복」이라고만 적고 비워 둔 칸이다.
            // 0으로 두면 「재생」 속성이 아무 일도 하지 않는 채로 굴러 나온다.
            Assert.Greater(EnemyAffixTable.RegenPerSecond, 0f,
                "「재생」의 초당 회복량이 0입니다.");

            Assert.Less(EnemyAffixTable.RegenPerSecond, 0.10f,
                "초당 10% 이상이면 즉발 화력 외에는 답이 없어집니다.");
        }

        // ── 7. 진영 ───────────────────────────────────────────────────

        [Test]
        public void 진영은_다섯이고_유형_아홉이_전부_소속을_갖는다()
        {
            Assert.AreEqual(FactionTable.Count,
                Enum.GetValues(typeof(Faction)).Length);

            var byFaction = ((EnemyArchetype[])Enum.GetValues(typeof(EnemyArchetype)))
                .GroupBy(a => EnemyArchetypeTable.Of(a).faction)
                .ToDictionary(g => g.Key, g => g.Count());

            // 문서 4절 — 야생 3 · 시설 2 · 실험체 2 · 정착 2.
            Assert.AreEqual(3, byFaction[Faction.Wild], "떠돌이: 잡귀 · 수귀 · 왕지네");
            Assert.AreEqual(2, byFaction[Faction.Facility], "부리던 것: 절굿공이귀 · 순라귀");
            Assert.AreEqual(2, byFaction[Faction.Subject], "살: 번개귀 · 침귀");
            Assert.AreEqual(2, byFaction[Faction.Settled], "헛것: 허깨비 · 무주귀");

            Assert.IsFalse(byFaction.ContainsKey(Faction.Friendly),
                "우호는 적 유형이 아닙니다.");
        }

        [Test]
        public void 우호는_누구와도_먼저_싸우지_않는다()
        {
            foreach (Faction other in (Faction[])Enum.GetValues(typeof(Faction)))
            {
                Assert.IsFalse(FactionTable.IsHostile(Faction.Friendly, other));
                Assert.IsFalse(FactionTable.IsHostile(other, Faction.Friendly));
            }

            Assert.IsFalse(FactionTable.IsHostileToPlayer(Faction.Friendly),
                "적대만 있으면 구역이 사격장이 됩니다. (문서 4절)");
        }

        [Test]
        public void 소속이_다르면_적대하고_같으면_적대하지_않는다()
        {
            Assert.IsTrue(FactionTable.IsHostile(Faction.Wild, Faction.Facility));
            Assert.IsTrue(FactionTable.IsHostile(Faction.Subject, Faction.Settled));

            Assert.IsFalse(FactionTable.IsHostile(Faction.Wild, Faction.Wild));

            foreach (Faction f in (Faction[])Enum.GetValues(typeof(Faction)))
            {
                if (f == Faction.Friendly)
                    continue;

                Assert.IsTrue(FactionTable.IsHostileToPlayer(f), $"{f}는 플레이어와 적대합니다.");
            }
        }
    }
}
