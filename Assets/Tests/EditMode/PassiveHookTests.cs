using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>패시브 효과를 게임에 잇는다 (Audit A9 · 결정 2-74).</summary>
    public class PassiveHookTests
    {
        private static ItemDefinition Item(string id, int value = 10, ItemKind kind = ItemKind.Material)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            def.name = id;
            var so = new SerializedObject(def);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;
            so.FindProperty("kind").intValue = (int)kind;
            so.FindProperty("weight").floatValue = 0.1f;
            so.FindProperty("slotSize").intValue = 1;
            so.FindProperty("stackMax").intValue = 10;
            so.FindProperty("baseValue").intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            return def;
        }

        [Test]
        public void 안전_칸은_가방_맨_앞_n칸이_남는다()
        {
            var bag = new Inventory(10, 99f);
            bag.TryAdd(Item("a"), 1);
            bag.TryAdd(Item("b"), 1);
            bag.TryAdd(Item("c"), 1);

            int lost = bag.DropOnDeath(2);

            Assert.AreEqual(1, lost);
            Assert.AreEqual(2, bag.Stacks.Count);
            Assert.AreEqual("a", bag.Stacks[0].Definition.Id);
            Assert.AreEqual("b", bag.Stacks[1].Definition.Id);
            Assert.AreEqual(2, bag.DropOnDeath(0), "패시브가 없으면 전부 잃는다");
        }

        [Test]
        public void 희귀_드롭은_무언가_나올_확률을_배로_만든다()
        {
            var entries = new List<LootEntry> { new(Item("x"), 40), new(null, 60) };

            // q = 0.4 → ×1.12 = 0.448. 빈손 0.6 중 p를 되돌린다: 0.4 + 0.6p = 0.448 → p = 0.08
            Assert.AreEqual(0.08, LootRoller.RerollChance(entries, null, 1.12f), 1e-6);
            Assert.AreEqual(0d, LootRoller.RerollChance(entries, null, 1f));

            var random = new System.Random(3);
            int found = 0, rolls = 20000;
            var box = new LootContainer(rolls);
            found = LootRoller.Roll(entries, rolls, random, box, null, 1.12f);
            Assert.AreEqual(0.448, found / (double)rolls, 0.015);
        }

        [Test]
        public void 값_깎기와_재고_더하기()
        {
            ItemDefinition item = Item("x", 100, ItemKind.Consumable);
            var entry = new ShopEntry("x", 3, 1f);

            Assert.AreEqual(100, TradeRules.BuyPrice(item, entry));
            Assert.AreEqual(80, TradeRules.BuyPrice(item, entry, 20f));
            Assert.AreEqual(50, TradeRules.BuyPrice(item, entry, 90f), "최대 50%");

            var shop = new ShopState();
            shop.Restock(new[] { entry }, 2);
            Assert.AreEqual(5, shop.Remaining("x"));
        }

        [Test]
        public void 준비_중인_패시브는_배울_수_없다()
        {
            Assert.IsTrue(PassiveEffectInfo.IsPending(PassiveEffectType.CraftBench));

            // 9단계 맵에 이었다 (결정 2-93) — 이제 배울 수 있다.
            Assert.IsFalse(PassiveEffectInfo.IsPending(PassiveEffectType.MapLoot));
            Assert.IsFalse(PassiveEffectInfo.IsPending(PassiveEffectType.ExtractMark));
            Assert.IsFalse(PassiveEffectInfo.IsPending(PassiveEffectType.CorpseRecovery));
            Assert.IsFalse(PassiveEffectInfo.IsPending(PassiveEffectType.SafeSlots));
            Assert.AreEqual("옛 도면", PassiveEffectInfo.Name(PassiveEffectType.CraftBench));
        }
    }
}
