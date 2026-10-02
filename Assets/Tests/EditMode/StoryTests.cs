using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 이야기 표 · 진행 · 본문 나누기. (로드맵 3단계 · docs/Dokkaebi_Story.md)
    /// </summary>
    public class StoryTests
    {
        private static string ScriptText()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            return File.ReadAllText(Path.Combine(root, "docs/Dokkaebi_Story_Script.txt"));
        }

        // ── 표 ───────────────────────────────────────────────────────

        [Test]
        public void 장마다_구역_수가_문서와_같다()
        {
            int[] expected = { 2, 3, 4, 3, 4, 3, 2 };   // Story 2절

            Assert.AreEqual(expected.Length, StoryTable.Chapters.Count);

            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], StoryTable.Chapters[i].Zones.Length, $"{i}장");
        }

        [Test]
        public void 보스는_있는_구역에_나오고_장_보스는_장마다_하나다()
        {
            foreach (BossDefinition b in StoryTable.Bosses)
            {
                Assert.IsNotNull(StoryTable.Zone(b.ZoneId), $"{b.Name}의 구역 {b.ZoneId}이 없습니다.");
                Assert.Greater(b.Bodies.Length, 0, b.Name);
            }

            for (int c = 1; c <= StoryTable.LastChapter; c++)
            {
                int count = StoryTable.Bosses.Count(b => b.IsChapterBoss && StoryTable.Zone(b.ZoneId).Chapter == c);
                Assert.AreEqual(1, count, $"{c}장 보스 수");
            }
        }

        [Test]
        public void 사신패는_1장부터_4장_보스가_하나씩_내준다()
        {
            var tablets = StoryTable.Bosses.Where(b => b.Tablet.Length > 0).ToList();

            Assert.AreEqual(StoryTable.TabletsForGate, tablets.Count);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4 },
                tablets.Select(b => StoryTable.Zone(b.ZoneId).Chapter));
        }

        [Test]
        public void 보스로_끝내는_구역에는_보스가_있고_조각으로_끝내는_구역에는_조각이_있다()
        {
            foreach (ChapterDefinition c in StoryTable.Chapters)
            foreach (ZoneDefinition z in c.Zones)
            {
                if (z.ClearBy == ZoneClear.Boss)
                    Assert.Greater(StoryTable.BossesIn(z.Id).Count, 0, z.Id);

                if (z.ClearBy == ZoneClear.Piece)
                    Assert.IsNotNull(StoryTable.PieceIn(z.Id), z.Id);
            }
        }

        [Test]
        public void 기억의_조각은_아홉_장이고_구역마다_많아야_하나다()
        {
            Assert.AreEqual(9, StoryTable.Pieces.Count);
            Assert.AreEqual(9, StoryTable.Pieces.Select(p => p.ZoneId).Distinct().Count());

            foreach (PieceDefinition p in StoryTable.Pieces)
                Assert.IsNotNull(StoryTable.Zone(p.ZoneId), p.Id);
        }

        [Test]
        public void 상인이_여는_가게는_벙커_건물표에_있다()
        {
            Assert.AreEqual(4, StoryTable.Merchants.Count);

            foreach (MerchantDefinition m in StoryTable.Merchants)
                if (m.BuildingId.Length > 0)
                    Assert.IsNotNull(BuildingTable.Find(m.BuildingId), m.Name);
        }

        // ── 진행 ─────────────────────────────────────────────────────

        private static void See(StoryProgress p, string zone) => p.See(StoryTable.EnterEvent(zone));

        private static void ClearChapter(StoryProgress p, int chapter)
        {
            foreach (ZoneDefinition z in StoryTable.Chapter(chapter).Zones)
            {
                See(p, z.Id);

                foreach (BossDefinition b in StoryTable.BossesIn(z.Id))
                    p.Defeat(b.Id);

                PieceDefinition piece = StoryTable.PieceIn(z.Id);

                if (piece != null)
                    p.CollectPiece(piece.Id);
            }
        }

        [Test]
        public void 처음에는_0_1만_열려_있다()
        {
            var p = new StoryProgress();

            CollectionAssert.AreEqual(new[] { "0-1" }, p.OpenZones().Select(z => z.Id));
        }

        [Test]
        public void 구역은_앞_구역을_끝내면_열린다()
        {
            var p = new StoryProgress();

            See(p, "0-1");
            Assert.IsTrue(p.IsZoneOpen("0-2"));
            Assert.IsFalse(p.IsChapterOpen(1));

            See(p, "0-2");
            Assert.IsTrue(p.IsZoneOpen("1-1"));
            Assert.IsFalse(p.IsZoneOpen("1-2"), "야광귀를 쓰러뜨리기 전에 1-2가 열렸습니다.");

            p.Defeat("yagwanggwi");
            Assert.IsTrue(p.IsZoneOpen("1-2"));
        }

        [Test]
        public void 한_구역의_보스는_앞의_것부터_나온다()
        {
            var p = new StoryProgress();

            Assert.AreEqual("wongwi", p.NextBossIn("3-2").Id);
            p.Defeat("wongwi");
            Assert.AreEqual("dueoksini", p.NextBossIn("3-2").Id);
            Assert.IsFalse(p.IsCleared("3-2"), "두억시니가 남았는데 3-2를 끝냈습니다.");

            p.Defeat("dueoksini");
            Assert.IsTrue(p.IsCleared("3-2"));
            Assert.IsNull(p.NextBossIn("3-2"), "중간 보스만 있는 구역은 다 쓰러뜨리면 보스가 없다.");
        }

        [Test]
        public void 장_보스는_쓰러뜨린_뒤에도_다시_나온다()
        {
            var p = new StoryProgress();
            p.Defeat("hyeonmu");

            Assert.AreEqual("hyeonmu", p.NextBossIn("1-3").Id);
        }

        [Test]
        public void 다섯째_장은_사신패_넷이_있어야_열린다()
        {
            var p = new StoryProgress();

            for (int c = 0; c <= 3; c++)
                ClearChapter(p, c);

            Assert.AreEqual(3, p.Tablets.Count);
            Assert.IsFalse(p.IsChapterOpen(5));

            p.Defeat("baekho");
            Assert.AreEqual(4, p.Tablets.Count);
            Assert.IsTrue(p.IsChapterOpen(5), "사신패 넷이 모였는데 돌문이 열리지 않았습니다.");
        }

        [Test]
        public void 장을_끝내면_밤이_오고_그_밤의_상인이_온다()
        {
            var p = new StoryProgress();

            Assert.IsTrue(p.HasMerchant(StoryTable.Elder), "영감은 처음부터 있습니다.");
            Assert.IsFalse(p.IsBlueprintOpen(BuildingTable.GeneralStore));
            Assert.IsTrue(p.IsBlueprintOpen(BuildingTable.Workbench), "작업대는 상인 없이 짓습니다.");

            ClearChapter(p, 0);
            ClearChapter(p, 1);
            Assert.AreEqual(1, p.PendingNight());

            p.PassNight(1);
            Assert.IsTrue(p.HasMerchant(StoryTable.Chambong));
            Assert.IsTrue(p.HasMerchant(StoryTable.Debtor));
            Assert.IsFalse(p.HasMerchant(StoryTable.Gildal), "길달은 두 번째 밤에 옵니다.");
            Assert.IsTrue(p.IsBlueprintOpen(BuildingTable.GeneralStore));
            Assert.AreEqual(0, p.PendingNight());
        }

        [Test]
        public void 설계도가_닫혀_있으면_지을_수_없다()
        {
            var state = new BuildingState();
            BuildingDefinition store = BuildingTable.Find(BuildingTable.GeneralStore);

            Assert.AreEqual(BuildError.MissingMerchant,
                state.CanBuild(store, 99999, _ => 999, _ => false));
            Assert.AreEqual(BuildError.None,
                state.CanBuild(store, 99999, _ => 999, _ => true));
            Assert.AreEqual(BuildError.None,
                state.CanBuild(store, 99999, _ => 999), "판정을 넘기지 않으면 따지지 않는다.");
        }

        [Test]
        public void 진행은_세이브로_되살아난다_표에_없는_id는_버린다()
        {
            var p = new StoryProgress();
            p.Defeat("hyeonmu");
            p.CollectPiece("piece_3");
            p.CollectNotice("notice_1");
            p.See("enter_0-1");
            p.PassNight(1);

            SavedStory saved = p.Capture();
            saved.bosses.Add("없는_보스");

            var q = new StoryProgress();
            q.Restore(saved);

            Assert.IsTrue(q.HasDefeated("hyeonmu"));
            Assert.IsFalse(q.HasDefeated("없는_보스"));
            Assert.IsTrue(q.HasPiece("piece_3"));
            Assert.IsTrue(q.HasNotice("notice_1"));
            Assert.IsTrue(q.HasSeen("enter_0-1"));
            Assert.IsTrue(q.HasNight(1));
        }

        [Test]
        public void 구미호를_쓰러뜨리면_엔딩이다()
        {
            var p = new StoryProgress();
            Assert.IsFalse(p.IsEnded);
            p.Defeat("gumiho");
            Assert.IsTrue(p.IsEnded);
        }

        // ── 본문 ─────────────────────────────────────────────────────

        [Test]
        public void 게임이_읽는_본문은_docs의_본문과_같다()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            string copy = File.ReadAllText(Path.Combine(root, "Assets/Resources/Story/StoryScript.txt"));

            Assert.AreEqual(ScriptText(), copy,
                "본문이 바뀌었습니다. 「Dokkaebi/Story/이야기 본문 가져오기」를 실행하십시오.");
        }

        [Test]
        public void 본문에서_프롤로그_밤_다섯_조각_아홉이_나온다()
        {
            Dictionary<string, StoryPassage> p = StoryScriptParser.Parse(ScriptText());

            Assert.IsTrue(p.ContainsKey(StoryTable.PrologueEvent));

            for (int n = 1; n <= 5; n++)
                Assert.IsTrue(p.ContainsKey(StoryTable.NightEvent(n)), $"night_{n}");

            foreach (PieceDefinition piece in StoryTable.Pieces)
                Assert.IsTrue(p.ContainsKey(StoryTable.PieceEvent(piece.Id)), piece.Id);

            StringAssert.Contains("이 아이 이름은 아직 정해지지 않았다",
                p[StoryTable.PieceEvent("piece_1")].Pages[0]);
        }

        [Test]
        public void 본문의_구역마다_들어가는_이야기가_있고_보스마다_싸운_뒤_이야기가_있다()
        {
            Dictionary<string, StoryPassage> p = StoryScriptParser.Parse(ScriptText());

            foreach (ChapterDefinition c in StoryTable.Chapters)
            foreach (ZoneDefinition z in c.Zones)
                Assert.IsTrue(p.ContainsKey(StoryTable.EnterEvent(z.Id)), z.Id);

            foreach (BossDefinition b in StoryTable.Bosses)
            {
                Assert.IsTrue(p.ContainsKey(StoryTable.BossEvent(b.Id)), $"{b.Name}의 「싸움 끝에」 문단이 없습니다.");
                StringAssert.StartsWith("싸움", p[StoryTable.BossEvent(b.Id)].Pages[0], b.Name);
            }
        }

        [Test]
        public void 구역_이야기에는_기억의_조각_글이_섞이지_않는다()
        {
            Dictionary<string, StoryPassage> p = StoryScriptParser.Parse(ScriptText());

            foreach (StoryPassage passage in p.Values)
                foreach (string page in passage.Pages)
                    StringAssert.DoesNotStartWith("【기억의 조각", page, passage.Id);
        }

        [Test]
        public void 마지막_보스_이야기는_끝으로_닫힌다()
        {
            Dictionary<string, StoryPassage> p = StoryScriptParser.Parse(ScriptText());
            List<string> pages = p[StoryTable.BossEvent("gumiho")].Pages;

            Assert.AreEqual("(끝)", pages[pages.Count - 1]);
            StringAssert.Contains("오는 동안 이미 내 이야기가 생겼네", pages[pages.Count - 2],
                "엔딩은 그 한 줄 뒤에서 여백을 두고 끝난다 (결정 2-48).");
        }

        [Test]
        public void 사신패는_싸운_직후에_받는다()
        {
            Dictionary<string, StoryPassage> p = StoryScriptParser.Parse(ScriptText());

            foreach (BossDefinition b in StoryTable.Bosses)
            {
                if (b.Tablet.Length == 0)
                    continue;

                string after = string.Join("\n", p[StoryTable.BossEvent(b.Id)].Pages);
                StringAssert.Contains(b.Tablet, after, $"{b.Name}을 쓰러뜨린 뒤 이야기에 {b.Tablet}이 없습니다.");
            }
        }

        [Test]
        public void 기억의_조각은_세계관_설명이_아니라_프롤로그의_기억이다()
        {
            Dictionary<string, StoryPassage> p = StoryScriptParser.Parse(ScriptText());

            StringAssert.Contains("오늘이", p[StoryTable.PieceEvent("piece_5")].Pages[0]);

            foreach (PieceDefinition piece in StoryTable.Pieces)
                StringAssert.DoesNotContain("세 갈래", p[StoryTable.PieceEvent(piece.Id)].Pages[0], piece.Id);
        }

        [Test]
        public void 옛_설정의_잔재가_본문에_없다()
        {
            string body = ScriptText();
            body = body.Substring(0, body.IndexOf("부록 1"));

            foreach (string old in new[] { "방위", "기운", "너도 데려가", "사람이 될 수도 있어", "사신들", "숨겼" })
                StringAssert.DoesNotContain(old, body, $"본문에 「{old}」가 남았습니다.");
        }

        [Test]
        public void 조사는_받침을_따른다()
        {
            Assert.AreEqual("현무패를", Josa.EulReul("현무패"));
            Assert.AreEqual("참봉이", Josa.IGa("참봉"));
            Assert.AreEqual("빚쟁이가", Josa.IGa("빚쟁이"));
            Assert.AreEqual("길로", Josa.EuroRo("길"));
            Assert.AreEqual("돌문으로", Josa.EuroRo("돌문"));
        }
    }
}
