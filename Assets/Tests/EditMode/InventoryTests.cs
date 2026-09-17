using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blob.Tests
{
    /// <summary>
    /// 인벤토리 테스트. (docs/Blob_Equipment_System.md 5-1절)
    ///
    /// 장비 · 인자 · 전리품이 같은 인벤토리를 쓴다는 것이 이 게임의 핵심 결정이다.
    /// "인자를 챙길까, 전리품 공간을 남길까"가 성립하려면 셋이 같은 자원을 두고 경쟁해야 한다.
    /// </summary>
    public class InventoryTests
    {
        private static ItemDefinition Create(
            string id, ItemKind kind = ItemKind.Material,
            float weight = 1f, int slotSize = 1, int stackMax = 1,
            int maxDurability = 0, int value = 0)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            def.name = id;

            var so = new SerializedObject(def);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;
            so.FindProperty("description").stringValue = id + " 설명";
            so.FindProperty("kind").intValue = (int)kind;
            so.FindProperty("weight").floatValue = weight;
            so.FindProperty("slotSize").intValue = slotSize;
            so.FindProperty("stackMax").intValue = stackMax;
            so.FindProperty("maxDurability").intValue = maxDurability;
            so.FindProperty("baseValue").intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();

            return def;
        }

        // ── 적재 공간 ──────────────────────────────────────────────────

        [Test]
        public void 칸이_가득_차면_더_담을_수_없다()
        {
            var inv = new Inventory(slots: 3, weight: 100f);
            ItemDefinition item = Create("scrap");

            Assert.AreEqual(3, inv.TryAdd(item, 3));
            Assert.AreEqual(0, inv.FreeSlots);
            Assert.IsFalse(inv.CanAdd(item));
            Assert.AreEqual(0, inv.TryAdd(item));
        }

        [Test]
        public void 겹치는_아이템은_칸을_아낀다()
        {
            var inv = new Inventory(slots: 2, weight: 100f);
            ItemDefinition ammo = Create("ammo", stackMax: 30);

            Assert.AreEqual(30, inv.TryAdd(ammo, 30));
            Assert.AreEqual(1, inv.UsedSlots, "30개가 한 칸에 들어가야 합니다.");

            Assert.AreEqual(30, inv.TryAdd(ammo, 30));
            Assert.AreEqual(2, inv.UsedSlots);
            Assert.AreEqual(60, inv.CountOf(ammo));
        }

        [Test]
        public void 겹치기는_기존_칸의_여유부터_채운다()
        {
            var inv = new Inventory(slots: 1, weight: 100f);
            ItemDefinition ammo = Create("ammo", stackMax: 10);

            inv.TryAdd(ammo, 4);

            // 칸이 하나뿐이지만 여유 6이 있으므로 6개까지 더 들어간다.
            Assert.AreEqual(6, inv.TryAdd(ammo, 10));
            Assert.AreEqual(10, inv.CountOf(ammo));
            Assert.AreEqual(1, inv.UsedSlots);
        }

        [Test]
        public void 부분_적재를_허용한다()
        {
            // 엣지 케이스 — 공간이 모자라면 들어갈 만큼만 넣는다.
            // 전부 실패시키면 "한 칸 남았는데 아무것도 못 줍는" 상황이 된다.
            var inv = new Inventory(slots: 2, weight: 100f);
            ItemDefinition item = Create("scrap");

            Assert.AreEqual(2, inv.TryAdd(item, 5));
            Assert.AreEqual(2, inv.CountOf(item));
        }

        [Test]
        public void 여러_칸을_차지하는_아이템이_있다()
        {
            var inv = new Inventory(slots: 3, weight: 100f);
            ItemDefinition bulky = Create("crate", slotSize: 2);

            Assert.AreEqual(1, inv.TryAdd(bulky));
            Assert.AreEqual(2, inv.UsedSlots);
            Assert.AreEqual(1, inv.FreeSlots);
            Assert.IsFalse(inv.CanAdd(bulky), "2칸이 필요한데 1칸만 남았습니다.");
        }

        // ── 무게 ──────────────────────────────────────────────────────

        [Test]
        public void 무게는_상한을_넘어도_담을_수_있다()
        {
            // 중요 — 무게는 막지 않는다. 넘으면 느려질 뿐이다.
            // 막으면 "무거운 걸 주웠을 때 들고 갈까 버릴까"라는 추출 판단이 사라진다.
            var inv = new Inventory(slots: 10, weight: 5f);
            ItemDefinition heavy = Create("anvil", weight: 10f);

            Assert.AreEqual(1, inv.TryAdd(heavy));
            Assert.IsTrue(inv.IsOverweight);
            Assert.AreEqual(10f, inv.TotalWeight, 0.001f);
        }

        [Test]
        public void 과중량_단계가_비율대로_판정된다()
        {
            Assert.AreEqual(EncumbranceLevel.Normal, WeightCalculator.Evaluate(10f, 10f));
            Assert.AreEqual(EncumbranceLevel.Heavy, WeightCalculator.Evaluate(11f, 10f));
            Assert.AreEqual(EncumbranceLevel.Overloaded, WeightCalculator.Evaluate(13f, 10f));
            Assert.AreEqual(EncumbranceLevel.Immobile, WeightCalculator.Evaluate(16f, 10f));
        }

        [Test]
        public void 상한이_0이면_페널티를_주지_않는다()
        {
            // 엣지 케이스 — 0으로 나누면 무한대가 되어 항상 Immobile이 된다.
            Assert.AreEqual(EncumbranceLevel.Normal, WeightCalculator.Evaluate(100f, 0f));
        }

        [Test]
        public void 단계가_올라갈수록_이동이_느려진다()
        {
            Assert.AreEqual(1f, WeightCalculator.MoveMultiplier(EncumbranceLevel.Normal), 0.001f);
            Assert.Less(WeightCalculator.MoveMultiplier(EncumbranceLevel.Heavy), 1f);
            Assert.Less(WeightCalculator.MoveMultiplier(EncumbranceLevel.Overloaded),
                WeightCalculator.MoveMultiplier(EncumbranceLevel.Heavy));
            Assert.Less(WeightCalculator.MoveMultiplier(EncumbranceLevel.Immobile),
                WeightCalculator.MoveMultiplier(EncumbranceLevel.Overloaded));
        }

        [Test]
        public void 대시는_과적재부터_줄어든다()
        {
            // Heavy까지는 대시가 온전하다. 마지막 탈출 수단을 초반부터 뺏지 않는다.
            Assert.AreEqual(1f, WeightCalculator.DashMultiplier(EncumbranceLevel.Heavy), 0.001f);
            Assert.Less(WeightCalculator.DashMultiplier(EncumbranceLevel.Overloaded), 1f);
        }

        // ── 인자 · 장비 · 전리품이 같은 자원을 두고 경쟁한다 ──────────────

        [Test]
        public void 인자와_전리품이_같은_칸을_두고_경쟁한다()
        {
            // 이 게임의 핵심 결정 — "화력을 챙길까, 전리품 공간을 남길까"
            var inv = new Inventory(slots: 4, weight: 100f);

            ItemDefinition gem = Create("gem_fire", ItemKind.SkillGem, weight: 0.3f);
            ItemDefinition loot = Create("loot", weight: 2f);

            inv.TryAdd(gem, 3);

            Assert.AreEqual(1, inv.FreeSlots, "인자를 3개 챙기면 전리품 칸이 1개만 남습니다.");
            Assert.AreEqual(1, inv.TryAdd(loot, 3), "남은 1칸에만 들어갑니다.");
        }

        // ── 사망 페널티 ───────────────────────────────────────────────

        [Test]
        public void 죽으면_각인만_남고_전부_잃는다()
        {
            // 유저가 배울 규칙은 하나여야 한다 — 죽으면 들고 있던 것 전부.
            var inv = new Inventory(slots: 10, weight: 100f);

            ItemDefinition gem = Create("gem", ItemKind.SkillGem);
            ItemDefinition weapon = Create("weapon", ItemKind.Weapon);
            ItemDefinition loot = Create("loot", ItemKind.Material);
            ItemDefinition imprint = Create("imprint", ItemKind.Imprint);

            inv.TryAdd(gem);
            inv.TryAdd(weapon);
            inv.TryAdd(loot);
            inv.TryAdd(imprint);

            inv.DropOnDeath();

            Assert.AreEqual(0, inv.CountOf(gem), "인자도 장비와 똑같이 잃습니다.");
            Assert.AreEqual(0, inv.CountOf(weapon));
            Assert.AreEqual(0, inv.CountOf(loot));
            Assert.AreEqual(1, inv.CountOf(imprint), "각인만 남습니다.");
        }

        // ── 내구도 ────────────────────────────────────────────────────

        [Test]
        public void 내구도_33퍼센트_이하에서_성능이_떨어진다()
        {
            // 0이 되어야 망가지는 것이 아니다.
            ItemDefinition armour = Create("armour", ItemKind.Armour, maxDurability: 100);

            var stack = new ItemStack(armour);

            Assert.IsFalse(stack.IsWorn);

            stack.Damage(67);

            Assert.IsTrue(stack.IsWorn, "33 이하면 마모 상태여야 합니다.");
            Assert.IsFalse(stack.IsBroken);

            stack.Damage(100);

            Assert.IsTrue(stack.IsBroken);
            Assert.AreEqual(0, stack.Durability);
        }

        [Test]
        public void 겹치는_아이템에는_내구도를_두지_않는다()
        {
            // "내구도가 다른 것들을 어떻게 겹치는가"가 모호해진다. OnValidate가 막는다.
            ItemDefinition ammo = Create("ammo", stackMax: 30, maxDurability: 50);

            Assert.IsFalse(ammo.HasDurability, "겹치는 아이템은 내구도를 갖지 않습니다.");
        }

        [Test]
        public void 수리해도_최대치를_넘지_않는다()
        {
            ItemDefinition armour = Create("armour", ItemKind.Armour, maxDurability: 100);

            var stack = new ItemStack(armour);
            stack.Damage(50);

            Assert.AreEqual(50, stack.Repair(999));
            Assert.AreEqual(100, stack.Durability);
        }

        // ── 기타 ──────────────────────────────────────────────────────

        [Test]
        public void 총_가치를_계산한다()
        {
            // 레이드 손익 결산에 쓴다. (Master_Prompt 8-1절 — 정보를 숨기지 않는다)
            var inv = new Inventory(slots: 10, weight: 100f);

            inv.TryAdd(Create("cheap", value: 10), 1);
            inv.TryAdd(Create("pricey", value: 500), 1);

            Assert.AreEqual(510, inv.TotalValue);
        }

        [Test]
        public void null과_0개는_무시된다()
        {
            var inv = new Inventory(slots: 5, weight: 100f);

            Assert.AreEqual(0, inv.TryAdd(null));
            Assert.AreEqual(0, inv.TryAdd(Create("x"), 0));
            Assert.AreEqual(0, inv.Remove(null));
            Assert.IsFalse(inv.CanAdd(null));
            Assert.AreEqual(0, inv.UsedSlots);
        }

        [Test]
        public void 뺄_때_없는_만큼은_세지_않는다()
        {
            var inv = new Inventory(slots: 5, weight: 100f);
            ItemDefinition item = Create("ammo", stackMax: 10);

            inv.TryAdd(item, 5);

            Assert.AreEqual(5, inv.Remove(item, 99), "있는 만큼만 빠져야 합니다.");
            Assert.AreEqual(0, inv.UsedSlots, "빈 칸은 정리되어야 합니다.");
        }
    }
}
