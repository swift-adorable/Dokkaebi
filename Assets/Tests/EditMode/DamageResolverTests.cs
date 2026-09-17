using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// 피해 계산 공식 테스트. (docs/Blob_Combat_Baseline.md 3절)
    ///
    /// 이 공식이 스킬·장비·사냥의 모든 수치를 지배하므로 경계값까지 전수 검증한다.
    /// </summary>
    public class DamageResolverTests
    {
        private static DamageRequest Basic(int damage = 10)
        {
            DamageRequest r = DamageRequest.FromWeapon(damage, 0f);
            r.distance = -1f;
            return r;
        }

        // ── 방어도 보정 ────────────────────────────────────────────────

        [Test]
        public void 관통이_방어도_이상이면_감쇠가_없다()
        {
            Assert.AreEqual(1f, DamageResolver.ArmourMultiplier(0, 0), 0.0001f);
            Assert.AreEqual(1f, DamageResolver.ArmourMultiplier(3, 3), 0.0001f);
        }

        [Test]
        public void 초과_관통에는_보상이_없다()
        {
            // 이 성질이 "필요한 만큼만 쓴다"는 자원 경제를 만든다.
            float exact = DamageResolver.ArmourMultiplier(3, 3);
            float over = DamageResolver.ArmourMultiplier(3, 6);

            Assert.AreEqual(exact, over, 0.0001f);
            Assert.AreEqual(1f, over, 0.0001f);
        }

        [Test]
        public void 방어도_차이별_배율이_문서와_일치한다()
        {
            Assert.AreEqual(0.667f, DamageResolver.ArmourMultiplier(1, 0), 0.001f);
            Assert.AreEqual(0.500f, DamageResolver.ArmourMultiplier(2, 0), 0.001f);
            Assert.AreEqual(0.400f, DamageResolver.ArmourMultiplier(3, 0), 0.001f);
            Assert.AreEqual(0.333f, DamageResolver.ArmourMultiplier(4, 0), 0.001f);
            Assert.AreEqual(0.286f, DamageResolver.ArmourMultiplier(5, 0), 0.001f);
        }

        [Test]
        public void 방어도는_피해량의_크기와_무관하게_같은_비율로_깎는다()
        {
            // 이 성질이 없으면 저피해·다탄 빌드가 구조적으로 죽는다.
            // 정수 차감이었다면 작은 피해일수록 손실률이 커진다.
            var defence = DefenceProfile.Create(3, 3, ElementalResistances.Default);

            int small = DamageResolver.Resolve(Basic(8), defence);
            int large = DamageResolver.Resolve(Basic(80), defence);

            float smallRatio = small / 8f;
            float largeRatio = large / 80f;

            Assert.AreEqual(largeRatio, smallRatio, 0.05f,
                "피해량이 달라도 손실 비율은 같아야 합니다.");
        }

        [Test]
        public void 원거리는_머리_방어도_근접은_몸통_방어도를_쓴다()
        {
            var defence = DefenceProfile.Create(head: 4, body: 0, resist: ElementalResistances.Default);

            DamageRequest ranged = Basic(100);
            ranged.hitKind = HitKind.Ranged;

            DamageRequest melee = Basic(100);
            melee.hitKind = HitKind.Melee;

            Assert.AreEqual(33, DamageResolver.Resolve(ranged, defence));
            Assert.AreEqual(100, DamageResolver.Resolve(melee, defence));
        }

        // ── 사거리 보정 ────────────────────────────────────────────────

        [Test]
        public void 사거리_절반_이내는_온전한_피해가_들어간다()
        {
            Assert.AreEqual(1f, RangeFalloff.Multiplier(0f, 10f), 0.0001f);
            Assert.AreEqual(1f, RangeFalloff.Multiplier(5f, 10f), 0.0001f);
        }

        [Test]
        public void 사거리_절반을_넘으면_절반으로_깎인다()
        {
            Assert.AreEqual(0.5f, RangeFalloff.Multiplier(5.01f, 10f), 0.0001f);
            Assert.AreEqual(0.5f, RangeFalloff.Multiplier(10f, 10f), 0.0001f);
        }

        [Test]
        public void 사거리를_넘으면_피해가_없다()
        {
            Assert.AreEqual(0f, RangeFalloff.Multiplier(10.01f, 10f), 0.0001f);
            Assert.AreEqual(0, DamageResolver.Resolve(
                new DamageRequest { baseDamage = 100, distance = 99f, effectiveRange = 10f },
                DefenceProfile.None));
        }

        [Test]
        public void 사거리를_쓰지_않는_공격은_보정하지_않는다()
        {
            // 근접·장판·상태이상이 이 경로를 쓴다.
            Assert.AreEqual(1f, RangeFalloff.Multiplier(999f, 0f), 0.0001f);
            Assert.AreEqual(1f, RangeFalloff.Multiplier(-1f, 10f), 0.0001f);
        }

        // ── 속성 상성 ──────────────────────────────────────────────────

        [Test]
        public void 내성_0은_완전_면역이며_하한_1을_적용하지_않는다()
        {
            DamageRequest chaos = Basic(100);
            chaos.element = DamageElement.Chaos;

            var mechanical = DefenceProfile.Create(0, 0, ElementalResistances.Mechanical);

            Assert.AreEqual(0, DamageResolver.Resolve(chaos, mechanical),
                "기계형은 카오스에 완전 면역이어야 합니다.");
        }

        [Test]
        public void 약점_속성은_피해가_증폭된다()
        {
            var settled = DefenceProfile.Create(0, 0, ElementalResistances.Settled);

            DamageRequest physical = Basic(100);
            DamageRequest fire = Basic(100);
            fire.element = DamageElement.Fire;

            Assert.AreEqual(66, DamageResolver.Resolve(physical, settled));
            Assert.AreEqual(150, DamageResolver.Resolve(fire, settled));
        }

        [Test]
        public void 내성은_곱해지지_않고_가장_낮은_값_하나만_남는다()
        {
            // 곱연산으로 중첩하면 방어형 속성 2개만으로 공략 불가가 된다.
            var a = ElementalResistances.Default;
            a.fire = 0.5f;

            var b = ElementalResistances.Default;
            b.fire = 0.5f;
            b.cold = 0.25f;

            ElementalResistances merged = ElementalResistances.TakeLowest(a, b);

            Assert.AreEqual(0.5f, merged.fire, 0.0001f, "0.25가 되면 안 됩니다.");
            Assert.AreEqual(0.25f, merged.cold, 0.0001f);
        }

        // ── 상태이상은 방어도를 무시한다 ───────────────────────────────

        [Test]
        public void 상태이상_피해는_방어도를_무시하고_속성_상성만_받는다()
        {
            var heavy = DefenceProfile.Create(6, 6, ElementalResistances.Settled);

            DamageRequest dot = Basic(100);
            dot.element = DamageElement.Fire;
            dot.bypassArmour = true;

            // 방어도 6이 무시되므로 화염 약점 1.5배만 적용된다.
            Assert.AreEqual(150, DamageResolver.Resolve(dot, heavy));
        }

        // ── 기타 ──────────────────────────────────────────────────────

        [Test]
        public void 증가율은_가산_합산된다()
        {
            DamageRequest r = Basic(100);
            r.increasedPercent = 0.5f;

            Assert.AreEqual(150, DamageResolver.Resolve(r, DefenceProfile.None));
        }

        [Test]
        public void 면역이_아니면_최소_1의_피해가_들어간다()
        {
            // 고방어 구간에서 "때려도 숫자가 안 뜨는" 상태를 막는다.
            var wall = DefenceProfile.Create(7, 7, ElementalResistances.Default);

            Assert.AreEqual(1, DamageResolver.Resolve(Basic(1), wall));
        }

        [Test]
        public void 난이도_보정이_곱해진다()
        {
            Assert.AreEqual(80, DamageResolver.Resolve(Basic(100), DefenceProfile.None,
                DifficultyTable.EnemyDamage(DifficultyLevel.Balanced)));
            Assert.AreEqual(150, DamageResolver.Resolve(Basic(100), DefenceProfile.None,
                DifficultyTable.EnemyDamage(DifficultyLevel.Extreme)));
        }

        [Test]
        public void 폭주_난이도만_확장_상태이상을_활성화한다()
        {
            Assert.IsTrue(DifficultyTable.HasExtendedAilments(DifficultyLevel.Frenzy));
            Assert.IsFalse(DifficultyTable.HasExtendedAilments(DifficultyLevel.Extreme));
            Assert.IsFalse(DifficultyTable.HasExtendedAilments(DifficultyLevel.Survival));
        }

        [Test]
        public void 폭주는_피해가_가장_높고_체력이_가장_낮다()
        {
            Assert.AreEqual(1.6f, DifficultyTable.EnemyDamage(DifficultyLevel.Frenzy), 0.0001f);
            Assert.AreEqual(0.4f, DifficultyTable.EnemyHealth(DifficultyLevel.Frenzy), 0.0001f);
        }

        [Test]
        public void 기본_피해가_0_이하면_계산하지_않는다()
        {
            Assert.AreEqual(0, DamageResolver.Resolve(Basic(0), DefenceProfile.None));
            Assert.AreEqual(0, DamageResolver.Resolve(Basic(-5), DefenceProfile.None));
        }

        [Test]
        public void 감소율이_100퍼센트를_넘으면_피해가_없다()
        {
            DamageRequest r = Basic(100);
            r.increasedPercent = -1f;

            Assert.AreEqual(0, DamageResolver.Resolve(r, DefenceProfile.None));
        }

        // ── 문서 검산 재현 ────────────────────────────────────────────

        [Test]
        public void 문서_검산_4장_고유_정착체가_재현된다()
        {
            // Combat_Baseline 8절: 피해 36, 플레이어 방어 6 → 피격당 9
            var player = DefenceProfile.Create(6, 6, ElementalResistances.Default);

            DamageRequest hit = Basic(36);

            Assert.AreEqual(9, DamageResolver.Resolve(hit, player));
        }

        [Test]
        public void 문서_검산_정착체에는_속성이_2배_이상_차이를_만든다()
        {
            var settled = DefenceProfile.Create(0, 0, ElementalResistances.Settled);

            int physical = DamageResolver.Resolve(Basic(100), settled);

            DamageRequest fire = Basic(100);
            fire.element = DamageElement.Fire;
            int fireDamage = DamageResolver.Resolve(fire, settled);

            Assert.Greater(fireDamage / (float)physical, 2f,
                "속성 선택이 2배 이상의 차이를 만들어야 4장 설계 의도가 성립합니다.");
        }
    }
}
