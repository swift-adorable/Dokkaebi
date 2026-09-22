using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blob.Tests
{
    /// <summary>
    /// 퀵슬롯 계약 테스트.
    ///
    /// 【여기서 강제하는 것】
    ///   1. 같은 물건이 두 칸에 걸리지 않는다
    ///   2. 가방에서 사라진 것은 칸에서도 사라진다
    ///   3. 같은 칸을 다시 고르면 뺀다
    /// </summary>
    public class QuickSlotTests
    {
        private static ItemDefinition Definition(string id)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();

            var serialized = new SerializedObject(def);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = id;
            serialized.FindProperty("kind").intValue = (int)ItemKind.Consumable;
            serialized.FindProperty("weight").floatValue = 0.2f;
            serialized.FindProperty("slotSize").intValue = 1;
            serialized.FindProperty("stackMax").intValue = 5;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return def;
        }

        /// <summary>
        /// 가방에 넣고 【가방이 들고 있는 그 개체】를 돌려준다.
        ///
        /// TryAddStack은 겹칠 수 있는 물건이면 새 ItemStack을 만들어 넣고
        /// 넘긴 쪽을 비운다. 넘긴 개체를 그대로 들고 있으면 가방에 없는 것을
        /// 가리키게 된다 — 퀵슬롯이 가방의 개체만 가리켜야 하는 이유다.
        /// </summary>
        private static ItemStack AddTo(Inventory bag, ItemDefinition definition, int count)
        {
            bag.TryAdd(definition, count);

            return bag.Stacks[bag.Stacks.Count - 1];
        }

        [Test]
        public void 건_것이_그_칸에서_나온다()
        {
            var stack = new ItemStack(Definition("medkit"), 3);
            var quick = new QuickSlots();

            quick.Assign(2, stack);

            Assert.AreSame(stack, quick.Get(2));
            Assert.AreEqual(2, quick.IndexOf(stack));
        }

        [Test]
        public void 같은_물건이_두_칸에_걸리지_않는다()
        {
            // 두 칸에 같은 것이 걸리면, 하나를 다 썼을 때 남은 칸이
            // 없는 것을 가리키게 된다. 옮기는 것으로 처리한다.
            var stack = new ItemStack(Definition("medkit"), 3);
            var quick = new QuickSlots();

            quick.Assign(1, stack);
            quick.Assign(5, stack);

            Assert.IsNull(quick.Get(1), "옮기지 않고 복사됐습니다.");
            Assert.AreSame(stack, quick.Get(5));
        }

        [Test]
        public void 같은_칸을_다시_고르면_뺀다()
        {
            // 넣기와 빼기가 같은 동작이다 — 「빼기」 버튼을 따로 배우지 않아도 된다.
            var stack = new ItemStack(Definition("medkit"), 3);
            var quick = new QuickSlots();

            quick.Assign(0, stack);
            quick.Assign(0, stack);

            Assert.IsNull(quick.Get(0));
            Assert.AreEqual(QuickSlots.None, quick.IndexOf(stack));
        }

        [Test]
        public void 다른_것을_걸면_밀려난다()
        {
            var first = new ItemStack(Definition("medkit"), 1);
            var second = new ItemStack(Definition("water"), 1);
            var quick = new QuickSlots();

            quick.Assign(3, first);
            quick.Assign(3, second);

            Assert.AreSame(second, quick.Get(3));
            Assert.AreEqual(QuickSlots.None, quick.IndexOf(first));
        }

        [Test]
        public void 가방에서_사라진_것은_칸에서도_사라진다()
        {
            // 버리기 · 착용 · 소진처럼 가방을 건드리는 곳마다 퀵슬롯을 챙기게 하면
            // 한 군데만 빠져도 「없는 것을 가리키는 칸」이 남는다. 여기서만 판단한다.
            var bag = new Inventory(20, 100f);
            ItemStack stack = AddTo(bag, Definition("medkit"), 2);

            var quick = new QuickSlots();
            quick.Assign(4, stack);

            quick.Prune(bag);
            Assert.AreSame(stack, quick.Get(4), "가방에 있는데 지웠습니다.");

            bag.RemoveStack(stack);
            quick.Prune(bag);

            Assert.IsNull(quick.Get(4), "가방에서 사라졌는데 칸에 남아 있습니다.");
        }

        [Test]
        public void 다_쓴_것도_사라진다()
        {
            var bag = new Inventory(20, 100f);
            ItemStack stack = AddTo(bag, Definition("medkit"), 1);

            var quick = new QuickSlots();
            quick.Assign(0, stack);

            stack.Take(1);
            quick.Prune(bag);

            Assert.IsNull(quick.Get(0), "개수가 0인데 칸에 남아 있습니다.");
        }

        [Test]
        public void 엣지_범위_밖은_조용히_무시한다()
        {
            var stack = new ItemStack(Definition("medkit"), 1);
            var quick = new QuickSlots();

            Assert.DoesNotThrow(() => quick.Assign(-1, stack));
            Assert.DoesNotThrow(() => quick.Assign(QuickSlots.Count, stack));
            Assert.DoesNotThrow(() => quick.Clear(99));

            Assert.IsNull(quick.Get(-1));
            Assert.IsNull(quick.Get(QuickSlots.Count));
            Assert.AreEqual(QuickSlots.None, quick.IndexOf(null));
        }
    }
}
