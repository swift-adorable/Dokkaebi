using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>0장 한 판 — 본문 순서 · 길가 봇짐 · 0-2 첫 구슬 (결정 2-79).</summary>
    public class ChapterZeroTests
    {
        private static Dictionary<string, StoryPassage> Parse()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            return StoryScriptParser.Parse(File.ReadAllText(Path.Combine(root, "docs/Dokkaebi_Story_Script.txt")));
        }

        [Test]
        public void 고목_아래는_0_1_글에서_떨어져_소굴_도착_장면이_된다()
        {
            Dictionary<string, StoryPassage> p = Parse();

            Assert.IsTrue(p.ContainsKey(ChapterZeroTable.ArrivalEvent));

            string zeroOne = string.Join("\n", p[StoryTable.EnterEvent("0-1")].Pages);
            string arrival = string.Join("\n", p[ChapterZeroTable.ArrivalEvent].Pages);

            StringAssert.Contains("봇짐", zeroOne);
            StringAssert.DoesNotContain("새 얼굴이로구나", zeroOne, "영감과의 만남은 소굴에서");
            StringAssert.Contains("새 얼굴이로구나", arrival);
            StringAssert.Contains("빗자루는 여기 두고 다니거라", arrival);
            StringAssert.DoesNotContain("고목 뒤편 수풀", arrival, "0-2 글은 섞이지 않는다");
            Assert.IsFalse(p[ChapterZeroTable.ArrivalEvent].Pages.Any(StoryScriptParser.IsDirectionNote));
        }

        [Test]
        public void 새_게임은_0_1로_가고_0_1_뒤_처음_소굴에서_고목_아래를_본다()
        {
            var p = new StoryProgress();
            Assert.IsTrue(ChapterZeroTable.ShouldStartInZeroOne(p));
            Assert.IsFalse(ChapterZeroTable.ShouldPlayArrival(p));

            p.See(StoryTable.EnterEvent("0-1"));
            Assert.IsFalse(ChapterZeroTable.ShouldStartInZeroOne(p));
            Assert.IsTrue(ChapterZeroTable.ShouldPlayArrival(p));

            p.See(ChapterZeroTable.ArrivalEvent);
            Assert.IsFalse(ChapterZeroTable.ShouldPlayArrival(p));
        }

        [Test]
        public void 이_규칙_전에_0_2까지_본_세이브는_고목_아래를_다시_보지_않는다()
        {
            var p = new StoryProgress();
            p.See(StoryTable.EnterEvent("0-1"));
            p.See(StoryTable.EnterEvent("0-2"));

            Assert.IsFalse(ChapterZeroTable.ShouldPlayArrival(p));
        }

        [Test]
        public void 봇짐은_0_1에서_한_번만()
        {
            var p = new StoryProgress();
            Assert.IsTrue(ChapterZeroTable.ShouldPlaceBundle(p, "0-1"));
            Assert.IsFalse(ChapterZeroTable.ShouldPlaceBundle(p, "0-2"));

            p.See(ChapterZeroTable.BundleEvent);
            Assert.IsFalse(ChapterZeroTable.ShouldPlaceBundle(p, "0-1"));
        }

        [Test]
        public void 봇짐_속_물건과_환목궁이_아이템_카탈로그에_있다()
        {
            ItemCatalog catalog = ItemCatalog.Load();
            Assert.IsNotNull(catalog);
            Assert.IsNotNull(catalog.Find(ChapterZeroTable.BowId), "환목궁");

            foreach ((string id, int count) in ChapterZeroTable.BundleItems)
            {
                Assert.IsNotNull(catalog.Find(id), id);
                Assert.Greater(count, 0);
            }

            Assert.AreEqual("소환단", catalog.Find("con_medkit_small").DisplayName);
            Assert.AreEqual("식혜", catalog.Find("con_soda").DisplayName);
            Assert.AreEqual("미숫가루", catalog.Find("con_ration").DisplayName);
        }

        [Test]
        public void 첫_구슬은_0_2에서_한_번_화염_핵심_구슬()
        {
            var p = new StoryProgress();
            Assert.IsTrue(ChapterZeroTable.ShouldSendGiftCarrier(p, "0-2"));
            Assert.IsFalse(ChapterZeroTable.ShouldSendGiftCarrier(p, "0-1"));

            SkillCatalog skills = SkillCatalog.Load();
            SkillDefinition fire = skills.Find(ChapterZeroTable.GiftSkill);
            Assert.IsNotNull(fire);
            Assert.AreEqual(SkillCategory.Core, fire.Category);

            p.See(ChapterZeroTable.GiftEvent);
            Assert.IsFalse(ChapterZeroTable.ShouldSendGiftCarrier(p, "0-2"));
        }

        [Test]
        public void 안전판은_0장에서는_첫_구슬을_받은_뒤부터()
        {
            var p = new StoryProgress();

            Assert.IsFalse(ChapterZeroTable.FirstCoreSafety(p, 0), "0-1 · 0-2는 구슬 없이");
            Assert.IsTrue(ChapterZeroTable.FirstCoreSafety(p, 1), "1장부터는 늘");

            p.See(ChapterZeroTable.GiftEvent);
            Assert.IsTrue(ChapterZeroTable.FirstCoreSafety(p, 0));
        }
    }
}
