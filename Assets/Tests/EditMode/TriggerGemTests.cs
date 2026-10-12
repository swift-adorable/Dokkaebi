using NUnit.Framework;
using UnityEditor;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 【모였다 터지는 구슬 · 번지는 구슬】 (결정 2-102)
    ///   발동 구슬은 정해진 일마다 차서 100%에 저절로 터지고 0으로 돌아간다 · 발동이 일으킨 일은 세지 않는다
    ///   전령은 그 상태가 걸린 적을 처치하면 터져 둘레로 번진다
    /// </summary>
    public class TriggerGemTests
    {
        private const string SkillRoot = "Assets/Data/ScriptableObjects/Skills";

        private static int EventsToFire(string metaId, TriggerEvent e)
        {
            var charge = new TriggerCharge();
            charge.Bind(metaId);

            for (int i = 1; i <= 100; i++)
                if (charge.Add(e))
                    return i;

            return -1;
        }

        [Test]
        public void 발동_구슬은_정해진_횟수에_터진다()
        {
            Assert.AreEqual(13, EventsToFire(TriggerGemTable.OnShock, TriggerEvent.ShockInflicted));     // 8%
            Assert.AreEqual(10, EventsToFire(TriggerGemTable.OnIgnite, TriggerEvent.KilledIgnited));     // 10%
            Assert.AreEqual(15, EventsToFire(TriggerGemTable.OnFreeze, TriggerEvent.FreezeInflicted));   // 7%
            Assert.AreEqual(17, EventsToFire(TriggerGemTable.OnCrit, TriggerEvent.Critical));            // 6%
            Assert.AreEqual(20, EventsToFire(TriggerGemTable.OnAilment, TriggerEvent.IgniteInflicted));  // 5%
        }

        [Test]
        public void 상관없는_일은_차지_않는다()
        {
            Assert.AreEqual(-1, EventsToFire(TriggerGemTable.OnShock, TriggerEvent.Critical));
            Assert.AreEqual(-1, EventsToFire(TriggerGemTable.OnIgnite, TriggerEvent.IgniteInflicted));   // 거는 것이 아니라 처치
            Assert.AreEqual(-1, EventsToFire(TriggerGemTable.OnCrit, TriggerEvent.FreezeInflicted));
            Assert.AreEqual(-1, EventsToFire(null, TriggerEvent.Critical));

            // 원소 상태 이상 시 시전은 셋 다 센다.
            Assert.Greater(TriggerGemTable.ChargePer(TriggerGemTable.OnAilment, TriggerEvent.FreezeInflicted), 0f);
            Assert.Greater(TriggerGemTable.ChargePer(TriggerGemTable.OnAilment, TriggerEvent.ShockInflicted), 0f);
        }

        [Test]
        public void 터지면_0으로_돌아가고_구슬을_바꾸면_0부터()
        {
            var charge = new TriggerCharge();
            charge.Bind(TriggerGemTable.OnIgnite);

            for (int i = 0; i < 10; i++)
                charge.Add(TriggerEvent.KilledIgnited);
            Assert.AreEqual(0, charge.Percent);

            charge.Add(TriggerEvent.KilledIgnited);
            charge.Add(TriggerEvent.KilledIgnited);
            Assert.AreEqual(20, charge.Percent);

            charge.Bind(TriggerGemTable.OnIgnite);          // 같은 구슬 — 그대로
            Assert.AreEqual(20, charge.Percent);

            charge.Bind(TriggerGemTable.OnCrit);            // 다른 구슬 — 0부터
            Assert.AreEqual(0, charge.Percent);
        }

        [Test]
        public void 발동이_일으킨_일은_충전하지_않는다()
        {
            int inflicted = 0, crits = 0;
            void OnInflicted(Health h, StatusEffectType t) => inflicted++;
            void OnCritical(Health h) => crits++;

            PlayerHitEvents.Inflicted += OnInflicted;
            PlayerHitEvents.Critical += OnCritical;
            try
            {
                PlayerHitEvents.ReportInflicted(null, StatusEffectType.Freeze);
                PlayerHitEvents.ReportInflicted(null, StatusEffectType.Poison);   // 원소 셋이 아니다
                PlayerHitEvents.ReportCritical(null);

                PlayerHitEvents.BeginSuppress();
                PlayerHitEvents.ReportInflicted(null, StatusEffectType.Ignite);
                PlayerHitEvents.ReportCritical(null);
                PlayerHitEvents.EndSuppress();

                PlayerHitEvents.ReportInflicted(null, StatusEffectType.Shock);
            }
            finally
            {
                PlayerHitEvents.Inflicted -= OnInflicted;
                PlayerHitEvents.Critical -= OnCritical;
            }

            Assert.AreEqual(2, inflicted);
            Assert.AreEqual(1, crits);
            Assert.IsFalse(PlayerHitEvents.IsSuppressed);
        }

        [Test]
        public void 전령은_그_상태로_번진다()
        {
            Assert.AreEqual(StatusEffectType.Ignite, TriggerGemTable.HeraldSpread(StatusEffectType.Ignite));
            Assert.AreEqual(StatusEffectType.Freeze, TriggerGemTable.HeraldSpread(StatusEffectType.Freeze));
            Assert.AreEqual(StatusEffectType.Poison, TriggerGemTable.HeraldSpread(StatusEffectType.Poison));
            Assert.AreEqual(StatusEffectType.None, TriggerGemTable.HeraldSpread(StatusEffectType.Shock));
            Assert.AreEqual(StatusEffectType.None, TriggerGemTable.HeraldSpread(StatusEffectType.Bleed));

            // 피의 전령만 쌓인 출혈에 비례한다.
            Assert.AreEqual(4, TriggerGemTable.HeraldStacks(StatusEffectType.Bleed, 4));
            Assert.AreEqual(1, TriggerGemTable.HeraldStacks(StatusEffectType.Poison, 7));
        }

        [Test]
        public void 전령은_그_상태가_걸린_적에서만_터진다()
        {
            var status = new StatusEffectState();
            Assert.IsFalse(TriggerGemTable.HeraldMatches(StatusEffectType.Ignite, status));

            status.Apply(StatusEffectType.Ignite, 10f);
            Assert.IsTrue(TriggerGemTable.HeraldMatches(StatusEffectType.Ignite, status));
            Assert.IsFalse(TriggerGemTable.HeraldMatches(StatusEffectType.Bleed, status));
            Assert.IsFalse(TriggerGemTable.HeraldMatches(StatusEffectType.None, status));
        }

        [Test]
        public void 전령_다섯은_조건_상태를_가진다()
        {
            (string id, StatusEffectType st)[] heralds =
            {
                ("her_ash", StatusEffectType.Ignite), ("her_ice", StatusEffectType.Freeze),
                ("her_thunder", StatusEffectType.Shock), ("her_plague", StatusEffectType.Poison),
                ("her_blood", StatusEffectType.Bleed)
            };

            foreach (var (id, st) in heralds)
            {
                var def = AssetDatabase.LoadAssetAtPath<SkillDefinition>($"{SkillRoot}/Persistent/{id}.asset");
                Assert.IsNotNull(def, id);
                Assert.IsTrue(def.IsHerald, id);
                Assert.AreEqual(st, def.ConsumesStatus, id);
            }

            foreach (string id in new[] { TriggerGemTable.OnShock, TriggerGemTable.OnIgnite, TriggerGemTable.OnFreeze,
                                          TriggerGemTable.OnCrit, TriggerGemTable.OnAilment })
            {
                var def = AssetDatabase.LoadAssetAtPath<SkillDefinition>($"{SkillRoot}/Meta/{id}.asset");
                Assert.IsNotNull(def, id);
                Assert.AreEqual(id, def.Id);
            }
        }
    }
}
