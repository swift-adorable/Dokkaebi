using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 【전수조사 고침】 (결정 2-99)
    ///   1. 무기에 맞지 않는 탄은 든 것으로 치지 않고, 채우기가 덮어써 없애지 않는다
    ///   2. 「연소」 Ⅲ — 직접 피해는 절반, 상태이상 위력은 피해 감소에 깎이지 않는다
    ///   3. 가방은 「바로 그 묶음」을 들고 있는지 안다 (시전 중 팔기 · 창고 넣기)
    ///   4. 막이 소모품은 잡화 가게가 판다 (결정 2-91 ⑤)
    /// </summary>
    public class AuditFixTests
    {
        private const string ItemRoot = "Assets/Data/ScriptableObjects/Items";

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>($"{ItemRoot}/{path}");
            Assert.IsNotNull(asset, $"{path} 에셋이 없습니다.");
            return asset;
        }

        [Test]
        public void 맞지_않는_탄은_든_것으로_치지_않고_덮어쓰지_않는다()
        {
            var bow = Load<WeaponDefinition>("Weapons/wpn_t1_pipe.asset");
            var gun = Load<WeaponDefinition>("Weapons/wpn_t1_sechongtong.asset");
            var arrow = Load<ItemDefinition>($"Ammo/{AmmoTable.Arrow}.asset");
            var shot = Load<ItemDefinition>($"Ammo/{AmmoTable.Shot}.asset");

            var loadout = new EquipmentLoadout();
            var bag = new Inventory(1, 999f);   // 한 칸 — 화살을 돌려받을 자리가 없다

            Assert.IsTrue(loadout.TryEquip(new ItemStack(bow), EquipmentSlot.Weapon, out _));
            Assert.IsTrue(loadout.TryEquip(new ItemStack(arrow, 30), EquipmentSlot.Ammo, out _));
            Assert.AreEqual(30, loadout.LoadedAmmo);

            // 활을 총으로 바꿔 든다 — 가방이 가득해 화살이 통에 남는다
            loadout.Unequip(EquipmentSlot.Weapon);
            Assert.IsTrue(loadout.TryEquip(new ItemStack(gun), EquipmentSlot.Weapon, out _));
            bag.TryAdd(shot, 20);
            Assert.IsFalse(loadout.ReturnMismatchedAmmo(bag), "가방이 가득한데 돌려보냈다고 합니다.");

            Assert.AreEqual(0, loadout.LoadedAmmo, "총이 화살을 든 것으로 셉니다.");
            Assert.IsFalse(loadout.ConsumeAmmo(), "총이 화살을 쏩니다.");
            Assert.AreEqual(0, loadout.RefillAmmo(bag), "화살을 덮어쓰고 탄을 채웠습니다.");
            Assert.AreEqual(30, loadout.Get(EquipmentSlot.Ammo).Count, "통의 화살이 사라졌습니다.");
            Assert.AreEqual(20, bag.CountOf(shot));
        }

        [Test]
        public void 연소_3단은_직접_피해만_깎고_상태이상_위력은_지킨다()
        {
            var weapon = Load<WeaponDefinition>("Weapons/wpn_t1_pipe.asset");
            var imprint = Load<EquipmentDefinition>("Imprints/imp_smoulder_t3.asset");

            WeaponProfile before = WeaponProfile.Create(weapon, new EquipmentModifiers());
            var mods = new EquipmentModifiers();
            mods.Add(imprint);
            WeaponProfile after = WeaponProfile.Create(weapon, mods);

            Assert.AreEqual(before.Damage * 0.5f, after.Damage, 0.01f, "직접 피해 절반");

            // 탄은 깎인 직접 피해 × 위력으로 상태이상을 건다 — 원래 피해 대비 +80%여야 한다.
            float ailmentFromBase = after.Damage * after.AilmentPower / before.Damage;
            Assert.AreEqual(1.8f, ailmentFromBase, 0.01f);
        }

        [Test]
        public void 가방은_바로_그_묶음을_들고_있는지_안다()
        {
            var water = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemRoot}/Consumables/con_water.asset");
            Assert.IsNotNull(water);

            var bag = new Inventory(10, 999f);
            Assert.AreEqual(1, bag.TryAdd(water, 1));
            ItemStack stack = bag.Stacks[0];   // 퀵슬롯 · 메뉴가 가리키는 것은 가방 안의 그 묶음이다
            Assert.IsTrue(bag.Holds(stack));
            Assert.IsFalse(bag.Holds(new ItemStack(water, 1)), "같은 종류의 다른 묶음까지 든 것으로 봅니다.");

            // 팔기는 묶음을 통째로 빼 간다 — 묶음 자체는 비지 않아서 IsEmpty만으로는 시전을 막지 못했다.
            Assert.IsTrue(bag.RemoveStack(stack));
            Assert.IsFalse(bag.Holds(stack), "팔아 버린 묶음을 아직 든 것으로 봅니다.");
            Assert.IsFalse(stack.IsEmpty);
        }

        [Test]
        public void 막이_소모품은_잡화_가게가_판다()
        {
            foreach (string id in new[] { "con_guard_warmth", "con_guard_shield", "con_guard_cool", "con_guard_light" })
            {
                Assert.IsTrue(ShopTable.TryFind(ShopKind.General, id, out _), $"{id}를 잡화 가게가 팔지 않습니다.");
                Assert.IsFalse(ShopTable.TryFind(ShopKind.Apothecary, id, out _), $"{id}를 약탕간이 아직 팝니다.");
            }
        }
    
        [Test]
        public void 각인은_안전_칸을_쓰지_않는다()
        {
            var imprint = Load<EquipmentDefinition>("Imprints/imp_smoulder_t1.asset");
            var water = Load<ItemDefinition>("Consumables/con_water.asset");

            var bag = new Inventory(10, 999f);
            Assert.IsTrue(bag.TryAddStack(new ItemStack(imprint)));
            Assert.AreEqual(1, bag.TryAdd(water, 1));

            var safe = bag.SafeStacks(1);
            Assert.AreEqual(1, safe.Count);
            foreach (ItemStack s in safe)
                Assert.AreSame(water, s.Definition, "각인이 안전 칸을 차지했습니다 (각인은 어차피 남는다).");
        }

    }
}
