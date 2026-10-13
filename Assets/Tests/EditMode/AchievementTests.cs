using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Dokkaebi.Tests
{
    /// <summary>【업적】 (결정 2-118 · Progression_System 9절)</summary>
    public class AchievementTests
    {
        [Test]
        public void 여섯_갈래가_모두_있다()
        {
            var all = AchievementTable.All;
            Assert.AreEqual(6, all.Count(a => a.Kind == AchievementKind.Arrive), "각 장 첫 도착 6");
            Assert.AreEqual(4, all.Count(a => a.Kind == AchievementKind.Build), "상인 가게 넷");
            Assert.AreEqual(StoryTable.Bosses.Count * 2, all.Count(a => a.Kind == AchievementKind.BossKill), "보스별 1회 · 10회");
            Assert.AreEqual(3, all.Count(a => a.Kind == AchievementKind.Earn));
            Assert.AreEqual(4, all.Count(a => a.Kind == AchievementKind.Passive), "역행 제외 넷");
            Assert.IsFalse(all.Any(a => a.Kind == AchievementKind.Passive && a.Target.StartsWith("reg_")), "역행은 빼기로 했다");
            Assert.AreEqual(1, all.Count(a => a.Kind == AchievementKind.Workbench));
            Assert.AreEqual(1, all.Count(a => a.Kind == AchievementKind.Codex));
            Assert.AreEqual(1, all.Count(a => a.Kind == AchievementKind.Ending));
            Assert.AreEqual(all.Count, all.Select(a => a.Id).Distinct().Count(), "id 겹침 없음");
        }

        [Test]
        public void 판정과_한_번만_달성()
        {
            var s = new AchievementState();
            s.AddBossKill("gumiho");
            s.AddEarned(9_000);
            var c = new AchievementContext
            {
                Arrived = ch => ch == 1,
                BossKills = s.BossKills,
                Earned = s.Earned,
                WorkbenchStage = 4,
            };
            List<AchievementDefinition> fresh = s.Evaluate(c);
            var ids = fresh.Select(a => a.Id).ToList();
            CollectionAssert.Contains(ids, "arrive_1");
            CollectionAssert.Contains(ids, "boss_gumiho_1");
            CollectionAssert.DoesNotContain(ids, "boss_gumiho_10");
            CollectionAssert.DoesNotContain(ids, "earn_10000");
            CollectionAssert.Contains(ids, "workbench_4");
            Assert.AreEqual(0, s.Evaluate(c).Count, "이미 한 것은 다시 나오지 않는다");

            for (int i = 0; i < 9; i++) s.AddBossKill("gumiho");
            s.AddEarned(1_000);
            c.Earned = s.Earned;
            ids = s.Evaluate(c).Select(a => a.Id).ToList();
            CollectionAssert.Contains(ids, "boss_gumiho_10");
            CollectionAssert.Contains(ids, "earn_10000", "번 엽전의 합 — 써도 줄지 않는다");
        }

        [Test]
        public void 세이브로_되살린다()
        {
            var s = new AchievementState();
            s.AddBossKill("hyeonmu");
            s.AddEarned(123);
            s.Arrive(2);
            s.Evaluate(new AchievementContext { Arrived = s.HasArrived });
            s.Capture(out var unlocked, out var kills, out long earned, out var chapters);

            var t = new AchievementState();
            t.Restore(unlocked, kills, earned, chapters);
            Assert.AreEqual(1, t.BossKills("hyeonmu"));
            Assert.AreEqual(123, t.Earned);
            Assert.IsTrue(t.HasArrived(2));
            Assert.IsTrue(t.IsUnlocked("arrive_2"));
        }
    }
}
