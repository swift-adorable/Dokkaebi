using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 【쓰러진 자리 되찾기】 (결정 2-93) — 하나뿐 · 그 장에 다시 간 판에서 한 번 · 다시 쓰러지면 사라진다.
    /// </summary>
    public class FallenCacheTests
    {
        private static List<SavedItem> Lost(params string[] ids)
        {
            var list = new List<SavedItem>();
            foreach (string id in ids)
                list.Add(new SavedItem { id = id, count = 1 });
            return list;
        }

        [TearDown]
        public void Clean() => FallenCache.Clear();

        [Test]
        public void 배우지_않았으면_남지_않는다()
        {
            FallenCache.OnDeath(false, 2, Vector3.zero, Lost("a"));
            Assert.IsNull(FallenCache.Current);
        }

        [Test]
        public void 다른_장을_다녀와도_남아_있다()
        {
            FallenCache.OnDeath(true, 2, new Vector3(3f, 0f, 4f), Lost("a", "b"));

            Assert.IsNull(FallenCache.TakeFor(1), "다른 장에서 꺼내졌습니다.");
            Assert.IsNull(FallenCache.TakeFor(3));
            Assert.IsTrue(FallenCache.Has(2), "다른 장에 다녀오니 사라졌습니다.");
        }

        [Test]
        public void 그_장의_판에서_한_번만_꺼낸다()
        {
            FallenCache.OnDeath(true, 2, new Vector3(3f, 0f, 4f), Lost("a", "b"));

            SavedFallen taken = FallenCache.TakeFor(2);

            Assert.IsNotNull(taken);
            Assert.AreEqual(2, taken.items.Count);
            Assert.AreEqual(3f, taken.x, 0.001f);
            Assert.AreEqual(4f, taken.z, 0.001f);
            Assert.IsNull(FallenCache.TakeFor(2), "같은 자리가 두 번 나왔습니다.");
        }

        [Test]
        public void 다시_쓰러지면_옛_자리는_사라진다()
        {
            FallenCache.OnDeath(true, 2, Vector3.zero, Lost("old"));
            FallenCache.OnDeath(true, 4, Vector3.one, Lost("new"));

            Assert.IsFalse(FallenCache.Has(2), "옛 자리가 남았습니다.");
            Assert.IsTrue(FallenCache.Has(4));
            Assert.AreEqual("new", FallenCache.Current.items[0].id);

            // 잃은 것이 없거나 장 맵이 아닌 곳에서 쓰러져도 옛 자리는 사라진다.
            FallenCache.OnDeath(true, -1, Vector3.zero, Lost("x"));
            Assert.IsNull(FallenCache.Current);
        }

        [Test]
        public void 세이브를_오간다()
        {
            FallenCache.OnDeath(true, 5, new Vector3(-7f, 0f, 9f), Lost("a"));

            var data = new SaveData();
            FallenCache.Capture(data);
            FallenCache.Clear();
            FallenCache.Restore(data);

            Assert.IsTrue(FallenCache.Has(5));
            Assert.AreEqual(-7f, FallenCache.Current.x, 0.001f);

            FallenCache.Restore(new SaveData());
            Assert.IsNull(FallenCache.Current, "옛 세이브(자리 없음)에서 자리가 생겼습니다.");
        }

        [Test]
        public void 쓰러지면_잃은_묶음을_돌려준다()
        {
            var bag = new Inventory(10, 100f);
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            bag.TryAddStack(new ItemStack(def, 1));

            var lost = new List<ItemStack>();
            int count = bag.DropOnDeath(0, lost);

            Assert.AreEqual(1, count);
            Assert.AreEqual(1, lost.Count, "잃은 것을 쓰러진 자리로 넘기지 못합니다.");
            Object.DestroyImmediate(def);
        }
    }
}
