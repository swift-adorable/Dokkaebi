using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// 적재(Loadout) 테스트.
    ///
    /// v5 §1-2가 "적재 제한이 없으면 빌드 설계가 불가능하다"고 확정했으므로
    /// 슬롯 상한과 Core 필수 제약이 무너지지 않는지 검증한다.
    /// </summary>
    public class SkillLoadoutTests
    {
        [Test]
        public void 기본_슬롯은_6칸이다()
        {
            var loadout = new SkillLoadout();

            Assert.AreEqual(6, loadout.SlotCapacity);
            Assert.AreEqual(SkillLoadout.MinSlotCapacity, loadout.SlotCapacity);
        }

        [Test]
        public void 슬롯은_6_미만이나_11_초과로_설정할_수_없다()
        {
            // v8: 상한 11 = Core 2 + Support 6 + 발동 2 + 전령 1
            var loadout = new SkillLoadout();

            loadout.SlotCapacity = 3;
            Assert.AreEqual(SkillLoadout.MinSlotCapacity, loadout.SlotCapacity);

            loadout.SlotCapacity = 99;
            Assert.AreEqual(SkillLoadout.MaxSlotCapacity, loadout.SlotCapacity);
            Assert.AreEqual(11, loadout.SlotCapacity);
        }

        [Test]
        public void 슬롯을_가득_채우면_더_담을_수_없다()
        {
            var loadout = new SkillLoadout();

            for (int i = 0; i < 6; i++)
                Assert.IsTrue(loadout.TryAdd(SkillTestFactory.CreateCore($"core_{i}")));

            Assert.AreEqual(0, loadout.FreeSlots);
            Assert.IsFalse(loadout.TryAdd(SkillTestFactory.CreateCore("core_overflow")));
            Assert.AreEqual(6, loadout.Count);
        }

        [Test]
        public void 같은_스킬를_두_번_담을_수_없다()
        {
            var loadout = new SkillLoadout();
            SkillDefinition core = SkillTestFactory.CreateCore("core_pierce");

            Assert.IsTrue(loadout.TryAdd(core));
            Assert.IsFalse(loadout.TryAdd(core));
            Assert.AreEqual(1, loadout.Count);
        }

        [Test]
        public void Core가_없으면_유효하지_않다()
        {
            var loadout = new SkillLoadout();

            loadout.TryAdd(SkillTestFactory.CreateSupport("sup_a"));
            loadout.TryAdd(SkillTestFactory.CreatePersistent("per_a"));

            Assert.IsFalse(loadout.IsValid);
            Assert.IsFalse(loadout.HasCore);
            Assert.IsNotEmpty(loadout.ValidationMessage);
        }

        [Test]
        public void Core가_하나라도_있으면_유효하다()
        {
            var loadout = new SkillLoadout();

            loadout.TryAdd(SkillTestFactory.CreateSupport("sup_a"));
            loadout.TryAdd(SkillTestFactory.CreateCore("core_a"));

            Assert.IsTrue(loadout.IsValid);
            Assert.AreEqual(string.Empty, loadout.ValidationMessage);
        }

        [Test]
        public void 빈_적재는_유효하지_않다()
        {
            var loadout = new SkillLoadout();

            Assert.IsFalse(loadout.IsValid);
            Assert.IsNotEmpty(loadout.ValidationMessage);
        }

        [Test]
        public void 슬롯을_줄이면_초과분이_뒤에서부터_제거된다()
        {
            // 엣지 케이스: 세이브 데이터 롤백 등으로 슬롯이 줄어들 수 있다.
            var loadout = new SkillLoadout(11);

            for (int i = 0; i < 11; i++)
                loadout.TryAdd(SkillTestFactory.CreateCore($"core_{i}"));

            Assert.AreEqual(11, loadout.Count);

            loadout.SlotCapacity = 6;

            Assert.AreEqual(6, loadout.Count);
            Assert.AreEqual("core_0", loadout.Entries[0].Id);
            Assert.AreEqual("core_5", loadout.Entries[5].Id);
        }

        [Test]
        public void null은_담기지_않는다()
        {
            var loadout = new SkillLoadout();

            Assert.IsFalse(loadout.TryAdd(null));
            Assert.IsFalse(loadout.Contains(null));
            Assert.AreEqual(0, loadout.Count);
        }
    }
}
