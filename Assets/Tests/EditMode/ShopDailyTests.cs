using System.Collections.Generic;
using NUnit.Framework;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 【오늘의 물건 · 길달 보상】 (결정 2-98)
    ///   1. 풀은 평소엔 안 파는 것 · ×1.5 · 카탈로그에 있다
    ///   2. 대장간은 진행으로 열린다 — 3장 관통탄 · 5장 도면
    ///   3. 뽑기는 겹치지 않고, 다시 뽑기는 같은 것을 피한다
    ///   4. 다시 맞추기(Refit)는 있던 줄의 남은 수를 지키고 새 줄은 가득 채운다
    /// </summary>
    public class ShopDailyTests
    {
        [Test]
        public void 풀은_평소엔_안_파는_것이고_값은_비싸다()
        {
            ItemCatalog catalog = ItemCatalog.Load();
            Assert.IsNotNull(catalog, "Resources/ItemCatalog가 없습니다.");

            foreach (ShopKind kind in ShopTable.All)
            {
                List<ShopEntry> pool = ShopDailyTable.Pool(kind, _ => true);
                Assert.Greater(pool.Count, ShopDailyTable.Slots, $"{kind} 풀이 칸 수보다 작습니다.");

                var seen = new HashSet<string>();
                foreach (ShopEntry e in pool)
                {
                    Assert.IsTrue(seen.Add(e.ItemId), $"{kind} 풀에 {e.ItemId}가 두 번 있습니다.");
                    Assert.IsFalse(ShopTable.TryFind(kind, e.ItemId, out _), $"{e.ItemId}는 {kind}가 평소에 판다.");
                    Assert.AreEqual(ShopDailyTable.PriceMultiplier, e.PriceMultiplier);
                    Assert.IsNotNull(catalog.Find(e.ItemId), $"{kind} 풀의 {e.ItemId}가 카탈로그에 없습니다.");
                }
            }
        }

        [Test]
        public void 대장간은_장_진행으로_열린다()
        {
            Assert.AreEqual(0, ShopDailyTable.Pool(ShopKind.Smithy, c => c < ShopDailyTable.PiercingChapter).Count);

            List<ShopEntry> piercing = ShopDailyTable.Pool(ShopKind.Smithy, c => c < ShopDailyTable.BlueprintChapter);
            Assert.AreEqual(7, piercing.Count);
            foreach (ShopEntry e in piercing)
                Assert.Greater(AmmoTable.PenetrationBonusOf(e.ItemId), 0);

            Assert.AreEqual(14, ShopDailyTable.Pool(ShopKind.Smithy, _ => true).Count);
        }

        [Test]
        public void 잡화_가게_풀은_열린_장의_재료만_더한다()
        {
            List<ShopEntry> closed = ShopDailyTable.Pool(ShopKind.General, _ => false);
            List<ShopEntry> open = ShopDailyTable.Pool(ShopKind.General, _ => true);
            Assert.Greater(closed.Count, 0, "공용 요리 재료는 처음부터 있다.");
            Assert.Greater(open.Count, closed.Count);
            Assert.IsFalse(closed.Exists(e => e.ItemId == IngredientTable.Berry), "산열매는 나무에서 딴다.");
        }

        [Test]
        public void 뽑기는_겹치지_않고_다시_뽑기는_같은_것을_피한다()
        {
            List<ShopEntry> pool = ShopDailyTable.Pool(ShopKind.General, _ => true);
            List<string> first = ShopDailyTable.Roll(pool, 3, new System.Random(1));
            Assert.AreEqual(3, first.Count);
            Assert.AreEqual(3, new HashSet<string>(first).Count);

            List<string> second = ShopDailyTable.Roll(pool, 3, new System.Random(2), first);
            foreach (string id in second)
                Assert.IsFalse(first.Contains(id), "풀이 넉넉하면 같은 것을 다시 내지 않는다.");

            // 풀이 작으면 피할 것도 다시 쓴다
            var small = new List<ShopEntry> { new("a", 1, 1f), new("b", 1, 1f) };
            Assert.AreEqual(2, ShopDailyTable.Roll(small, 3, new System.Random(3), new[] { "a" }).Count);
            Assert.AreEqual(0, ShopDailyTable.Roll(new List<ShopEntry>(), 3, new System.Random(4)).Count);
        }

        [Test]
        public void 다시_맞추기는_남은_수를_지키고_새_줄은_가득()
        {
            var state = new ShopState();
            var table = new List<ShopEntry> { new("fixed", 5, 1f), new("old", 3, 1.5f) };
            state.Restock(table);
            state.TryConsume("fixed");
            state.TryConsume("old");

            var next = new List<ShopEntry> { new("fixed", 5, 1f), new("new", 4, 1.5f) };
            state.Refit(next, 0, new[] { "new" });

            Assert.AreEqual(4, state.Remaining("fixed"));
            Assert.AreEqual(4, state.Remaining("new"));
            Assert.AreEqual(0, state.Remaining("old"));
        }

        [Test]
        public void 장부방_가게_일은_장부방과_가게와_엽전이_있어야()
        {
            Assert.AreEqual(LedgerError.NoLedger, LedgerTable.CanServeShop(false, true, 9999, 100));
            Assert.AreEqual(LedgerError.ShopClosed, LedgerTable.CanServeShop(true, false, 9999, 100));
            Assert.AreEqual(LedgerError.NotEnoughGold, LedgerTable.CanServeShop(true, true, 99, 100));
            Assert.AreEqual(LedgerError.None, LedgerTable.CanServeShop(true, true, 100, 100));
        }

        [Test]
        public void 길달은_화살_서른을_준다()
        {
            MerchantDefinition gildal = StoryTable.Merchant(StoryTable.Gildal);
            Assert.AreEqual(1, gildal.Rewards.Length);
            Assert.AreEqual(AmmoTable.Arrow, gildal.Rewards[0].itemId);
            Assert.AreEqual(30, gildal.Rewards[0].count);
        }
    }
}
