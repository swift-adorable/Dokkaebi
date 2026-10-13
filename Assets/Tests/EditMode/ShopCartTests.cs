using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 결정 2-120 — 가게의 담아 두기 · 합계 · 한꺼번에 사기, 위쪽 탭, 지도의 진행 중 퀘스트, 상세의 얻는 곳 · 쓰는 곳.
    /// 한꺼번에 사기는 TradeRules와 같은 「전부 아니면 전혀」다.
    /// </summary>
    public class ShopCartTests
    {
        private static ItemDefinition Create(string id, int value, int stackMax = 1, ItemKind kind = ItemKind.Consumable)
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
            so.FindProperty("baseValue").intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();

            return def;
        }

        private static ShopState Stocked(params ShopEntry[] table)
        {
            var shop = new ShopState();
            shop.Restock(table);
            return shop;
        }

        // ── 담아 두기 ─────────────────────────────────────────────────

        [Test]
        public void 같은_물건은_한_줄로_합치고_0이면_뺀다()
        {
            var cart = new ShopCart();
            cart.Add("a", 2);
            cart.Add("b", 1);
            cart.Add("a", 3);

            Assert.AreEqual(2, cart.Lines.Count);
            Assert.AreEqual(5, cart.CountOf("a"));
            Assert.AreEqual("a", cart.Lines[0].ItemId, "있던 줄은 자리를 지킨다");

            cart.Set("a", 0);
            Assert.AreEqual(0, cart.CountOf("a"));
            Assert.AreEqual(1, cart.Lines.Count);

            cart.Remove("b");
            Assert.IsTrue(cart.IsEmpty);
        }

        [Test]
        public void 합계는_줄마다_하나_값_곱하기_개수다()
        {
            ItemDefinition a = Create("a", 100, stackMax: 10);
            ItemDefinition b = Create("b", 30, stackMax: 10);
            var items = new List<CartItem>
            {
                new(a, new ShopEntry("a", 5, 1.0f), 2),
                new(b, new ShopEntry("b", 5, 1.5f), 3),
            };

            Assert.AreEqual(2 * 100 + 3 * 45, CartRules.Total(items));
            Assert.AreEqual(2 * 90 + 3 * 41, CartRules.Total(items, 10f), "값 깎기는 줄마다 올림");
        }

        [Test]
        public void 막히는_이유를_순서대로_찾는다()
        {
            ItemDefinition a = Create("a", 100);
            ShopState shop = Stocked(new ShopEntry("a", 2, 1f));
            var bag = new Inventory(5, 100f);

            Assert.AreEqual(TradeError.EmptyCart,
                CartRules.CanBuy(new List<CartItem>(), shop.Remaining, 1000, bag, 0f, out _));

            var over = new List<CartItem> { new(a, new ShopEntry("a", 2, 1f), 3) };
            Assert.AreEqual(TradeError.OutOfStock, CartRules.CanBuy(over, shop.Remaining, 1000, bag, 0f, out int failed));
            Assert.AreEqual(0, failed);

            var two = new List<CartItem> { new(a, new ShopEntry("a", 2, 1f), 2) };
            Assert.AreEqual(TradeError.NotEnoughGold, CartRules.CanBuy(two, shop.Remaining, 199, bag, 0f, out _));
            Assert.AreEqual(TradeError.None, CartRules.CanBuy(two, shop.Remaining, 200, bag, 0f, out _));

            var small = new Inventory(1, 100f);
            Assert.AreEqual(TradeError.NoSpace, CartRules.CanBuy(two, shop.Remaining, 200, small, 0f, out _),
                "겹치지 않는 둘은 두 칸이 든다");
        }

        [Test]
        public void 줄_여럿의_칸을_더해서_잰다()
        {
            ItemDefinition arrow = Create("arrow", 1, stackMax: 100, kind: ItemKind.Ammo);
            ItemDefinition kit = Create("kit", 50);
            var bag = new Inventory(3, 100f);
            bag.TryAdd(arrow, 60);   // 한 칸 · 40 남음

            Assert.AreEqual(0, bag.SlotsNeeded(arrow, 40), "있던 칸의 여유부터");
            Assert.AreEqual(1, bag.SlotsNeeded(arrow, 41));

            var items = new List<CartItem>
            {
                new(arrow, new ShopEntry("arrow", 300, 1f), 41),
                new(kit, new ShopEntry("kit", 3, 1f), 1),
            };

            Assert.AreEqual(2, CartRules.SlotsNeeded(items, bag));
            ShopState shop = Stocked(new ShopEntry("arrow", 300, 1f), new ShopEntry("kit", 3, 1f));
            Assert.AreEqual(TradeError.None, CartRules.CanBuy(items, shop.Remaining, 1000, bag, 0f, out _));

            items.Add(new CartItem(Create("kit2", 50), new ShopEntry("kit2", 3, 1f), 1));
            Assert.AreEqual(3, CartRules.SlotsNeeded(items, bag), "남은 칸 둘에 셋은 못 든다");
        }

        [Test]
        public void 막히면_아무것도_사지_않는다()
        {
            ItemDefinition a = Create("a", 100);
            ItemDefinition b = Create("b", 100);
            ShopState shop = Stocked(new ShopEntry("a", 3, 1f), new ShopEntry("b", 3, 1f));
            var bag = new Inventory(2, 100f);
            int gold = 1000;

            var items = new List<CartItem>
            {
                new(a, new ShopEntry("a", 3, 1f), 2),
                new(b, new ShopEntry("b", 3, 1f), 1),
            };

            Assert.AreEqual(TradeError.NoSpace, CartRules.Buy(shop, items, bag, ref gold, 0f, out _));
            Assert.AreEqual(1000, gold);
            Assert.AreEqual(3, shop.Remaining("a"));
            Assert.AreEqual(0, bag.UsedSlots);
        }

        [Test]
        public void 사면_합계만큼_빠지고_재고가_준다()
        {
            ItemDefinition a = Create("a", 100, stackMax: 10);
            ItemDefinition b = Create("b", 40);
            ShopState shop = Stocked(new ShopEntry("a", 5, 1f), new ShopEntry("b", 2, 1f));
            var bag = new Inventory(5, 100f);
            int gold = 1000;

            var items = new List<CartItem>
            {
                new(a, new ShopEntry("a", 5, 1f), 3),
                new(b, new ShopEntry("b", 2, 1f), 2),
            };

            Assert.AreEqual(TradeError.None, CartRules.Buy(shop, items, bag, ref gold, 0f, out _));
            Assert.AreEqual(1000 - 380, gold);
            Assert.AreEqual(2, shop.Remaining("a"));
            Assert.AreEqual(0, shop.Remaining("b"));
            Assert.AreEqual(3, bag.CountOf(a));
            Assert.AreEqual(2, bag.CountOf(b));
        }

        [Test]
        public void 이유_한_줄은_얼마나_모자란지_적는다()
        {
            Assert.AreEqual("엽전이 1,200 모자랍니다.", CartRules.Explain(TradeError.NotEnoughGold, 1200, 0, null));
            Assert.AreEqual("가방 자리가 2칸 모자랍니다.", CartRules.Explain(TradeError.NoSpace, 0, 2, null));
            Assert.AreEqual("「물병」 재고가 모자랍니다.", CartRules.Explain(TradeError.OutOfStock, 0, 0, "물병"));
            Assert.AreEqual(string.Empty, CartRules.Explain(TradeError.None, 0, 0, null));
            Assert.AreEqual("담은 것이 없습니다.", CartRules.Explain(TradeError.EmptyCart, 0, 0, null));
        }

        // ── 위쪽 탭 ───────────────────────────────────────────────────

        [Test]
        public void 탭은_정해진_순서로_겹치지_않게_나뉜다()
        {
            List<ItemKind> kinds = ShopTabs.KindsOf(new[]
            {
                ItemKind.Backpack, ItemKind.Armour, ItemKind.Weapon, ItemKind.Ammo, ItemKind.Weapon,
            });

            CollectionAssert.AreEqual(new[] { ItemKind.Weapon, ItemKind.Ammo, ItemKind.Armour, ItemKind.Backpack }, kinds);
            Assert.IsTrue(ShopTabs.Shows(kinds));
            Assert.IsFalse(ShopTabs.Shows(ShopTabs.KindsOf(new[] { ItemKind.Consumable, ItemKind.Consumable })),
                "종류가 하나면 탭 줄을 띄우지 않는다");
            Assert.AreEqual("탄", ShopTabs.Name(ItemKind.Ammo));
        }

        // ── 지도의 진행 중 퀘스트 ─────────────────────────────────────

        [Test]
        public void 진행_중_퀘스트는_따라가는_것이_맨_앞이고_갈_곳이_없어도_줄은_남는다()
        {
            var p = new StoryProgress();
            var t = new QuestTracker(p);

            Assert.AreEqual(0, QuestZones.ActiveList(t, 1).Count);

            p.See(StoryTable.EnterEvent("0-1"));   // 영감의 잡화 가게 — 짓기 목표라 구역이 없다
            List<QuestPlace> list = QuestZones.ActiveList(t, 0);

            QuestPlace store = list.First(x => x.Quest.Id == "sub_elder_store");
            Assert.IsFalse(store.HasPlace);

            QuestDefinition tracked = t.Tracked();
            if (tracked != null)
                Assert.AreSame(tracked, list[0].Quest);

            Assert.AreEqual(list.Count, t.Visible().Count(q => t.StateOf(q) == QuestState.Active));
        }

        [Test]
        public void 다음_목표_구역이_이_장에_있으면_그_구역이다()
        {
            var p = new StoryProgress();
            var t = new QuestTracker(p);

            foreach (ZoneDefinition z in StoryTable.Chapter(0).Zones)
            {
                p.See(StoryTable.EnterEvent(z.Id));
                foreach (BossDefinition b in StoryTable.BossesIn(z.Id))
                    p.Defeat(b.Id);
            }

            QuestDefinition main = QuestTable.Quest(QuestTable.MainMyStory);
            if (t.StateOf(main) != QuestState.Active)
                Assert.Inconclusive("0장을 비워도 메인이 열리지 않는다 — 본문 진행 규칙이 바뀌었다");

            string zone = QuestZones.ZoneOf(t.NextObjective(main));
            Assert.IsNotNull(zone);
            int chapter = StoryTable.Zone(zone).Chapter;

            QuestPlace here = QuestZones.ActiveList(t, chapter).First(x => x.Quest == main);
            Assert.AreEqual(zone, here.ZoneId);

            QuestPlace elsewhere = QuestZones.ActiveList(t, chapter + 1).First(x => x.Quest == main);
            Assert.IsFalse(elsewhere.HasPlace, "다른 장 지도에서는 갈 곳이 없다");
        }

        // ── 얻는 곳 · 쓰는 곳 ─────────────────────────────────────────

        [Test]
        public void 물병은_샘과_잡화_가게에서_얻고_부뚜막과_약탕에_쓴다()
        {
            List<string> sources = ItemSources.Sources(SpringTable.WaterId);
            CollectionAssert.Contains(sources, SpringTable.Name);
            CollectionAssert.Contains(sources, ShopTable.GeneralStoreName);

            List<string> uses = ItemSources.Uses(SpringTable.WaterId);
            Assert.IsTrue(uses.Any(u => u.StartsWith(CookingTable.HearthName + "(")), string.Join(" / ", uses));
            Assert.IsTrue(uses.Any(u => u.StartsWith(ApothecaryTable.PotName + "(")), string.Join(" / ", uses));
        }

        [Test]
        public void 쇠붙이는_건물_짓기와_작업대에_쓴다()
        {
            List<string> uses = ItemSources.Uses(WorkbenchTable.Scrap, id => BuildingTable.Find(id)?.Name ?? id);

            Assert.IsTrue(uses.Any(u => u.StartsWith("짓기(") && u.Contains("작업대")), string.Join(" / ", uses));
            Assert.IsTrue(uses.Any(u => u.StartsWith("작업대(")), string.Join(" / ", uses));
            Assert.IsTrue(uses.Any(u => u.StartsWith("작업대 단계 올리기")), string.Join(" / ", uses));
        }

        [Test]
        public void 고기는_묶음_재료로도_찾는다()
        {
            List<string> uses = ItemSources.Uses(IngredientTable.Pheasant);
            Assert.IsTrue(uses.Any(u => u.Contains("con_sanjeok")), string.Join(" / ", uses));
        }

        [Test]
        public void 도면과_고유_장비와_장_재료의_출처()
        {
            CollectionAssert.Contains(ItemSources.Sources(WorkbenchTable.BlueprintOf(WeaponKind.Bow)), ItemSources.BlueprintSource);

            string bossId = BossUniqueTable.Items.Keys.First();
            string uniq = BossUniqueTable.ItemOf(bossId);
            Assert.IsTrue(ItemSources.Sources(uniq).Any(s => s.StartsWith(StoryTable.Boss(bossId).Name)));

            List<string> radish = ItemSources.Sources(IngredientTable.Radish, _ => true);
            CollectionAssert.Contains(radish, "1장 시체");
        }

        [Test]
        public void 이름이_많으면_외_n으로_줄인다()
        {
            Assert.AreEqual("작업대(가 · 나 · 다 외 2)", ItemSources.Group("작업대", new[] { "가", "나", "다", "라", "마" }));
            Assert.AreEqual("짓기(가)", ItemSources.Group("짓기", new[] { "가" }));
            Assert.AreEqual(string.Empty, ItemSources.Describe("no_such_item"));
        }

        [Test]
        public void 카탈로그가_시체_전리품을_안다()
        {
            ItemCatalog catalog = ItemCatalog.Load();
            Assert.IsNotNull(catalog);
            Assert.IsTrue(catalog.InCorpseLoot(WorkbenchTable.Scrap), "쇠붙이는 시체에서 나온다");
            Assert.IsFalse(catalog.InCorpseLoot(IngredientTable.Berry), "산열매는 나무에서만");
        }
    }
}
