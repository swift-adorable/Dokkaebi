using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blob.Tests
{
    /// <summary>
    /// 전리품 칸과 추첨.
    ///
    /// 지켜야 할 것 하나 — 【아이템은 사라지지 않는다.】
    /// 가방에 자리가 없으면 전리품 칸에 그대로 남아야 한다.
    /// 조용히 증발하면 유저는 "버그인가 원래 그런가"를 판단할 수 없다.
    /// </summary>
    public class LootContainerTests
    {
        private static ItemDefinition Create(
            string id, float weight = 1f, int value = 0, int stackMax = 1)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            def.name = id;

            var so = new SerializedObject(def);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;
            so.FindProperty("kind").intValue = (int)ItemKind.Material;
            so.FindProperty("weight").floatValue = weight;
            so.FindProperty("slotSize").intValue = 1;
            so.FindProperty("stackMax").intValue = stackMax;
            so.FindProperty("baseValue").intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();

            return def;
        }

        // ── 칸 ────────────────────────────────────────────────────────────

        [Test]
        public void 빈_칸부터_차례로_채운다()
        {
            var loot = new LootContainer(3);

            Assert.IsTrue(loot.TryPut(Create("a")));
            Assert.IsTrue(loot.TryPut(Create("b")));

            Assert.AreEqual(2, loot.UsedSlots);
            Assert.IsNotNull(loot.Get(0));
            Assert.IsNotNull(loot.Get(1));
            Assert.IsNull(loot.Get(2));
        }

        [Test]
        public void 칸이_가득_차면_더_담기지_않는다()
        {
            var loot = new LootContainer(2);

            loot.TryPut(Create("a"));
            loot.TryPut(Create("b"));

            Assert.IsTrue(loot.IsFull);
            Assert.IsFalse(loot.TryPut(Create("c")));
        }

        [Test]
        public void 꺼낸_칸은_비고_중간_칸도_다시_쓸_수_있다()
        {
            var loot = new LootContainer(3);

            loot.TryPut(Create("a"));
            loot.TryPut(Create("b"));

            Assert.IsNotNull(loot.Take(0));
            Assert.IsNull(loot.Get(0));

            loot.TryPut(Create("c"));

            Assert.IsNotNull(loot.Get(0), "빈 칸이 생기면 그 칸부터 다시 쓴다.");
        }

        // ── 가방으로 옮기기 ───────────────────────────────────────────────

        [Test]
        public void 가방으로_옮기면_전리품_칸이_빈다()
        {
            var loot = new LootContainer(3);
            var bag = new Inventory(slots: 10, weight: 100f);

            ItemDefinition item = Create("a");
            loot.TryPut(item);

            Assert.IsTrue(loot.TryTakeTo(0, bag));

            Assert.IsNull(loot.Get(0));
            Assert.AreEqual(1, bag.CountOf(item));
        }

        [Test]
        public void 가방에_자리가_없으면_전리품_칸에_그대로_남는다()
        {
            // 【핵심】 아이템을 없애는 경로를 만들지 않는다.
            var loot = new LootContainer(3);
            var bag = new Inventory(slots: 0, weight: 100f);

            loot.TryPut(Create("a"));

            Assert.IsFalse(loot.TryTakeTo(0, bag));
            Assert.IsNotNull(loot.Get(0), "가방에 못 넣었는데 전리품에서도 사라지면 아이템이 증발합니다.");
        }

        [Test]
        public void 전부_줍기는_가능한_만큼만_옮긴다()
        {
            var loot = new LootContainer(4);
            var bag = new Inventory(slots: 2, weight: 100f);

            for (int i = 0; i < 4; i++)
                loot.TryPut(Create($"item_{i}"));

            int moved = loot.TakeAllTo(bag);

            Assert.AreEqual(2, moved);
            Assert.AreEqual(2, loot.UsedSlots, "못 들어간 것은 남아 있어야 합니다.");
        }

        [Test]
        public void 무게는_가방_한도를_넘겨도_옮길_수_있다()
        {
            // 무게는 담는 것을 막지 않는다. 느려질 뿐이다. (Inventory와 같은 규칙)
            var loot = new LootContainer(2);
            var bag = new Inventory(slots: 10, weight: 1f);

            loot.TryPut(Create("anvil", weight: 50f));

            Assert.IsTrue(loot.TryTakeTo(0, bag));
            Assert.IsTrue(bag.IsOverweight);
        }

        [Test]
        public void 총_무게와_총_가치를_알려준다()
        {
            var loot = new LootContainer(4);

            loot.TryPut(Create("a", weight: 1.5f, value: 100));
            loot.TryPut(Create("b", weight: 2.5f, value: 200));

            Assert.AreEqual(4f, loot.TotalWeight, 0.0001f);
            Assert.AreEqual(300, loot.TotalValue);
        }

        // ── 추첨 ──────────────────────────────────────────────────────────

        [Test]
        public void 가중치가_0이면_뽑히지_않는다()
        {
            ItemDefinition never = Create("never");
            ItemDefinition always = Create("always");

            var entries = new List<LootEntry>
            {
                new(never, weight: 0),
                new(always, weight: 5)
            };

            for (int seed = 0; seed < 50; seed++)
            {
                Assert.IsTrue(LootRoller.TryPick(entries, new System.Random(seed), out LootEntry picked));
                Assert.AreSame(always, picked.item);
            }
        }

        [Test]
        public void 뽑을_것이_없으면_실패한다()
        {
            Assert.IsFalse(LootRoller.TryPick(null, new System.Random(0), out _));
            Assert.IsFalse(LootRoller.TryPick(new List<LootEntry>(), new System.Random(0), out _));
        }

        [Test]
        public void 빈손_줄은_칸을_쓰지_않는다()
        {
            // item이 비어 있는 줄은 「아무것도 안 나옴」이다. 빈손 확률을 표로 표현한다.
            var entries = new List<LootEntry> { new(null, weight: 10) };
            var loot = new LootContainer(4);

            int placed = LootRoller.Roll(entries, 4, new System.Random(0), loot);

            Assert.AreEqual(0, placed);
            Assert.IsTrue(loot.IsEmpty);
        }

        [Test]
        public void 칸이_차면_더_뽑지_않는다()
        {
            var entries = new List<LootEntry> { new(Create("a"), weight: 10) };
            var loot = new LootContainer(2);

            int placed = LootRoller.Roll(entries, 10, new System.Random(0), loot);

            Assert.AreEqual(2, placed);
        }

        [Test]
        public void 같은_시드는_같은_결과를_낸다()
        {
            var entries = new List<LootEntry>
            {
                new(Create("a"), weight: 3),
                new(Create("b"), weight: 3),
                new(Create("c"), weight: 3)
            };

            var first = new LootContainer(5);
            var second = new LootContainer(5);

            LootRoller.Roll(entries, 5, new System.Random(777), first);
            LootRoller.Roll(entries, 5, new System.Random(777), second);

            for (int i = 0; i < 5; i++)
            {
                Assert.AreSame(first.Get(i)?.Definition, second.Get(i)?.Definition,
                    $"{i}번 칸이 다릅니다. 고정 시드 재현이 깨지면 밸런스 검증을 할 수 없습니다.");
            }
        }

        [Test]
        public void 개수_범위_안에서_나온다()
        {
            var entries = new List<LootEntry>
            {
                new(Create("ammo", stackMax: 30), weight: 10, minCount: 5, maxCount: 9)
            };

            for (int seed = 0; seed < 40; seed++)
            {
                var loot = new LootContainer(1);

                LootRoller.Roll(entries, 1, new System.Random(seed), loot);

                int count = loot.Get(0).Count;

                Assert.GreaterOrEqual(count, 5);
                Assert.LessOrEqual(count, 9);
            }
        }
    }
}
