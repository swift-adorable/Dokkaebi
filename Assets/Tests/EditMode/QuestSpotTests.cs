using System.Linq;
using NUnit.Framework;

namespace Dokkaebi.Tests
{
    /// <summary>【퀘스트를 본문 행동대로】 (결정 2-108 · QuestSpotTable)</summary>
    public class QuestSpotTests
    {
        private static QuestTracker Tracker(StoryProgress p) => new(p, _ => false);

        [Test]
        public void 자리_id는_겹치지_않고_퀘스트가_있다()
        {
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (QuestSpotDefinition s in QuestSpotTable.Spots)
            {
                Assert.IsTrue(ids.Add(s.Id), s.Id);
                Assert.IsNotNull(QuestTable.Quest(s.QuestId), s.Id);
                Assert.IsNotEmpty(s.Label, s.Id);
                if (s.ZoneId != QuestSpotDefinition.BunkerZone)
                    Assert.IsNotNull(StoryTable.Zone(s.ZoneId), $"{s.Id} — {s.ZoneId}");
                foreach (string b in s.RequiresBosses)
                    Assert.IsNotNull(StoryTable.Boss(b), $"{s.Id} — {b}");
            }
        }

        [Test]
        public void 봇짐은_야광귀를_쓰러뜨린_뒤에_나타난다()
        {
            var p = new StoryProgress();
            QuestSpotDefinition bundle = QuestSpotTable.Spot(QuestSpotTable.BundleBack);

            Assert.IsFalse(QuestSpotTable.IsAvailable(bundle, p, Tracker(p)), "퀘스트를 받기 전");

            p.See(StoryTable.EnterEvent("1-1"));
            Assert.IsFalse(QuestSpotTable.IsAvailable(bundle, p, Tracker(p)), "야광귀가 아직 있다");

            p.Defeat("yagwanggwi");
            Assert.IsTrue(QuestSpotTable.IsAvailable(bundle, p, Tracker(p)));

            p.See(bundle.DoneEvent);
            Assert.IsFalse(QuestSpotTable.IsAvailable(bundle, p, Tracker(p)), "한 번 주우면 끝");
            Assert.AreEqual(QuestState.Completed, Tracker(p).StateOf(QuestSpotTable.Bundle));
        }

        [Test]
        public void 물레방아는_안부_절구_강철이_소식_순서다()
        {
            var p = new StoryProgress();
            p.See(StoryTable.NightEvent(1));

            QuestSpotDefinition mortar = QuestSpotTable.Spot(QuestSpotTable.Mortar);
            Assert.IsFalse(QuestSpotTable.IsAvailable(mortar, p, Tracker(p)), "안부부터");

            p.See(QuestSpotTable.EventOf(QuestSpotTable.MillGreet));
            Assert.IsTrue(QuestSpotTable.IsAvailable(mortar, p, Tracker(p)));

            p.See(mortar.DoneEvent);
            QuestDefinition q = QuestTable.Quest(QuestSpotTable.Watermill);
            Assert.AreEqual("1 / 1", Tracker(p).ProgressText(q.Objectives[1]), "절구를 들었다");

            p.See(QuestSpotTable.EventOf(QuestSpotTable.MillMortar));
            QuestSpotDefinition news = QuestSpotTable.Spot(QuestSpotTable.ElderMillNews);
            Assert.IsFalse(QuestSpotTable.IsAvailable(news, p, Tracker(p)), "강철이부터");

            p.Defeat("gangcheori");
            Assert.IsTrue(QuestSpotTable.IsAvailable(news, p, Tracker(p)));
        }

        [Test]
        public void 총명탕은_약재_셋_불씨_다섯을_모아_달여야_건넨다()
        {
            var p = new StoryProgress();
            p.See(StoryTable.NightEvent(2));
            QuestDefinition q = QuestTable.Quest(QuestSpotTable.Chongmyeong);
            QuestTracker t = Tracker(p);

            foreach (string h in QuestSpotTable.Herbs)
                p.See(QuestSpotTable.EventOf(h));
            Assert.IsTrue(t.IsMet(q.Objectives[0]));
            Assert.AreEqual("0 / 5", t.ProgressText(q.Objectives[1]));

            // 처녀 · 몽달 아궁이는 그 둘을 물리쳐야 나타난다.
            QuestSpotDefinition maiden = QuestSpotTable.Spot(QuestSpotTable.Embers[0]);
            Assert.IsFalse(QuestSpotTable.IsAvailable(maiden, p, t));
            p.Defeat("wongwi");
            Assert.IsTrue(QuestSpotTable.IsAvailable(maiden, p, t));

            QuestSpotDefinition brew = QuestSpotTable.Spot(QuestSpotTable.KitchenBrew);
            Assert.IsFalse(QuestSpotTable.IsAvailable(brew, p, t), "불씨가 모자라다");

            foreach (string e in QuestSpotTable.Embers)
                p.See(QuestSpotTable.EventOf(e));
            Assert.IsTrue(QuestSpotTable.IsAvailable(brew, p, t));
        }

        [Test]
        public void 넋은_구미호를_물리친_뒤_보낸다()
        {
            var p = new StoryProgress();
            p.See(StoryTable.EnterEvent("5-3"));
            p.See(StoryTable.EnterEvent("6-1"));
            QuestSpotDefinition send = QuestSpotTable.Spot(QuestSpotTable.SendSoul);

            Assert.IsFalse(QuestSpotTable.IsAvailable(send, p, Tracker(p)));
            p.Defeat("gumiho");
            Assert.IsTrue(QuestSpotTable.IsAvailable(send, p, Tracker(p)));

            p.See(send.DoneEvent);
            Assert.AreEqual(QuestState.Completed, Tracker(p).StateOf(QuestTable.MainLastStory));
        }

        [Test]
        public void 퀘스트_줄의_설명은_본문_퀘스트_문단_그대로()
        {
            // 퀘스트 창의 한 줄 설명 — 본문 「(퀘스트 …) 이름 — 설명」의 설명 (결정 2-106 · 2-108).
            string script = System.IO.File.ReadAllText(System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath), "docs/Dokkaebi_Story_Script.txt"));

            foreach (QuestDefinition q in QuestTable.Quests.Where(x => !string.IsNullOrEmpty(x.Summary)))
                StringAssert.Contains(q.Summary.Replace(" (구역 목표)", string.Empty), script, q.Id);
        }
    }
}
