using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>Boss Data — 이야기 보스 17의 체력 · 피해 (결정 2-68 · Combat_Baseline 5-1절).</summary>
    public class BossDataTests
    {
        [Test]
        public void 이야기_보스마다_몸_수만큼_있다()
        {
            foreach (BossDefinition b in StoryTable.Bosses)
            {
                BossStats s = BossDataTable.Of(b.Id);
                Assert.IsNotNull(s, b.Id);
                Assert.AreEqual(b.Bodies.Length, s.Health.Length, b.Id);
                Assert.AreEqual(b.Bodies.Length, s.Damage.Length, b.Id);
            }

            Assert.AreEqual(StoryTable.Bosses.Count, BossDataTable.All.Count);
        }

        [Test]
        public void 보스_몸에_Boss_Data가_들어간다()
        {
            BossDefinition hyeonmu = StoryTable.Boss("hyeonmu");
            EnemyProfile p = EnemyProfile.ForBoss(hyeonmu, 0);
            Assert.AreEqual(500, p.health);
            Assert.AreEqual(21, p.damage);
            Assert.AreEqual(2f, p.armour, "현무 방어도 2 — 1장 무기는 관통 0");

            EnemyProfile gumiho = EnemyProfile.ForBoss(StoryTable.Boss("gumiho"), 0);
            Assert.AreEqual(1f, gumiho.resistances.fire, "구미호는 화염 약점이 없다");
            Assert.AreEqual(0.66f, gumiho.resistances.physical, 0.001f, "나머지 저항은 몸(허깨비) 그대로");

            EnemyProfile eoduk = EnemyProfile.ForBoss(StoryTable.Boss("eodukssini"), 0);
            Assert.AreEqual(1.5f, eoduk.resistances.fire, "다른 허깨비 몸은 화염 1.5 그대로");
            Assert.AreEqual(EnemyArchetypeTable.Of(EnemyArchetype.Settled).armour + EnemyRarityTable.ArmourBonus(EnemyRarity.Unique), eoduk.armour);
        }

        [Test]
        public void 장_보스는_장이_오를수록_단단하다_곡선이_뒤집히지_않는다()
        {
            // 처치 시간은 일부러 같게(40초) 맞췄으므로 맨 체력이 아니라 「뚫리는 정도를 뺀 체력」을 본다.
            // 그 장에 처음 들어갈 때의 무기 티어(장-1, 1장은 1)의 방어 관통 — research/sim/enemy_sim.py W [가정].
            int[] pen = { 0, 0, 1, 2, 3, 5, 6 };
            float last = 0f;

            foreach (BossDefinition b in StoryTable.Bosses.Where(b => b.IsChapterBoss))
            {
                int chapter = StoryTable.Zone(b.ZoneId).Chapter;
                int tier = Mathf.Max(1, chapter - 1);
                EnemyProfile p = EnemyProfile.ForBoss(b, 0);
                float through = 2f / (Mathf.Max(p.armour - pen[tier], 0f) + 2f);
                float effective = p.health / (through * p.resistances.fire);

                Assert.Greater(effective, last, b.Id);
                last = effective;
            }

            Assert.AreEqual(BossDataTable.All.Max(s => s.Health.Max()), BossDataTable.Of("gumiho").Health[0], "구미호가 가장 단단하다");
        }

        [Test]
        public void 코드가_Combat_Baseline_5_1절_표와_같다()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            string[] lines = File.ReadAllLines(Path.Combine(root, "docs/Dokkaebi_Combat_Baseline.md"));

            var health = new Dictionary<string, List<int>>();
            var damage = new Dictionary<string, List<int>>();

            foreach (string line in lines)
            {
                string[] c = line.Split('|').Select(x => x.Trim()).ToArray();

                if (c.Length < 12 || !c[2].StartsWith("`"))
                    continue;

                string id = c[2].Trim('`');
                if (!health.ContainsKey(id)) { health[id] = new List<int>(); damage[id] = new List<int>(); }
                health[id].Add(int.Parse(c[7].Replace("*", "").Replace(",", "")));
                damage[id].Add(int.Parse(c[8]));
            }

            Assert.AreEqual(BossDataTable.All.Count, health.Count, "문서 표의 보스 수");

            foreach (BossStats s in BossDataTable.All)
            {
                CollectionAssert.AreEqual(health[s.Id], s.Health, $"{s.Id} 체력");
                CollectionAssert.AreEqual(damage[s.Id], s.Damage, $"{s.Id} 피해");
            }
        }
        // ── 처치 시간 계약 — 기준 빌드에서 중간 20초 · 장 40초 · 최종 60초 (결정 2-68) ──
        // 무기 표는 Combat_Baseline 3절, 빌드 배율은 research/sim/enemy_sim.py의 BUILD [가정] 그대로다.
        // 체력 · 방어도 · 화염 배율은 실제 코드 경로(EnemyProfile.ForBoss)에서 읽는다 — 누가 체력만 바꿔도 여기서 잡힌다.
        private static readonly float[] WeaponDamage = { 0f, 10f, 13f, 16f, 22f, 28f, 34f };
        private static readonly float[] WeaponInterval = { 1f, 0.40f, 0.35f, 0.32f, 0.45f, 0.50f, 0.45f };
        private static readonly float[] WeaponPenetration = { 0f, 0f, 1f, 2f, 3f, 5f, 6f };
        private static readonly float[] Build = { 1f, 1.0f, 1.2f, 1.5f, 1.8f, 2.2f, 2.6f };

        private static float KillSeconds(BossDefinition boss)
        {
            int chapter = StoryTable.Zone(boss.ZoneId).Chapter;
            int tier = Mathf.Max(1, chapter - 1);
            float total = 0f;

            for (int i = 0; i < boss.Bodies.Length; i++)
            {
                EnemyProfile p = EnemyProfile.ForBoss(boss, i);
                float through = 2f / (Mathf.Max(p.armour - WeaponPenetration[tier], 0f) + 2f);
                float dps = WeaponDamage[tier] / WeaponInterval[tier] * Build[chapter] * through * p.resistances.fire;
                total += p.health / dps;
            }

            return total;
        }

        [Test]
        public void 기준_빌드에서_처치_시간이_20_40_60초다()
        {
            foreach (BossDefinition b in StoryTable.Bosses)
            {
                float target = b.Id == "gumiho" ? BossDataTable.FinalBossSeconds
                             : b.IsChapterBoss ? BossDataTable.ChapterBossSeconds
                             : BossDataTable.MidBossSeconds;

                Assert.AreEqual(target, KillSeconds(b), target * 0.05f, $"{b.Name} ({b.Id})");
            }
        }
    }
}
