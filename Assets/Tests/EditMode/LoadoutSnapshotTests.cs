using NUnit.Framework;
using UnityEditor;

namespace Blob.Tests
{
    /// <summary>
    /// 착용 장비가 실제 성능으로 바뀌는지. (로드맵 6-H)
    ///
    /// 이 테스트가 없으면 6-C가 반복된다 —
    /// 에셋과 계산은 맞는데 게임에는 연결되지 않은 상태.
    /// 여기서 검증하는 것은 "숫자가 맞는가"가 아니라 "합쳐지는가"다.
    /// </summary>
    public class LoadoutSnapshotTests
    {
        private const string ItemRoot = "Assets/Data/ScriptableObjects/Items";

        private static EquipmentDefinition Load(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<EquipmentDefinition>($"{ItemRoot}/{path}");

            Assert.IsNotNull(asset,
                $"{path} 에셋이 없습니다. 「Blob/Equipment/장비 에셋 전체 생성」을 실행하십시오.");

            return asset;
        }

        private static ItemStack Stack(EquipmentDefinition definition)
            => new(definition, 1);

        // ── 무기 ─────────────────────────────────────────────────────────

        [Test]
        public void 무기를_착용하면_사격_성능이_무기_값이_된다()
        {
            var loadout = new EquipmentLoadout();
            var weapon = (WeaponDefinition)Load("Weapons/wpn_t5_rail.asset");

            Assert.IsTrue(loadout.TryEquip(Stack(weapon), EquipmentSlot.Weapon, out _));

            LoadoutSnapshot snapshot =
                LoadoutSnapshot.Create(loadout, EncumbranceLevel.Normal);

            Assert.AreEqual(weapon.BaseDamage, snapshot.Weapon.Damage, 0.001f, "기본 피해");
            Assert.AreEqual(weapon.FireInterval, snapshot.Weapon.FireInterval, 0.001f, "발사 간격");
            Assert.AreEqual(weapon.EffectiveRange, snapshot.Weapon.EffectiveRange, 0.001f, "유효 사거리");
            Assert.AreEqual(5, snapshot.Weapon.ArmourPenetration, "방어 관통");
        }

        [Test]
        public void 무기가_없으면_맨몸_성능이_되고_0이_아니다()
        {
            LoadoutSnapshot snapshot =
                LoadoutSnapshot.Create(new EquipmentLoadout(), EncumbranceLevel.Normal);

            Assert.Greater(snapshot.Weapon.Damage, 0f,
                "맨몸 피해가 0이면 무기를 잃었을 때 시체를 회수하러 갈 수 없습니다.");
        }

        /// <summary>티어를 올린 보람이 실제 DPS로 나타나야 한다.</summary>
        [Test]
        public void 티어가_높은_무기가_실제로_더_센_수치를_만든다()
        {
            var low = new EquipmentLoadout();
            low.TryEquip(Stack(Load("Weapons/wpn_t1_pipe.asset")), EquipmentSlot.Weapon, out _);

            var high = new EquipmentLoadout();
            high.TryEquip(Stack(Load("Weapons/wpn_t6_eraser.asset")), EquipmentSlot.Weapon, out _);

            LoadoutSnapshot a = LoadoutSnapshot.Create(low, EncumbranceLevel.Normal);
            LoadoutSnapshot b = LoadoutSnapshot.Create(high, EncumbranceLevel.Normal);

            Assert.Greater(b.Weapon.Damage, a.Weapon.Damage);
            Assert.Greater(b.Weapon.ArmourPenetration, a.Weapon.ArmourPenetration);
        }

        // ── 각인이 무기에 실제로 얹히는가 ─────────────────────────────────

        /// <summary>진격 Ⅰ·Ⅱ의 대가는 사거리다.</summary>
        [Test]
        public void 진격_2단_각인이_무기_피해를_올리고_사거리를_깎는다()
        {
            var bare = new EquipmentLoadout();
            bare.TryEquip(Stack(Load("Weapons/wpn_t3_acid.asset")), EquipmentSlot.Weapon, out _);

            var withImprint = new EquipmentLoadout();
            withImprint.TryEquip(Stack(Load("Weapons/wpn_t3_acid.asset")), EquipmentSlot.Weapon, out _);
            withImprint.TryEquip(Stack(Load("Imprints/imp_charge_t2.asset")), EquipmentSlot.ImprintA, out _);

            LoadoutSnapshot before = LoadoutSnapshot.Create(bare, EncumbranceLevel.Normal);
            LoadoutSnapshot after = LoadoutSnapshot.Create(withImprint, EncumbranceLevel.Normal);

            Assert.Greater(after.Weapon.Damage, before.Weapon.Damage, "피해가 오르지 않았습니다.");
            Assert.Less(after.Weapon.EffectiveRange, before.Weapon.EffectiveRange, "사거리가 줄지 않았습니다.");
        }

        /// <summary>
        /// 【Ⅲ의 대가는 같은 축이 아니라 다른 축이다.】
        /// 진격 Ⅲ은 사거리를 더 깎지 않고 체력을 계속 깎는다.
        /// 그래야 Ⅲ 두 개를 끼는 선택이 실제로 위험해진다.
        /// (docs/Blob_Imprint_System.md 2절)
        /// </summary>
        [Test]
        public void 진격_3단_각인의_대가는_사거리가_아니라_체력이다()
        {
            var loadout = new EquipmentLoadout();
            loadout.TryEquip(Stack(Load("Weapons/wpn_t3_acid.asset")), EquipmentSlot.Weapon, out _);

            LoadoutSnapshot before = LoadoutSnapshot.Create(loadout, EncumbranceLevel.Normal);

            var t3 = Load("Imprints/imp_charge_t3.asset");
            loadout.TryEquip(Stack(t3), EquipmentSlot.ImprintA, out _);

            LoadoutSnapshot after = LoadoutSnapshot.Create(loadout, EncumbranceLevel.Normal);

            Assert.Greater(after.Weapon.Damage, before.Weapon.Damage, "피해가 오르지 않았습니다.");

            Assert.AreEqual(before.Weapon.EffectiveRange, after.Weapon.EffectiveRange, 0.001f,
                "진격 Ⅲ은 사거리를 건드리지 않습니다.");

            Assert.Less(t3.GetStat(EquipmentStatType.HealthRegen), 0f,
                "진격 Ⅲ의 대가(체력 지속 감소)가 없습니다.");
        }

        [Test]
        public void 중장_3단_각인은_대시_쿨다운을_사실상_막는다()
        {
            var loadout = new EquipmentLoadout();
            loadout.TryEquip(Stack(Load("Imprints/imp_bulwark_t3.asset")), EquipmentSlot.ImprintA, out _);

            LoadoutSnapshot snapshot = LoadoutSnapshot.Create(loadout, EncumbranceLevel.Normal);

            Assert.Greater(snapshot.DashCooldownScale, 50f, "「대시 불가」가 반영되지 않았습니다.");
        }

        // ── 방어도 ────────────────────────────────────────────────────────

        [Test]
        public void 방어구를_입으면_방어도가_실제로_생긴다()
        {
            var loadout = new EquipmentLoadout();
            loadout.TryEquip(Stack(Load("Armour/arm_head_t4.asset")), EquipmentSlot.Head, out _);
            loadout.TryEquip(Stack(Load("Armour/arm_body_t4_heavy.asset")), EquipmentSlot.Body, out _);

            LoadoutSnapshot snapshot = LoadoutSnapshot.Create(loadout, EncumbranceLevel.Normal);

            Assert.Greater(snapshot.Defence.headArmour, 0, "머리 방어도가 0입니다.");
            Assert.Greater(snapshot.Defence.bodyArmour, 0, "몸통 방어도가 0입니다.");
        }

        // ── 무게 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 【무게가 실제로 느리게 만든다.】
        /// 이 연결이 없던 동안에는 과중량이 UI 문구만 바꿨다. (docs/Blob_Audit.md A4)
        /// </summary>
        [Test]
        public void 과중량이_이동_배율을_실제로_깎는다()
        {
            var loadout = new EquipmentLoadout();

            float normal = LoadoutSnapshot.Create(loadout, EncumbranceLevel.Normal).MoveScale;
            float heavy = LoadoutSnapshot.Create(loadout, EncumbranceLevel.Heavy).MoveScale;
            float immobile = LoadoutSnapshot.Create(loadout, EncumbranceLevel.Immobile).MoveScale;

            Assert.AreEqual(1f, normal, 0.001f);
            Assert.Less(heavy, normal, "과중량인데 느려지지 않습니다.");
            Assert.Less(immobile, heavy);
        }

        [Test]
        public void 심한_과중량부터_대시_거리가_준다()
        {
            var loadout = new EquipmentLoadout();

            Assert.AreEqual(1f,
                LoadoutSnapshot.Create(loadout, EncumbranceLevel.Heavy).DashDistanceScale, 0.001f,
                "조금 무거운 정도로 구르기를 뺏으면 안 됩니다.");

            Assert.Less(
                LoadoutSnapshot.Create(loadout, EncumbranceLevel.Overloaded).DashDistanceScale, 1f);
        }

        /// <summary>
        /// 장비의 기동 옵션과 무게가 곱해진다.
        /// 가벼운 장비를 골라도 전리품을 가득 채우면 느려진다 — 그 교환이 핵심이다.
        /// </summary>
        [Test]
        public void 이동_배율은_장비와_무게가_곱해진_값이다()
        {
            var loadout = new EquipmentLoadout();
            loadout.TryEquip(Stack(Load("Imprints/imp_feather_t2.asset")), EquipmentSlot.ImprintA, out _);

            float light = LoadoutSnapshot.Create(loadout, EncumbranceLevel.Normal).MoveScale;
            float lightButHeavy = LoadoutSnapshot.Create(loadout, EncumbranceLevel.Overloaded).MoveScale;

            Assert.Greater(light, 1f, "경량 각인이 이동을 올리지 않습니다.");
            Assert.Less(lightButHeavy, light, "경량 각인을 껴도 무게는 느리게 만들어야 합니다.");
        }

        // ── 체력 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 【체력은 늘어나지 않는다.】 장비도 패시브도 올리지 않는다.
        /// 각인만 깎는다. (docs/Blob_Audit.md B2 결정)
        /// </summary>
        [Test]
        public void 장비는_최대_체력을_올리지_않는다()
        {
            var loadout = new EquipmentLoadout();
            loadout.TryEquip(Stack(Load("Weapons/wpn_t6_eraser.asset")), EquipmentSlot.Weapon, out _);
            loadout.TryEquip(Stack(Load("Armour/arm_body_t6_heavy.asset")), EquipmentSlot.Body, out _);
            loadout.TryEquip(Stack(Load("Backpacks/bag_t6_haul.asset")), EquipmentSlot.Backpack, out _);

            LoadoutSnapshot snapshot = LoadoutSnapshot.Create(loadout, EncumbranceLevel.Normal);

            Assert.AreEqual(CombatConstants.PlayerBaseHealth, snapshot.MaxHealth,
                "최고 티어로 도배해도 최대 체력은 100이어야 합니다.");
        }

        [Test]
        public void 각인은_최대_체력을_깎되_바닥_아래로는_못_내린다()
        {
            var loadout = new EquipmentLoadout();
            loadout.TryEquip(Stack(Load("Imprints/imp_feather_t2.asset")), EquipmentSlot.ImprintA, out _);
            loadout.TryEquip(Stack(Load("Imprints/imp_devour_t2.asset")), EquipmentSlot.ImprintB, out _);

            LoadoutSnapshot snapshot = LoadoutSnapshot.Create(loadout, EncumbranceLevel.Normal);

            Assert.Less(snapshot.MaxHealth, CombatConstants.PlayerBaseHealth, "각인이 체력을 깎지 않습니다.");

            Assert.GreaterOrEqual(snapshot.MaxHealth, CombatConstants.PlayerMinHealth,
                "각인을 겹쳐도 즉사하는 체력이 되면 안 됩니다.");
        }

        // ── 치명타 ────────────────────────────────────────────────────────

        /// <summary>
        /// 【치명타 기본 확률은 0이다.】
        ///
        /// 모든 무기가 조금씩 크리가 뜨면 전 구간에 무작위성이 깔려
        /// 플레이어가 자기 실력을 판단하기 어려워진다.
        /// 0으로 두면 크리는 「내가 정밀 각인을 골랐다」는 선택의 결과가 된다.
        /// </summary>
        [Test]
        public void 정밀_각인이_없으면_치명타가_뜨지_않는다()
        {
            var loadout = new EquipmentLoadout();
            loadout.TryEquip(Stack(Load("Weapons/wpn_t6_eraser.asset")), EquipmentSlot.Weapon, out _);

            LoadoutSnapshot snapshot = LoadoutSnapshot.Create(loadout, EncumbranceLevel.Normal);

            Assert.AreEqual(0f, snapshot.Weapon.CriticalChance, 0.0001f,
                "최고 티어 무기만으로는 치명타가 뜨면 안 됩니다.");
        }

        [Test]
        public void 정밀_각인이_치명타_확률과_배율을_올린다()
        {
            var loadout = new EquipmentLoadout();
            loadout.TryEquip(Stack(Load("Weapons/wpn_t1_pipe.asset")), EquipmentSlot.Weapon, out _);
            loadout.TryEquip(Stack(Load("Imprints/imp_precision_t3.asset")), EquipmentSlot.ImprintA, out _);

            LoadoutSnapshot snapshot = LoadoutSnapshot.Create(loadout, EncumbranceLevel.Normal);

            Assert.Greater(snapshot.Weapon.CriticalChance, 0f, "정밀 각인이 확률을 주지 않습니다.");

            Assert.Greater(snapshot.Weapon.CriticalMultiplier,
                CombatConstants.BaseCriticalMultiplier, "정밀 Ⅲ은 배율도 올립니다.");

            // 정밀 Ⅲ의 대가 — 발사 간격이 크게 늘어난다.
            Assert.Greater(snapshot.Weapon.FireInterval, CombatConstants.BaseFireInterval,
                "정밀 Ⅲ의 대가(발사 간격)가 적용되지 않았습니다.");
        }

        /// <summary>상한이 없으면 나중에 부착물이 들어올 때 100%가 된다.</summary>
        [Test]
        public void 치명타_확률에는_상한이_있다()
        {
            var loadout = new EquipmentLoadout();

            // 정밀 Ⅲ + Ⅱ = 0.35 + 0.18 = 0.53 → 상한 0.5에서 잘려야 한다.
            loadout.TryEquip(Stack(Load("Imprints/imp_precision_t3.asset")), EquipmentSlot.ImprintA, out _);
            loadout.TryEquip(Stack(Load("Imprints/imp_precision_t2.asset")), EquipmentSlot.ImprintB, out _);

            LoadoutSnapshot snapshot = LoadoutSnapshot.Create(loadout, EncumbranceLevel.Normal);

            Assert.LessOrEqual(snapshot.Weapon.CriticalChance, CombatConstants.MaxCriticalChance,
                "치명타 확률이 상한을 넘었습니다.");
        }

        [Test]
        public void 착용이_없으면_기본값이다()
        {
            LoadoutSnapshot snapshot = LoadoutSnapshot.Create(null, EncumbranceLevel.Normal);

            Assert.AreEqual(CombatConstants.PlayerBaseHealth, snapshot.MaxHealth);
            Assert.AreEqual(1f, snapshot.MoveScale, 0.001f);
            Assert.AreEqual(0, snapshot.Defence.headArmour);
        }
    }
}
