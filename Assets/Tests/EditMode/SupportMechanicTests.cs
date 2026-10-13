using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>【보조 구슬 고유 효과 — 추격 · 기세 · 운율】 (결정 2-116)</summary>
    public class SupportMechanicTests
    {
        [Test]
        public void 세_구슬의_고유_효과를_알아본다()
        {
            Assert.AreEqual(SupportMechanic.Homing, SupportMechanics.Of("sup_pursuit"));
            Assert.AreEqual(SupportMechanic.Momentum, SupportMechanics.Of("sup_momentum"));
            Assert.AreEqual(SupportMechanic.Cadence, SupportMechanics.Of("sup_cadence"));
            Assert.AreEqual(SupportMechanic.None, SupportMechanics.Of("sup_pierce"));
        }

        [Test]
        public void 추격은_상태이상에_걸린_적을_먼저_쫓는다()
        {
            Assert.Less(SupportMechanics.HomingScore(6f, true), SupportMechanics.HomingScore(3f, false), "6m 걸린 적 < 3m 멀쩡한 적");
            Assert.Less(SupportMechanics.HomingScore(2f, false), SupportMechanics.HomingScore(7f, true), "너무 멀면 가까운 쪽");
            Assert.IsTrue(SupportMechanics.InFront(Vector3.forward, new Vector3(1f, 0f, 2f)));
            Assert.IsFalse(SupportMechanics.InFront(Vector3.forward, new Vector3(0f, 0f, -2f)), "뒤로 돌아 쫓지 않는다");
        }

        [Test]
        public void 기세는_움직이는_동안만_붙고_멈추면_바로_사라진다()
        {
            var m = new SupportMechanics.MomentumState();
            for (int i = 0; i < 10; i++) m.Tick(0.1f, 0.02f);   // 5 m/s로 1m
            Assert.IsFalse(m.Active, "1m로는 아직");
            for (int i = 0; i < 30; i++) m.Tick(0.1f, 0.02f);   // 합 4m
            Assert.IsTrue(m.Active);
            m.Tick(0f, 0.02f);
            Assert.IsFalse(m.Active, "멈추면 즉시 소멸");
        }

        [Test]
        public void 운율은_이어_쏠수록_빨라지고_상한에서_1초_과열()
        {
            var c = new SupportMechanics.CadenceState();
            float t = 0f, interval = 0.4f;
            for (int i = 0; i < SupportMechanics.CadenceMaxStacks - 1; i++)
            {
                c.OnShot(t, interval);
                t += interval * c.IntervalMultiplier;
            }
            Assert.Less(c.IntervalMultiplier, 0.5f, "상한 바로 앞에서는 두 배 가까이 빠르다");
            c.OnShot(t, interval);
            Assert.IsTrue(c.Overheated(t + 0.5f), "상한 — 과열");
            Assert.IsFalse(c.Overheated(t + 1.01f), "1초 뒤 풀림");
            Assert.AreEqual(1f, c.IntervalMultiplier, 0.0001f, "과열 뒤 처음부터");

            var d = new SupportMechanics.CadenceState();
            d.OnShot(0f, interval);
            d.OnShot(0.4f, interval);
            Assert.AreEqual(2, d.Stacks);
            d.OnShot(3f, interval);
            Assert.AreEqual(1, d.Stacks, "쉬었다 쏘면 이어 쏜 것이 아니다");
        }

        [Test]
        public void 운율은_쉬지_않고_쏘면_평균으로_이득이다()
        {
            foreach (float interval in new[] { 0.32f, 0.4f, 0.5f })
            {
                float avg = SupportMechanics.CadenceAverageMultiplier(interval);
                Assert.Less(avg, 0.95f, $"간격 {interval}");
                Assert.Greater(avg, 0.7f, $"간격 {interval} — 너무 세지 않게");
            }
        }

        [Test]
        public void 기세와_운율은_늘_합산되지_않는다()
        {
            var momentum = SkillTestFactory.CreateSupport("sup_momentum");
            var cadence = SkillTestFactory.CreateSupport("sup_cadence", fireIntervalMultiplier: 0.85f);
            var pursuit = SkillTestFactory.CreateSupport("sup_pursuit", speedMultiplier: 0.75f);
            try
            {
                var so = new UnityEditor.SerializedObject(momentum);
                so.FindProperty("damageIncrease").floatValue = 0.2f;
                so.ApplyModifiedPropertiesWithoutUndo();

                var m = new WeaponModifiers();
                m.Apply(momentum);
                m.Apply(cadence);
                m.Apply(pursuit);
                Assert.AreEqual(0f, m.DamageIncrease, 0.0001f, "기세는 움직일 때만");
                Assert.AreEqual(0.2f, m.MomentumDamageIncrease, 0.0001f);
                Assert.AreEqual(1f, m.FireIntervalMultiplier, 0.0001f, "운율은 이어 쏠 때만");
                Assert.IsTrue(m.Cadence);
                Assert.IsTrue(m.Homing);
                Assert.AreEqual(0.75f, m.SpeedMultiplier, 0.0001f, "추격의 대가(탄속)는 그대로");

                m.Reset();
                Assert.IsFalse(m.Homing || m.Cadence);
                Assert.AreEqual(0f, m.MomentumDamageIncrease);
            }
            finally
            {
                Object.DestroyImmediate(momentum);
                Object.DestroyImmediate(cadence);
                Object.DestroyImmediate(pursuit);
            }
        }
    }
}
