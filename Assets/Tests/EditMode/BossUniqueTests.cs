using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>【중간 보스 고유 장비】 (결정 2-109 · 부록 1136)</summary>
    public class BossUniqueTests
    {
        [Test]
        public void 중간_보스_열하나는_고유_장비가_있고_장_보스는_없다()
        {
            ItemCatalog catalog = Resources.Load<ItemCatalog>("ItemCatalog");
            Assert.IsNotNull(catalog);

            int mids = 0;
            foreach (BossDefinition boss in StoryTable.Bosses)
            {
                string id = BossUniqueTable.ItemOf(boss.Id);

                if (boss.IsChapterBoss)
                {
                    Assert.IsNull(id, boss.Id);
                    continue;
                }

                mids++;
                Assert.IsNotNull(id, boss.Id);

                var item = catalog.Find(id) as EquipmentDefinition;
                Assert.IsNotNull(item, $"{boss.Id} — {id} 에셋이 카탈로그에 없다");

                int chapter = StoryTable.Zone(boss.ZoneId).Chapter;
                Assert.AreEqual(chapter, item.Tier, $"{id} — 티어 = 그 장");
                Assert.Greater(item.BaseValue, 0, id);
            }

            Assert.AreEqual(11, mids);
        }

        [Test]
        public void 다시_나왔을_때만_떨어뜨린다()
        {
            BossDefinition yagwanggwi = StoryTable.Boss("yagwanggwi");
            BossDefinition hyeonmu = StoryTable.Boss("hyeonmu");

            Assert.IsFalse(BossUniqueTable.DropsOn(yagwanggwi, alreadyDefeated: false), "이야기에서 처음 만날 때는 아니다");
            Assert.IsTrue(BossUniqueTable.DropsOn(yagwanggwi, alreadyDefeated: true));
            Assert.IsFalse(BossUniqueTable.DropsOn(hyeonmu, alreadyDefeated: true), "장 보스는 없다");
        }
    }
}
