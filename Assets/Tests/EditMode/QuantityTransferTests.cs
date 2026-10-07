using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>수량을 골라 옮기기 · 팔기 (결정 2-77 · QuantityTransfer).</summary>
    public class QuantityTransferTests
    {
        private static ItemDefinition Item(string id, int value = 10, int stackMax = 10)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            def.name = id;
            var so = new SerializedObject(def);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;
            so.FindProperty("kind").intValue = (int)ItemKind.Material;
            so.FindProperty("weight").floatValue = 0.1f;
            so.FindProperty("slotSize").intValue = 1;
            so.FindProperty("stackMax").intValue = stackMax;
            so.FindProperty("baseValue").intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            return def;
        }

        [Test]
        public void 일부만_옮기면_남은_것은_그_칸에_남는다()
        {
            ItemDefinition rice = Item("rice");
            var bag = new Inventory(10, 99f);
            var stash = new Inventory(10, 99f);
            bag.TryAdd(rice, 7);

            Assert.IsTrue(QuantityTransfer.Move(bag, stash, bag.Stacks[0], 3));
            Assert.AreEqual(4, bag.CountOf(rice));
            Assert.AreEqual(3, stash.CountOf(rice));

            Assert.IsTrue(QuantityTransfer.Move(bag, stash, bag.Stacks[0], 99), "전부 = 칸째");
            Assert.AreEqual(0, bag.CountOf(rice));
            Assert.AreEqual(7, stash.CountOf(rice));
        }

        [Test]
        public void 전리품에서_일부만_줍는다()
        {
            ItemDefinition rice = Item("rice");
            var box = new LootContainer(4);
            box.TryPut(rice, 5);
            var bag = new Inventory(10, 99f);

            Assert.IsTrue(QuantityTransfer.Take(box, 0, bag, 2));
            Assert.AreEqual(2, bag.CountOf(rice));
            Assert.AreEqual(3, box.Get(0).Count);

            Assert.IsTrue(QuantityTransfer.Take(box, 0, bag, 3));
            Assert.IsNull(box.Get(0));
        }

        [Test]
        public void 일부만_팔면_한_개_값_곱하기_개수()
        {
            ItemDefinition rice = Item("rice", value: 20);
            var bag = new Inventory(10, 99f);
            bag.TryAdd(rice, 6);
            int gold = 0;

            Assert.AreEqual(TradeError.None, QuantityTransfer.Sell(bag.Stacks[0], 2, bag, 0f, ref gold, out int earned));
            Assert.AreEqual(20, earned, "가치 20의 절반 × 2");
            Assert.AreEqual(4, bag.CountOf(rice));

            Assert.AreEqual(TradeError.None, QuantityTransfer.Sell(bag.Stacks[0], 4, bag, 0f, ref gold, out earned));
            Assert.AreEqual(0, bag.CountOf(rice));
            Assert.AreEqual(60, gold);
        }
    }
}
