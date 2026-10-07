using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>Cooking Data — 영감의 가게에서 재료로 이미 있는 음식을 만든다 (결정 2-64 · CookingTable).</summary>
    public class CookingTests
    {
        private static ItemCatalog Catalog => ItemCatalog.Load();

        [Test]
        public void 만드는_것은_이미_있는_음식뿐이고_재료도_카탈로그에_있다()
        {
            ItemCatalog catalog = Catalog;

            foreach (Recipe r in CookingTable.All)
            {
                ItemDefinition output = catalog.Find(r.OutputId);
                Assert.IsNotNull(output, r.OutputId);
                Assert.IsTrue(r.OutputId.StartsWith("con_"), $"{r.OutputId} — 소모품(음식)이어야 한다");

                foreach (MaterialCost c in r.Inputs)
                    Assert.IsNotNull(catalog.Find(c.ItemId), $"{r.OutputId}: 재료 {c.ItemId}");
            }

            Assert.IsNull(CookingTable.ForOutput("con_water"), "샘물은 길어 오는 것");
            Assert.IsNull(CookingTable.ForOutput("con_whisky"), "막걸리는 빚는 것");
            Assert.AreEqual(4, CookingTable.All.Count);
        }

        [Test]
        public void 만들면_엽전을_아낀다_재료_값의_합이_음식_값보다_작다()
        {
            ItemCatalog catalog = Catalog;

            foreach (Recipe r in CookingTable.All)
            {
                int inputs = r.Inputs.Sum(c => catalog.Find(c.ItemId).BaseValue * c.Count);
                Assert.Less(inputs, catalog.Find(r.OutputId).BaseValue * r.OutputCount, r.OutputId);
            }
        }

        [Test]
        public void 잡화_가게를_놓아야_열린다()
        {
            Assert.IsFalse(CookingTable.IsUnlocked(_ => false));
            Assert.IsTrue(CookingTable.IsUnlocked(id => id == BuildingTable.GeneralStore));
            Assert.AreEqual(CookError.Locked, CookingTable.CanCook(CookingTable.All[0], _ => 99, false));
        }

        [Test]
        public void 재료는_창고에서_먼저_빼고_음식은_가방으로()
        {
            ItemCatalog catalog = Catalog;
            var stash = new Inventory(40, 999f);
            var bag = new Inventory(20, 30f);
            ItemDefinition rice = catalog.Find(CookingTable.Rice);
            ItemDefinition nurungji = catalog.Find("con_canned");

            stash.TryAdd(rice, 1);
            bag.TryAdd(rice, 3);

            Assert.AreEqual(CookError.None, CookingTable.Cook(CookingTable.ForOutput("con_canned"), stash, bag, nurungji, true));
            Assert.AreEqual(0, BuildingState.CountIn(stash, null, CookingTable.Rice), "창고 먼저");
            Assert.AreEqual(2, BuildingState.CountIn(null, bag, CookingTable.Rice));
            Assert.AreEqual(1, BuildingState.CountIn(null, bag, "con_canned"));

            Assert.AreEqual(CookError.None, CookingTable.Cook(CookingTable.ForOutput("con_canned"), stash, bag, nurungji, true));
            Assert.AreEqual(CookError.MissingMaterial, CookingTable.Cook(CookingTable.ForOutput("con_canned"), stash, bag, nurungji, true));
        }
    }
}
