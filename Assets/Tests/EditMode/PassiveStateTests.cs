using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Blob.Tests
{
    /// <summary>
    /// 패시브 — 계정 축의 영구 성장.
    ///
    /// 【이 파일이 지키는 것】 패시브가 전투 수치를 주지 않는다.
    /// 규칙을 문서에만 적어 두면 언젠가 "방어력 +5 하나쯤이야"로 무너진다.
    /// 그래서 enum 수준에서 검사한다.
    /// </summary>
    public class PassiveStateTests
    {
        private static PassiveNode Node(
            string id, PassiveEffectType effect = PassiveEffectType.CarrySlots,
            float value = 1f, int level = 1, int cost = 100, string[] prereq = null)
        {
            var node = ScriptableObject.CreateInstance<PassiveNode>();
            node.name = id;
            node.EditorSet(id, id, id + " 설명", effect, value, level, cost, prereq, 0, 0);

            return node;
        }

        private static PassiveTree Tree(params PassiveNode[] nodes)
        {
            var tree = ScriptableObject.CreateInstance<PassiveTree>();
            tree.EditorSetNodes(new List<PassiveNode>(nodes), 4, 4);

            return tree;
        }

        // ── 전투 수치 금지 ────────────────────────────────────────────────

        [Test]
        public void 패시브_효과에_전투_수치가_없다()
        {
            // 방어도·피해·체력·이동·감지·대시는 전부 장비의 몫이다.
            // 여기에 생기면 장비를 고를 이유가 사라지고 성장 축 하나가 죽는다.
            string[] forbidden =
            {
                "Armour", "Damage", "Health", "Resist", "Penetration",
                "Move", "Dash", "View", "Detect", "Critical", "Hearing"
            };

            foreach (PassiveEffectType type in System.Enum.GetValues(typeof(PassiveEffectType)))
            {
                string name = type.ToString();

                foreach (string word in forbidden)
                {
                    Assert.IsFalse(name.Contains(word),
                        $"PassiveEffectType.{name} 은 전투 수치입니다. 장비가 담당해야 합니다.");
                }
            }
        }

        // ── 배우기 조건 ───────────────────────────────────────────────────

        [Test]
        public void 뿌리_칸은_바로_배울_수_있다()
        {
            var state = new PassiveState();
            PassiveNode root = Node("root");

            Assert.AreEqual(PassiveError.None, state.CanLearn(root, accountLevel: 1, credits: 1000));
            Assert.AreEqual(100, state.TryLearn(root, 1, 1000));
            Assert.IsTrue(state.IsLearned(root));
        }

        [Test]
        public void 선행을_배우지_않으면_배울_수_없다()
        {
            var state = new PassiveState();
            PassiveNode child = Node("child", prereq: new[] { "root" });

            Assert.AreEqual(PassiveError.MissingPrerequisite,
                state.CanLearn(child, accountLevel: 99, credits: 99999));
        }

        [Test]
        public void 선행_안내가_레벨_안내보다_먼저_나온다()
        {
            // 위 칸부터 눌러 보는 조작에서 "먼저 아래를 배우십시오"가 더 쓸모 있다.
            var state = new PassiveState();
            PassiveNode child = Node("child", level: 10, prereq: new[] { "root" });

            Assert.AreEqual(PassiveError.MissingPrerequisite,
                state.CanLearn(child, accountLevel: 1, credits: 99999));
        }

        [Test]
        public void 계정_레벨이_모자라면_배울_수_없다()
        {
            var state = new PassiveState();
            PassiveNode node = Node("late", level: 5);

            Assert.AreEqual(PassiveError.LevelTooLow, state.CanLearn(node, 4, 99999));
            Assert.AreEqual(PassiveError.None, state.CanLearn(node, 5, 99999));
        }

        [Test]
        public void 크레딧이_모자라면_배울_수_없다()
        {
            var state = new PassiveState();
            PassiveNode node = Node("pricey", cost: 5000);

            Assert.AreEqual(PassiveError.NotEnoughCredits, state.CanLearn(node, 1, 4999));
            Assert.AreEqual(0, state.TryLearn(node, 1, 4999));
            Assert.IsFalse(state.IsLearned(node));
        }

        [Test]
        public void 같은_칸을_두_번_배울_수_없다()
        {
            var state = new PassiveState();
            PassiveNode node = Node("once");

            state.TryLearn(node, 1, 1000);

            Assert.AreEqual(PassiveError.AlreadyLearned, state.CanLearn(node, 1, 1000));
            Assert.AreEqual(0, state.TryLearn(node, 1, 1000));
        }

        [Test]
        public void 선행을_배우면_다음_칸이_열린다()
        {
            var state = new PassiveState();
            PassiveNode root = Node("root");
            PassiveNode child = Node("child", prereq: new[] { "root" });

            state.TryLearn(root, 1, 1000);

            Assert.AreEqual(PassiveError.None, state.CanLearn(child, 1, 1000));
        }

        // ── 합산 ──────────────────────────────────────────────────────────

        [Test]
        public void 같은_효과는_합산된다()
        {
            PassiveNode a = Node("a", PassiveEffectType.CarrySlots, value: 2);
            PassiveNode b = Node("b", PassiveEffectType.CarrySlots, value: 4);
            PassiveNode other = Node("c", PassiveEffectType.CarryWeight, value: 8);

            PassiveTree tree = Tree(a, b, other);
            var state = new PassiveState();

            state.TryLearn(a, 1, 1000);
            state.TryLearn(b, 1, 1000);
            state.TryLearn(other, 1, 1000);

            Assert.AreEqual(6f, state.Total(tree, PassiveEffectType.CarrySlots), 0.0001f);
            Assert.AreEqual(8f, state.Total(tree, PassiveEffectType.CarryWeight), 0.0001f);
        }

        [Test]
        public void 배우지_않은_칸은_합산되지_않는다()
        {
            PassiveNode a = Node("a", PassiveEffectType.CarrySlots, value: 2);
            PassiveNode b = Node("b", PassiveEffectType.CarrySlots, value: 4);

            PassiveTree tree = Tree(a, b);
            var state = new PassiveState();

            state.TryLearn(a, 1, 1000);

            Assert.AreEqual(2f, state.Total(tree, PassiveEffectType.CarrySlots), 0.0001f);
        }

        [Test]
        public void 해금형은_배웠는지만_본다()
        {
            PassiveNode bench = Node("bench", PassiveEffectType.CraftBench, value: 1);
            PassiveTree tree = Tree(bench);
            var state = new PassiveState();

            Assert.IsFalse(state.HasUnlock(tree, PassiveEffectType.CraftBench));

            state.TryLearn(bench, 1, 1000);

            Assert.IsTrue(state.HasUnlock(tree, PassiveEffectType.CraftBench));
            Assert.IsTrue(PassiveEffectInfo.IsUnlockFlag(PassiveEffectType.CraftBench));
        }

        [Test]
        public void 세이브_복원은_배운_목록을_그대로_되살린다()
        {
            var state = new PassiveState();

            state.Restore(new[] { "a", "b", "c" });

            Assert.AreEqual(3, state.Count);
            Assert.IsTrue(state.IsLearned("b"));

            state.Restore(null);

            Assert.AreEqual(0, state.Count);
        }
    }
}
