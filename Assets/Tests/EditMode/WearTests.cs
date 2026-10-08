using NUnit.Framework;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 【장비 닳기 · 수리 · 분해】 (결정 2-96 · Equipment 4절)
    ///   1. 작은 닳기를 모았다가 1이 되면 깎는다
    ///   2. 수리는 엽전 · 티어 4 이상만 최대 내구도가 조금 준다 · 미리 알려 준다 · 세이브에 남는다
    ///   3. 분해는 장비만 · 제작법 재료의 절반 · 닳을수록 덜
    /// </summary>
    public class WearTests
    {
        private static ItemDefinition Find(string id)
        {
            ItemDefinition d = ItemCatalog.Load()?.Find(id);
            Assert.IsNotNull(d, id);
            return d;
        }

        [Test]
        public void 작은_닳기를_모았다가_깎는다()
        {
            var acc = new WearAccumulator();
            int total = 0;
            for (int i = 0; i < 100; i++)
                total += acc.Add(WearTable.WeaponPerShot);

            Assert.AreEqual(6, total, "100발에 6이 닳아야 합니다 (0.06 × 100).");
            Assert.AreEqual(0, acc.Add(0f));
        }

        [Test]
        public void 원거리는_머리_근접은_몸통이_닳는다()
        {
            Assert.AreEqual(EquipmentSlot.Head, WearTable.ArmourSlotFor(HitKind.Ranged));
            Assert.AreEqual(EquipmentSlot.Body, WearTable.ArmourSlotFor(HitKind.Melee));
        }

        [Test]
        public void 수리는_닳은_만큼_엽전이고_새것은_고칠_게_없다()
        {
            ItemDefinition bow = Find("wpn_t1_pipe");
            var fresh = new ItemStack(bow);
            Assert.AreEqual(0, WearTable.RepairCost(fresh));
            Assert.IsFalse(WearTable.NeedsRepair(fresh));

            var half = new ItemStack(bow, 1, bow.MaxDurability / 2);
            var worn = new ItemStack(bow, 1, 0);
            Assert.Greater(WearTable.RepairCost(half), 0);
            Assert.Greater(WearTable.RepairCost(worn), WearTable.RepairCost(half), "더 닳았는데 싸다.");
        }

        [Test]
        public void 티어_4_이상만_고칠_때마다_최대_내구도가_준다()
        {
            ItemDefinition low = Find("wpn_t3_acid");
            var lowStack = new ItemStack(low, 1, 1);
            Assert.AreEqual(lowStack.MaxDurability, WearTable.MaxAfterRepair(lowStack), "티어 3인데 상한이 줄어든다.");
            lowStack.Repair(lowStack.MaxDurability);
            Assert.AreEqual(low.MaxDurability, lowStack.MaxDurability);

            ItemDefinition high = Find("wpn_t4_breaker");
            var highStack = new ItemStack(high, 1, 1);
            int expected = WearTable.MaxAfterRepair(highStack);
            Assert.Less(expected, high.MaxDurability, "티어 4인데 상한이 그대로다.");

            highStack.Repair(highStack.MaxDurability);
            Assert.AreEqual(expected, highStack.MaxDurability, "미리 알려 준 값과 다르다.");
            Assert.AreEqual(expected, highStack.Durability, "고쳤는데 가득 차지 않았다.");
        }

        [Test]
        public void 줄어든_최대_내구도가_세이브에_남는다()
        {
            ItemDefinition high = Find("wpn_t4_breaker");
            var stack = new ItemStack(high, 1, 1);
            stack.Repair(stack.MaxDurability);

            SavedItem saved = SaveManager.ToSavedItem(stack);
            var back = new ItemStack(high, saved.count, saved.durability, saved.maxDurability);

            Assert.AreEqual(stack.MaxDurability, back.MaxDurability);
            Assert.AreEqual(stack.Durability, back.Durability);

            SavedItem fresh = SaveManager.ToSavedItem(new ItemStack(high));
            Assert.AreEqual(-1, fresh.maxDurability, "새것인데 상한을 적었다.");
        }

        [Test]
        public void 분해는_장비만_재료_절반쯤_닳을수록_덜()
        {
            Assert.IsFalse(WearTable.CanDismantle(Find("con_water")), "물병이 분해된다.");
            Assert.IsFalse(WearTable.CanDismantle(Find(AmmoTable.Arrow)));
            Assert.IsTrue(WearTable.CanDismantle(Find("arm_head_t2")));

            ItemDefinition helmet = Find("arm_head_t2");
            CraftRecipe recipe = WorkbenchTable.Find("arm_head_t2");
            var fresh = WearTable.DismantleYield(new ItemStack(helmet));
            var broken = WearTable.DismantleYield(new ItemStack(helmet, 1, 0));

            int Sum(System.Collections.Generic.List<MaterialCost> l) { int n = 0; foreach (var c in l) n += c.Count; return n; }
            int recipeTotal = 0;
            foreach (MaterialCost c in recipe.Inputs) recipeTotal += c.Count;

            Assert.LessOrEqual(Sum(fresh), recipeTotal / 2 + 1, "만드는 것보다 많이 나온다.");
            Assert.Less(Sum(broken), Sum(fresh), "망가진 것이 덜 나오지 않는다.");
            Assert.Greater(Sum(broken), 0, "망가져도 쇠붙이 하나는 나와야 한다.");
        }
    }
}
