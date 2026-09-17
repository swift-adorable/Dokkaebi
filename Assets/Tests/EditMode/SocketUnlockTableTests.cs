using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// 각성 레벨 → 소켓 개방 표가 문서(Skill_System.md 12-1절)와 일치하는지 고정한다.
    ///
    /// 이 표는 「레벨업의 보상이 무엇인가」 그 자체다.
    /// 선택창을 없앤 뒤 레벨업을 계속 원하게 만드는 유일한 동력이므로
    /// 조용히 바뀌면 안 된다.
    /// </summary>
    public class SocketUnlockTableTests
    {
        [Test]
        public void 레벨_1은_핵심_1과_소켓_1로_시작한다()
        {
            SocketCapacity capacity = SocketUnlockTable.Evaluate(1);

            Assert.AreEqual(1, capacity.CoreSlots);
            Assert.AreEqual(1, capacity.SocketsInCore0);
            Assert.AreEqual(0, capacity.SocketsInCore1);
            Assert.AreEqual(0, capacity.MetaSlots);
            Assert.AreEqual(0, capacity.HeraldSlots);
        }

        [Test]
        public void 레벨_0_이하도_레벨_1로_취급한다()
        {
            // 각성 레벨이 0이 되는 경로는 없어야 하지만, 생겨도 아무것도 못 끼우는
            // 상태로 런이 시작되면 안 된다.
            Assert.AreEqual(1, SocketUnlockTable.Evaluate(0).CoreSlots);
            Assert.AreEqual(1, SocketUnlockTable.Evaluate(-5).CoreSlots);
        }

        [Test]
        public void 두번째_핵심은_레벨_7에_열린다()
        {
            Assert.AreEqual(1, SocketUnlockTable.Evaluate(6).CoreSlots);
            Assert.AreEqual(2, SocketUnlockTable.Evaluate(7).CoreSlots);
            Assert.AreEqual(7, SocketUnlockTable.SecondCoreUnlockLevel);
        }

        [Test]
        public void 발동은_11과_13에_열린다()
        {
            Assert.AreEqual(0, SocketUnlockTable.Evaluate(10).MetaSlots);
            Assert.AreEqual(1, SocketUnlockTable.Evaluate(11).MetaSlots);
            Assert.AreEqual(2, SocketUnlockTable.Evaluate(13).MetaSlots);
        }

        [Test]
        public void 전령은_15에_열린다()
        {
            Assert.AreEqual(0, SocketUnlockTable.Evaluate(14).HeraldSlots);
            Assert.AreEqual(1, SocketUnlockTable.Evaluate(15).HeraldSlots);
        }

        [Test]
        public void 최고_레벨에서_전부_열린다()
        {
            SocketCapacity capacity = SocketUnlockTable.Evaluate(SocketUnlockTable.FullyOpenLevel);

            Assert.AreEqual(SocketUnlockTable.MaxCores, capacity.CoreSlots);
            Assert.AreEqual(SocketUnlockTable.SocketsPerCore, capacity.SocketsInCore0);
            Assert.AreEqual(SocketUnlockTable.SocketsPerCore, capacity.SocketsInCore1);
            Assert.AreEqual(SocketUnlockTable.MaxMetas, capacity.MetaSlots);
            Assert.AreEqual(SocketUnlockTable.MaxHeralds, capacity.HeraldSlots);
        }

        [Test]
        public void 레벨이_올라도_자리는_줄지_않는다()
        {
            int previous = 0;

            for (int level = 1; level <= 30; level++)
            {
                int total = SocketUnlockTable.Evaluate(level).TotalSlots;

                Assert.GreaterOrEqual(total, previous,
                    $"Lv.{level}에서 자리가 줄었습니다. 소켓은 닫히지 않습니다.");

                previous = total;
            }
        }

        [Test]
        public void 최고_레벨_이후로는_더_열리지_않는다()
        {
            int full = SocketUnlockTable.Evaluate(SocketUnlockTable.FullyOpenLevel).TotalSlots;

            Assert.AreEqual(full, SocketUnlockTable.Evaluate(99).TotalSlots);
            Assert.AreEqual(0, SocketUnlockTable.NextUnlockLevel(SocketUnlockTable.FullyOpenLevel));
        }

        [Test]
        public void 개방이_있는_레벨만_설명_문구를_돌려준다()
        {
            // 짝수 레벨에는 아무것도 열리지 않는다. 토스트를 띄우면 안 된다.
            Assert.IsEmpty(SocketUnlockTable.DescribeUnlock(2));
            Assert.IsEmpty(SocketUnlockTable.DescribeUnlock(4));

            Assert.IsNotEmpty(SocketUnlockTable.DescribeUnlock(3));
            Assert.IsNotEmpty(SocketUnlockTable.DescribeUnlock(7));

            Assert.AreEqual(0, SocketUnlockTable.SlotsOpenedAt(2));
            Assert.AreEqual(1, SocketUnlockTable.SlotsOpenedAt(3));

            // Lv7은 Core 1 + 소켓 1 = 2개가 한꺼번에 열린다.
            Assert.AreEqual(2, SocketUnlockTable.SlotsOpenedAt(7));
        }

        [Test]
        public void 표의_레벨은_오름차순이다()
        {
            for (int i = 1; i < SocketUnlockTable.RowCount; i++)
            {
                Assert.Greater(SocketUnlockTable.LevelAt(i), SocketUnlockTable.LevelAt(i - 1),
                    "표의 레벨이 오름차순이 아니면 Evaluate가 잘못된 행을 고릅니다.");
            }
        }

        [Test]
        public void 다음_개방_레벨을_알려준다()
        {
            Assert.AreEqual(3, SocketUnlockTable.NextUnlockLevel(1));
            Assert.AreEqual(3, SocketUnlockTable.NextUnlockLevel(2));
            Assert.AreEqual(7, SocketUnlockTable.NextUnlockLevel(6));
        }
    }
}
