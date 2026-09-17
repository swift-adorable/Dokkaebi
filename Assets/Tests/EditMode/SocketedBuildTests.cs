using System.Collections.Generic;
using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// 소켓 장착 규칙. (Skill_System.md 12절)
    ///
    /// 여기서 지켜야 할 것 세 가지 —
    ///  1. 열리지 않은 자리에는 끼울 수 없다.
    ///  2. 뺄 수 있고, 뺀 인자는 【사라지지 않는다】.
    ///  3. 태그 게이팅과 상호 배타는 이전과 똑같이 작동한다.
    /// </summary>
    public class SocketedBuildTests
    {
        private static SocketedBuild At(int level)
        {
            var build = new SocketedBuild();
            build.SetAwakeningLevel(level);

            return build;
        }

        private static SkillDefinition Core(string id, SkillTag tags = SkillTag.Projectile,
                                            int level = 1)
        {
            return SkillTestFactory.CreateCore(id, tags, requiredLevel: level);
        }

        private static SkillDefinition Support(string id, SkillTag requiredTags = SkillTag.None,
                                               int level = 1)
        {
            return SkillTestFactory.CreateSupport(id, requiredTags, requiredLevel: level);
        }

        // ── 슬롯 개방 ─────────────────────────────────────────────────────

        [Test]
        public void 레벨_1에는_핵심_1개와_소켓_1개만_쓴다()
        {
            SocketedBuild build = At(1);

            Assert.IsTrue(build.TryEquipCore(Core("core_a"), 0));
            Assert.IsFalse(build.TryEquipCore(Core("core_b"), 1),
                "2번째 핵심은 Lv7에 열립니다.");

            Assert.IsTrue(build.TryEquipSupport(Support("sup_a"), 0, 0));
            Assert.IsFalse(build.TryEquipSupport(Support("sup_b"), 0, 1),
                "2번째 소켓은 Lv3에 열립니다.");
        }

        [Test]
        public void 잠긴_자리는_이유를_잠김으로_알려준다()
        {
            SocketedBuild build = At(1);
            build.TryEquipCore(Core("core_a"), 0);

            Assert.AreEqual(SocketError.SlotLocked, build.CanEquipCore(Core("core_b"), 1));
            Assert.AreEqual(SocketError.SlotLocked, build.CanEquipSupport(Support("sup_b"), 0, 1));
        }

        [Test]
        public void 레벨을_올리면_이미_끼운_것은_그대로_두고_자리만_열린다()
        {
            SocketedBuild build = At(1);
            SkillDefinition core = Core("core_a");

            build.TryEquipCore(core, 0);
            build.TryEquipSupport(Support("sup_a"), 0, 0);

            build.SetAwakeningLevel(7);

            Assert.AreSame(core, build.GetCore(0));
            Assert.IsTrue(build.TryEquipCore(Core("core_b"), 1));
            Assert.IsTrue(build.TryEquipSupport(Support("sup_b"), 0, 1));
        }

        // ── 요구 레벨 ─────────────────────────────────────────────────────

        [Test]
        public void 요구_레벨이_높은_인자는_끼울_수_없다()
        {
            SocketedBuild build = At(5);

            Assert.AreEqual(SocketError.LevelTooHigh,
                build.CanEquipCore(Core("core_late", level: 11), 0));

            Assert.IsFalse(build.TryEquipCore(Core("core_late", level: 11), 0));
        }

        // ── 태그 게이팅 ───────────────────────────────────────────────────

        [Test]
        public void 요구_태그를_만족하지_않는_핵심에는_끼워지지_않는다()
        {
            SocketedBuild build = At(SocketUnlockTable.FullyOpenLevel);

            build.TryEquipCore(Core("core_aoe", SkillTag.AreaOfEffect), 0);

            Assert.AreEqual(SocketError.TagMismatch,
                build.CanEquipSupport(Support("sup_proj", SkillTag.Projectile), 0, 0));
        }

        [Test]
        public void 핵심이_없는_소켓에는_끼울_수_없다()
        {
            SocketedBuild build = At(SocketUnlockTable.FullyOpenLevel);

            Assert.AreEqual(SocketError.NoCore, build.CanEquipSupport(Support("sup_a"), 0, 0));
        }

        [Test]
        public void 분류가_맞지_않는_자리는_거부한다()
        {
            SocketedBuild build = At(SocketUnlockTable.FullyOpenLevel);

            Assert.AreEqual(SocketError.WrongCategory, build.CanEquipCore(Support("sup_a"), 0));
            Assert.AreEqual(SocketError.WrongCategory, build.CanEquipSupport(Core("core_a"), 0, 0));
        }

        // ── 교체 · 탈착 ───────────────────────────────────────────────────

        [Test]
        public void 이미_낀_인자를_빼서_다른_것으로_갈아끼울_수_있다()
        {
            // 확정 기획 — "레이드 중에 이미 끼운 젬을 빼서 바꿔 끼울 수 있는가" → 된다.
            SocketedBuild build = At(SocketUnlockTable.FullyOpenLevel);

            SkillDefinition core = Core("core_a");
            SkillDefinition first = Support("sup_first");
            SkillDefinition second = Support("sup_second");

            build.TryEquipCore(core, 0);
            build.TryEquipSupport(first, 0, 0);

            var returned = new List<SkillDefinition>();

            Assert.IsTrue(build.TryEquipSupport(second, 0, 0, returned));

            Assert.AreSame(second, build.GetSocket(0, 0));
            CollectionAssert.AreEqual(new[] { first }, returned,
                "밀려난 인자는 사라지지 않고 호출부로 돌아와야 합니다.");
        }

        [Test]
        public void 핵심을_빼면_그_소켓의_보조도_함께_돌아온다()
        {
            SocketedBuild build = At(SocketUnlockTable.FullyOpenLevel);

            SkillDefinition core = Core("core_a");
            SkillDefinition supportA = Support("sup_a");
            SkillDefinition supportB = Support("sup_b");

            build.TryEquipCore(core, 0);
            build.TryEquipSupport(supportA, 0, 0);
            build.TryEquipSupport(supportB, 0, 1);

            var returned = new List<SkillDefinition>();

            build.UnequipCore(0, returned);

            Assert.IsNull(build.GetCore(0));
            Assert.IsNull(build.GetSocket(0, 0));
            Assert.AreEqual(3, returned.Count, "핵심 1 + 보조 2가 전부 돌아와야 합니다.");
            CollectionAssert.Contains(returned, supportA);
            CollectionAssert.Contains(returned, supportB);
        }

        [Test]
        public void 핵심을_바꾸면_태그를_잃은_보조만_돌아온다()
        {
            SocketedBuild build = At(SocketUnlockTable.FullyOpenLevel);

            build.TryEquipCore(Core("core_fire", SkillTag.Projectile | SkillTag.Fire), 0);

            SkillDefinition projectileSupport = Support("sup_proj", SkillTag.Projectile);
            SkillDefinition fireSupport = Support("sup_fire", SkillTag.Fire);

            build.TryEquipSupport(projectileSupport, 0, 0);
            build.TryEquipSupport(fireSupport, 0, 1);

            var returned = new List<SkillDefinition>();

            // 투사체 태그가 없는 핵심으로 교체한다.
            Assert.IsTrue(build.TryEquipCore(Core("core_aoe_fire", SkillTag.AreaOfEffect | SkillTag.Fire),
                0, returned));

            CollectionAssert.Contains(returned, projectileSupport);
            Assert.AreSame(fireSupport, build.GetSocket(0, 1),
                "요구 태그를 여전히 만족하는 보조는 그대로 남아야 합니다.");
        }

        [Test]
        public void 빈_자리를_빼면_아무_일도_일어나지_않는다()
        {
            SocketedBuild build = At(SocketUnlockTable.FullyOpenLevel);

            Assert.IsNull(build.UnequipSupport(0, 0));
            Assert.IsNull(build.UnequipCore(0));
            Assert.IsNull(build.UnequipHerald());
        }

        // ── 중복 ──────────────────────────────────────────────────────────

        [Test]
        public void 같은_보조를_한_핵심_안에서_두_번_끼울_수_없다()
        {
            SocketedBuild build = At(SocketUnlockTable.FullyOpenLevel);
            SkillDefinition support = Support("sup_a");

            build.TryEquipCore(Core("core_a"), 0);
            build.TryEquipSupport(support, 0, 0);

            Assert.AreEqual(SocketError.Duplicate, build.CanEquipSupport(support, 0, 1));
        }

        [Test]
        public void 같은_보조를_다른_핵심에는_하나씩_끼울_수_있다()
        {
            // 11-3절 — 중복 드랍을 허용하고, 두 Core에 하나씩 끼우는 것이 그 용도다.
            SocketedBuild build = At(SocketUnlockTable.FullyOpenLevel);
            SkillDefinition support = Support("sup_a");

            build.TryEquipCore(Core("core_a"), 0);
            build.TryEquipCore(Core("core_b"), 1);

            Assert.IsTrue(build.TryEquipSupport(support, 0, 0));
            Assert.IsTrue(build.TryEquipSupport(support, 1, 0));
        }

        [Test]
        public void 같은_핵심을_두_자리에_넣을_수_없다()
        {
            SocketedBuild build = At(SocketUnlockTable.FullyOpenLevel);
            SkillDefinition core = Core("core_a");

            build.TryEquipCore(core, 0);

            Assert.AreEqual(SocketError.Duplicate, build.CanEquipCore(core, 1));
        }

        // ── 상호 배타 ─────────────────────────────────────────────────────

        [Test]
        public void 상호_배타는_장착을_막지_않고_양쪽을_무효화한다()
        {
            // 14절 2번 — 다양성을 "중복 금지"로 강제하지 않는다.
            SocketedBuild build = At(SocketUnlockTable.FullyOpenLevel);

            SkillDefinition longFuse = SkillTestFactory.CreateSupport(
                "sup_long_fuse", SkillTag.Projectile,
                mutuallyExclusiveIds: new[] { "sup_short_fuse" });

            SkillDefinition shortFuse = Support("sup_short_fuse", SkillTag.Projectile);

            build.TryEquipCore(Core("core_a"), 0);

            Assert.IsTrue(build.TryEquipSupport(longFuse, 0, 0));
            Assert.IsTrue(build.TryEquipSupport(shortFuse, 0, 1));

            Assert.IsTrue(build.IsNullified(longFuse));
            Assert.IsTrue(build.IsNullified(shortFuse));
        }

        // ── 기타 ──────────────────────────────────────────────────────────

        [Test]
        public void 빈_소켓_수는_열리고_핵심이_있는_자리만_센다()
        {
            SocketedBuild build = At(5);   // 핵심 1 + 소켓 3

            Assert.AreEqual(0, build.FreeSocketCount, "핵심이 없으면 소켓은 세지 않습니다.");

            build.TryEquipCore(Core("core_a"), 0);

            Assert.AreEqual(3, build.FreeSocketCount);

            build.TryEquipSupport(Support("sup_a"), 0, 0);

            Assert.AreEqual(2, build.FreeSocketCount);
        }

        [Test]
        public void 전부_비우면_끼운_것이_전부_돌아온다()
        {
            SocketedBuild build = At(SocketUnlockTable.FullyOpenLevel);

            build.TryEquipCore(Core("core_a"), 0);
            build.TryEquipSupport(Support("sup_a"), 0, 0);
            build.TryEquipMeta(SkillTestFactory.CreateMeta("meta_a"), 0);
            build.TryEquipHerald(SkillTestFactory.CreatePersistent("herald_a"));

            var returned = new List<SkillDefinition>();

            build.UnequipAll(returned);

            Assert.AreEqual(4, returned.Count);
            Assert.AreEqual(0, build.EquippedCount);
        }

        [Test]
        public void Clear는_인자를_없애지_않고_자리만_비운다()
        {
            // 사망 시의 소멸은 Inventory.DropOnDeath의 몫이다. 규칙을 한 곳에만 둔다.
            SocketedBuild build = At(SocketUnlockTable.FullyOpenLevel);

            build.TryEquipCore(Core("core_a"), 0);
            build.Clear();

            Assert.AreEqual(0, build.EquippedCount);
            Assert.AreEqual(1, build.AwakeningLevel, "런이 끝나면 각성 레벨도 초기화됩니다.");
        }

        [Test]
        public void 변경_이벤트는_실제로_바뀔_때만_발행된다()
        {
            SocketedBuild build = At(SocketUnlockTable.FullyOpenLevel);

            int changes = 0;
            build.OnChanged += () => changes++;

            build.TryEquipCore(Core("core_a"), 0);
            Assert.AreEqual(1, changes);

            // 잠긴 자리라 실패한다. 이벤트가 발행되면 안 된다.
            build.TryEquipSupport(Support("sup_a"), 1, 0);
            Assert.AreEqual(1, changes);
        }
    }
}
