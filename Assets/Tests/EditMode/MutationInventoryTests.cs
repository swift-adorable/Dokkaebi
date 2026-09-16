using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Blob.Tests
{
    /// <summary>Mutation 보유/합산 및 후보 추첨 테스트.</summary>
    public class MutationInventoryTests
    {
        private readonly List<MutationDefinition> created = new();

        private MutationDefinition Make(
            string id,
            MutationRarity rarity = MutationRarity.Common,
            int maxStacks = 1,
            ProjectileBehaviourType behaviour = ProjectileBehaviourType.None,
            int charges = 1,
            int extraProjectiles = 0,
            float fireIntervalMultiplier = 1f)
        {
            MutationDefinition definition = MutationTestFactory.Create(
                id, rarity, maxStacks, behaviour, charges, extraProjectiles,
                fireIntervalMultiplier: fireIntervalMultiplier);

            created.Add(definition);

            return definition;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (MutationDefinition definition in created)
            {
                if (definition != null)
                    UnityEngine.Object.DestroyImmediate(definition);
            }

            created.Clear();
        }

        // ── 보유 및 중첩 ─────────────────────────────────────────

        [Test]
        public void Add하면_중첩이_증가한다()
        {
            var inventory = new MutationInventory();
            MutationDefinition fork = Make("fork", maxStacks: 3);

            Assert.IsTrue(inventory.Add(fork));
            Assert.AreEqual(1, inventory.GetStacks(fork));

            Assert.IsTrue(inventory.Add(fork));
            Assert.AreEqual(2, inventory.GetStacks(fork));
            Assert.AreEqual(1, inventory.UniqueCount);
            Assert.AreEqual(2, inventory.TotalStacks);
        }

        [Test]
        public void 중첩_상한에_도달하면_더_추가되지_않는다()
        {
            var inventory = new MutationInventory();
            MutationDefinition pierce = Make("pierce", maxStacks: 2);

            inventory.Add(pierce);
            inventory.Add(pierce);

            Assert.IsFalse(inventory.CanAdd(pierce));
            Assert.IsFalse(inventory.Add(pierce));
            Assert.AreEqual(2, inventory.GetStacks(pierce));
        }

        [Test]
        public void 변경시_OnChanged가_발행된다()
        {
            var inventory = new MutationInventory();
            MutationDefinition chain = Make("chain");

            int callCount = 0;
            inventory.OnChanged += () => callCount++;

            inventory.Add(chain);
            inventory.Add(chain);   // 상한 도달로 실패

            Assert.AreEqual(1, callCount, "실패한 추가는 이벤트를 발행하면 안 됩니다.");
        }

        // ── 합산 ─────────────────────────────────────────────────

        [Test]
        public void 행동_횟수가_중첩만큼_합산된다()
        {
            var inventory = new MutationInventory();

            MutationDefinition chain = Make("chain", maxStacks: 3,
                behaviour: ProjectileBehaviourType.Chain, charges: 2);

            inventory.Add(chain);
            inventory.Add(chain);

            WeaponModifiers modifiers = inventory.GetModifiers();

            Assert.AreEqual(4, modifiers.Behaviours.ChainRemaining, "2중첩 x 2회 = 4회");
        }

        [Test]
        public void 서로_다른_행동이_각각_합산된다()
        {
            var inventory = new MutationInventory();

            inventory.Add(Make("fork", behaviour: ProjectileBehaviourType.Fork, charges: 1));
            inventory.Add(Make("pierce", behaviour: ProjectileBehaviourType.Pierce, charges: 3));

            WeaponModifiers modifiers = inventory.GetModifiers();

            Assert.AreEqual(1, modifiers.Behaviours.ForkRemaining);
            Assert.AreEqual(3, modifiers.Behaviours.PierceRemaining);
        }

        [Test]
        public void 비용은_중첩마다_곱해진다()
        {
            var inventory = new MutationInventory();

            MutationDefinition heavy = Make("heavy", maxStacks: 3, fireIntervalMultiplier: 1.2f);

            inventory.Add(heavy);
            inventory.Add(heavy);

            WeaponModifiers modifiers = inventory.GetModifiers();

            Assert.AreEqual(1.44f, modifiers.FireIntervalMultiplier, 0.001f,
                "1.2 x 1.2 = 1.44. 누적될수록 대가가 커져야 합니다.");
        }

        [Test]
        public void 총_투사체_수는_최소_1발이다()
        {
            var inventory = new MutationInventory();

            Assert.AreEqual(1, inventory.GetModifiers().TotalProjectiles);

            inventory.Add(Make("multi", extraProjectiles: 2));

            Assert.AreEqual(3, inventory.GetModifiers().TotalProjectiles);
        }

        [Test]
        public void Clear하면_보정치가_기본값으로_돌아간다()
        {
            var inventory = new MutationInventory();

            inventory.Add(Make("fork", behaviour: ProjectileBehaviourType.Fork));
            inventory.Clear();

            WeaponModifiers modifiers = inventory.GetModifiers();

            Assert.AreEqual(0, modifiers.Behaviours.ForkRemaining);
            Assert.AreEqual(1f, modifiers.FireIntervalMultiplier, 0.0001f);
            Assert.AreEqual(0, inventory.TotalStacks);
        }

        // ── 후보 추첨 ────────────────────────────────────────────

        [Test]
        public void 요청한_개수만큼_중복없이_뽑는다()
        {
            var catalog = new List<MutationDefinition>
            {
                Make("a"), Make("b"), Make("c"), Make("d")
            };

            var inventory = new MutationInventory();

            List<MutationDefinition> result =
                MutationDraft.Draw(catalog, inventory, 3, new System.Random(12345));

            Assert.AreEqual(3, result.Count);
            CollectionAssert.AllItemsAreUnique(result);
        }

        [Test]
        public void 중첩_상한에_도달한_것은_후보에서_제외된다()
        {
            MutationDefinition maxed = Make("maxed", maxStacks: 1);
            MutationDefinition open = Make("open", maxStacks: 5);

            var catalog = new List<MutationDefinition> { maxed, open };
            var inventory = new MutationInventory();

            inventory.Add(maxed);

            List<MutationDefinition> result =
                MutationDraft.Draw(catalog, inventory, 2, new System.Random(1));

            Assert.AreEqual(1, result.Count);
            Assert.AreSame(open, result[0]);
        }

        [Test]
        public void 같은_시드는_같은_결과를_낸다()
        {
            var catalog = new List<MutationDefinition>
            {
                Make("a"), Make("b", MutationRarity.Rare), Make("c", MutationRarity.Epic)
            };

            var inventory = new MutationInventory();

            List<MutationDefinition> first =
                MutationDraft.Draw(catalog, inventory, 2, new System.Random(777));
            List<MutationDefinition> second =
                MutationDraft.Draw(catalog, inventory, 2, new System.Random(777));

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void 등급이_낮을수록_가중치가_높다()
        {
            Assert.Greater(MutationDraft.GetWeight(MutationRarity.Common),
                           MutationDraft.GetWeight(MutationRarity.Rare));
            Assert.Greater(MutationDraft.GetWeight(MutationRarity.Rare),
                           MutationDraft.GetWeight(MutationRarity.Epic));
        }

        // ── 엣지 케이스 ──────────────────────────────────────────

        [Test]
        public void 엣지_null_Mutation은_무시된다()
        {
            var inventory = new MutationInventory();

            Assert.IsFalse(inventory.CanAdd(null));
            Assert.IsFalse(inventory.Add(null));
            Assert.AreEqual(0, inventory.GetStacks(null));
            Assert.AreEqual(0, inventory.TotalStacks);
        }

        [Test]
        public void 엣지_후보가_요청수보다_적으면_가능한_만큼만_반환한다()
        {
            var catalog = new List<MutationDefinition> { Make("only") };

            List<MutationDefinition> result =
                MutationDraft.Draw(catalog, new MutationInventory(), 3, new System.Random(1));

            Assert.AreEqual(1, result.Count, "부족하다고 예외가 나거나 중복이 들어가면 안 됩니다.");
        }

        [Test]
        public void 엣지_빈_카탈로그와_null_카탈로그를_안전하게_처리한다()
        {
            var inventory = new MutationInventory();

            Assert.AreEqual(0,
                MutationDraft.Draw(new List<MutationDefinition>(), inventory, 3, new System.Random(1)).Count);

            Assert.AreEqual(0,
                MutationDraft.Draw(null, inventory, 3, new System.Random(1)).Count);
        }

        [Test]
        public void 엣지_카탈로그의_중복_항목은_한_번만_센다()
        {
            MutationDefinition duplicated = Make("dup", maxStacks: 5);

            var catalog = new List<MutationDefinition> { duplicated, duplicated, duplicated };

            List<MutationDefinition> result =
                MutationDraft.Draw(catalog, new MutationInventory(), 3, new System.Random(1));

            Assert.AreEqual(1, result.Count, "같은 에셋이 여러 칸을 차지하면 선택지가 무의미해집니다.");
        }

        [Test]
        public void 엣지_카탈로그의_null_항목은_건너뛴다()
        {
            var catalog = new List<MutationDefinition> { null, Make("valid"), null };

            List<MutationDefinition> result =
                MutationDraft.Draw(catalog, new MutationInventory(), 3, new System.Random(1));

            Assert.AreEqual(1, result.Count);
            Assert.IsNotNull(result[0]);
        }

        [Test]
        public void 엣지_0개_이하를_요청하면_빈_결과를_준다()
        {
            var catalog = new List<MutationDefinition> { Make("a"), Make("b") };

            Assert.AreEqual(0,
                MutationDraft.Draw(catalog, new MutationInventory(), 0, new System.Random(1)).Count);
            Assert.AreEqual(0,
                MutationDraft.Draw(catalog, new MutationInventory(), -5, new System.Random(1)).Count);
        }
    }
}
