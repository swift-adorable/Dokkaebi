using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 퀘스트 표(Quest Data) — 메인 2 · 서브 9가 본문 · 이야기 표 · 진행 규칙과 맞는가.
    /// (결정 2-55 · 2-56 · Story Lock 2-67)
    /// </summary>
    public class QuestTests
    {
        private static string ScriptText()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            return File.ReadAllText(Path.Combine(root, "docs/Dokkaebi_Story_Script.txt"));
        }

        /// <summary>장 하나를 이야기 순서대로 끝낸다 — 들어가고 · 보스를 쓰러뜨리고 · 조각을 줍는다.</summary>
        private static void ClearChapter(StoryProgress p, int chapter)
        {
            foreach (ZoneDefinition z in StoryTable.Chapter(chapter).Zones)
            {
                p.See(StoryTable.EnterEvent(z.Id));

                foreach (BossDefinition b in StoryTable.BossesIn(z.Id))
                    p.Defeat(b.Id);

                PieceDefinition piece = StoryTable.PieceIn(z.Id);
                if (piece != null)
                    p.CollectPiece(piece.Id);
            }

            if (chapter >= 1 && chapter <= 5)
                p.See(StoryTable.NightEvent(chapter));
        }

        [Test]
        public void 메인은_둘_서브는_아홉이다()
        {
            Assert.AreEqual(2, QuestTable.OfKind(QuestKind.Main).Count());
            Assert.AreEqual(9, QuestTable.OfKind(QuestKind.Sub).Count());
            Assert.AreEqual(QuestTable.Quests.Count, QuestTable.Quests.Select(q => q.Id).Distinct().Count());
        }

        [Test]
        public void 퀘스트_이름이_본문의_퀘스트_문단과_같다()
        {
            string text = ScriptText();

            foreach (QuestDefinition q in QuestTable.Quests)
            {
                string tag = q.Kind == QuestKind.Main ? "(퀘스트 / 메인)" : "(퀘스트 / 서브)";
                StringAssert.Contains($"{tag} {q.Title} —", text, q.Id);
            }
        }

        [Test]
        public void 본문의_퀘스트는_전부_표에_있다()
        {
            var titles = new HashSet<string>(QuestTable.Quests.Select(q => q.Title));

            foreach (string line in ScriptText().Split('\n'))
            {
                string l = line.Trim();

                if (!l.StartsWith("(퀘스트 / 메인) ") && !l.StartsWith("(퀘스트 / 서브) "))
                    continue;

                string title = l.Substring(l.IndexOf(')') + 2).Split(new[] { " —" }, System.StringSplitOptions.None)[0];
                Assert.IsTrue(titles.Contains(title), title);
            }
        }

        [Test]
        public void 목표와_시작이_가리키는_것이_전부_있다()
        {
            var events = new HashSet<string>(StoryScriptParser.Parse(ScriptText()).Keys);

            foreach (QuestDefinition q in QuestTable.Quests)
            {
                Assert.IsTrue(events.Contains(q.StartEvent), $"{q.Id} 시작 {q.StartEvent}");
                Assert.IsNotNull(StoryTable.Merchant(q.GiverId), $"{q.Id} 준 사람 {q.GiverId}");
                Assert.Greater(q.Objectives.Length, 0, q.Id);

                foreach (QuestObjective o in q.Objectives)
                {
                    string at = $"{q.Id}: {o.Text}";

                    switch (o.Condition)
                    {
                        case QuestCondition.Seen:
                            Assert.IsTrue(events.Contains(o.Target), at);
                            break;
                        case QuestCondition.DefeatBoss:
                            Assert.IsNotNull(StoryTable.Boss(o.Target), at);
                            break;
                        case QuestCondition.ClearZone:
                            Assert.IsNotNull(StoryTable.Zone(o.Target), at);
                            break;
                        case QuestCondition.Build:
                            Assert.IsTrue(BuildingTable.Find(o.Target) != null
                                          || QuestTable.PlannedBuildings.Contains(o.Target), at);
                            break;
                    }
                }
            }
        }

        [Test]
        public void 목표가_여는_장이_진행_규칙과_같다()
        {
            var p = new StoryProgress();
            var tracker = new QuestTracker(p);
            QuestDefinition main = QuestTable.Quest(QuestTable.MainMyStory);

            for (int chapter = 0; chapter <= StoryTable.LastChapter; chapter++)
            {
                var before = main.Objectives.Where(o => o.UnlocksChapter > 0 && !tracker.IsMet(o)).ToList();

                foreach (QuestObjective o in before)
                    Assert.IsFalse(p.IsChapterOpen(o.UnlocksChapter), $"{o.Text} 전에 {o.UnlocksChapter}장이 열렸다");

                ClearChapter(p, chapter);

                foreach (QuestObjective o in main.Objectives.Where(o => o.UnlocksChapter > 0 && tracker.IsMet(o)))
                    Assert.IsTrue(p.IsChapterOpen(o.UnlocksChapter), $"{o.Text} 뒤에 {o.UnlocksChapter}장이 닫혀 있다");
            }

            Assert.AreEqual(QuestState.Completed, tracker.StateOf(QuestTable.MainMyStory));
            Assert.AreEqual(QuestState.Completed, tracker.StateOf(QuestTable.MainLastStory));
        }

        [Test]
        public void 메인_둘이_이어진다()
        {
            var p = new StoryProgress();
            var t = new QuestTracker(p);

            Assert.AreEqual(QuestState.Hidden, t.StateOf(QuestTable.MainMyStory));

            ClearChapter(p, 0);
            Assert.AreEqual(QuestState.Active, t.StateOf(QuestTable.MainMyStory));
            Assert.AreEqual("hyeonmu", t.NextObjective(QuestTable.Quest(QuestTable.MainMyStory)).Target);
            Assert.AreSame(QuestTable.Quest(QuestTable.MainMyStory), t.Tracked());

            for (int c = 1; c <= 5; c++)
                ClearChapter(p, c);

            Assert.AreEqual(QuestState.Completed, t.StateOf(QuestTable.MainMyStory));
            Assert.AreEqual(QuestState.Active, t.StateOf(QuestTable.MainLastStory));
            Assert.AreSame(QuestTable.Quest(QuestTable.MainLastStory), t.Tracked());
        }

        [Test]
        public void 가게_퀘스트는_건물을_지으면_끝난다()
        {
            var p = new StoryProgress();
            var built = new HashSet<string>();
            var t = new QuestTracker(p, built.Contains);

            Assert.AreEqual(QuestState.Hidden, t.StateOf("sub_elder_store"));

            p.See(StoryTable.EnterEvent("0-1"));
            Assert.AreEqual(QuestState.Active, t.StateOf("sub_elder_store"));

            built.Add(BuildingTable.GeneralStore);
            Assert.AreEqual(QuestState.Completed, t.StateOf("sub_elder_store"));
        }

        [Test]
        public void 목표를_앞질러_채워도_다음_목표는_첫_빈칸이다()
        {
            var p = new StoryProgress();
            var t = new QuestTracker(p);
            QuestDefinition q = QuestTable.Quest("sub_watermill");

            p.PassNight(1);
            p.See(StoryTable.NightEvent(1));
            p.Defeat("gangcheori");

            Assert.AreEqual(QuestState.Active, t.StateOf(q));
            Assert.AreEqual(1, t.MetCount(q));
            Assert.AreSame(q.Objectives[0], t.NextObjective(q));
        }

        [Test]
        public void 서브_아홉은_이야기를_끝까지_가면_가게_넷만_남는다()
        {
            var p = new StoryProgress();
            var t = new QuestTracker(p);

            for (int c = 0; c <= StoryTable.LastChapter; c++)
                ClearChapter(p, c);

            foreach (QuestDefinition q in QuestTable.OfKind(QuestKind.Sub))
            {
                bool shop = q.Objectives.Any(o => o.Condition == QuestCondition.Build);
                Assert.AreEqual(shop ? QuestState.Active : QuestState.Completed, t.StateOf(q), q.Id);
            }
        }
        // ── 추적 HUD 글 (임시 화면) ───────────────────────────────────

        [Test]
        public void HUD는_메인의_다음_목표를_보인다()
        {
            var p = new StoryProgress();
            var t = new QuestTracker(p);
            ClearChapter(p, 0);

            QuestDefinition q = t.Tracked();
            Assert.AreEqual("메인 · 내 이야기를 찾는 길", QuestHudText.Title(q));
            Assert.AreEqual("현무패 — 북쪽 젖은 장터길 (0/6)", QuestHudText.Objective(t, q));

            ClearChapter(p, 1);
            Assert.AreEqual("청룡패 — 동쪽 물레방아길 (1/6)", QuestHudText.Objective(t, q));
        }

        [Test]
        public void 알림은_받음_갱신_완료를_한_번씩_알린다()
        {
            var p = new StoryProgress();
            var t = new QuestTracker(p);

            Assert.IsEmpty(QuestHudText.Diff(null, QuestHudText.Snapshot(t)), "처음 보는 상태는 알리지 않는다");

            var a = QuestHudText.Snapshot(t);
            p.See(StoryTable.EnterEvent("0-1"));
            p.See(StoryTable.EnterEvent("0-2"));
            CollectionAssert.Contains(QuestHudText.Diff(a, QuestHudText.Snapshot(t)), "새 퀘스트 — 내 이야기를 찾는 길");

            var b = QuestHudText.Snapshot(t);
            p.See(StoryTable.EnterEvent("1-1"));
            p.Defeat("yagwanggwi");
            List<string> diff = QuestHudText.Diff(b, QuestHudText.Snapshot(t));
            CollectionAssert.Contains(diff, "완료 — 봇짐 되찾기");
            CollectionAssert.DoesNotContain(diff, "새 퀘스트 — 봇짐 되찾기", "받자마자 끝나면 완료만 알린다");

            var c = QuestHudText.Snapshot(t);
            p.Defeat("dalgyal");
            p.Defeat("hyeonmu");
            CollectionAssert.Contains(QuestHudText.Diff(c, QuestHudText.Snapshot(t)), "목표 갱신 — 내 이야기를 찾는 길");

            Assert.IsEmpty(QuestHudText.Diff(QuestHudText.Snapshot(t), QuestHudText.Snapshot(t)));
        }
    }
}
