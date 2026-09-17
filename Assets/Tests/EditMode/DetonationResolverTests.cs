using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// 기폭(원소 작렬)과 잔류물 대응표 테스트. (docs/Blob_Skill_System.md 5-2절)
    /// </summary>
    public class DetonationResolverTests
    {
        [Test]
        public void 상태가_없으면_아무_일도_일어나지_않는다()
        {
            // 「원소 작렬은 단독으로 무의미하다」를 코드에서 보장한다.
            DetonationResult r = DetonationResolver.Resolve(StatusEffectType.None, 1, 10);

            Assert.IsFalse(r.Detonated);
            Assert.AreEqual(0, r.Damage);
        }

        [Test]
        public void 상태별_폭발_속성이_부여_Core와_일치한다()
        {
            Assert.AreEqual(DamageElement.Fire,
                DetonationResolver.Resolve(StatusEffectType.Ignite, 1, 10).Element);
            Assert.AreEqual(DamageElement.Chaos,
                DetonationResolver.Resolve(StatusEffectType.Poison, 1, 10).Element);
            Assert.AreEqual(DamageElement.Cold,
                DetonationResolver.Resolve(StatusEffectType.Freeze, 1, 10).Element);
            Assert.AreEqual(DamageElement.Lightning,
                DetonationResolver.Resolve(StatusEffectType.Shock, 1, 10).Element);
            Assert.AreEqual(DamageElement.Physical,
                DetonationResolver.Resolve(StatusEffectType.Bleed, 1, 10).Element);
        }

        [Test]
        public void 중첩이_쌓일수록_폭발_피해가_커진다()
        {
            int one = DetonationResolver.Resolve(StatusEffectType.Poison, 1, 10).Damage;
            int ten = DetonationResolver.Resolve(StatusEffectType.Poison, 10, 10).Damage;

            Assert.AreEqual(one * 10, ten, "중독 10중첩이 기폭의 최대 보상이어야 합니다.");
        }

        [Test]
        public void 중독_기폭만_상태를_전이시킨다()
        {
            // 독 구름 확산. 중첩을 쌓는 정체성이 기폭에서도 유지된다.
            Assert.AreEqual(StatusEffectType.Poison,
                DetonationResolver.Resolve(StatusEffectType.Poison, 3, 10).SpreadStatus);

            // 동결도 인접 동결을 전이시킨다.
            Assert.AreEqual(StatusEffectType.Freeze,
                DetonationResolver.Resolve(StatusEffectType.Freeze, 1, 10).SpreadStatus);

            // 점화·출혈은 전이 대신 잔류물을 남긴다.
            Assert.AreEqual(StatusEffectType.None,
                DetonationResolver.Resolve(StatusEffectType.Ignite, 1, 10).SpreadStatus);
        }

        [Test]
        public void 감전_기폭은_반경이_더_넓다()
        {
            // 낙뢰 3회로 피해가 나뉘므로 범위로 보상한다.
            float shock = DetonationResolver.Resolve(StatusEffectType.Shock, 1, 10).Radius;
            float ignite = DetonationResolver.Resolve(StatusEffectType.Ignite, 1, 10).Radius;

            Assert.Greater(shock, ignite);
        }

        [Test]
        public void 기폭_범위_보정이_적용된다()
        {
            // 「짧은 퓨즈」의 대가 — 기폭 범위 -30%
            float full = DetonationResolver.Resolve(StatusEffectType.Ignite, 1, 10).Radius;
            float reduced = DetonationResolver.Resolve(StatusEffectType.Ignite, 1, 10, 0.7f).Radius;

            Assert.AreEqual(full * 0.7f, reduced, 0.001f);
        }

        [Test]
        public void 응집은_기폭_대상이_아니다()
        {
            // 응집은 전이 범위를 넓히는 증폭 상태이지 소모 대상이 아니다.
            Assert.IsFalse(DetonationResolver.Resolve(StatusEffectType.Congeal, 1, 10).Detonated);
        }

        [Test]
        public void 기본_피해가_0이면_기폭하지_않는다()
        {
            Assert.IsFalse(DetonationResolver.Resolve(StatusEffectType.Ignite, 1, 0).Detonated);
            Assert.IsFalse(DetonationResolver.Resolve(StatusEffectType.Ignite, 1, -5).Detonated);
        }

        // ── 잔류물 대응표 ──────────────────────────────────────────────────

        [Test]
        public void 상태를_지닌_채_죽으면_해당_잔류물을_남긴다()
        {
            Assert.AreEqual(GroundEffectType.FireZone, GroundEffectTable.FromStatus(StatusEffectType.Ignite));
            Assert.AreEqual(GroundEffectType.ToxicSwamp, GroundEffectTable.FromStatus(StatusEffectType.Poison));
            Assert.AreEqual(GroundEffectType.FrostField, GroundEffectTable.FromStatus(StatusEffectType.Freeze));
            Assert.AreEqual(GroundEffectType.BloodZone, GroundEffectTable.FromStatus(StatusEffectType.Bleed));
        }

        [Test]
        public void 감전과_응집은_잔류물을_남기지_않는다()
        {
            // 감전은 증폭 전용이고, 응집은 중력 붕괴 Core가 직접 우물을 만든다.
            Assert.AreEqual(GroundEffectType.None, GroundEffectTable.FromStatus(StatusEffectType.Shock));
            Assert.AreEqual(GroundEffectType.None, GroundEffectTable.FromStatus(StatusEffectType.Congeal));
        }

        [Test]
        public void 서리_장판은_동결을_바로_걸지_않는다()
        {
            // 장판 하나로 무리 전체가 굳어 버리면 동결의 임계치 설계가 무의미해진다.
            Assert.AreEqual(StatusEffectType.None, GroundEffectTable.AppliesStatus(GroundEffectType.FrostField));

            // 나머지 잔류물은 진입 시 상태를 건다.
            Assert.AreEqual(StatusEffectType.Ignite, GroundEffectTable.AppliesStatus(GroundEffectType.FireZone));
            Assert.AreEqual(StatusEffectType.Poison, GroundEffectTable.AppliesStatus(GroundEffectType.ToxicSwamp));
            Assert.AreEqual(StatusEffectType.Bleed, GroundEffectTable.AppliesStatus(GroundEffectType.BloodZone));
            Assert.AreEqual(StatusEffectType.Congeal, GroundEffectTable.AppliesStatus(GroundEffectType.GravityWell));
        }

        [Test]
        public void 잔류물_5종이_전부_어딘가에서_만들어진다()
        {
            // 만들 수단이 없는 잔류물은 죽은 enum이다.
            // 중력 우물만 Core가 직접 만들고, 나머지 4종은 사망 시 생성된다.
            var fromDeath = new[]
            {
                GroundEffectTable.FromStatus(StatusEffectType.Ignite),
                GroundEffectTable.FromStatus(StatusEffectType.Poison),
                GroundEffectTable.FromStatus(StatusEffectType.Freeze),
                GroundEffectTable.FromStatus(StatusEffectType.Bleed)
            };

            foreach (GroundEffectType g in System.Enum.GetValues(typeof(GroundEffectType)))
            {
                if (g == GroundEffectType.None || g == GroundEffectType.GravityWell)
                    continue;

                CollectionAssert.Contains(fromDeath, g, $"{g}를 만드는 수단이 없습니다.");
            }
        }
    }
}
