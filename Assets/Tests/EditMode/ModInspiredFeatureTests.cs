using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 결정 2-142 — 덕코프 인기 모드에서 가져온 기능들.
    /// 등급(가치) · 뒤지기(가려진 칸) · 철수 결산 · 약 세기(강화판 · 희석판) · 보스 소문.
    /// </summary>
    public class ModInspiredFeatureTests
    {
        private static ItemDefinition Create(string id, int value)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            def.name = id;
            var so = new SerializedObject(def);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;
            so.FindProperty("kind").intValue = (int)ItemKind.Material;
            so.FindProperty("weight").floatValue = 0.1f;
            so.FindProperty("slotSize").intValue = 1;
            so.FindProperty("stackMax").intValue = 1;
            so.FindProperty("baseValue").intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            return def;
        }

        // ── 등급 ──────────────────────────────────────────────────────

        [Test]
        public void 등급은_한_개_가치로_매긴다()
        {
            Assert.AreEqual(0, ItemGrade.Of(0));
            Assert.AreEqual(0, ItemGrade.Of(99));
            Assert.AreEqual(1, ItemGrade.Of(100));
            Assert.AreEqual(2, ItemGrade.Of(300));
            Assert.AreEqual(3, ItemGrade.Of(1000));
            Assert.AreEqual(4, ItemGrade.Of(3000));
            Assert.AreEqual(5, ItemGrade.Of(30000));
        }

        [Test]
        public void 좋은_물건일수록_오래_뒤진다()
        {
            for (int g = 1; g < ItemGrade.Count; g++)
                Assert.Greater(ItemGrade.SearchSeconds(g), ItemGrade.SearchSeconds(g - 1));
        }

        // ── 뒤지기 ────────────────────────────────────────────────────

        [Test]
        public void 새로_넣은_물건은_가려져_있다()
        {
            var loot = new LootContainer(4);
            loot.TryPut(Create("a", 10));
            loot.TryPut(Create("b", 10));

            Assert.IsFalse(loot.IsRevealed(0));
            Assert.AreEqual(0, loot.FirstHidden());

            loot.Reveal(0);
            Assert.IsTrue(loot.IsRevealed(0));
            Assert.AreEqual(1, loot.FirstHidden());
        }

        [Test]
        public void 전부_줍기는_드러난_것만_옮긴다()
        {
            var loot = new LootContainer(4);
            var bag = new Inventory(slots: 8, weight: 100f);
            loot.TryPut(Create("a", 10));
            loot.TryPut(Create("b", 10));
            loot.Reveal(1);

            Assert.AreEqual(1, loot.TakeRevealedTo(bag));
            Assert.IsNotNull(loot.Get(0), "가려진 것은 상자에 남는다");
            Assert.IsNull(loot.Get(1));
        }

        [Test]
        public void 내가_떨군_봇짐은_처음부터_다_보인다()
        {
            var loot = new LootContainer(4);
            loot.TryPut(Create("a", 10));
            loot.RevealAll();
            Assert.AreEqual(-1, loot.FirstHidden());
        }

        [Test]
        public void 빈_칸에_새로_넣으면_다시_가려진다()
        {
            var loot = new LootContainer(2);
            var bag = new Inventory(slots: 8, weight: 100f);
            loot.TryPut(Create("a", 10));
            loot.RevealAll();
            loot.TryTakeTo(0, bag);

            loot.TryPut(Create("b", 10));
            Assert.IsFalse(loot.IsRevealed(0));
        }

        // ── 철수 결산 ─────────────────────────────────────────────────

        [Test]
        public void 철수_결산은_늘어난_가치와_시간과_처치를_남긴다()
        {
            RaidLedger.Begin(1000, 10f, "1-1");
            RaidLedger.CountKill();
            RaidLedger.CountKill();
            RaidLedger.Summary s = RaidLedger.End(3500, 130f);

            Assert.AreEqual(2500, s.Gain);
            Assert.AreEqual(120f, s.Seconds, 0.001f);
            Assert.AreEqual(2, s.Kills);
            Assert.IsTrue(RaidLedger.Pending.HasValue);
            RaidLedger.ClearPending();
        }

        [Test]
        public void 죽으면_결산이_없다()
        {
            RaidLedger.Begin(1000, 0f, "1-1");
            RaidLedger.Abort();
            Assert.IsFalse(RaidLedger.Pending.HasValue);
            Assert.IsFalse(RaidLedger.Running);
        }

        [Test]
        public void 판_밖에서_잡은_것은_세지_않는다()
        {
            RaidLedger.Abort();
            RaidLedger.CountKill();
            RaidLedger.Begin(0, 0f, "1-1");
            Assert.AreEqual(0, RaidLedger.End(0, 1f).Kills);
            RaidLedger.ClearPending();
        }

        [Test]
        public void 시간은_분과_초로_적는다()
        {
            Assert.AreEqual("45초", RaidLedger.FormatTime(45f));
            Assert.AreEqual("12분 04초", RaidLedger.FormatTime(724f));
        }

        // ── 약 세기 ───────────────────────────────────────────────────

        [Test]
        public void 강화판_축지환은_두_배로_빠르다()
        {
            var normal = new StatusEffectState();
            normal.Apply(StatusEffectType.Haste, 1f);

            var strong = new StatusEffectState();
            strong.Apply(StatusEffectType.Haste, 2f);

            float normalBonus = normal.SpeedMultiplier - 1f;
            float strongBonus = strong.SpeedMultiplier - 1f;
            Assert.AreEqual(normalBonus * 2f, strongBonus, 0.0001f);
        }

        [Test]
        public void 이로운_상태는_약한_것으로도_덮어쓴다()
        {
            var state = new StatusEffectState();
            state.Apply(StatusEffectType.Haste, 2f);
            state.Apply(StatusEffectType.Haste, 0.4f);
            Assert.AreEqual(0.4f, state.PotencyOf(StatusEffectType.Haste), 0.0001f);
        }

        [Test]
        public void 희석판은_오래_간다()
        {
            var state = new StatusEffectState();
            state.Apply(StatusEffectType.Haste, 0.4f, 2f);
            Assert.AreEqual(StatusEffectTable.Get(StatusEffectType.Haste).Duration * 2f,
                state.RemainingOf(StatusEffectType.Haste), 0.001f);
        }

        [Test]
        public void 강화판과_희석판은_약탕간에서_달인다()
        {
            ItemCatalog catalog = ItemCatalog.Load();
            foreach (string id in new[] { "con_stim", "con_coagulant", "con_regen", "con_expander" })
            {
                foreach (string variant in new[] { $"{id}_strong", $"{id}_thin" })
                {
                    Assert.IsNotNull(catalog.Find(variant), $"{variant} 에셋이 카탈로그에 없다");
                    bool brewed = false;
                    foreach (CraftRecipe r in ApothecaryTable.Brews)
                        brewed |= r.OutputId == variant;
                    Assert.IsTrue(brewed, $"{variant} 를 달일 수 없다");
                }
            }

            Assert.AreEqual(2f, catalog.Find("con_stim_strong").Consumable.GrantPotency, 0.0001f);
            Assert.AreEqual(0.4f, catalog.Find("con_stim_thin").Consumable.GrantPotency, 0.0001f);
        }

        // ── 보스 소문 ─────────────────────────────────────────────────

        [Test]
        public void 퀘스트가_없으면_소문도_없다()
        {
            Assert.IsFalse(BossRumor.Heard("hyeonmu", null));
            Assert.IsFalse(BossRumor.Heard(null, null));
        }
    }
}
