using NUnit.Framework;

namespace Dokkaebi.Tests
{
    /// <summary>0장 튜토리얼 가이드 (결정 2-85 · TutorialTable).</summary>
    public class TutorialTests
    {
        private static string IdAt(int index) => TutorialTable.Steps[index].Id;

        [Test]
        public void 처음엔_봇짐_열기부터()
        {
            var p = new StoryProgress();
            int index = TutorialTable.Evaluate(p, default, out int done);

            Assert.AreEqual("tut_bundle", IdAt(index));
            Assert.AreEqual(0, done);
            Assert.IsFalse(ChapterZeroTable.TutorialDone(p));
        }

        [Test]
        public void 순서대로_한_단계씩_넘어간다()
        {
            var p = new StoryProgress();
            var f = new TutorialFacts { BundleOpened = true };

            Assert.AreEqual("tut_take", IdAt(TutorialTable.Evaluate(p, f, out _)));

            f.WeaponInHand = true;
            Assert.AreEqual("tut_equip", IdAt(TutorialTable.Evaluate(p, f, out _)));

            f.WeaponEquipped = true;
            Assert.AreEqual("tut_kill", IdAt(TutorialTable.Evaluate(p, f, out _)));

            f.KillsThisStep = 2;
            Assert.AreEqual("tut_kill", IdAt(TutorialTable.Evaluate(p, f, out _)), "셋을 채워야 한다");

            f.KillsThisStep = 3;
            Assert.AreEqual("tut_loot", IdAt(TutorialTable.Evaluate(p, f, out _)));
        }

        [Test]
        public void 이미_해_둔_일은_한꺼번에_넘어간다()
        {
            var p = new StoryProgress();
            var f = new TutorialFacts { BundleOpened = true, WeaponEquipped = true };

            int index = TutorialTable.Evaluate(p, f, out int done);

            Assert.AreEqual("tut_kill", IdAt(index));
            Assert.AreEqual(3, done, "봇짐 · 줍기 · 장착");
        }

        [Test]
        public void 쓰러져_소굴에_와도_0_1_단계는_건너뛰고_소굴_단계로()
        {
            var p = new StoryProgress();
            var f = new TutorialFacts { Arrived = true };

            int index = TutorialTable.Evaluate(p, f, out int done);

            Assert.AreEqual("tut_stash", IdAt(index));
            Assert.AreEqual(1, done, "건너뛴 단계는 「마쳤다」로 세지 않는다 — 돌아가기만 마쳤다");
        }

        [Test]
        public void 끝까지_하면_튜토리얼이_끝나고_바로_착용이_켜진다()
        {
            var p = new StoryProgress();
            var f = new TutorialFacts
            {
                BundleOpened = true, WeaponEquipped = true, KillsThisStep = 3, CorpseLooted = true,
                Arrived = true, StashUsed = true, Entered02 = true, GiftDropped = true,
                CoreGemOwned = true, CoreSocketed = true, Extracted02 = true,
            };

            Assert.AreEqual(-1, TutorialTable.Evaluate(p, f, out _));
            Assert.IsTrue(TutorialTable.IsDone(p));
            Assert.IsTrue(ChapterZeroTable.TutorialDone(p));
        }

        [Test]
        public void 구슬_없이_0_2에서_철수하면_구슬_단계를_건너뛴다()
        {
            var p = new StoryProgress();
            var f = new TutorialFacts
            {
                BundleOpened = true, WeaponEquipped = true, KillsThisStep = 3, CorpseLooted = true,
                Arrived = true, StashUsed = true, Entered02 = true, Extracted02 = true,
            };

            Assert.AreEqual(-1, TutorialTable.Evaluate(p, f, out _));
        }

        [Test]
        public void 옛_세이브는_1_1에_들어섰으면_끝난_것으로()
        {
            var p = new StoryProgress();
            p.See(StoryTable.EnterEvent("1-1"));

            Assert.IsTrue(ChapterZeroTable.TutorialDone(p));
        }

        [Test]
        public void 다_끝내기는_모든_단계를_적는다()
        {
            var p = new StoryProgress();
            TutorialTable.CompleteAll(p);

            foreach (TutorialStep step in TutorialTable.Steps)
                Assert.IsTrue(p.HasSeen(step.Id), step.Id);

            Assert.AreEqual(-1, TutorialTable.Evaluate(p, default, out _));
        }

        [Test]
        public void 단계_id는_겹치지_않고_안내가_있다()
        {
            var ids = new System.Collections.Generic.HashSet<string>();

            foreach (TutorialStep step in TutorialTable.Steps)
            {
                Assert.IsTrue(ids.Add(step.Id), step.Id);
                Assert.IsNotEmpty(step.Text);
                Assert.IsNotEmpty(step.Hint);
            }
        }
    }
}
