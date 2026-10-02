using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blob.Tests
{
    /// <summary>
    /// 소모품 판정. (docs/Blob_Consumable_System.md)
    ///
    /// 이 표의 핵심은 「썼는데 아무 일도 안 일어났다」를 만들지 않는 것이다.
    /// 가방에서 한 개가 사라졌는데 화면에 변화가 없으면 플레이어는 그것이
    /// 버그인지 규칙인지 구분할 방법이 없다.
    /// </summary>
    public class ConsumableUseTests
    {
        private static ItemDefinition Create(
            string id,
            ConsumableCategory category = ConsumableCategory.Restore,
            int heal = 0, float water = 0f, float energy = 0f,
            float waterCost = 0f, float energyCost = 0f,
            StatusEffectType cure = StatusEffectType.None,
            int cureStacks = 99, int useCost = 0, int maxDurability = 0,
            ItemKind kind = ItemKind.Consumable)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            def.name = id;

            var so = new SerializedObject(def);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;
            so.FindProperty("kind").intValue = (int)kind;
            so.FindProperty("maxDurability").intValue = maxDurability;

            SerializedProperty effect = so.FindProperty("consumable");
            effect.FindPropertyRelative("category").intValue = (int)category;
            effect.FindPropertyRelative("heal").intValue = heal;
            effect.FindPropertyRelative("water").floatValue = water;
            effect.FindPropertyRelative("energy").floatValue = energy;
            effect.FindPropertyRelative("waterCost").floatValue = waterCost;
            effect.FindPropertyRelative("energyCost").floatValue = energyCost;
            effect.FindPropertyRelative("cure").intValue = (int)cure;
            effect.FindPropertyRelative("cureStacks").intValue = cureStacks;
            effect.FindPropertyRelative("useCost").intValue = useCost;

            so.ApplyModifiedPropertiesWithoutUndo();

            return def;
        }

        /// <summary>체력 절반 · 수분 에너지 절반 · 상태 없음.</summary>
        private static ConsumableSubject Half(bool hasCureTarget = false, bool dead = false,
                                              int durability = 0)
        {
            return new ConsumableSubject(50, 100, dead, 50f, 100f, 50f, 100f,
                hasCureTarget, durability);
        }

        private static ConsumableSubject Full()
        {
            return new ConsumableSubject(100, 100, false, 100f, 100f, 100f, 100f, false);
        }

        // ── 회복 ──────────────────────────────────────────────────────

        [Test]
        public void 회복은_빈_만큼만_들어간다()
        {
            ItemDefinition kit = Create("kit", heal: 80);

            ConsumableOutcome outcome = ConsumableUse.Evaluate(kit, Half());

            Assert.IsTrue(outcome.Ok);

            // 100 − 50 = 50까지만. 남는 30은 버려지는 것이 아니라 아예 세지 않는다.
            Assert.AreEqual(50, outcome.Heal);
        }

        [Test]
        public void 체력이_가득하면_회복약은_쓰이지_않는다()
        {
            ItemDefinition kit = Create("kit", heal: 80);

            ConsumableOutcome outcome = ConsumableUse.Evaluate(kit, Full());

            Assert.IsFalse(outcome.Ok);
            Assert.AreEqual(ConsumableError.NothingToDo, outcome.Error);
        }

        // ── 해제 ──────────────────────────────────────────────────────

        [Test]
        public void 걸리지_않은_상태는_풀_수_없다()
        {
            ItemDefinition antidote = Create("antidote", ConsumableCategory.Cure,
                cure: StatusEffectType.Poison);

            ConsumableOutcome outcome = ConsumableUse.Evaluate(antidote, Half(hasCureTarget: false));

            Assert.IsFalse(outcome.Ok);
            Assert.AreEqual(ConsumableError.NothingToDo, outcome.Error);
        }

        [Test]
        public void 걸려_있으면_푼다()
        {
            ItemDefinition antidote = Create("antidote", ConsumableCategory.Cure,
                cure: StatusEffectType.Poison);

            ConsumableOutcome outcome = ConsumableUse.Evaluate(antidote, Half(hasCureTarget: true));

            Assert.IsTrue(outcome.Ok);
            Assert.AreEqual(StatusEffectType.Poison, outcome.Cure);
            Assert.AreEqual(0, outcome.Heal);
        }

        [Test]
        public void 해제와_회복이_같이_있으면_하나만_되어도_쓸_수_있다()
        {
            // 쑥 — 출혈 해제 + 소량 회복.
            ItemDefinition bandage = Create("bandage", ConsumableCategory.Cure,
                heal: 10, cure: StatusEffectType.Bleed);

            // 출혈은 없지만 체력이 비어 있다 → 회복만으로도 성립한다.
            ConsumableOutcome outcome = ConsumableUse.Evaluate(bandage, Half(hasCureTarget: false));

            Assert.IsTrue(outcome.Ok);
            Assert.AreEqual(10, outcome.Heal);
            Assert.AreEqual(StatusEffectType.None, outcome.Cure);
        }

        // ── 음료 · 음식 ───────────────────────────────────────────────

        [Test]
        public void 음료는_수분을_채우고_체력은_건드리지_않는다()
        {
            ItemDefinition water = Create("water", ConsumableCategory.Sustenance, water: 30f);

            ConsumableOutcome outcome = ConsumableUse.Evaluate(water, Half());

            Assert.IsTrue(outcome.Ok);
            Assert.AreEqual(30f, outcome.Water, 0.001f);

            // 【음식은 체력을 채우지 않는다.】 (결정 2-32)
            Assert.AreEqual(0, outcome.Heal);
        }

        [Test]
        public void 수분이_가득하면_넘치는_만큼은_세지_않는다()
        {
            ItemDefinition bottle = Create("bottle", ConsumableCategory.Sustenance, water: 40f);

            var nearlyFull = new ConsumableSubject(100, 100, false, 90f, 100f, 50f, 100f, false);

            ConsumableOutcome outcome = ConsumableUse.Evaluate(bottle, nearlyFull);

            Assert.IsTrue(outcome.Ok);
            Assert.AreEqual(10f, outcome.Water, 0.001f);
        }

        // ── 대가 ──────────────────────────────────────────────────────

        [Test]
        public void 대가는_채움과_별개로_그대로_실린다()
        {
            // 에너지 바 — 배고픔은 해결되나 갈증을 유발한다. [확인됨 — 덕코프]
            ItemDefinition bar = Create("bar", ConsumableCategory.Sustenance,
                energy: 25f, waterCost: 10f);

            ConsumableOutcome outcome = ConsumableUse.Evaluate(bar, Half());

            Assert.IsTrue(outcome.Ok);
            Assert.AreEqual(25f, outcome.Energy, 0.001f);
            Assert.AreEqual(10f, outcome.WaterCost, 0.001f);
        }

        [Test]
        public void 수분이_모자라도_사용을_막지_않는다()
        {
            // 덕코프의 주사약이 그렇다. 「무리해서 쓴다」가 선택지여야 한다.
            ItemDefinition shot = Create("shot", ConsumableCategory.Restore,
                heal: 20, waterCost: 50f);

            var dry = new ConsumableSubject(50, 100, false, 5f, 100f, 50f, 100f, false);

            ConsumableOutcome outcome = ConsumableUse.Evaluate(shot, dry);

            Assert.IsTrue(outcome.Ok);
            Assert.AreEqual(50f, outcome.WaterCost, 0.001f);
        }

        // ── 막는 경우 ─────────────────────────────────────────────────

        [Test]
        public void 소모품이_아니면_쓸_수_없다()
        {
            ItemDefinition scrap = Create("scrap", heal: 50, kind: ItemKind.Material);

            Assert.AreEqual(ConsumableError.NotConsumable,
                ConsumableUse.Evaluate(scrap, Half()).Error);
        }

        [Test]
        public void 효과가_비어_있으면_쓸_수_없다()
        {
            // 강화·저항은 분류만 있고 값이 없다 — 담을 축이 아직 없기 때문이다.
            ItemDefinition stim = Create("stim", ConsumableCategory.Boost);

            Assert.AreEqual(ConsumableError.NoEffect,
                ConsumableUse.Evaluate(stim, Half()).Error);

            Assert.IsFalse(stim.IsUsable);
        }

        [Test]
        public void 죽어_있으면_쓸_수_없다()
        {
            ItemDefinition kit = Create("kit", heal: 80);

            Assert.AreEqual(ConsumableError.Dead,
                ConsumableUse.Evaluate(kit, Half(dead: true)).Error);
        }

        [Test]
        public void 소모품이어도_효과가_있어야_쓸_수_있는_물건이다()
        {
            Assert.IsTrue(Create("kit", heal: 10).IsUsable);
            Assert.IsFalse(Create("empty", ConsumableCategory.Ward).IsUsable);
        }

        // ── 충전(내구도)형 ────────────────────────────────────────────
        //
        // 덕코프의 구급상자는 한 번 쓰고 사라지지 않는다.
        // 소형 125/25 = 5회 · 환단 175/25 = 7회 · 대형 400/40 = 10회.
        // [확인됨 — 아이템 #15 · #16 · #17]

        [Test]
        public void 충전이_남아_있으면_쓸_수_있고_닳을_양이_실린다()
        {
            ItemDefinition kit = Create("medkit", heal: 20,
                useCost: 25, maxDurability: 175);

            ConsumableOutcome outcome = ConsumableUse.Evaluate(kit, Half(durability: 175));

            Assert.IsTrue(outcome.Ok);
            Assert.AreEqual(20, outcome.Heal);
            Assert.AreEqual(25, outcome.UseCost);
        }

        [Test]
        public void 충전이_모자라면_빈_통이다()
        {
            ItemDefinition kit = Create("medkit", heal: 20,
                useCost: 25, maxDurability: 175);

            // 24는 한 번 값(25)에 모자란다. 반만 쓰이게 두지 않는다 —
            // 「조금 회복됐는데 왜 사라졌지」가 생긴다.
            ConsumableOutcome outcome = ConsumableUse.Evaluate(kit, Half(durability: 24));

            Assert.IsFalse(outcome.Ok);
            Assert.AreEqual(ConsumableError.Empty, outcome.Error);
        }

        [Test]
        public void 충전형이_아니면_닳을_양이_0이다()
        {
            // 0은 「아이템 하나가 통째로 사라진다」는 뜻이다.
            ItemDefinition aspirin = Create("aspirin", heal: 10);

            Assert.AreEqual(0, ConsumableUse.Evaluate(aspirin, Half()).UseCost);
        }

        // ── 중첩 단위 해제 ────────────────────────────────────────────

        [Test]
        public void 덜어_낼_중첩_수가_실린다()
        {
            // 소형은 출혈 1중첩, 환단은 2중첩. 싼 도구가 비싼 도구와
            // 같은 일을 하면 비싼 쪽을 살 이유가 없다.
            ItemDefinition small = Create("small", cure: StatusEffectType.Bleed, cureStacks: 1);
            ItemDefinition big = Create("big", cure: StatusEffectType.Bleed, cureStacks: 2);

            Assert.AreEqual(1, ConsumableUse.Evaluate(small, Half(hasCureTarget: true)).CureStacks);
            Assert.AreEqual(2, ConsumableUse.Evaluate(big, Half(hasCureTarget: true)).CureStacks);
        }

        [Test]
        public void 걸리지_않았으면_중첩_수도_0이다()
        {
            ItemDefinition small = Create("small", cure: StatusEffectType.Bleed,
                cureStacks: 1, heal: 5);

            ConsumableOutcome outcome = ConsumableUse.Evaluate(small, Half(hasCureTarget: false));

            Assert.IsTrue(outcome.Ok);
            Assert.AreEqual(0, outcome.CureStacks);
        }
    }
}
