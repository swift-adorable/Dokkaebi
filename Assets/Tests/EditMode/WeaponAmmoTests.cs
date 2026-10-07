using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>무기 5종 · 탄 7종 · 화살통 · 탄창 (결정 2-43 · 2-80).</summary>
    public class WeaponAmmoTests
    {
        private const string ItemRoot = "Assets/Data/ScriptableObjects/Items";

        // ── 종류 배율 ──────────────────────────────────────────────

        [Test]
        public void 느린_무기일수록_쏘는_동안_초당_피해가_높다()
        {
            // 1티어 활 기준 (10 · 0.40초).
            float Dps(WeaponKind k) => WeaponKindTable.ShotDamage(k, 10f, 0.40f) / WeaponKindTable.Interval(k, 0.40f);

            Assert.AreEqual(25f, Dps(WeaponKind.Bow), 0.01f, "활 = 기준");
            Assert.Less(Dps(WeaponKind.RepeatingCrossbow), Dps(WeaponKind.Bow), "연발 쇠뇌는 빠른 대신 약하다");
            Assert.Greater(Dps(WeaponKind.Crossbow), Dps(WeaponKind.Pyeonjeon), "같은 간격이면 채우기가 긴 쪽이 세다");
            Assert.Greater(Dps(WeaponKind.Rocket), Dps(WeaponKind.ScatterGun), "가장 느린 신기전이 가장 세다");
        }

        [Test]
        public void 표의_값과_같다()
        {
            // 결정 2-80 표 (1티어): 한 발 피해 · 간격
            Assert.AreEqual(0.58f, WeaponKindTable.Interval(WeaponKind.Pyeonjeon, 0.40f), 0.001f);
            Assert.AreEqual(16.2f, WeaponKindTable.ShotDamage(WeaponKind.Pyeonjeon, 10f, 0.40f), 0.1f);
            Assert.AreEqual(4.2f, WeaponKindTable.ShotDamage(WeaponKind.RepeatingCrossbow, 10f, 0.40f), 0.1f);
            Assert.AreEqual(37.5f, WeaponKindTable.ShotDamage(WeaponKind.Rocket, 10f, 0.40f), 0.2f);
        }

        [Test]
        public void 총통_계열만_탄창이고_나머지는_화살통()
        {
            foreach (WeaponKindInfo k in WeaponKindTable.All)
            {
                bool gun = k.Kind == WeaponKind.Gun || k.Kind == WeaponKind.ScatterGun;
                Assert.AreEqual(gun ? "탄창" : "화살통", k.HolderName, k.Name);
                Assert.IsTrue(AmmoTable.IsAmmo(k.AmmoId), k.Name);
            }
        }

        // ── 에셋 ──────────────────────────────────────────────────

        [Test]
        public void 무기_23종이_종류와_탄을_가진다()
        {
            foreach (WeaponAssetGenerator.Spec spec in WeaponAssetGenerator.Table())
            {
                var asset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{ItemRoot}/Weapons/{spec.id}.asset");
                Assert.IsNotNull(asset, spec.id);
                Assert.AreEqual(spec.kind, asset.WeaponType, spec.id);
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemRoot}/Ammo/{asset.AmmoId}.asset"),
                    $"{spec.id}의 탄 {asset.AmmoId}");
            }
        }

        [Test]
        public void 탄_7종_에셋이_표와_같다()
        {
            Assert.AreEqual(7, AmmoTable.All.Count);

            foreach (AmmoInfo a in AmmoTable.All)
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemRoot}/Ammo/{a.Id}.asset");
                Assert.IsNotNull(item, a.Id);
                Assert.AreEqual(ItemKind.Ammo, item.Kind, a.Id);
                Assert.AreEqual(a.Name, item.DisplayName);
                Assert.AreEqual(a.StackMax, item.StackMax);
            }
        }

        [Test]
        public void 시체에서는_그_장에서_쓰는_무기의_탄만_나온다()
        {
            Assert.IsTrue(AmmoTable.DropsIn(AmmoTable.Arrow, 0), "0장도 화살");
            Assert.IsTrue(AmmoTable.DropsIn(AmmoTable.Shot, 1));
            Assert.IsFalse(AmmoTable.DropsIn(AmmoTable.Rocket, 3), "신기전은 4장부터");
            Assert.IsTrue(AmmoTable.DropsIn(AmmoTable.Rocket, 4));
            Assert.IsTrue(AmmoTable.DropsIn("scrap_metal", 0), "탄이 아니면 막지 않는다");
        }

        // ── 화살통 · 탄창 ─────────────────────────────────────────

        private static (EquipmentLoadout loadout, Inventory bag, ItemDefinition arrow) Setup(int arrowsInBag)
        {
            var bow = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{ItemRoot}/Weapons/wpn_t1_pipe.asset");
            var arrow = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemRoot}/Ammo/{AmmoTable.Arrow}.asset");
            var loadout = new EquipmentLoadout();
            var bag = new Inventory(20, 999f);

            Assert.IsTrue(loadout.TryEquip(new ItemStack(bow), EquipmentSlot.Weapon, out _));
            if (arrowsInBag > 0)
                bag.TryAdd(arrow, arrowsInBag);

            return (loadout, bag, arrow);
        }

        [Test]
        public void 가방에서_통을_채우면_담는_수까지만()
        {
            var (loadout, bag, arrow) = Setup(50);

            Assert.AreEqual(30, loadout.RefillAmmo(bag));
            Assert.AreEqual(30, loadout.LoadedAmmo);
            Assert.AreEqual(20, bag.CountOf(arrow));

            Assert.IsTrue(loadout.ConsumeAmmo());
            Assert.AreEqual(1, loadout.RefillAmmo(bag), "빈 자리만큼");
        }

        [Test]
        public void 무기가_쓰지_않는_탄은_통에_넣을_수_없다()
        {
            var (loadout, _, arrow) = Setup(0);
            var shot = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemRoot}/Ammo/{AmmoTable.Shot}.asset");

            Assert.IsTrue(loadout.CanEquip(new ItemStack(arrow, 10), EquipmentSlot.Ammo));
            Assert.IsFalse(loadout.CanEquip(new ItemStack(shot, 10), EquipmentSlot.Ammo));
            Assert.IsFalse(loadout.CanEquip(new ItemStack(arrow, 31), EquipmentSlot.Ammo), "담는 수를 넘는다");
        }

        [Test]
        public void 무기를_벗으면_통의_탄은_가방으로()
        {
            var (loadout, bag, arrow) = Setup(30);
            loadout.RefillAmmo(bag);

            loadout.Unequip(EquipmentSlot.Weapon);
            Assert.IsTrue(loadout.ReturnMismatchedAmmo(bag));
            Assert.AreEqual(0, loadout.LoadedAmmo);
            Assert.AreEqual(30, bag.CountOf(arrow));
        }

        [Test]
        public void 쓰러지면_통의_탄도_무기와_함께_잃는다()
        {
            var (loadout, bag, _) = Setup(30);
            loadout.RefillAmmo(bag);

            var lost = loadout.DropOnDeath();
            Assert.IsTrue(lost.Any(s => s.Definition.Kind == ItemKind.Ammo));
            Assert.AreEqual(0, loadout.LoadedAmmo);
        }

        // ── 채우기 · 메기 ─────────────────────────────────────────

        [Test]
        public void 통이_비면_가방에서_채우는_동안_못_쏜다()
        {
            var feed = new AmmoFeed();

            Assert.AreEqual(AmmoFeedEvent.None, feed.Tick(true, 1, 30, 10, 1f, 0.1f));
            Assert.IsTrue(feed.CanShootWeapon(1));

            Assert.AreEqual(AmmoFeedEvent.ReloadStarted, feed.Tick(true, 0, 30, 10, 1f, 0.1f));
            Assert.IsFalse(feed.CanShootWeapon(0));
            Assert.IsFalse(feed.UseUnarmed, "채우는 동안 맨손으로 바뀌지 않는다");

            Assert.AreEqual(AmmoFeedEvent.None, feed.Tick(true, 0, 30, 10, 1f, 0.5f));
            Assert.AreEqual(AmmoFeedEvent.ReloadCompleted, feed.Tick(true, 0, 30, 10, 1f, 0.6f));
            Assert.AreEqual(AmmoFeedState.Ready, feed.State);
        }

        [Test]
        public void 탄이_다_떨어지면_무기를_메고_맨손으로_탄이_생기면_다시_든다()
        {
            var feed = new AmmoFeed();
            feed.Tick(true, 1, 30, 0, 1f, 0.1f);

            feed.Tick(true, 0, 30, 0, 1f, 0.1f);
            Assert.AreEqual(AmmoFeedState.Slinging, feed.State);
            Assert.IsFalse(feed.UseUnarmed, "메는 동안은 아무것도 못 쏜다");

            Assert.AreEqual(AmmoFeedEvent.Slung, feed.Tick(true, 0, 30, 0, 1f, AmmoFeed.SlingSeconds));
            Assert.IsTrue(feed.UseUnarmed);

            Assert.AreEqual(AmmoFeedEvent.Drawn, feed.Tick(true, 0, 30, 5, 1f, 0.1f));
            Assert.AreEqual(AmmoFeedState.Reloading, feed.State);
        }

        [Test]
        public void 덜_찬_통은_R로_채운다()
        {
            var feed = new AmmoFeed();
            feed.Tick(true, 10, 30, 50, 1f, 0.1f);

            Assert.AreEqual(AmmoFeedEvent.None, feed.Tick(true, 10, 30, 50, 1f, 0.1f));
            Assert.AreEqual(AmmoFeedEvent.ReloadStarted, feed.Tick(true, 10, 30, 50, 1f, 0.1f, reloadRequested: true));
        }

        [Test]
        public void 무기가_없으면_맨손이고_탄을_쓰지_않는다()
        {
            var feed = new AmmoFeed();
            feed.Tick(false, 0, 0, 0, 0f, 0.1f);

            Assert.IsTrue(feed.UseUnarmed);
            Assert.AreEqual(AmmoFeedState.Unarmed, feed.State);
        }
        // ── 무기 두 자루 · 미리 채우기 (결정 2-81) ─────────────────

        [Test]
        public void 미리_채우기는_25퍼센트_이하에서_가방에_탄이_있을_때만()
        {
            Assert.IsTrue(AmmoTable.ShouldOfferReload(7, 30, 10), "화살 30 → 7발부터");
            Assert.IsFalse(AmmoTable.ShouldOfferReload(8, 30, 10));
            Assert.IsTrue(AmmoTable.ShouldOfferReload(1, 4, 10), "신기전 4 → 1발");
            Assert.IsTrue(AmmoTable.ShouldOfferReload(2, 8, 10), "산탄 8 → 2발");
            Assert.IsFalse(AmmoTable.ShouldOfferReload(3, 30, 0), "가방에 없으면 안 뜬다");
        }

        [Test]
        public void 무기는_두_자리_어디에나_걸리고_빈_쪽부터_찬다()
        {
            var bow = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{ItemRoot}/Weapons/wpn_t1_pipe.asset");
            var gun = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{ItemRoot}/Weapons/wpn_t1_sechongtong.asset");
            var loadout = new EquipmentLoadout();

            Assert.AreEqual(EquipmentSlot.Weapon, loadout.FreeWeaponSlot());
            loadout.TryEquip(new ItemStack(bow), EquipmentSlot.Weapon, out _);
            Assert.AreEqual(EquipmentSlot.Weapon2, loadout.FreeWeaponSlot());
            Assert.IsTrue(loadout.TryEquip(new ItemStack(gun), EquipmentSlot.Weapon2, out _));

            Assert.AreSame(bow, loadout.ActiveWeaponDefinition);
            Assert.AreEqual(AmmoTable.Arrow, loadout.AmmoId);

            Assert.IsTrue(loadout.SetActiveWeapon(1));
            Assert.AreSame(gun, loadout.ActiveWeaponDefinition);
            Assert.AreEqual(AmmoTable.Shot, loadout.AmmoId);
            Assert.AreEqual(EquipmentSlot.Weapon2, loadout.FreeWeaponSlot(), "둘 다 차 있으면 든 쪽과 바꾼다");
        }

        [Test]
        public void 무기마다_제_통이_있고_바꿔_들어도_남는다()
        {
            var bow = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{ItemRoot}/Weapons/wpn_t1_pipe.asset");
            var gun = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{ItemRoot}/Weapons/wpn_t1_sechongtong.asset");
            var arrow = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemRoot}/Ammo/{AmmoTable.Arrow}.asset");
            var shot = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemRoot}/Ammo/{AmmoTable.Shot}.asset");
            var loadout = new EquipmentLoadout();
            var bag = new Inventory(20, 999f);

            loadout.TryEquip(new ItemStack(bow), EquipmentSlot.Weapon, out _);
            loadout.TryEquip(new ItemStack(gun), EquipmentSlot.Weapon2, out _);
            bag.TryAdd(arrow, 50);
            bag.TryAdd(shot, 50);

            Assert.AreEqual(30, loadout.RefillAmmo(bag, 0));
            Assert.AreEqual(20, loadout.RefillAmmo(bag, 1));

            loadout.ConsumeAmmo();
            Assert.AreEqual(29, loadout.LoadedAt(0));

            loadout.SetActiveWeapon(1);
            loadout.ConsumeAmmo();
            Assert.AreEqual(29, loadout.LoadedAt(0), "메고 있는 활의 화살통은 그대로");
            Assert.AreEqual(19, loadout.LoadedAt(1));
            Assert.IsTrue(loadout.ReturnMismatchedAmmo(bag));
            Assert.AreEqual(29, loadout.LoadedAt(0), "제 무기의 탄은 돌려보내지 않는다");
        }

        [Test]
        public void 메고_있는_무기의_옵션은_들어가지_않는다()
        {
            var bow = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{ItemRoot}/Weapons/wpn_t1_pipe.asset");
            var heavy = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{ItemRoot}/Weapons/wpn_t4_breaker.asset");
            var loadout = new EquipmentLoadout();
            loadout.TryEquip(new ItemStack(bow), EquipmentSlot.Weapon, out _);
            loadout.TryEquip(new ItemStack(heavy), EquipmentSlot.Weapon2, out _);

            Assert.AreEqual(0f, loadout.Modifiers.Get(EquipmentStatType.MoveAbility), 0.0001f, "흑각궁은 메고만 있다");

            loadout.SetActiveWeapon(1);
            Assert.Less(loadout.Modifiers.Get(EquipmentStatType.MoveAbility), 0f, "흑각궁을 들면 대가가 붙는다");
        }

        [Test]
        public void 바꿔_드는_동안은_못_쏘고_다_들면_새_무기로()
        {
            var feed = new AmmoFeed();
            feed.Tick(true, 10, 30, 10, 1f, 0.1f);
            Assert.AreEqual(AmmoFeedState.Ready, feed.State);

            feed.BeginSwitch();
            Assert.IsFalse(feed.CanShootWeapon(10));
            Assert.IsFalse(feed.UseUnarmed);

            feed.Tick(true, 0, 20, 5, 2f, AmmoFeed.SwitchSeconds);
            Assert.AreEqual(AmmoFeedState.Reloading, feed.State, "새 무기의 통이 비어 있으면 바로 채운다");
        }

        [Test]
        public void 퀵슬롯_1_2번은_무기_자리라_물건을_걸_수_없다()
        {
            var quick = new QuickSlots();
            var item = new ItemStack(AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemRoot}/Ammo/{AmmoTable.Arrow}.asset"), 1);

            quick.Assign(0, item);
            quick.Assign(1, item);
            Assert.IsNull(quick.Get(0));
            Assert.IsNull(quick.Get(1));

            quick.Assign(QuickSlots.FirstItemSlot, item);
            Assert.AreSame(item, quick.Get(QuickSlots.FirstItemSlot));
        }
    }
}
