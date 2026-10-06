using System.Collections.Generic;
using System.Linq;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// Zone Data — 장마다 계절 · 무기 티어 · 일반 적 풀 · 재료 · 물가 태그, 구역마다 모은 것.
    /// (결정 2-57 · 2-59 · 2-60 · 2-63 · 2-64 · 2-65 · Story 8절 · Hunting 1절)
    /// </summary>
    public class ZoneDataTests
    {
        private static EnemyArchetype[] Pool(int chapter)
            => ZoneDataTable.Chapter(chapter).Pool.Select(e => e.Archetype).OrderBy(a => a).ToArray();

        [Test]
        public void 장마다_하나씩_있다()
        {
            Assert.AreEqual(StoryTable.Chapters.Count, ZoneDataTable.Chapters.Count);

            for (int i = 0; i < ZoneDataTable.Chapters.Count; i++)
                Assert.AreEqual(i, ZoneDataTable.Chapters[i].Chapter);
        }

        [Test]
        public void 계절은_결정_2_59와_같다()
        {
            Season[] expected = { Season.Spring, Season.Winter, Season.Spring, Season.Summer, Season.Autumn, Season.Winter, Season.Spring };

            for (int c = 1; c <= 6; c++)
                Assert.AreEqual(expected[c], ZoneDataTable.Chapter(c).Season, $"{c}장");
        }

        [Test]
        public void 장이_곧_무기_티어다()
        {
            for (int c = 0; c <= 6; c++)
                Assert.AreEqual(c, ZoneDataTable.Chapter(c).WeaponTier);
        }

        [Test]
        public void 장별_재료는_넷씩_겹치지_않는다()
        {
            var all = new List<string>();

            for (int c = 1; c <= 6; c++)
            {
                Assert.AreEqual(4, ZoneDataTable.Chapter(c).Materials.Length, $"{c}장");
                all.AddRange(ZoneDataTable.Chapter(c).Materials);
            }

            Assert.AreEqual(all.Count, all.Distinct().Count());
        }

        [Test]
        public void 일반_적_구성이_결정_2_57과_같다()
        {
            CollectionAssert.AreEqual(new[] { EnemyArchetype.Scav, EnemyArchetype.Crusher }, Pool(1));
            CollectionAssert.AreEqual(new[] { EnemyArchetype.Scav, EnemyArchetype.Dynamo, EnemyArchetype.Lurker }, Pool(2));
            CollectionAssert.AreEqual(new[] { EnemyArchetype.Chemic, EnemyArchetype.Specimen }, Pool(3));
            CollectionAssert.AreEqual(new[] { EnemyArchetype.Scav, EnemyArchetype.Settled, EnemyArchetype.Sentry }, Pool(4));
            CollectionAssert.AreEqual(new[] { EnemyArchetype.Sentry, EnemyArchetype.Wraith }, Pool(5));
            CollectionAssert.AreEqual(new[] { EnemyArchetype.Scav, EnemyArchetype.Wraith }, Pool(6));

            foreach (ChapterData c in ZoneDataTable.Chapters)
                Assert.IsTrue(c.Pool.All(e => e.Weight > 0), $"{c.Chapter}장");
        }

        [Test]
        public void 드물게_위주가_비중에_보인다()
        {
            ChapterData one = ZoneDataTable.Chapter(1);
            ChapterData six = ZoneDataTable.Chapter(6);

            Assert.Less(one.Pool.First(e => e.Archetype == EnemyArchetype.Crusher).Weight * 5, one.TotalWeight, "1장 절굿공이귀는 드물게");
            Assert.Greater(six.Pool.First(e => e.Archetype == EnemyArchetype.Wraith).Weight * 2, six.TotalWeight, "6장은 무주귀 위주");
        }

        [Test]
        public void 고르기는_비중대로다()
        {
            Assert.AreEqual(EnemyArchetype.Scav, ZoneDataTable.Pick(1, 0f));
            Assert.AreEqual(EnemyArchetype.Scav, ZoneDataTable.Pick(1, 0.89f));
            Assert.AreEqual(EnemyArchetype.Crusher, ZoneDataTable.Pick(1, 0.95f));
            Assert.AreEqual(EnemyArchetype.Crusher, ZoneDataTable.Pick(1, 1f), "끝값도 풀 안");

            for (int c = 0; c <= 6; c++)
                for (float r = 0f; r < 1f; r += 0.05f)
                    Assert.IsTrue(ZoneDataTable.InPool(c, ZoneDataTable.Pick(c, r)), $"{c}장 {r}");
        }

        [Test]
        public void 조건이_더하는_적은_장_풀_안에서만()
        {
            Assert.IsFalse(ZoneDataTable.AllowsExtra(3, EnemyArchetype.Lurker), "3장 장마비 수귀는 안 나온다 (2-64)");
            Assert.IsTrue(ZoneDataTable.AllowsExtra(2, EnemyArchetype.Lurker), "2장 보름 수귀");
            Assert.IsTrue(ZoneDataTable.AllowsExtra(5, EnemyArchetype.Wraith), "5장 삭 무주귀");
            Assert.IsTrue(ZoneDataTable.AllowsExtra(6, EnemyArchetype.Wraith), "6장 삭 무주귀");
            Assert.IsFalse(ZoneDataTable.AllowsExtra(1, EnemyArchetype.Wraith), "1장 삭에는 무주귀가 없다");
            Assert.IsTrue(ZoneDataTable.AllowsExtra(3, EnemyArchetype.Chemic), "3장 독안개 왕지네");
        }

        [Test]
        public void 물가_태그는_있는_구역에만_있고_2장에_있다()
        {
            int water = 0;

            foreach (ChapterDefinition c in StoryTable.Chapters)
                foreach (ZoneDefinition z in c.Zones)
                    if (ZoneDataTable.IsWaterside(z.Id))
                    {
                        water++;
                        if (z.Chapter == 2) Assert.Pass();
                    }

            Assert.Fail("2장(보름 수귀)에 물가 구역이 없다");
        }

        [Test]
        public void 궂은_날은_6장_꽃비만_아프지_않다()
        {
            for (int c = 1; c <= 5; c++)
                Assert.IsTrue(ZoneDataTable.Chapter(c).BadWeatherHurts, $"{c}장");

            Assert.IsFalse(ZoneDataTable.Chapter(6).BadWeatherHurts);
            Assert.AreEqual("독안개", ZoneDataTable.Chapter(3).BadWeather2, "여름 궂은 날 Ⅱ = 독안개 (2-64)");
        }

        [Test]
        public void 구역_요약에_보스_조각_방_퀘스트가_모인다()
        {
            foreach (ChapterDefinition c in StoryTable.Chapters)
                foreach (ZoneDefinition z in c.Zones)
                    Assert.IsNotNull(ZoneDataTable.Summary(z.Id), z.Id);

            ZoneSummary well = ZoneDataTable.Summary("1-3");
            Assert.AreEqual("hyeonmu", well.Bosses.Last().Id);
            Assert.IsTrue(well.Quests.Any(q => q.Id == QuestTable.MainMyStory));

            ZoneSummary bamboo = ZoneDataTable.Summary("2-1");
            Assert.IsTrue(bamboo.Quests.Any(q => q.Id == "sub_watermill"));
            Assert.IsNotNull(bamboo.Piece);
            Assert.IsNotNull(bamboo.Notice);
            Assert.IsTrue(bamboo.IsWaterside);

            // 퀘스트 목표가 닿는 구역은 전부 요약에 잡힌다.
            foreach (QuestDefinition q in QuestTable.Quests)
                foreach (QuestObjective o in q.Objectives.Where(o => o.Condition == QuestCondition.ClearZone))
                    Assert.IsTrue(ZoneDataTable.Summary(o.Target).Quests.Contains(q), $"{q.Id} → {o.Target}");
        }
        [Test]
        public void 장별_재료_이름이_Story_8절_표와_같다()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            string[] lines = File.ReadAllLines(Path.Combine(root, "docs/Dokkaebi_Story.md"));

            for (int c = 1; c <= 6; c++)
            {
                string row = lines.First(l => l.StartsWith($"| {c} | {c} |"));
                string cell = row.Split('|')[4].Replace("**", "").Trim();
                string[] names = cell.Split('·').Select(x => x.Trim()).ToArray();

                CollectionAssert.AreEqual(names, ZoneDataTable.Chapter(c).Materials, $"{c}장");
            }
        }
    }
}
