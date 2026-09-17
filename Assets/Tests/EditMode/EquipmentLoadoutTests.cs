using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blob.Tests
{
    /// <summary>
    /// 장비 착용 · 옵션 합산 테스트. (docs/Blob_Equipment_System.md)
    /// </summary>
    public class EquipmentLoadoutTests
    {
        private static EquipmentDefinition Create(
            string id, EquipmentSlot slot, int tier = 1,
            EquipmentStat[] stats = null, string imprintFamily = "",
            StatusEffectType immunity = StatusEffectType.None,
            int maxDurability = 0, float weight = 1f)
        {
            var def = ScriptableObject.CreateInstance<EquipmentDefinition>();
            def.name = id;

            var so = new SerializedObject(def);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;
            so.FindProperty("description").stringValue = id + " 설명";
            so.FindProperty("kind").intValue =
                (int)(slot == EquipmentSlot.ImprintA || slot == EquipmentSlot.ImprintB
                    ? ItemKind.Imprint
                    : slot == EquipmentSlot.Weapon ? ItemKind.Weapon
                    : slot == EquipmentSlot.Backpack ? ItemKind.Backpack : ItemKind.Armour);
            so.FindProperty("tier").intValue = tier;
            so.FindProperty("weight").floatValue = weight;
            so.FindProperty("maxDurability").intValue = maxDurability;
            so.FindProperty("slot").intValue = (int)slot;
            so.FindProperty("imprintFamily").stringValue = imprintFamily;
            so.FindProperty("immunity").intValue = (int)immunity;

            SerializedProperty list = so.FindProperty("stats");
            int count = stats?.Length ?? 0;
            list.arraySize = count;

            for (int i = 0; i < count; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("type").intValue = (int)stats[i].type;
                e.FindPropertyRelative("value").floatValue = stats[i].value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            return def;
        }

        private static EquipmentStat[] S(params (EquipmentStatType, float)[] pairs)
            => pairs.Select(p => new EquipmentStat(p.Item1, p.Item2)).ToArray();

        // ── 슬롯 ──────────────────────────────────────────────────────

        [Test]
        public void 지정된_슬롯에만_들어간다()
        {
            var loadout = new EquipmentLoadout();
            EquipmentDefinition helmet = Create("helmet", EquipmentSlot.Head);

            Assert.IsTrue(loadout.CanEquip(new ItemStack(helmet), EquipmentSlot.Head));
            Assert.IsFalse(loadout.CanEquip(new ItemStack(helmet), EquipmentSlot.Body));
        }

        [Test]
        public void 착용하면_기존_장비가_반환된다()
        {
            var loadout = new EquipmentLoadout();

            var first = new ItemStack(Create("helmet_a", EquipmentSlot.Head));
            var second = new ItemStack(Create("helmet_b", EquipmentSlot.Head));

            Assert.IsTrue(loadout.TryEquip(first, EquipmentSlot.Head, out ItemStack none));
            Assert.IsNull(none);

            Assert.IsTrue(loadout.TryEquip(second, EquipmentSlot.Head, out ItemStack previous));
            Assert.AreSame(first, previous, "벗겨진 장비는 버려지지 않고 돌아와야 합니다.");
        }

        // ── 각인 중복 규칙 ────────────────────────────────────────────

        [Test]
        public void 각인은_종류와_등급이_둘_다_같으면_중복_불가다()
        {
            var loadout = new EquipmentLoadout();

            var a = new ItemStack(Create("sense_2", EquipmentSlot.ImprintA, tier: 2, imprintFamily: "sense"));
            var same = new ItemStack(Create("sense_2b", EquipmentSlot.ImprintA, tier: 2, imprintFamily: "sense"));

            loadout.TryEquip(a, EquipmentSlot.ImprintA, out _);

            Assert.IsFalse(loadout.CanEquip(same, EquipmentSlot.ImprintB));
        }

        [Test]
        public void 같은_종류라도_등급이_다르면_장착된다()
        {
            // 감지 II + 감지 III 는 장착된다. [확인됨 — 덕코프]
            // 이 규칙이 있어야 "최상위 하나를 두 개 끼는" 단조로운 답이 막힌다.
            var loadout = new EquipmentLoadout();

            var two = new ItemStack(Create("sense_2", EquipmentSlot.ImprintA, tier: 2, imprintFamily: "sense"));
            var three = new ItemStack(Create("sense_3", EquipmentSlot.ImprintA, tier: 3, imprintFamily: "sense"));

            loadout.TryEquip(two, EquipmentSlot.ImprintA, out _);

            Assert.IsTrue(loadout.CanEquip(three, EquipmentSlot.ImprintB));
        }

        [Test]
        public void 종류가_다르면_같은_등급이라도_장착된다()
        {
            var loadout = new EquipmentLoadout();

            var sense = new ItemStack(Create("sense_2", EquipmentSlot.ImprintA, tier: 2, imprintFamily: "sense"));
            var armour = new ItemStack(Create("armour_2", EquipmentSlot.ImprintA, tier: 2, imprintFamily: "armour"));

            loadout.TryEquip(sense, EquipmentSlot.ImprintA, out _);

            Assert.IsTrue(loadout.CanEquip(armour, EquipmentSlot.ImprintB));
        }

        [Test]
        public void 각인은_두_슬롯_어디에나_들어간다()
        {
            var loadout = new EquipmentLoadout();
            var imprint = new ItemStack(Create("imp", EquipmentSlot.ImprintA, imprintFamily: "x"));

            Assert.IsTrue(loadout.CanEquip(imprint, EquipmentSlot.ImprintB));
        }

        // ── 옵션 합산 ─────────────────────────────────────────────────

        [Test]
        public void 같은_옵션은_가산_합산된다()
        {
            var loadout = new EquipmentLoadout();

            loadout.TryEquip(new ItemStack(Create("helmet", EquipmentSlot.Head,
                stats: S((EquipmentStatType.HeadArmour, 4f)))), EquipmentSlot.Head, out _);

            loadout.TryEquip(new ItemStack(Create("imp", EquipmentSlot.ImprintA, imprintFamily: "armour",
                stats: S((EquipmentStatType.HeadArmour, 1.2f)))), EquipmentSlot.ImprintA, out _);

            Assert.AreEqual(5.2f, loadout.Modifiers.Get(EquipmentStatType.HeadArmour), 0.001f);
        }

        [Test]
        public void 음수_옵션이_곧_대가다()
        {
            // 「신의 용접 헬멧」 — 머리 +6에 시야각 -30%
            var loadout = new EquipmentLoadout();

            EquipmentDefinition helmet = Create("welder", EquipmentSlot.Head, tier: 6,
                stats: S((EquipmentStatType.HeadArmour, 6f), (EquipmentStatType.ViewAngle, -0.3f)));

            Assert.IsTrue(helmet.HasDrawback);

            loadout.TryEquip(new ItemStack(helmet), EquipmentSlot.Head, out _);

            Assert.AreEqual(6f, loadout.Modifiers.Get(EquipmentStatType.HeadArmour), 0.001f);
            Assert.AreEqual(-0.3f, loadout.Modifiers.Get(EquipmentStatType.ViewAngle), 0.001f);
        }

        [Test]
        public void 방어_정보로_변환된다()
        {
            var loadout = new EquipmentLoadout();

            loadout.TryEquip(new ItemStack(Create("helmet", EquipmentSlot.Head,
                stats: S((EquipmentStatType.HeadArmour, 4f), (EquipmentStatType.ResistFire, 0.12f)))),
                EquipmentSlot.Head, out _);

            DefenceProfile defence = loadout.Modifiers.ToDefenceProfile();

            Assert.AreEqual(4, defence.headArmour);
            Assert.AreEqual(0, defence.bodyArmour);
            Assert.AreEqual(0.88f, defence.resistances.fire, 0.001f, "내성 옵션은 배율에서 빼는 값입니다.");
        }

        [Test]
        public void 내성이_음수로_뒤집히지_않는다()
        {
            // 엣지 케이스 — 음수가 되면 피해가 회복이 된다.
            var loadout = new EquipmentLoadout();

            loadout.TryEquip(new ItemStack(Create("absurd", EquipmentSlot.Head,
                stats: S((EquipmentStatType.ResistFire, 5f)))), EquipmentSlot.Head, out _);

            Assert.AreEqual(0f, loadout.Modifiers.ToDefenceProfile().resistances.fire, 0.001f);
        }

        [Test]
        public void 방어도는_상한을_넘지_않는다()
        {
            var loadout = new EquipmentLoadout();

            loadout.TryEquip(new ItemStack(Create("absurd", EquipmentSlot.Head,
                stats: S((EquipmentStatType.HeadArmour, 999f)))), EquipmentSlot.Head, out _);

            Assert.AreEqual(CombatConstants.MaxArmour, loadout.Modifiers.ToDefenceProfile().headArmour);
        }

        // ── 내구도 연동 ───────────────────────────────────────────────

        [Test]
        public void 내구도가_0이면_방어_옵션만_정지한다()
        {
            // 탐지·수집·적재 옵션은 계속 작동한다.
            var loadout = new EquipmentLoadout();

            EquipmentDefinition helmet = Create("helmet", EquipmentSlot.Head, maxDurability: 100,
                stats: S((EquipmentStatType.HeadArmour, 5f), (EquipmentStatType.DetectDistance, 2f)));

            var stack = new ItemStack(helmet);
            stack.Damage(100);

            loadout.TryEquip(stack, EquipmentSlot.Head, out _);

            Assert.AreEqual(0f, loadout.Modifiers.Get(EquipmentStatType.HeadArmour), 0.001f);
            Assert.AreEqual(2f, loadout.Modifiers.Get(EquipmentStatType.DetectDistance), 0.001f,
                "탐지 옵션은 계속 작동해야 합니다.");
        }

        [Test]
        public void 마모_상태에서는_방어_옵션이_절반이_된다()
        {
            var loadout = new EquipmentLoadout();

            EquipmentDefinition helmet = Create("helmet", EquipmentSlot.Head, maxDurability: 100,
                stats: S((EquipmentStatType.HeadArmour, 6f)));

            var stack = new ItemStack(helmet);
            stack.Damage(70);

            Assert.IsTrue(stack.IsWorn);

            loadout.TryEquip(stack, EquipmentSlot.Head, out _);

            Assert.AreEqual(3f, loadout.Modifiers.Get(EquipmentStatType.HeadArmour), 0.001f);
        }

        // ── 누적 게이트 ───────────────────────────────────────────────

        [Test]
        public void 격리_방호는_장비와_소모품을_합쳐_계산된다()
        {
            // 올 오어 낫싱이 아니라서 「장비 1점 + 소모품 1개」 조합이 성립한다.
            var loadout = new EquipmentLoadout();

            loadout.TryEquip(new ItemStack(Create("ward_helm", EquipmentSlot.Head,
                stats: S((EquipmentStatType.ContainmentWard, 1f)))), EquipmentSlot.Head, out _);

            Assert.AreEqual(1, loadout.ContainmentWard(), "장비만으로는 1단계");
            Assert.AreEqual(2, loadout.ContainmentWard(consumableBonus: 1), "주사 1개를 더하면 2단계");
        }

        // ── 면역 ──────────────────────────────────────────────────────

        [Test]
        public void 저항형_각인이_상태이상_면역을_준다()
        {
            var loadout = new EquipmentLoadout();

            loadout.TryEquip(new ItemStack(Create("fireproof_2", EquipmentSlot.ImprintA,
                tier: 2, imprintFamily: "fireproof", immunity: StatusEffectType.Ignite)),
                EquipmentSlot.ImprintA, out _);

            Assert.IsTrue(loadout.Modifiers.IsImmuneTo(StatusEffectType.Ignite));
            Assert.IsFalse(loadout.Modifiers.IsImmuneTo(StatusEffectType.Freeze));
        }

        // ── 사망 페널티 ───────────────────────────────────────────────

        [Test]
        public void 죽으면_각인만_남고_전부_벗겨진다()
        {
            var loadout = new EquipmentLoadout();

            loadout.TryEquip(new ItemStack(Create("helmet", EquipmentSlot.Head)), EquipmentSlot.Head, out _);
            loadout.TryEquip(new ItemStack(Create("weapon", EquipmentSlot.Weapon)), EquipmentSlot.Weapon, out _);
            loadout.TryEquip(new ItemStack(Create("imp", EquipmentSlot.ImprintA, imprintFamily: "x")),
                EquipmentSlot.ImprintA, out _);

            var lost = loadout.DropOnDeath();

            Assert.AreEqual(2, lost.Count);
            Assert.IsTrue(loadout.IsEmpty(EquipmentSlot.Head));
            Assert.IsTrue(loadout.IsEmpty(EquipmentSlot.Weapon));
            Assert.IsFalse(loadout.IsEmpty(EquipmentSlot.ImprintA), "각인은 남습니다.");
        }

        // ── 기타 ──────────────────────────────────────────────────────

        [Test]
        public void 벗으면_합산에서_빠진다()
        {
            var loadout = new EquipmentLoadout();

            loadout.TryEquip(new ItemStack(Create("helmet", EquipmentSlot.Head,
                stats: S((EquipmentStatType.HeadArmour, 5f)))), EquipmentSlot.Head, out _);

            Assert.AreEqual(5f, loadout.Modifiers.Get(EquipmentStatType.HeadArmour), 0.001f);

            loadout.Unequip(EquipmentSlot.Head);

            Assert.AreEqual(0f, loadout.Modifiers.Get(EquipmentStatType.HeadArmour), 0.001f);
        }

        [Test]
        public void null과_장비가_아닌_아이템은_착용되지_않는다()
        {
            var loadout = new EquipmentLoadout();

            Assert.IsFalse(loadout.CanEquip(null, EquipmentSlot.Head));

            var plain = ScriptableObject.CreateInstance<ItemDefinition>();

            Assert.IsFalse(loadout.CanEquip(new ItemStack(plain), EquipmentSlot.Head));
        }

        [Test]
        public void 착용_장비의_무게가_합산된다()
        {
            var loadout = new EquipmentLoadout();

            loadout.TryEquip(new ItemStack(Create("helmet", EquipmentSlot.Head, weight: 3.6f)), EquipmentSlot.Head, out _);
            loadout.TryEquip(new ItemStack(Create("vest", EquipmentSlot.Body, weight: 7.6f)), EquipmentSlot.Body, out _);

            Assert.AreEqual(11.2f, loadout.TotalWeight, 0.001f);
        }
    }
}
