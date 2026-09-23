using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blob.Tests
{
    /// <summary>
    /// 창고 · 잡화 상점 계약. (로드맵 8-I · docs/Blob_Bunker_System.md 3절)
    ///
    /// 【전부 아니면 전혀】 — 골드만 빠지거나 물건만 빠지는 거래가 한 번이라도
    /// 생기면 플레이어는 상점을 믿지 않는다. 그 계약을 여기서 강제한다.
    /// </summary>
    public class TradeTests
    {
        private static ItemDefinition Create(
            string id, int value, int stackMax = 1, int maxDurability = 0,
            ItemKind kind = ItemKind.Consumable, bool noTrade = false)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            def.name = id;

            var so = new SerializedObject(def);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;
            so.FindProperty("kind").intValue = (int)kind;
            so.FindProperty("weight").floatValue = 0.1f;
            so.FindProperty("slotSize").intValue = 1;
            so.FindProperty("stackMax").intValue = stackMax;
            so.FindProperty("maxDurability").intValue = maxDurability;
            so.FindProperty("baseValue").intValue = value;
            so.FindProperty("noTrade").boolValue = noTrade;
            so.ApplyModifiedPropertiesWithoutUndo();

            return def;
        }

        // ── 값 ───────────────────────────────────────────────────────

        [Test]
        public void 구매가는_가치_곱하기_가격_계수를_올림한다()
        {
            ItemDefinition item = Create("x", 101);

            Assert.AreEqual(101, TradeRules.BuyPrice(item, new ShopEntry("x", 1, 1.00f)));
            Assert.AreEqual(152, TradeRules.BuyPrice(item, new ShopEntry("x", 1, 1.50f)));
        }

        [Test]
        public void 판매가는_가치의_절반이고_흥정이_더한다()
        {
            ItemDefinition item = Create("x", 200, stackMax: 5);

            Assert.AreEqual(100, TradeRules.SellPrice(new ItemStack(item, 1), 0f));
            Assert.AreEqual(108, TradeRules.SellPrice(new ItemStack(item, 1), 8f));
            Assert.AreEqual(300, TradeRules.SellPrice(new ItemStack(item, 3), 0f), "칸을 통째로 판다");
        }

        [Test]
        public void 닳은_물건은_그만큼_싸게_팔린다()
        {
            ItemDefinition kit = Create("kit", 400, maxDurability: 100);

            Assert.AreEqual(100, TradeRules.SellPrice(new ItemStack(kit, 1, 50), 0f));
            Assert.AreEqual(TradeError.Worthless, TradeRules.CanSell(new ItemStack(kit, 1, 0), 0f));
        }

        [Test]
        public void 거래_불가_아이템은_팔_수_없다()
        {
            ItemDefinition imprint = Create("imp", 5000, kind: ItemKind.Imprint, noTrade: true);

            Assert.AreEqual(TradeError.NoTrade, TradeRules.CanSell(new ItemStack(imprint), 0f));
        }

        [Test]
        public void 흥정을_다_배워도_되팔아_남기지_못한다()
        {
            // 사서 바로 팔면 이득이면 상점이 골드 샘이 된다.
            // 흥정 계열을 전부 더해도 판매 배율이 가격 계수 1.00보다 작아야 한다.
            float maxBonus = 100f;   // 넉넉하게 +100%까지 본다
            ItemDefinition item = Create("x", 999);

            foreach (ShopEntry entry in ShopTable.General)
            {
                int buy = TradeRules.BuyPrice(item, entry);
                int sell = TradeRules.SellPrice(new ItemStack(item), maxBonus);

                Assert.LessOrEqual(sell, buy, $"{entry.ItemId}: 되팔아 남는다");
            }
        }

        // ── 구매 ─────────────────────────────────────────────────────

        [Test]
        public void 사면_골드와_재고가_빠지고_가방에_들어온다()
        {
            ItemDefinition item = Create("x", 100, stackMax: 3);
            var entry = new ShopEntry("x", 2, 1.00f);
            var shop = new ShopState();
            shop.Restock(new[] { entry });
            var bag = new Inventory(5, 30f);
            int gold = 250;

            Assert.AreEqual(TradeError.None, TradeRules.Buy(shop, item, entry, bag, ref gold));

            Assert.AreEqual(150, gold);
            Assert.AreEqual(1, shop.Remaining("x"));
            Assert.AreEqual(1, bag.CountOf(item));
        }

        [Test]
        public void 골드가_모자라면_아무것도_움직이지_않는다()
        {
            ItemDefinition item = Create("x", 100);
            var entry = new ShopEntry("x", 2, 1.00f);
            var shop = new ShopState();
            shop.Restock(new[] { entry });
            var bag = new Inventory(5, 30f);
            int gold = 99;

            Assert.AreEqual(TradeError.NotEnoughGold, TradeRules.Buy(shop, item, entry, bag, ref gold));

            Assert.AreEqual(99, gold);
            Assert.AreEqual(2, shop.Remaining("x"));
            Assert.AreEqual(0, bag.CountOf(item));
        }

        [Test]
        public void 가방이_차면_골드를_빼지_않는다()
        {
            ItemDefinition item = Create("x", 100);
            ItemDefinition filler = Create("f", 1);
            var entry = new ShopEntry("x", 2, 1.00f);
            var shop = new ShopState();
            shop.Restock(new[] { entry });
            var bag = new Inventory(1, 30f);
            bag.TryAdd(filler);
            int gold = 1000;

            Assert.AreEqual(TradeError.NoSpace, TradeRules.Buy(shop, item, entry, bag, ref gold));

            Assert.AreEqual(1000, gold);
            Assert.AreEqual(2, shop.Remaining("x"));
        }

        [Test]
        public void 품절이면_살_수_없다()
        {
            ItemDefinition item = Create("x", 10);
            var entry = new ShopEntry("x", 1, 1.00f);
            var shop = new ShopState();
            shop.Restock(new[] { entry });
            var bag = new Inventory(5, 30f);
            int gold = 1000;

            Assert.AreEqual(TradeError.None, TradeRules.Buy(shop, item, entry, bag, ref gold));
            Assert.AreEqual(TradeError.OutOfStock, TradeRules.Buy(shop, item, entry, bag, ref gold));
            Assert.AreEqual(990, gold);
        }

        // ── 판매 ─────────────────────────────────────────────────────

        [Test]
        public void 팔면_가방에서_빠지고_골드가_는다()
        {
            ItemDefinition item = Create("x", 200, stackMax: 5);
            var bag = new Inventory(5, 30f);
            bag.TryAdd(item, 3);
            ItemStack stack = bag.Stacks[0];
            int gold = 0;

            Assert.AreEqual(TradeError.None, TradeRules.Sell(stack, bag, 0f, ref gold, out int earned));

            Assert.AreEqual(300, earned);
            Assert.AreEqual(300, gold);
            Assert.AreEqual(0, bag.CountOf(item));
        }

        [Test]
        public void 가방에_없는_칸은_팔리지_않는다()
        {
            ItemDefinition item = Create("x", 200);
            var bag = new Inventory(5, 30f);
            int gold = 0;

            Assert.AreNotEqual(TradeError.None,
                TradeRules.Sell(new ItemStack(item), bag, 0f, ref gold, out _));
            Assert.AreEqual(0, gold);
        }

        // ── 재고 ─────────────────────────────────────────────────────

        [Test]
        public void 옛_세이브의_재고는_표에_맞춰_깎고_없는_줄은_가득_채운다()
        {
            var table = new[] { new ShopEntry("a", 3, 1f), new ShopEntry("b", 2, 1f) };
            var shop = new ShopState();

            shop.Restore(table, new List<SavedStock>
            {
                new SavedStock { id = "a", remaining = 9 },    // 표보다 많다 → 3
                new SavedStock { id = "gone", remaining = 1 }  // 표에 없다 → 버린다
            });

            Assert.AreEqual(3, shop.Remaining("a"));
            Assert.AreEqual(2, shop.Remaining("b"), "저장에 없는 줄은 가득");
            Assert.AreEqual(0, shop.Remaining("gone"));
        }

        [Test]
        public void 상점표의_아이템은_전부_카탈로그에_있다()
        {
            // 표의 id가 에셋과 어긋나면 그 칸이 빈칸으로 그려지고 살 수 없다.
            ItemCatalog catalog = ItemCatalog.Load();
            Assert.IsNotNull(catalog, "Resources/ItemCatalog가 없습니다.");

            foreach (ShopEntry entry in ShopTable.General)
                Assert.IsNotNull(catalog.Find(entry.ItemId), $"카탈로그에 없음: {entry.ItemId}");
        }

        // ── 창고 ─────────────────────────────────────────────────────

        [Test]
        public void 창고로_옮기면_겹치는_것은_합쳐진다()
        {
            ItemDefinition scrap = Create("scrap", 5, stackMax: 20, kind: ItemKind.Material);
            var bag = new Inventory(5, 30f);
            var stash = new Inventory(5, float.MaxValue);
            stash.TryAdd(scrap, 4);
            bag.TryAdd(scrap, 3);

            Assert.IsTrue(Inventory.MoveStack(bag, stash, bag.Stacks[0]));

            Assert.AreEqual(1, stash.UsedSlots);
            Assert.AreEqual(7, stash.CountOf(scrap));
            Assert.AreEqual(0, bag.CountOf(scrap));
        }

        [Test]
        public void 받는_쪽이_차면_아무것도_움직이지_않는다()
        {
            ItemDefinition kit = Create("kit", 5, maxDurability: 100);
            var bag = new Inventory(1, 30f);
            bag.TryAdd(Create("f", 1));
            var stash = new Inventory(5, float.MaxValue);
            stash.TryAdd(kit, 1, 40);
            ItemStack stack = stash.Stacks[0];

            Assert.IsFalse(Inventory.MoveStack(stash, bag, stack));

            Assert.AreEqual(1, stash.CountOf(kit));
            Assert.AreEqual(40, stash.Stacks[0].Durability, "내구도가 그대로여야 한다");
        }
    }
}
