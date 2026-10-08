using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 【약탕간 · 장부방】 (결정 2-97)
    ///   1. 달이기 — 재료가 들고 약이 나온다 · 약탕간이 없으면 못 한다 · 표의 아이템이 모두 있다
    ///   2. 샘가 — 약탕간이 있을 때만 판마다 모이고, 끝이 있다 · 세이브를 오간다
    ///   3. 장 지도 — 값을 치르면 안개가 다 걷히고, 다 걷힌 지도는 다시 못 산다
    ///   4. 처치 기록 — 문턱마다 더 알고, 세이브를 오간다
    /// </summary>
    public class ApothecaryLedgerTests
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

        private void Give(string id, int count) => stock[id] = Count(id) + count;

        [SetUp]
        public void Fill() => stock = new Dictionary<string, int>();

        // ── 1. 달이기 ────────────────────────────────────────────────

        [Test]
        public void 약재로_약을_달인다()
        {
            CraftRecipe bandage = ApothecaryTable.Brews[0];
            stock[ApothecaryTable.Herb] = 1;

            Assert.AreEqual(CraftError.None, ApothecaryTable.Brew(bandage, true, Count, Remove, (_, _) => true, Give));
            Assert.AreEqual(0, Count(ApothecaryTable.Herb));
            Assert.AreEqual(bandage.OutputCount, Count(bandage.OutputId));

            Assert.AreEqual(CraftError.MissingMaterial, ApothecaryTable.Brew(bandage, true, Count, Remove, (_, _) => true, Give));
        }

        [Test]
        public void 약탕간이_없거나_자리가_없으면_아무것도_쓰지_않는다()
        {
            CraftRecipe bandage = ApothecaryTable.Brews[0];
            stock[ApothecaryTable.Herb] = 5;

            Assert.AreEqual(CraftError.NoWorkbench, ApothecaryTable.Brew(bandage, false, Count, Remove, (_, _) => true, Give));
            Assert.AreEqual(CraftError.NoRoom, ApothecaryTable.Brew(bandage, true, Count, Remove, (_, _) => false, Give));
            Assert.AreEqual(5, Count(ApothecaryTable.Herb));
            StringAssert.Contains("약탕간", ApothecaryTable.Explain(CraftError.NoWorkbench));
        }

        [Test]
        public void 달이는_약_표의_아이템이_모두_있고_전부_약재를_쓴다()
        {
            ItemCatalog catalog = ItemCatalog.Load();
            Assert.IsNotNull(catalog, "Resources/ItemCatalog가 없습니다.");
            Assert.IsNotNull(catalog.Find(ApothecaryTable.Herb));

            var outputs = new HashSet<string>();
            foreach (CraftRecipe r in ApothecaryTable.Brews)
            {
                Assert.IsNotNull(catalog.Find(r.OutputId), $"달일 것 {r.OutputId}가 없습니다.");
                Assert.IsTrue(outputs.Add(r.OutputId), $"{r.OutputId}가 두 번 있습니다.");
                Assert.AreEqual(0, r.Gold);

                bool herb = false;
                foreach (MaterialCost c in r.Inputs)
                {
                    Assert.IsNotNull(catalog.Find(c.ItemId), $"{r.OutputId}의 재료 {c.ItemId}가 없습니다.");
                    herb |= c.ItemId == ApothecaryTable.Herb;
                }

                Assert.IsTrue(herb, $"{r.OutputId}에 약재가 들지 않습니다.");
            }
        }

        // ── 2. 샘가 ──────────────────────────────────────────────────

        [Test]
        public void 샘가는_약탕간이_있을_때만_모이고_끝이_있다()
        {
            var side = new SpringsideState();
            var pool = new List<string> { "test_chapter_item" };

            side.AccrueNight(false, pool, new System.Random(1));
            Assert.IsTrue(side.IsEmpty);

            for (int night = 0; night < 50; night++)
                side.AccrueNight(true, pool, new System.Random(night));

            Assert.AreEqual(ApothecaryTable.HerbCap, side.CountOf(ApothecaryTable.Herb));
            Assert.LessOrEqual(side.CountOf("test_chapter_item"), ApothecaryTable.ChapterCap);
            Assert.Greater(side.CountOf("test_chapter_item"), 0, "50밤이면 장 재료가 한 번은 모여야 한다.");
        }

        [Test]
        public void 한_밤에는_약재_둘이_모인다()
        {
            var side = new SpringsideState();
            side.AccrueNight(true, null, new System.Random(3));
            Assert.AreEqual(ApothecaryTable.HerbsPerNight, side.CountOf(ApothecaryTable.Herb));
        }

        [Test]
        public void 샘가는_거두면_비고_세이브를_오간다()
        {
            var side = new SpringsideState();
            side.AccrueNight(true, null, new System.Random(3));
            side.AccrueNight(true, null, new System.Random(4));

            var copy = new SpringsideState();
            copy.Restore(side.Capture());
            Assert.AreEqual(side.CountOf(ApothecaryTable.Herb), copy.CountOf(ApothecaryTable.Herb));

            List<MaterialCost> taken = copy.TakeAll();
            Assert.AreEqual(1, taken.Count);
            Assert.IsTrue(copy.IsEmpty);

            copy.Restore(null);
            Assert.IsTrue(copy.IsEmpty);
        }

        [Test]
        public void 샘가_장_재료는_열린_장에서만()
        {
            List<string> none = ApothecaryTable.ChapterPool(c => false);
            Assert.AreEqual(0, none.Count);

            List<string> first = ApothecaryTable.ChapterPool(c => c == 1);
            foreach (Ingredient i in IngredientTable.ChapterOnly(1))
                Assert.Contains(i.Id, first);
        }

        // ── 3. 장 지도 ───────────────────────────────────────────────

        [Test]
        public void 장_지도를_사면_안개가_다_걷히고_다시는_못_산다()
        {
            var fog = new FogGrid(new Vector2(20f, 20f));
            int price = LedgerTable.MapPrice(1);

            Assert.AreEqual(LedgerError.NoLedger, LedgerTable.CanBuyMap(false, true, fog, 9999, 1));
            Assert.AreEqual(LedgerError.ChapterClosed, LedgerTable.CanBuyMap(true, false, fog, 9999, 1));
            Assert.AreEqual(LedgerError.NoMap, LedgerTable.CanBuyMap(true, true, null, 9999, 1));
            Assert.AreEqual(LedgerError.NotEnoughGold, LedgerTable.CanBuyMap(true, true, fog, price - 1, 1));
            Assert.AreEqual(LedgerError.None, LedgerTable.CanBuyMap(true, true, fog, price, 1));

            fog.RevealAll();
            Assert.IsTrue(LedgerTable.IsFullyRevealed(fog));
            Assert.AreEqual(LedgerError.AlreadyRevealed, LedgerTable.CanBuyMap(true, true, fog, 9999, 1));
        }

        [Test]
        public void 뒤_장일수록_지도가_비싸다()
        {
            for (int c = 1; c <= StoryTable.LastChapter; c++)
                Assert.Greater(LedgerTable.MapPrice(c), LedgerTable.MapPrice(c - 1));
        }

        // ── 4. 처치 기록 ─────────────────────────────────────────────

        [Test]
        public void 문턱마다_더_안다()
        {
            Assert.AreEqual(0, LedgerTable.InfoLevel(0));
            Assert.AreEqual(1, LedgerTable.InfoLevel(1));
            Assert.AreEqual(1, LedgerTable.InfoLevel(4));
            Assert.AreEqual(2, LedgerTable.InfoLevel(5));
            Assert.AreEqual(3, LedgerTable.InfoLevel(15));
            Assert.AreEqual(4, LedgerTable.InfoLevel(30));
            Assert.AreEqual(4, LedgerTable.InfoLevel(999));

            Assert.AreEqual(4, LedgerTable.ToNextLevel(1));
            Assert.AreEqual(0, LedgerTable.ToNextLevel(30));
        }

        [Test]
        public void 처치_기록은_갈래와_등급으로_세고_세이브를_오간다()
        {
            var record = new KillRecord();
            record.Add(EnemyArchetype.Scav, EnemyRarity.Normal);
            record.Add(EnemyArchetype.Scav, EnemyRarity.Normal);
            record.Add(EnemyArchetype.Scav, EnemyRarity.Rare);
            record.Add(EnemyArchetype.Wraith, EnemyRarity.Unique);

            Assert.AreEqual(3, record.Count(EnemyArchetype.Scav));
            Assert.AreEqual(2, record.Count(EnemyArchetype.Scav, EnemyRarity.Normal));
            Assert.AreEqual(4, record.Total);

            var copy = new KillRecord();
            copy.Restore(record.Capture());
            Assert.AreEqual(3, copy.Count(EnemyArchetype.Scav));
            Assert.AreEqual(1, copy.Count(EnemyArchetype.Wraith, EnemyRarity.Unique));

            // 모르는 갈래 · 등급은 버린다
            copy.Restore(new List<SavedKill> { new() { archetype = 99, rarity = 0, count = 3 } });
            Assert.AreEqual(0, copy.Total);
        }

        [Test]
        public void 장부방과_약탕이_자리를_연다()
        {
            Assert.AreEqual(BunkerStation.Kind.Ledger, BuildingTable.Find(BuildingTable.LedgerRoom).Opens);
            Assert.AreEqual(15, (int)BunkerStation.Kind.Brewing);
            Assert.AreEqual(16, (int)BunkerStation.Kind.Ledger);
        }
    }
}
