using System.Collections.Generic;
using NUnit.Framework;

namespace Dokkaebi.Tests
{
    /// <summary>결정 2-143 — 잠긴 곳(담 두른 마당 · 들어가는 집) · 열쇠 · 열쇠고리.</summary>
    public class LockedSiteTests
    {
        [TearDown]
        public void Clean() => KeyRing.Reset();

        [Test]
        public void 잠긴_곳은_판마다_같은_자리다()
        {
            List<LockSite> first = LockSiteTable.All();
            LockSiteTable.ClearCache();
            List<LockSite> second = LockSiteTable.All();

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].Id, second[i].Id);
                Assert.AreEqual(first[i].Center, second[i].Center);
            }
        }

        [Test]
        public void 잠긴_곳은_덩어리_금줄_중요한_자리와_겹치지_않는다()
        {
            foreach (ChapterMap map in ZoneMapTable.All)
                foreach (ZoneArea area in map.Areas)
                {
                    IReadOnlyList<LockSite> sites = LockSiteTable.For(map, area.ZoneId);
                    for (int i = 0; i < sites.Count; i++)
                    {
                        var others = new List<LockSite>(sites);
                        others.RemoveAt(i);
                        Assert.IsTrue(LockSiteTable.Fits(map, sites[i], others), $"{sites[i].Id} 자리가 겹친다");
                    }
                }
        }

        [Test]
        public void 길잡이_장에는_잠긴_곳이_없고_구역마다_많아야_둘이다()
        {
            foreach (LockSite s in LockSiteTable.All())
                Assert.Greater(s.Chapter, 0, s.Id);

            var perZone = new Dictionary<string, int>();
            foreach (LockSite s in LockSiteTable.All())
                perZone[s.ZoneId] = perZone.TryGetValue(s.ZoneId, out int n) ? n + 1 : 1;
            foreach (var kv in perZone)
                Assert.LessOrEqual(kv.Value, 2, kv.Key);

            Assert.GreaterOrEqual(LockSiteTable.All().Count, 20, "장 맵 대부분에 잠긴 곳이 있어야 한다");
        }

        [Test]
        public void 문마다_그_문만_여는_열쇠가_카탈로그에_있다()
        {
            ItemCatalog catalog = ItemCatalog.Load();
            var ids = new HashSet<string>();
            foreach (LockSite s in LockSiteTable.All())
            {
                Assert.IsTrue(ids.Add(s.KeyId), $"열쇠 id가 겹친다 {s.KeyId}");
                ItemDefinition key = catalog.Find(s.KeyId);
                Assert.IsNotNull(key, $"{s.KeyId} 열쇠 에셋이 없다 — 「열쇠 · 연장 에셋 생성」");
                Assert.AreEqual(ItemKind.Key, key.Kind);
                Assert.AreEqual(KeyRing.UsesPerKey, key.MaxDurability);
                Assert.IsTrue(KeyRing.IsDoorKey(key));
            }

            Assert.IsNotNull(catalog.Find(KeyAssetIds.CrowbarId));
            Assert.IsNotNull(catalog.Find(KeyAssetIds.PowderId));
            Assert.IsFalse(KeyRing.IsDoorKey(catalog.Find(KeyAssetIds.CrowbarId)));
        }

        [Test]
        public void 열쇠고리는_한_번만_걸리고_세이브로_되살아난다()
        {
            Assert.IsTrue(KeyRing.Register("key_1-1_gate"));
            Assert.IsFalse(KeyRing.Register("key_1-1_gate"));
            Assert.IsTrue(KeyRing.IsRegistered("key_1-1_gate"));

            List<string> saved = KeyRing.Capture();
            KeyRing.Reset();
            Assert.IsFalse(KeyRing.IsRegistered("key_1-1_gate"));

            KeyRing.Restore(saved);
            Assert.IsTrue(KeyRing.IsRegistered("key_1-1_gate"));
        }

        [Test]
        public void 열쇠는_세_번_쓰면_부러진다()
        {
            ItemDefinition key = ItemCatalog.Load().Find(LockSiteTable.All()[0].KeyId);
            var stack = new ItemStack(key);
            for (int i = 0; i < KeyRing.UsesPerKey; i++)
            {
                Assert.IsFalse(stack.IsBroken);
                stack.Damage(1);
            }
            Assert.IsTrue(stack.IsBroken);
        }

        [Test]
        public void 문은_남쪽_벽_가운데다()
        {
            LockSite s = LockSiteTable.All()[0];
            Assert.AreEqual(s.Center.x, s.Door.x, 0.001f);
            Assert.AreEqual(s.Center.y - s.Size.y * 0.5f, s.Door.z, 0.001f);
            Assert.IsTrue(s.Inside(new UnityEngine.Vector3(s.Center.x, 0f, s.Center.y)));
            Assert.IsFalse(s.Inside(s.Door + new UnityEngine.Vector3(0f, 0f, -1f)));
        }

        [Test]
        public void 세이브_판은_24다()
        {
            Assert.GreaterOrEqual(SaveData.CurrentVersion, 24);
            Assert.IsNotNull(new SaveData().registeredKeys);
        }
    }
}
