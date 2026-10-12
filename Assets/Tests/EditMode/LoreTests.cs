using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 【세계관 도감 · 팝업 · 퀘스트 창】 (결정 2-106) — 본문 괄호 줄을 그대로 읽는다.
    /// </summary>
    public class LoreTests
    {
        private static string ScriptText()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            return File.ReadAllText(Path.Combine(root, "docs/Dokkaebi_Story_Script.txt"));
        }

        [Test]
        public void 도감_일곱_팝업_하나()
        {
            List<LoreEntry> lore = LoreTable.Parse(ScriptText());

            Assert.AreEqual(7, lore.Count(e => e.Kind == LoreKind.Codex));
            Assert.AreEqual(1, lore.Count(e => e.Kind == LoreKind.Popup));

            CollectionAssert.AreEquivalent(
                new[] { "도깨비 터", "도깨비의 탄생", "장승", "잊힘과 도깨비", "넋불", "꽃밭", "구미호" },
                lore.Where(e => e.Kind == LoreKind.Codex).Select(e => e.Title).ToArray());
            Assert.AreEqual("잊힘", lore.Single(e => e.Kind == LoreKind.Popup).Title);
        }

        [Test]
        public void 오의삼_이후에_열리는_줄은_오의삼을_봐야_열리고_메모는_보이지_않는다()
        {
            List<LoreEntry> lore = LoreTable.Parse(ScriptText());

            foreach (string title in new[] { "도깨비의 탄생", "잊힘과 도깨비" })
            {
                LoreEntry e = lore.Single(x => x.Title == title);
                Assert.AreEqual(StoryTable.EnterEvent("5-3"), e.UnlockEvent, title);
                StringAssert.DoesNotContain("이후에 열린다", e.Body, title);
            }

            Assert.AreEqual(StoryTable.PrologueEvent, lore.Single(x => x.Title == "도깨비 터").UnlockEvent);
        }

        [Test]
        public void 열리는_때는_본문에_있는_토막이다()
        {
            Dictionary<string, StoryPassage> passages = StoryScriptParser.Parse(ScriptText());

            foreach (LoreEntry e in LoreTable.Parse(ScriptText()))
                Assert.IsTrue(passages.ContainsKey(e.UnlockEvent), $"{e.Title} → {e.UnlockEvent}");
        }

        [Test]
        public void 튜토리얼_안내는_본문_글을_쓴다()
        {
            // 결정 2-107 — 「(UI 튜토리얼 / …)」 줄 이름이 단계의 Note와 맞아야 한다.
            List<LoreEntry> lore = LoreTable.Parse(ScriptText());

            foreach (TutorialStep step in TutorialTable.Steps)
                if (step.Note != null)
                    Assert.IsNotEmpty(LoreTable.BodyOf(lore, LoreKind.Tutorial, step.Note), step.Id);

            StringAssert.Contains("매듭에 묶은 새김패", LoreTable.BodyOf(lore, LoreKind.Tutorial, "쓰러짐"));
        }

        [Test]
        public void 토막을_보면_열린다()
        {
            var progress = new StoryProgress();
            LoreEntry e = LoreTable.Parse(ScriptText()).Single(x => x.Title == "도깨비 터");

            Assert.IsFalse(LoreTable.IsUnlocked(e, progress));
            progress.See(StoryTable.PrologueEvent);
            Assert.IsTrue(LoreTable.IsUnlocked(e, progress));
        }
    }
}
