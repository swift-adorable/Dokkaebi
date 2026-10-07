using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>기폭 · 잔류물 연결 (Audit A5 · A6 · 결정 2-74 · SkillZoneTable · DetonationLoadout).</summary>
    public class SkillZoneTests
    {
        private static SkillDefinition Skill(string id)
        {
            SkillDefinition d = SkillCatalog.Load().Find(id);
            Assert.IsNotNull(d, id);
            return d;
        }

        private static SocketedBuild Build()
        {
            var build = new SocketedBuild();
            build.SetLevel(15);
            return build;
        }

        [Test]
        public void 기폭할_상태는_중첩이_가장_많은_것()
        {
            var status = new StatusEffectState();
            Assert.AreEqual(StatusEffectType.None, SkillZoneTable.PickDetonation(status));

            status.Apply(StatusEffectType.Ignite, 10f);
            status.Apply(StatusEffectType.Poison, 10f);
            status.Apply(StatusEffectType.Poison, 10f);
            Assert.AreEqual(StatusEffectType.Poison, SkillZoneTable.PickDetonation(status));
        }

        [Test]
        public void 최대_중첩이면_터질_상태를_돌려준다()
        {
            var status = new StatusEffectState();
            int max = StatusEffectTable.Get(StatusEffectType.Poison).MaxStacks;

            for (int i = 0; i < max - 1; i++)
                status.Apply(StatusEffectType.Poison, 10f);
            Assert.IsFalse(SkillZoneTable.AtMaxStacks(status, out _, out _, out _));

            // 최대 중첩 = 부식으로 넘어간 순간
            status.Apply(StatusEffectType.Poison, 10f);
            Assert.IsTrue(SkillZoneTable.AtMaxStacks(status, out StatusEffectType detonateAs,
                                                     out StatusEffectType consume, out int full));
            Assert.AreEqual(StatusEffectType.Poison, detonateAs);
            Assert.AreEqual(StatusEffectType.Corrode, consume);
            Assert.AreEqual(max, full);
        }

        [Test]
        public void 잔류물은_여덟개까지_넘치면_오래된_것부터()
        {
            var set = new GroundZoneSet();
            GroundZone first = null;

            for (int i = 0; i < SkillZoneTable.MaxGrounds; i++)
            {
                GroundZone z = set.Add(new GroundZone(GroundEffectType.FireZone, Vector3.zero, 2f, 4f, 10f), out GroundZone ev);
                first ??= z;
                Assert.IsNull(ev);
            }

            set.Add(new GroundZone(GroundEffectType.BloodZone, Vector3.zero, 2f, 4f, 10f), out GroundZone evicted);
            Assert.AreSame(first, evicted);
            Assert.AreEqual(SkillZoneTable.MaxGrounds, set.Count);
        }

        [Test]
        public void 잔류물은_깔리자마자_걸고_0점5초마다_건다()
        {
            var zone = new GroundZone(GroundEffectType.ToxicSwamp, Vector3.zero, 2f, 4f, 10f);
            Assert.IsTrue(zone.Advance(0.01f));
            Assert.IsFalse(zone.Advance(0.2f));
            Assert.IsTrue(zone.Advance(0.3f));
            Assert.IsTrue(zone.Contains(new Vector3(1.9f, 0f, 0f)));
            Assert.IsFalse(zone.Contains(new Vector3(2.1f, 0f, 0f)));

            zone.Advance(5f);
            Assert.IsTrue(zone.IsExpired);
        }

        [Test]
        public void 서리_장판은_동결이_아니라_냉기를_건다()
        {
            Assert.AreEqual(StatusEffectType.Chill, SkillZoneTable.ZoneStatus(GroundEffectType.FrostField));
            Assert.AreEqual(StatusEffectType.Ignite, SkillZoneTable.ZoneStatus(GroundEffectType.FireZone));
            Assert.AreEqual(StatusEffectType.Congeal, SkillZoneTable.ZoneStatus(GroundEffectType.GravityWell));
        }

        [Test]
        public void 기폭_보조_젬은_탄에_섞이지_않고_기폭에만_걸린다()
        {
            SocketedBuild build = Build();
            Assert.IsTrue(build.TryEquipCore(Skill(SkillZoneTable.ElementalBurstId), 0));
            Assert.IsTrue(build.TryEquipSupport(Skill("sup_short_fuse"), 0, 0));
            Assert.IsTrue(build.TryEquipSupport(Skill("sup_chain_detonation"), 0, 1));

            WeaponModifiers bullets = build.GetModifiers();
            Assert.AreEqual(1f, bullets.RangeMultiplier, 1e-4, "짧은 퓨즈가 탄 사거리를 줄이면 안 된다");
            Assert.AreEqual(0f, bullets.DamageIncrease, 1e-4, "연쇄 기폭의 +20%는 기폭에만");

            DetonationLoadout loadout = build.GetDetonationLoadout(0);
            Assert.IsTrue(loadout.IsValid);
            Assert.AreEqual(0.7f, loadout.RadiusMultiplier, 1e-4);
            Assert.AreEqual(0f, loadout.Fuse, 1e-4, "짧은 퓨즈 = 지연 없음");
            Assert.AreEqual(1, loadout.Chains);
            Assert.AreEqual(SkillZoneTable.DetonationCooldown + 0.4f, loadout.Cooldown, 1e-4);
            Assert.AreEqual(0.20f, loadout.DamageIncrease, 1e-4);
        }

        [Test]
        public void 긴_퓨즈는_지연되고_중첩을_전부_쓴다()
        {
            SocketedBuild build = Build();
            build.TryEquipCore(Skill(SkillZoneTable.ElementalBurstId), 0);
            Assert.IsTrue(build.TryEquipSupport(Skill("sup_long_fuse"), 0, 0));

            DetonationLoadout loadout = build.GetDetonationLoadout(0);
            Assert.AreEqual(0.8f, loadout.Fuse, 1e-4);
            Assert.IsTrue(loadout.ConsumesAllStacks);
            Assert.AreEqual(0.35f, loadout.DamageIncrease, 1e-4);
        }

        [Test]
        public void 부여_핵심_젬은_기폭_장비가_아니다()
        {
            SocketedBuild build = Build();
            build.TryEquipCore(Skill("core_fire"), 0);
            Assert.IsFalse(build.GetDetonationLoadout(0).IsValid);
            Assert.AreEqual(SkillZoneTable.DefaultFuse, DetonationLoadout.For(Skill(SkillZoneTable.ShockwaveId)).Fuse, 1e-4);
        }

        [Test]
        public void 중력_붕괴의_잔류물_보조_젬은_우물에_걸린다()
        {
            SocketedBuild build = Build();
            Assert.IsTrue(build.TryEquipCore(Skill(SkillZoneTable.GravityCollapseId), 0));
            Assert.IsTrue(build.TryEquipSupport(Skill("sup_lasting_ground"), 0, 0));

            DetonationLoadout loadout = build.GetDetonationLoadout(0);
            Assert.AreEqual(2f, loadout.DurationMultiplier, 1e-4);
            Assert.AreEqual(0.75f, loadout.ZoneRadiusMultiplier, 1e-4);
            Assert.AreEqual(1f, build.GetModifiers().AilmentDurationMultiplier, 1e-4, "탄의 상태 지속은 그대로");
        }
    }
}
