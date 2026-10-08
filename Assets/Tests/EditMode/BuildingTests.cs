using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 벙커 건설 계약. (로드맵 8-K · docs/Dokkaebi_Bunker_System.md 3-4절)
    /// </summary>
    public class BuildingTests
    {
        private static ItemDefinition Material(string id)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            def.name = id;

            var so = new SerializedObject(def);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;
            so.FindProperty("kind").intValue = (int)ItemKind.Material;
            so.FindProperty("weight").floatValue = 0.1f;
            so.FindProperty("slotSize").intValue = 1;
            so.FindProperty("stackMax").intValue = 20;
            so.FindProperty("baseValue").intValue = 10;
            so.ApplyModifiedPropertiesWithoutUndo();

            return def;
        }

        private static readonly BuildingDefinition Shed = new(
            "shed", "헛간", "", gold: 100,
            materials: new[] { new MaterialCost("scrap", 5) },
            requiredBuildings: null, width: 2f, depth: 1f, opens: BunkerStation.Kind.None);

        private static readonly BuildingDefinition Tower = new(
            "tower", "탑", "", gold: 0, materials: null,
            requiredBuildings: new[] { "shed" }, width: 2f, depth: 1f, opens: BunkerStation.Kind.None);

        [Test]
        public void 지으면_골드와_재료가_빠지고_가진다()
        {
            var state = new BuildingState();
            ItemDefinition scrap = Material("scrap");
            var stash = new Inventory(10, float.MaxValue);
            var bag = new Inventory(10, 30f);
            stash.TryAdd(scrap, 3);
            bag.TryAdd(scrap, 4);
            int gold = 150;

            Assert.AreEqual(BuildError.None, state.Build(Shed, ref gold, stash, bag));

            Assert.AreEqual(50, gold);
            Assert.IsTrue(state.Owns("shed"));
            Assert.IsFalse(state.IsPlaced("shed"), "짓는 것과 놓는 것은 따로다");
            Assert.AreEqual(0, stash.CountOf(scrap), "창고에서 먼저 뺀다");
            Assert.AreEqual(2, bag.CountOf(scrap));
        }

        [Test]
        public void 재료가_모자라면_아무것도_빠지지_않는다()
        {
            var state = new BuildingState();
            ItemDefinition scrap = Material("scrap");
            var stash = new Inventory(10, float.MaxValue);
            stash.TryAdd(scrap, 4);
            int gold = 1000;

            Assert.AreEqual(BuildError.NotEnoughMaterials, state.Build(Shed, ref gold, stash, null));

            Assert.AreEqual(1000, gold);
            Assert.AreEqual(4, stash.CountOf(scrap));
            Assert.IsFalse(state.Owns("shed"));
        }

        [Test]
        public void 골드가_모자라면_짓지_못한다()
        {
            var state = new BuildingState();
            var stash = new Inventory(10, float.MaxValue);
            stash.TryAdd(Material("scrap"), 5);
            int gold = 99;

            Assert.AreEqual(BuildError.NotEnoughGold, state.Build(Shed, ref gold, stash, null));
        }

        [Test]
        public void 선행_건물이_없으면_짓지_못하고_재활용해도_선행은_채운다()
        {
            var state = new BuildingState();
            int gold = 0;

            Assert.AreEqual(BuildError.MissingBuilding, state.Build(Tower, ref gold, null, null));

            state.Own("shed");
            state.Place("shed", new BuildingPose(0f, 0f, 0));
            state.Recycle("shed");

            Assert.AreEqual(BuildError.None, state.Build(Tower, ref gold, null, null),
                "재활용은 손실이 없다 — 가진 채로 목록에 돌아간다");
        }

        [Test]
        public void 같은_건물은_두_번_짓지_않는다()
        {
            var state = new BuildingState();
            state.Own("shed");
            int gold = 0;

            Assert.AreEqual(BuildError.None, state.Build(Tower, ref gold, null, null));
            Assert.AreEqual(BuildError.AlreadyOwned, state.Build(Tower, ref gold, null, null));
        }

        [Test]
        public void 가지지_않은_건물은_놓을_수_없다()
        {
            var state = new BuildingState();

            Assert.IsFalse(state.Place("shed", new BuildingPose(1f, 1f, 0)));
        }

        // ── 바닥 ─────────────────────────────────────────────────────

        [Test]
        public void 회전하면_가로세로가_바뀐다()
        {
            Rect flat = BunkerLayout.Footprint(Shed, new BuildingPose(0f, 0f, 0));
            Rect turned = BunkerLayout.Footprint(Shed, new BuildingPose(0f, 0f, 1));

            Assert.AreEqual(2f, flat.width, 1e-4f);
            Assert.AreEqual(1f, flat.height, 1e-4f);
            Assert.AreEqual(1f, turned.width, 1e-4f);
            Assert.AreEqual(2f, turned.height, 1e-4f);
        }

        [Test]
        public void 벽_밖이나_겹치는_자리에는_놓지_못한다()
        {
            Rect inside = BunkerLayout.Footprint(Shed, new BuildingPose(0f, 3f, 0));
            Rect outside = BunkerLayout.Footprint(Shed, new BuildingPose(BunkerLayout.RoomWidth * 0.5f, 0f, 0));

            Assert.IsTrue(BunkerLayout.CanPlace(inside, null));
            Assert.IsFalse(BunkerLayout.CanPlace(outside, null));

            var occupied = new List<Rect> { BunkerLayout.Footprint(Shed, new BuildingPose(0.5f, 3f, 0)) };
            Assert.IsFalse(BunkerLayout.CanPlace(inside, occupied));
        }

        [Test]
        public void 고정_자리_셋은_방_안에_있고_서로_겹치지_않는다()
        {
            Rect[] fixedRects =
            {
                BunkerLayout.FixedRect(BunkerLayout.StashPosition),
                BunkerLayout.FixedRect(BunkerLayout.DeparturePosition),
                BunkerLayout.FixedRect(BunkerLayout.BlueprintPosition)
            };

            for (int i = 0; i < fixedRects.Length; i++)
            {
                var others = new List<Rect>(fixedRects);
                others.RemoveAt(i);

                Assert.IsTrue(BunkerLayout.CanPlace(fixedRects[i], others), $"고정 자리 {i}");
            }
        }

        // ── 표 ───────────────────────────────────────────────────────

        [Test]
        public void 건물표의_재료와_선행_건물은_전부_있다()
        {
            ItemCatalog catalog = ItemCatalog.Load();
            Assert.IsNotNull(catalog);

            foreach (BuildingDefinition definition in BuildingTable.All)
            {
                foreach (MaterialCost cost in definition.Materials)
                    Assert.IsNotNull(catalog.Find(cost.ItemId), $"{definition.Id}: 재료 {cost.ItemId} 없음");

                foreach (string required in definition.RequiredBuildings)
                    Assert.IsNotNull(BuildingTable.Find(required), $"{definition.Id}: 선행 {required} 없음");
            }
        }

        [Test]
        public void 상점_건물은_상점을_연다()
        {
            Assert.AreEqual(BunkerStation.Kind.GeneralStore, BuildingTable.Find(BuildingTable.GeneralStore).Opens);
            Assert.AreEqual(BunkerStation.Kind.Smithy, BuildingTable.Find(BuildingTable.Smithy).Opens);
            Assert.AreEqual(BunkerStation.Kind.Apothecary, BuildingTable.Find(BuildingTable.Apothecary).Opens);
            Assert.AreEqual(BunkerStation.Kind.Ledger, BuildingTable.Find(BuildingTable.LedgerRoom).Opens, "장부방 — 장 지도 · 처치 기록 (결정 2-97)");
        }

        [Test]
        public void 상인_넷이_가게_넷을_맡는다_결정_2_52()
        {
            Assert.AreEqual(BuildingTable.GeneralStore, StoryTable.Merchant(StoryTable.Elder).BuildingId);
            Assert.AreEqual(BuildingTable.Apothecary, StoryTable.Merchant(StoryTable.Chambong).BuildingId);
            Assert.AreEqual(BuildingTable.Smithy, StoryTable.Merchant(StoryTable.Debtor).BuildingId);
            Assert.AreEqual(BuildingTable.LedgerRoom, StoryTable.Merchant(StoryTable.Gildal).BuildingId);
            Assert.IsNull(StoryTable.MerchantFor(BuildingTable.Workbench), "작업대는 상인 없이");
        }

        [Test]
        public void 약은_약탕간에서만_판다_결정_2_57()
        {
            ItemCatalog catalog = ItemCatalog.Load();

            foreach (ShopKind kind in new[] { ShopKind.General, ShopKind.Smithy })
                foreach (ShopEntry entry in ShopTable.For(kind))
                    Assert.IsFalse(entry.ItemId.StartsWith("con_medkit") || entry.ItemId.StartsWith("con_stim")
                                   || entry.ItemId.StartsWith("con_ward") || entry.ItemId == "con_bandage",
                        $"{kind}: {entry.ItemId}");

            Assert.IsTrue(ShopTable.TryFind(ShopKind.Apothecary, "con_medkit_small", out _));
            Assert.IsTrue(ShopTable.TryFind(ShopKind.General, "con_water", out _), "음식은 잡화 가게");
            Assert.IsTrue(ShopTable.TryFind(ShopKind.Smithy, "wpn_t1_pipe", out _));
            Assert.IsTrue(ShopTable.TryFind(ShopKind.Smithy, "arm_head_t1", out _), "무기 + 방어구 = 대장간");
            Assert.IsNotNull(catalog);
        }

        [Test]
        public void 옛_무기_방어구_상점은_대장간으로_읽힌다()
        {
            Assert.AreEqual(BuildingTable.Smithy, BuildingTable.Canonical(BuildingTable.LegacyWeaponShop));
            Assert.AreEqual(BuildingTable.Smithy, BuildingTable.Canonical(BuildingTable.LegacyArmourShop));
            Assert.AreEqual(BuildingTable.GeneralStore, BuildingTable.Canonical(BuildingTable.GeneralStore));
            Assert.IsNull(BuildingTable.Find(BuildingTable.LegacyWeaponShop));
        }

        // ── 상점 ─────────────────────────────────────────────────────

        [Test]
        public void 모든_상점표의_아이템은_카탈로그에_있다()
        {
            ItemCatalog catalog = ItemCatalog.Load();

            foreach (ShopKind kind in ShopTable.All)
            {
                foreach (ShopEntry entry in ShopTable.For(kind))
                    Assert.IsNotNull(catalog.Find(entry.ItemId), $"{kind}: {entry.ItemId} 없음");
            }
        }

        [Test]
        public void 대장간은_티어_3까지만_판다()
        {
            ItemCatalog catalog = ItemCatalog.Load();

            foreach (ShopKind kind in new[] { ShopKind.Smithy })
            {
                foreach (ShopEntry entry in ShopTable.For(kind))
                    Assert.LessOrEqual(catalog.Find(entry.ItemId).Tier, 3, $"{kind}: {entry.ItemId}");
            }
        }

        [Test]
        public void 세_가게_모두_물건을_사_준다()
        {
            // 결정 2-37 — 대장간 · 약탕간에서도 판다.
            foreach (ShopKind kind in ShopTable.All)
                Assert.IsTrue(ShopTable.BuysFromPlayer(kind), kind.ToString());
        }
        [Test]
        public void 판_9_세이브의_무기_방어구_상점은_대장간_하나로_되살아난다()
        {
            BuildingManager.Restore(new List<SavedBuilding>
            {
                new SavedBuilding { id = BuildingTable.LegacyWeaponShop, placed = true, x = 7f, z = 0f },
                new SavedBuilding { id = BuildingTable.LegacyArmourShop, placed = true, x = 7f, z = -3f },
                new SavedBuilding { id = BuildingTable.GeneralStore, placed = false }
            });

            Assert.IsTrue(BuildingManager.State.IsPlaced(BuildingTable.Smithy));
            Assert.IsTrue(BuildingManager.State.TryGetPose(BuildingTable.Smithy, out BuildingPose pose));
            Assert.AreEqual(0f, pose.Z, "먼저 읽힌 자리");
            Assert.IsTrue(BuildingManager.State.Owns(BuildingTable.GeneralStore));
            Assert.IsFalse(BuildingManager.State.Owns(BuildingTable.LegacyWeaponShop));

            BuildingManager.Restore(null);
        }

        [Test]
        public void 판_9_세이브의_무기_방어구_재고는_대장간_재고가_된다()
        {
            ShopManager.Restore(new List<SavedStock>
            {
                new SavedStock { shop = "Weapon", id = "wpn_t1_pipe", remaining = 0 },
                new SavedStock { shop = "Armour", id = "arm_head_t1", remaining = 0 },
                new SavedStock { shop = "General", id = "con_medkit_small", remaining = 0 }
            });

            Assert.AreEqual(0, ShopManager.Of(ShopKind.Smithy).Remaining("wpn_t1_pipe"));
            Assert.AreEqual(0, ShopManager.Of(ShopKind.Smithy).Remaining("arm_head_t1"));
            Assert.AreEqual(3, ShopManager.Of(ShopKind.Apothecary).Remaining("con_medkit_small"), "약탕간은 가득 찬 재고로 시작");
            Assert.AreEqual(0, ShopManager.Of(ShopKind.General).Remaining("con_medkit_small"), "잡화 가게는 약을 팔지 않는다");

            ShopManager.Reset();
        }
    }
}
