using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Dokkaebi.Tests
{
    /// <summary>Cooking Data — 부뚜막에서 재료로 이미 있는 음식을 만든다 (결정 2-64 · 2-71 · 2-73 · CookingTable · IngredientTable).</summary>
    public class CookingTests
    {
        private static ItemCatalog Catalog => ItemCatalog.Load();

        private static int ValueOf(ItemCatalog catalog, string id)
            => IngredientTable.IsTag(id)
                ? IngredientTable.Members(id).Min(m => catalog.Find(m).BaseValue)
                : catalog.Find(id).BaseValue;

        [Test]
        public void 레시피는_스물두_가지_작음8_보통9_큼5()
        {
            Assert.AreEqual(22, CookingTable.All.Count);
            Assert.AreEqual(8, CookingTable.All.Count(r => r.Tier == CookTier.Small));
            Assert.AreEqual(9, CookingTable.All.Count(r => r.Tier == CookTier.Medium));
            Assert.AreEqual(5, CookingTable.All.Count(r => r.Tier == CookTier.Large));
            Assert.AreEqual(22, CookingTable.All.Select(r => r.OutputId).Distinct().Count(), "음식 하나에 레시피 하나");
        }

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
                    foreach (string member in IngredientTable.Members(c.ItemId))
                        Assert.IsNotNull(catalog.Find(member), $"{r.OutputId}: 재료 {member}");
            }

            Assert.IsNull(CookingTable.ForOutput("con_water"), "물병은 샘에서 긷는 것");
            Assert.IsNull(CookingTable.ForOutput("con_whisky"), "막걸리는 빚는 것");
        }

        [Test]
        public void 작은_음식은_공용_재료와_물만으로_만든다()
        {
            foreach (Recipe r in CookingTable.All.Where(r => r.Tier == CookTier.Small))
                foreach (MaterialCost c in r.Inputs)
                {
                    if (c.ItemId == SpringTable.WaterId)
                        continue;

                    Ingredient i = IngredientTable.Find(c.ItemId);
                    Assert.IsNotNull(i, $"{r.OutputId}: {c.ItemId}");
                    Assert.IsTrue(i.IsCommon, $"{r.OutputId}: {i.Name}은(는) 공용이 아니다");
                }

            foreach (Recipe r in CookingTable.All.Where(r => r.Tier != CookTier.Small))
                Assert.IsTrue(r.Inputs.Any(c => IngredientTable.IsTag(c.ItemId)
                                                || IngredientTable.Find(c.ItemId)?.IsCommon == false),
                    $"{r.OutputId} — 보통 · 큼은 장 재료가 하나 이상 들어간다");
        }

        [Test]
        public void 모든_재료는_어느_레시피에든_쓰인다()
        {
            var used = new HashSet<string>(CookingTable.All
                .SelectMany(r => r.Inputs)
                .SelectMany(c => IngredientTable.Members(c.ItemId)));

            foreach (Ingredient i in IngredientTable.All)
                Assert.IsTrue(used.Contains(i.Id), $"{i.Name} — 쓰이지 않는 재료는 뺀다 (결정 2-73)");
        }

        [Test]
        public void 만들면_엽전을_아낀다_재료_값의_합이_음식_값보다_작다()
        {
            ItemCatalog catalog = Catalog;

            foreach (Recipe r in CookingTable.All)
            {
                int inputs = r.Inputs.Sum(c => ValueOf(catalog, c.ItemId) * c.Count);
                Assert.Less(inputs, catalog.Find(r.OutputId).BaseValue * r.OutputCount, r.OutputId);
            }
        }

        [Test]
        public void 물을_넣은_음료는_물병_물보다_목을_더_축인다()
        {
            ItemCatalog catalog = Catalog;
            float water = catalog.Find(SpringTable.WaterId).Consumable.Water;
            Assert.AreEqual(20f, water, "물병 = 수분 20 (결정 2-73)");

            foreach (Recipe r in CookingTable.All.Where(r => r.Inputs.Any(c => c.ItemId == SpringTable.WaterId)))
                Assert.Greater(catalog.Find(r.OutputId).Consumable.Water, water, r.OutputId);
        }

        [Test]
        public void 장_재료는_그_장과_같은_계절의_뒤_장에서만_나온다()
        {
            Assert.IsTrue(IngredientTable.DropsIn(IngredientTable.Rice, 0), "공용은 0장에서도");
            Assert.IsTrue(IngredientTable.DropsIn(IngredientTable.Berry, 6), "산열매는 공용");
            Assert.IsTrue(IngredientTable.DropsIn("scrap_metal", 3), "재료가 아닌 것은 그대로");

            Assert.IsTrue(IngredientTable.DropsIn(IngredientTable.RiceCake, 1));
            Assert.IsTrue(IngredientTable.DropsIn(IngredientTable.RiceCake, 5), "겨울 — 1장 재료가 5장에서도");
            Assert.IsFalse(IngredientTable.DropsIn(IngredientTable.Venison, 1), "거꾸로는 아니다");
            Assert.IsTrue(IngredientTable.DropsIn(IngredientTable.GlutinousRice, 6), "봄 — 2장 재료가 6장에서도");
            Assert.IsFalse(IngredientTable.DropsIn(IngredientTable.Honey, 2));
            Assert.IsFalse(IngredientTable.DropsIn(IngredientTable.GlutinousRice, 0), "0장(봄)은 공용만");
            Assert.IsFalse(IngredientTable.DropsIn(IngredientTable.Ginseng, 4));
            Assert.IsTrue(IngredientTable.DropsIn(IngredientTable.Ginger, 1), "생강 = 1장 재료");
        }

        [Test]
        public void 산열매는_몬스터가_아니라_나무에서_딴다()
        {
            Assert.AreEqual(0, IngredientTable.Find(IngredientTable.Berry).DropWeight);
            Assert.IsNotNull(Catalog.Find(IngredientTable.Berry), "아이템은 있다");

            LootTable common = UnityEditor.AssetDatabase.LoadAssetAtPath<LootTable>(
                "Assets/Data/ScriptableObjects/Loot/LootTable_Common.asset");
            Assert.IsNotNull(common);
            foreach (LootEntry e in common.Entries)
                Assert.AreNotEqual(IngredientTable.Berry, e.item != null ? e.item.Id : null, "전리품 표에 없다 (결정 2-76)");
        }

        [Test]
        public void 전리품_추첨은_장이_맞지_않는_재료를_건너뛴다()
        {
            ItemCatalog catalog = Catalog;
            var entries = new List<LootEntry>
            {
                new(catalog.Find(IngredientTable.Honey), 1000),
                new(catalog.Find(IngredientTable.Rice), 1),
            };

            var random = new System.Random(7);
            for (int i = 0; i < 50; i++)
            {
                Assert.IsTrue(LootRoller.TryPick(entries, random, out LootEntry picked,
                    item => IngredientTable.DropsIn(item.Id, 1)));
                Assert.AreEqual(IngredientTable.Rice, picked.item.Id, "1장에서 꿀은 나오지 않는다");
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
            ItemDefinition rice = catalog.Find(IngredientTable.Rice);
            ItemDefinition nurungji = catalog.Find("con_canned");

            stash.TryAdd(rice, 1);
            bag.TryAdd(rice, 3);

            Assert.AreEqual(CookError.None, CookingTable.Cook(CookingTable.ForOutput("con_canned"), stash, bag, nurungji, true));
            Assert.AreEqual(0, BuildingState.CountIn(stash, null, IngredientTable.Rice), "창고 먼저");
            Assert.AreEqual(2, BuildingState.CountIn(null, bag, IngredientTable.Rice));
            Assert.AreEqual(1, BuildingState.CountIn(null, bag, "con_canned"));

            Assert.AreEqual(CookError.None, CookingTable.Cook(CookingTable.ForOutput("con_canned"), stash, bag, nurungji, true));
            Assert.AreEqual(CookError.MissingMaterial, CookingTable.Cook(CookingTable.ForOutput("con_canned"), stash, bag, nurungji, true));
        }

        [Test]
        public void 산적은_고기를_섞어_써도_된다()
        {
            ItemCatalog catalog = Catalog;
            var stash = new Inventory(40, 999f);
            var bag = new Inventory(20, 30f);
            stash.TryAdd(catalog.Find(IngredientTable.Pheasant), 1);
            bag.TryAdd(catalog.Find(IngredientTable.Boar), 1);

            Recipe sanjeok = CookingTable.ForOutput("con_sanjeok");
            Assert.AreEqual(CookError.None, CookingTable.Cook(sanjeok, stash, bag, catalog.Find("con_sanjeok"), true));
            Assert.AreEqual(0, BuildingState.CountIn(stash, bag, IngredientTable.Pheasant));
            Assert.AreEqual(0, BuildingState.CountIn(stash, bag, IngredientTable.Boar));
            Assert.AreEqual(CookError.MissingMaterial, CookingTable.Cook(sanjeok, stash, bag, catalog.Find("con_sanjeok"), true));
        }

        [Test]
        public void 샘은_한_밤에_여덟_병_넘치면_쌓이지_않는다()
        {
            ItemDefinition water = Catalog.Find(SpringTable.WaterId);
            var bag = new Inventory(20, 99f);
            var spring = new SpringState();

            for (int i = 0; i < SpringTable.PerNight; i++)
                Assert.AreEqual(SpringError.None, spring.Draw(bag, water));

            Assert.AreEqual(SpringError.Dry, spring.Draw(bag, water));
            Assert.AreEqual(8, BuildingState.CountIn(null, bag, SpringTable.WaterId));

            spring.Refill();
            spring.Refill();
            Assert.AreEqual(SpringTable.PerNight, spring.Remaining, "쌓이지 않는다");

            spring.Restore(99);
            Assert.AreEqual(SpringTable.PerNight, spring.Remaining);
        }

        [Test]
        public void 옛_맑은_물은_물병_물로_읽는다()
        {
            Assert.AreEqual(SpringTable.WaterId, Catalog.Find("water_bottle")?.Id);
            Assert.AreEqual("물병", Catalog.Find(SpringTable.WaterId).DisplayName);
        }
    }
}
