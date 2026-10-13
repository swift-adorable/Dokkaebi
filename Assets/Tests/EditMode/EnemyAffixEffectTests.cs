using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>【적 속성 넷의 실제 효과】 (결정 2-117 · Hunting_System 3절)</summary>
    public class EnemyAffixEffectTests
    {
        private static Health NewHealth(int max)
        {
            var go = new GameObject("health");
            var h = go.AddComponent<Health>();
            typeof(Health).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(h, null);
            h.SetMaxHealth(max, refill: true);
            h.ConfigureForTest(0f);
            return h;
        }

        private static DamageRequest Hit(int damage) => new DamageRequest
        {
            source = DamageSource.Player,
            baseDamage = damage,
            element = DamageElement.Physical,
            hitKind = HitKind.Melee,
            effectiveRange = 999f,
            criticalMultiplier = 1f
        };

        [Test]
        public void 재생은_초당_최대_체력의_2퍼센트()
        {
            Assert.AreEqual(2f, EnemyAffixTable.RegenAmount(100, 50, EnemyAffixTable.RegenPerSecond, 1f), 0.0001f);
            Assert.AreEqual(0f, EnemyAffixTable.RegenAmount(100, 100, EnemyAffixTable.RegenPerSecond, 1f), "다 찼으면 0");
            Assert.AreEqual(0f, EnemyAffixTable.RegenAmount(100, 0, EnemyAffixTable.RegenPerSecond, 1f), "죽었으면 0");

            EnemyProfile regen = EnemyProfile.Build(EnemyArchetype.Scav, EnemyRarity.Rare, new[] { EnemyAffix.Regenerating });
            Assert.AreEqual(EnemyAffixTable.RegenPerSecond, EnemyAffixEffects.RegenFor(regen), 0.0001f);
        }

        [Test]
        public void 이야기_보스도_판_특성_재생을_받는다()
        {
            EnemyProfile boss = EnemyProfile.ForBoss(StoryTable.Boss("gumiho"), 0);
            Assert.IsTrue(boss.isStoryBoss);
            Assert.AreEqual(0f, EnemyAffixEffects.RegenFor(boss), "판 특성이 없으면 재생 없음");
            boss.regenPerSecond = EnemyAffixTable.RegenPerSecond;   // 판 특성 「되살아나는 잡귀」
            Assert.AreEqual(EnemyAffixTable.RegenPerSecond, EnemyAffixEffects.RegenFor(boss), "보스도 재생한다 (사용자 2026-10-13)");
            Assert.IsFalse(EnemyProfile.Build(EnemyArchetype.Scav, EnemyRarity.Normal, System.Array.Empty<EnemyAffix>()).isStoryBoss);
        }

        [Test]
        public void 껍질은_첫_직접_피격_한_번을_막는다()
        {
            Health h = NewHealth(100);
            try
            {
                h.ShellCharges = 1;
                Assert.AreEqual(0, h.TakeDamage(Hit(30)), "첫 피격 무효");
                Assert.AreEqual(100, h.Current);
                Assert.AreEqual(0, h.ShellCharges);
                Assert.Greater(h.TakeDamage(Hit(30)), 0, "두 번째부터 들어간다");
            }
            finally
            {
                Object.DestroyImmediate(h.gameObject);
            }
        }

        [Test]
        public void 속성이_프로필에_실린다()
        {
            EnemyProfile p = EnemyProfile.Build(EnemyArchetype.Scav, EnemyRarity.Rare,
                new[] { EnemyAffix.Shell, EnemyAffix.Splitting, EnemyAffix.Hulking });
            Assert.IsTrue(p.hasShell);
            Assert.IsTrue(p.splitsOnDeath);
            Assert.IsTrue(p.immuneToControl);
            Assert.AreEqual(2, EnemyAffixTable.SplitCount);
            Assert.Less(EnemyAffixTable.SplitHealthScale, 1f, "갈라진 몸은 약하다");
        }
    }
}
