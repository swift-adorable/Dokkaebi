using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blob.Tests
{
    /// <summary>
    /// 패시브 — 계정 축의 영구 성장. (docs/Blob_Passive_System.md)
    ///
    /// 【이 파일이 지키는 것 두 가지】
    ///  1. 패시브가 전투 수치를 주지 않는다. 문서에만 적어 두면
    ///     언젠가 "방어력 +5 하나쯤이야"로 무너진다. 그래서 enum 수준에서 검사한다.
    ///  2. 아이템이 조용히 사라지거나 공짜로 배워지지 않는다.
    ///     검사(CanLearn)와 소모(TryLearn)가 어긋나면 둘 중 하나가 일어난다.
    /// </summary>
    public class PassiveStateTests
    {
        private static ItemDefinition Item(string id, int stackMax = 20)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            def.name = id;

            var so = new SerializedObject(def);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;
            so.FindProperty("kind").intValue = (int)ItemKind.Material;
            so.FindProperty("weight").floatValue = 0.5f;
            so.FindProperty("slotSize").intValue = 1;
            so.FindProperty("stackMax").intValue = stackMax;
            so.ApplyModifiedPropertiesWithoutUndo();

            return def;
        }

        private static PassiveNode Node(
            string id,
            PassiveBranch branch = PassiveBranch.Adapt,
            PassiveEffectType effect = PassiveEffectType.CarrySlots,
            float value = 1f, int level = 1, int cost = 100,
            string[] prereq = null, PassiveMaterial[] materials = null)
        {
            var node = ScriptableObject.CreateInstance<PassiveNode>();
            node.name = id;
            node.EditorSet(id, id, id + " 설명", branch, effect, value,
                level, cost, materials, prereq, 0, 0);

            return node;
        }

        private static PassiveTree Tree(params PassiveNode[] nodes)
        {
            var tree = ScriptableObject.CreateInstance<PassiveTree>();
            tree.EditorSetNodes(new List<PassiveNode>(nodes), 3, 4);

            return tree;
        }

        private static PassiveContext Ctx(
            int level = 1, int credits = 100000,
            Inventory materials = null, bool discovered = false)
        {
            return new PassiveContext(level, credits, materials, discovered);
        }

        // ── 전투 수치 금지 ────────────────────────────────────────────────

        [Test]
        public void 패시브_효과에_전투_수치가_없다()
        {
            // 방어도·피해·체력·이동·시야·감지·대시는 전부 장비의 몫이다.
            // 덕코프는 「생존 본능 = 최대 생명력 +5」를 패시브로 주지만 따르지 않는다.
            // 우리 장비는 옵션이 25종이라 겹치면 장비를 고를 이유가 사라진다.
            string[] forbidden =
            {
                "Armour", "Damage", "Health", "Resist", "Penetration",
                "Move", "Dash", "View", "Critical", "Hearing", "Speed", "Reload"
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

        [Test]
        public void 계열은_다섯_개다()
        {
            // 화면 한 장에 들어와야 한다. 6개 이상으로 늘리지 않는다. (6절 4번)
            Assert.AreEqual(PassiveBranchInfo.Count,
                System.Enum.GetValues(typeof(PassiveBranch)).Length);

            Assert.AreEqual(5, PassiveBranchInfo.Count);
        }

        [Test]
        public void 계열마다_해금_방식이_정해져_있다()
        {
            // 덕코프 구조 — 블랙마켓은 레벨 없이 돈만, 이상한 개조는 조우로. [확인됨]
            Assert.AreEqual(PassiveUnlockKind.AccountLevel,
                PassiveBranchInfo.UnlockKind(PassiveBranch.Adapt));

            Assert.AreEqual(PassiveUnlockKind.CreditsOnly,
                PassiveBranchInfo.UnlockKind(PassiveBranch.Brokerage));

            Assert.AreEqual(PassiveUnlockKind.Discovery,
                PassiveBranchInfo.UnlockKind(PassiveBranch.Regression));
        }

        // ── 배우기 조건 ───────────────────────────────────────────────────

        [Test]
        public void 뿌리_칸은_바로_배울_수_있다()
        {
            var state = new PassiveState();
            PassiveNode root = Node("root");

            Assert.AreEqual(PassiveError.None, state.CanLearn(root, Ctx()));
            Assert.AreEqual(100, state.TryLearn(root, Ctx()));
            Assert.IsTrue(state.IsLearned(root));
        }

        [Test]
        public void 선행_안내가_레벨_안내보다_먼저_나온다()
        {
            // 위 칸부터 눌러 보는 조작에서 "먼저 아래를 배우십시오"가 더 쓸모 있다.
            var state = new PassiveState();
            PassiveNode child = Node("child", level: 10, prereq: new[] { "root" });

            Assert.AreEqual(PassiveError.MissingPrerequisite, state.CanLearn(child, Ctx(level: 1)));
        }

        [Test]
        public void 계정_레벨이_모자라면_배울_수_없다()
        {
            var state = new PassiveState();
            PassiveNode node = Node("late", level: 5);

            Assert.AreEqual(PassiveError.LevelTooLow, state.CanLearn(node, Ctx(level: 4)));
            Assert.AreEqual(PassiveError.None, state.CanLearn(node, Ctx(level: 5)));
        }

        [Test]
        public void 중개_계열은_계정_레벨을_보지_않는다()
        {
            // 덕코프 블랙마켓 업그레이드에는 해금레벨 열 자체가 없다. [확인됨]
            var state = new PassiveState();

            PassiveNode brokerage = Node("brok", PassiveBranch.Brokerage,
                PassiveEffectType.SellPrice, level: 99);

            Assert.AreEqual(PassiveError.None, state.CanLearn(brokerage, Ctx(level: 1)));
        }

        [Test]
        public void 역행_계열은_발견_전까지_배울_수_없다()
        {
            var state = new PassiveState();

            PassiveNode hidden = Node("reg", PassiveBranch.Regression,
                PassiveEffectType.CraftBench);

            Assert.AreEqual(PassiveError.BranchUndiscovered,
                state.CanLearn(hidden, Ctx(discovered: false)));

            Assert.AreEqual(PassiveError.None,
                state.CanLearn(hidden, Ctx(discovered: true)));
        }

        [Test]
        public void 크레딧이_모자라면_배울_수_없다()
        {
            var state = new PassiveState();
            PassiveNode node = Node("pricey", cost: 5000);

            Assert.AreEqual(PassiveError.NotEnoughCredits, state.CanLearn(node, Ctx(credits: 4999)));
            Assert.AreEqual(0, state.TryLearn(node, Ctx(credits: 4999)));
            Assert.IsFalse(state.IsLearned(node));
        }

        [Test]
        public void 같은_칸을_두_번_배울_수_없다()
        {
            var state = new PassiveState();
            PassiveNode node = Node("once");

            state.TryLearn(node, Ctx());

            Assert.AreEqual(PassiveError.AlreadyLearned, state.CanLearn(node, Ctx()));
            Assert.AreEqual(0, state.TryLearn(node, Ctx()));
        }

        // ── 필요물품 ──────────────────────────────────────────────────────

        [Test]
        public void 필요물품이_모자라면_배울_수_없다()
        {
            // 덕코프 스킬 표의 「필요물품」 열. [확인됨]
            // 크레딧만 쓰면 시간을 들이면 전부 열린다. 결정이 없다.
            ItemDefinition core = Item("memory_core");

            PassiveNode node = Node("needs", materials: new[] { new PassiveMaterial(core, 3) });

            var bag = new Inventory(slots: 10, weight: 100f);
            bag.TryAdd(core, 2);

            var state = new PassiveState();

            Assert.AreEqual(PassiveError.MissingMaterials, state.CanLearn(node, Ctx(materials: bag)));

            bag.TryAdd(core, 1);

            Assert.AreEqual(PassiveError.None, state.CanLearn(node, Ctx(materials: bag)));
        }

        [Test]
        public void 배우면_필요물품이_실제로_소모된다()
        {
            // 검사와 소모가 갈라지면 「공짜로 배워지는」 상태가 조용히 생긴다.
            ItemDefinition core = Item("memory_core");
            ItemDefinition scrap = Item("scrap_metal");

            PassiveNode node = Node("needs", materials: new[]
            {
                new PassiveMaterial(core, 2),
                new PassiveMaterial(scrap, 5)
            });

            var bag = new Inventory(slots: 10, weight: 100f);
            bag.TryAdd(core, 3);
            bag.TryAdd(scrap, 9);

            var state = new PassiveState();

            Assert.AreEqual(100, state.TryLearn(node, Ctx(materials: bag)));

            Assert.AreEqual(1, bag.CountOf(core));
            Assert.AreEqual(4, bag.CountOf(scrap));
        }

        [Test]
        public void 배우지_못하면_필요물품도_소모되지_않는다()
        {
            ItemDefinition core = Item("memory_core");

            PassiveNode node = Node("needs", cost: 9999,
                materials: new[] { new PassiveMaterial(core, 2) });

            var bag = new Inventory(slots: 10, weight: 100f);
            bag.TryAdd(core, 5);

            var state = new PassiveState();

            Assert.AreEqual(0, state.TryLearn(node, Ctx(credits: 10, materials: bag)));
            Assert.AreEqual(5, bag.CountOf(core), "배우지 못했는데 재료가 사라졌습니다.");
        }

        [Test]
        public void 재료_출처가_없으면_재료_검사를_생략한다()
        {
            // 창고가 아직 없는 구간에서도 트리를 검증할 수 있어야 한다.
            ItemDefinition core = Item("memory_core");

            PassiveNode node = Node("needs", materials: new[] { new PassiveMaterial(core, 99) });

            var state = new PassiveState();

            Assert.AreEqual(PassiveError.None, state.CanLearn(node, Ctx(materials: null)));
        }

        // ── 트리 모양 ─────────────────────────────────────────────────────

        [Test]
        public void 다이아몬드_구조에서는_양쪽을_모두_배워야_정점에_닿는다()
        {
            // 덕코프 「영양 관리 3 → 4·5 → 6」과 같은 모양. [확인됨]
            var state = new PassiveState();

            PassiveNode root = Node("root");
            PassiveNode left = Node("left", prereq: new[] { "root" });
            PassiveNode right = Node("right", prereq: new[] { "root" });
            PassiveNode top = Node("top", prereq: new[] { "left", "right" });

            state.TryLearn(root, Ctx());
            state.TryLearn(left, Ctx());

            Assert.AreEqual(PassiveError.MissingPrerequisite, state.CanLearn(top, Ctx()));

            state.TryLearn(right, Ctx());

            Assert.AreEqual(PassiveError.None, state.CanLearn(top, Ctx()));
        }

        [Test]
        public void 계열을_넘는_선행이_성립한다()
        {
            // 덕코프 「식이요법 = 낚시 3 + 영양 관리 3」과 같은 교차 선행. [확인됨]
            // 한 갈래만 파면 닿지 않는 칸이 생겨야 넓게 파는 이유가 생긴다.
            var state = new PassiveState();

            PassiveNode fromOther = Node("meta_root", PassiveBranch.Metabolism,
                PassiveEffectType.AbsorbAmount);

            PassiveNode crossing = Node("reg_node", PassiveBranch.Regression,
                PassiveEffectType.CodexAuto, prereq: new[] { "meta_root" });

            Assert.AreEqual(PassiveError.MissingPrerequisite,
                state.CanLearn(crossing, Ctx(discovered: true)));

            state.TryLearn(fromOther, Ctx());

            Assert.AreEqual(PassiveError.None, state.CanLearn(crossing, Ctx(discovered: true)));
        }

        // ── 합산 ──────────────────────────────────────────────────────────

        [Test]
        public void 같은_효과는_합산된다()
        {
            PassiveNode a = Node("a", effect: PassiveEffectType.CarrySlots, value: 2);
            PassiveNode b = Node("b", effect: PassiveEffectType.CarrySlots, value: 4);
            PassiveNode other = Node("c", effect: PassiveEffectType.CarryWeight, value: 8);

            PassiveTree tree = Tree(a, b, other);
            var state = new PassiveState();

            state.TryLearn(a, Ctx());
            state.TryLearn(b, Ctx());
            state.TryLearn(other, Ctx());

            Assert.AreEqual(6f, state.Total(tree, PassiveEffectType.CarrySlots), 0.0001f);
            Assert.AreEqual(8f, state.Total(tree, PassiveEffectType.CarryWeight), 0.0001f);
        }

        [Test]
        public void 배우지_않은_칸은_합산되지_않는다()
        {
            PassiveNode a = Node("a", effect: PassiveEffectType.CarrySlots, value: 2);
            PassiveNode b = Node("b", effect: PassiveEffectType.CarrySlots, value: 4);

            PassiveTree tree = Tree(a, b);
            var state = new PassiveState();

            state.TryLearn(a, Ctx());

            Assert.AreEqual(2f, state.Total(tree, PassiveEffectType.CarrySlots), 0.0001f);
        }

        [Test]
        public void 해금형은_배웠는지만_본다()
        {
            PassiveNode bench = Node("bench", PassiveBranch.Regression,
                PassiveEffectType.CraftBench);

            PassiveTree tree = Tree(bench);
            var state = new PassiveState();

            Assert.IsFalse(state.HasUnlock(tree, PassiveEffectType.CraftBench));

            state.TryLearn(bench, Ctx(discovered: true));

            Assert.IsTrue(state.HasUnlock(tree, PassiveEffectType.CraftBench));
            Assert.IsTrue(PassiveEffectInfo.IsUnlockFlag(PassiveEffectType.CraftBench));
        }

        [Test]
        public void 계열별로_배운_수를_센다()
        {
            PassiveNode a = Node("a", PassiveBranch.Adapt);
            PassiveNode b = Node("b", PassiveBranch.Adapt);
            PassiveNode c = Node("c", PassiveBranch.Metabolism);

            PassiveTree tree = Tree(a, b, c);
            var state = new PassiveState();

            state.TryLearn(a, Ctx());
            state.TryLearn(c, Ctx());

            Assert.AreEqual(1, state.CountIn(tree, PassiveBranch.Adapt));
            Assert.AreEqual(1, state.CountIn(tree, PassiveBranch.Metabolism));
            Assert.AreEqual(2, tree.CountIn(PassiveBranch.Adapt));
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
