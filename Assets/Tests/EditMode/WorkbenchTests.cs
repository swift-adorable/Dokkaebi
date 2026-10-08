using System.Collections.Generic;
using NUnit.Framework;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 【작업대 제작 Ⅰ ~ Ⅳ】 (결정 2-95 · Bunker 5절 「만들려면 먼저 바쳐야 한다」)
    ///   1. 단계는 엽전 + 재료로 하나씩 오른다
    ///   2. Ⅱ부터는 바쳐야 열리고, 바친 것은 사라진다
    ///   3. 만들면 재료 · 엽전이 들고 물건이 나온다 · 자리가 없으면 아무것도 쓰지 않는다
    ///   4. 표가 가리키는 아이템이 모두 있다 · 방어 관통탄은 같은 무기의 통에 들어간다
    /// </summary>
    public class WorkbenchTests
    {
        private Dictionary<string, int> stock;
        private int Count(string id) => stock.TryGetValue(id, out int n) ? n : 0;

        private int Remove(string id, int count)
        {
            int have = Count(id);
            int removed = System.Math.Min(have, count);
            stock[id] = have - removed;
            return removed;
        }

        [SetUp]
        public void Fill() => stock = new Dictionary<string, int>();

        private void Give(string id, int count) => stock[id] = Count(id) + count;

        // ── 1. 단계 ──────────────────────────────────────────────────

        [Test]
        public void 작업대를_짓기_전에는_아무것도_못_한다()
        {
            var state = new WorkbenchState();
            CraftRecipe arrow = WorkbenchTable.Find(AmmoTable.Arrow);

            Assert.AreEqual(CraftError.NoWorkbench, state.CanCraft(arrow, false, 9999, Count));
            Assert.AreEqual(CraftError.NoWorkbench, state.CanUpgrade(false, 9999, Count));
        }

        [Test]
        public void 단계는_값을_치르고_하나씩_오른다()
        {
            var state = new WorkbenchState();
            StageCost cost = WorkbenchTable.CostToReach(2);
            int gold = cost.Gold - 1;

            foreach (MaterialCost c in cost.Materials) Give(c.ItemId, c.Count);

            Assert.AreEqual(CraftError.NotEnoughGold, state.Upgrade(true, ref gold, Count, Remove));

            gold = cost.Gold;
            Assert.AreEqual(CraftError.None, state.Upgrade(true, ref gold, Count, Remove));
            Assert.AreEqual(2, state.Stage);
            Assert.AreEqual(0, gold);
            foreach (MaterialCost c in cost.Materials)
                Assert.AreEqual(0, Count(c.ItemId), "재료가 남았습니다.");

            state.Restore(WorkbenchTable.MaxStage, null);
            Assert.AreEqual(CraftError.MaxStage, state.CanUpgrade(true, 99999, Count));
        }

        // ── 2. 바치기 ────────────────────────────────────────────────

        [Test]
        public void 일단계는_바치지_않아도_열려_있다()
        {
            foreach (CraftRecipe r in WorkbenchTable.InStage(1))
                Assert.IsFalse(r.NeedsUnlock, $"{r.OutputId}를 바쳐야 합니다.");
        }

        [Test]
        public void 이단계부터는_바쳐야_열리고_바친_것은_사라진다()
        {
            foreach (CraftRecipe r in WorkbenchTable.All)
                if (r.Stage >= 2)
                    Assert.IsTrue(r.NeedsUnlock, $"{r.OutputId}가 바치지 않고 열립니다.");

            var state = new WorkbenchState();
            state.Restore(2, null);
            CraftRecipe helmet = WorkbenchTable.Find("arm_head_t2");

            Assert.AreEqual(CraftError.NotUnlocked, state.CanCraft(helmet, true, 9999, Count));
            Assert.AreEqual(CraftError.MissingOffering, state.CanUnlock(helmet, true, Count));

            Give("arm_head_t2", 1);
            Assert.AreEqual(CraftError.None, state.Unlock(helmet, true, Count, Remove));
            Assert.AreEqual(0, Count("arm_head_t2"), "견본이 남았습니다 — 바친 것은 사라져야 합니다.");
            Assert.IsTrue(state.IsUnlocked(helmet));
            Assert.AreEqual(CraftError.AlreadyUnlocked, state.CanUnlock(helmet, true, Count));
        }

        [Test]
        public void 단계가_모자라면_바칠_수도_없다()
        {
            var state = new WorkbenchState();
            CraftRecipe helmet = WorkbenchTable.Find("arm_head_t2");
            Give("arm_head_t2", 1);

            Assert.AreEqual(CraftError.StageLocked, state.CanUnlock(helmet, true, Count));
        }

        [Test]
        public void 사단계_무기는_도면과_바로_아래_티어_견본을_바친다()
        {
            CraftRecipe bow4 = WorkbenchTable.Find("wpn_t4_breaker");
            var ids = new List<string>();
            foreach (MaterialCost c in bow4.UnlockCost) ids.Add(c.ItemId);

            CollectionAssert.Contains(ids, WorkbenchTable.BlueprintOf(WeaponKind.Bow));
            CollectionAssert.Contains(ids, "wpn_t3_acid", "흑각궁의 견본은 향각궁이어야 합니다.");

            Assert.AreEqual("wpn_t4_breaker", WorkbenchTable.SampleFor(Row("wpn_t5_rail")));
            Assert.AreEqual("wpn_t2_gwoljangno", WorkbenchTable.SampleFor(Row("wpn_t5_gangno")), "강노 아래는 궐장노뿐입니다.");
            Assert.IsNotNull(WorkbenchTable.SampleFor(Row("wpn_t4_sosingijeon")), "소신기전의 견본이 없습니다.");
        }

        private static WorkbenchTable.WeaponRow Row(string id)
        {
            foreach (WorkbenchTable.WeaponRow w in WorkbenchTable.Weapons)
                if (w.Id == id) return w;
            Assert.Fail(id);
            return default;
        }

        // ── 3. 만들기 ────────────────────────────────────────────────

        [Test]
        public void 만들면_재료와_엽전이_들고_물건이_나온다()
        {
            var state = new WorkbenchState();
            CraftRecipe arrow = WorkbenchTable.Find(AmmoTable.Arrow);
            foreach (MaterialCost c in arrow.Inputs) Give(c.ItemId, c.Count);
            int gold = 0;
            string made = null;
            int madeCount = 0;

            CraftError error = state.Craft(arrow, true, ref gold, Count, Remove,
                (id, n) => true, (id, n) => { made = id; madeCount = n; });

            Assert.AreEqual(CraftError.None, error);
            Assert.AreEqual(AmmoTable.Arrow, made);
            Assert.AreEqual(arrow.OutputCount, madeCount);
            foreach (MaterialCost c in arrow.Inputs)
                Assert.AreEqual(0, Count(c.ItemId));
        }

        [Test]
        public void 자리가_없으면_아무것도_쓰지_않는다()
        {
            var state = new WorkbenchState();
            CraftRecipe arrow = WorkbenchTable.Find(AmmoTable.Arrow);
            foreach (MaterialCost c in arrow.Inputs) Give(c.ItemId, c.Count);
            int gold = 0;

            CraftError error = state.Craft(arrow, true, ref gold, Count, Remove, (id, n) => false, (id, n) => { });

            Assert.AreEqual(CraftError.NoRoom, error);
            foreach (MaterialCost c in arrow.Inputs)
                Assert.AreEqual(c.Count, Count(c.ItemId), "자리가 없는데 재료가 사라졌습니다.");
        }

        [Test]
        public void 세이브를_오간다()
        {
            var state = new WorkbenchState();
            state.Restore(3, new[] { "arm_head_t2", "ammo_arrow_ap" });

            var copy = new WorkbenchState();
            copy.Restore(state.Stage, state.Unlocked);

            Assert.AreEqual(3, copy.Stage);
            Assert.IsTrue(copy.IsUnlocked(WorkbenchTable.Find("arm_head_t2")));

            copy.Restore(99, null);
            Assert.AreEqual(WorkbenchTable.MaxStage, copy.Stage, "망가진 단계가 잘리지 않았습니다.");
        }

        // ── 4. 표 ───────────────────────────────────────────────────

        [Test]
        public void 단계마다_만들_것이_있다()
        {
            for (int stage = 1; stage <= WorkbenchTable.MaxStage; stage++)
                CollectionAssert.IsNotEmpty(new List<CraftRecipe>(WorkbenchTable.InStage(stage)), $"{stage}단계가 비었습니다.");
        }

        [Test]
        public void 표가_가리키는_아이템이_모두_있다()
        {
            ItemCatalog catalog = ItemCatalog.Load();
            Assert.IsNotNull(catalog, "Resources/ItemCatalog가 없습니다.");

            foreach (CraftRecipe r in WorkbenchTable.All)
            {
                Assert.IsNotNull(catalog.Find(r.OutputId), $"만들 것 {r.OutputId}가 없습니다.");

                foreach (MaterialCost c in r.Inputs)
                    Assert.IsNotNull(catalog.Find(c.ItemId), $"{r.OutputId}의 재료 {c.ItemId}가 없습니다.");

                foreach (MaterialCost c in r.UnlockCost)
                    Assert.IsNotNull(catalog.Find(c.ItemId), $"{r.OutputId}의 바칠 것 {c.ItemId}가 없습니다.");
            }

            for (int stage = 2; stage <= WorkbenchTable.MaxStage; stage++)
                foreach (MaterialCost c in WorkbenchTable.CostToReach(stage).Materials)
                    Assert.IsNotNull(catalog.Find(c.ItemId), $"{stage}단계 재료 {c.ItemId}가 없습니다.");
        }

        [Test]
        public void 무기_표가_에셋과_같다()
        {
            ItemCatalog catalog = ItemCatalog.Load();

            foreach (WorkbenchTable.WeaponRow w in WorkbenchTable.Weapons)
            {
                var weapon = catalog.Find(w.Id) as WeaponDefinition;
                Assert.IsNotNull(weapon, w.Id);
                Assert.AreEqual(w.Kind, weapon.WeaponType, $"{w.Id} 종류가 다릅니다.");
                Assert.AreEqual(w.Tier, weapon.Tier, $"{w.Id} 티어가 다릅니다.");
            }
        }

        [Test]
        public void 방어_관통탄은_같은_무기의_통에_들어가고_더_뚫는다()
        {
            foreach (AmmoInfo a in AmmoTable.All)
            {
                if (!a.IsArmourPiercing)
                    continue;

                Assert.IsTrue(AmmoTable.Accepts(a.Family, a.Id), a.Id);
                Assert.IsFalse(AmmoTable.Accepts(AmmoTable.Arrow == a.Family ? AmmoTable.Shot : AmmoTable.Arrow, a.Id),
                    $"{a.Id}가 다른 무기에 들어갑니다.");
                Assert.Greater(AmmoTable.PenetrationBonusOf(a.Id), 0);
                Assert.Less(AmmoTable.DamageScaleOf(a.Id), 1f, "방어 관통탄이 손해 없이 셉니다.");
                Assert.AreEqual(AmmoTable.CapacityOf(a.Family), a.Capacity, "통에 담기는 수가 다릅니다.");
                Assert.GreaterOrEqual(a.FirstTier, AmmoTable.PiercingFirstChapter, "방어 관통탄이 너무 일찍 나옵니다.");
            }

            Assert.AreEqual(0, AmmoTable.PenetrationBonusOf(AmmoTable.Arrow));
            Assert.AreEqual(1f, AmmoTable.DamageScaleOf(null), 0.0001f);
        }

        [Test]
        public void 방어_관통탄은_가게에서_팔지_않는다()
        {
            foreach (ShopKind kind in ShopTable.All)
                foreach (ShopEntry e in ShopTable.For(kind))
                    Assert.IsFalse(e.ItemId.EndsWith(AmmoTable.PiercingSuffix), $"{e.ItemId}를 팝니다 — 작업대 Ⅲ에서만 나와야 합니다.");
        }
    }
}
