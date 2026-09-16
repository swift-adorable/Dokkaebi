using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// 적재(Loadout) 테스트.
    ///
    /// v5 §1-2가 "적재 제한이 없으면 빌드 설계가 불가능하다"고 확정했으므로
    /// 슬롯 상한과 Core 필수 제약이 무너지지 않는지 검증한다.
    /// </summary>
    public class MutationLoadoutTests
    {
        [Test]
        public void 기본_슬롯은_8칸이다()
        {
            var loadout = new MutationLoadout();

            Assert.AreEqual(8, loadout.SlotCapacity);
            Assert.AreEqual(MutationLoadout.MinSlotCapacity, loadout.SlotCapacity);
        }

        [Test]
        public void 슬롯은_8_미만이나_14_초과로_설정할_수_없다()
        {
            var loadout = new MutationLoadout();

            loadout.SlotCapacity = 3;
            Assert.AreEqual(8, loadout.SlotCapacity);

            loadout.SlotCapacity = 99;
            Assert.AreEqual(14, loadout.SlotCapacity);
        }

        [Test]
        public void 슬롯을_가득_채우면_더_담을_수_없다()
        {
            var loadout = new MutationLoadout();

            for (int i = 0; i < 8; i++)
                Assert.IsTrue(loadout.TryAdd(MutationTestFactory.CreateCore($"core_{i}")));

            Assert.AreEqual(0, loadout.FreeSlots);
            Assert.IsFalse(loadout.TryAdd(MutationTestFactory.CreateCore("core_overflow")));
            Assert.AreEqual(8, loadout.Count);
        }

        [Test]
        public void 같은_변이를_두_번_담을_수_없다()
        {
            var loadout = new MutationLoadout();
            MutationDefinition core = MutationTestFactory.CreateCore("core_pierce");

            Assert.IsTrue(loadout.TryAdd(core));
            Assert.IsFalse(loadout.TryAdd(core));
            Assert.AreEqual(1, loadout.Count);
        }

        [Test]
        public void Core가_없으면_유효하지_않다()
        {
            var loadout = new MutationLoadout();

            loadout.TryAdd(MutationTestFactory.CreateSupport("sup_a"));
            loadout.TryAdd(MutationTestFactory.CreatePersistent("per_a", 10));

            Assert.IsFalse(loadout.IsValid);
            Assert.IsFalse(loadout.HasCore);
            Assert.IsNotEmpty(loadout.ValidationMessage);
        }

        [Test]
        public void Core가_하나라도_있으면_유효하다()
        {
            var loadout = new MutationLoadout();

            loadout.TryAdd(MutationTestFactory.CreateSupport("sup_a"));
            loadout.TryAdd(MutationTestFactory.CreateCore("core_a"));

            Assert.IsTrue(loadout.IsValid);
            Assert.AreEqual(string.Empty, loadout.ValidationMessage);
        }

        [Test]
        public void 빈_적재는_유효하지_않다()
        {
            var loadout = new MutationLoadout();

            Assert.IsFalse(loadout.IsValid);
            Assert.IsNotEmpty(loadout.ValidationMessage);
        }

        [Test]
        public void 슬롯을_줄이면_초과분이_뒤에서부터_제거된다()
        {
            // 엣지 케이스: 세이브 데이터 롤백 등으로 슬롯이 줄어들 수 있다.
            var loadout = new MutationLoadout(14);

            for (int i = 0; i < 14; i++)
                loadout.TryAdd(MutationTestFactory.CreateCore($"core_{i}"));

            Assert.AreEqual(14, loadout.Count);

            loadout.SlotCapacity = 8;

            Assert.AreEqual(8, loadout.Count);
            Assert.AreEqual("core_0", loadout.Entries[0].Id);
            Assert.AreEqual("core_7", loadout.Entries[7].Id);
        }

        [Test]
        public void null은_담기지_않는다()
        {
            var loadout = new MutationLoadout();

            Assert.IsFalse(loadout.TryAdd(null));
            Assert.IsFalse(loadout.Contains(null));
            Assert.AreEqual(0, loadout.Count);
        }
    }
}
