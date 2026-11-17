using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 【본문에 맞춤】 (결정 2-104)
    ///   1. 참봉은 보상을 가방(봇짐)에 넣어 준다 — 본문 346
    ///   2. 샘은 하나 — 약탕간은 샘 곁 정해진 자리에만 선다 — 본문 326 · 342
    ///   3. 튜토리얼 0-1은 걸어서 고목에 닿으면 들어간다, 철수는 0-2에서 — 본문 72 · 144
    ///   (0장 궂은비는 WeatherTests)
    /// </summary>
    public class StoryAlignTests
    {
        [Test]
        public void 참봉만_가방에_넣어_준다()
        {
            Assert.IsTrue(StoryTable.RewardsToBag(StoryTable.Chambong));
            Assert.IsFalse(StoryTable.RewardsToBag(StoryTable.Debtor));
            Assert.IsFalse(StoryTable.RewardsToBag(StoryTable.Gildal));
            Assert.IsFalse(StoryTable.RewardsToBag(null));
        }

        [Test]
        public void 약탕간은_샘_곁_정해진_자리에만()
        {
            Assert.IsTrue(BunkerLayout.TryFixedPose(BuildingTable.Apothecary, out BuildingPose pose));
            Assert.IsFalse(BunkerLayout.TryFixedPose(BuildingTable.Smithy, out _));

            BuildingDefinition apothecary = BuildingTable.Find(BuildingTable.Apothecary);
            Rect rect = BunkerLayout.Footprint(apothecary, pose);

            // 방 안에 들고, 샘 · 창고 · 출발 · 설계도 자리와 겹치지 않는다.
            Assert.IsTrue(BunkerLayout.CanPlace(rect, new[]
            {
                BunkerLayout.FixedRect(BunkerLayout.SpringPosition),
                BunkerLayout.FixedRect(BunkerLayout.StashPosition),
                BunkerLayout.FixedRect(BunkerLayout.DeparturePosition),
                BunkerLayout.FixedRect(BunkerLayout.BlueprintPosition)
            }));

            // 샘 곁 — 약탕간 자리 가장자리가 샘 자리에서 1m 안 (건물이 커져 가운데끼리 거리 대신 틈을 잰다).
            Rect spring = BunkerLayout.FixedRect(BunkerLayout.SpringPosition);
            float gapX = Mathf.Max(0f, Mathf.Max(spring.xMin - rect.xMax, rect.xMin - spring.xMax));
            float gapZ = Mathf.Max(0f, Mathf.Max(spring.yMin - rect.yMax, rect.yMin - spring.yMax));
            Assert.LessOrEqual(Mathf.Max(gapX, gapZ), 1f);
        }

        [Test]
        public void 튜토리얼_영의일도_오초_버텨야_철수한다()
        {
            // 결정 2-138 — 0-1(튜토리얼)도 길목 원 안에서 5초. 닿기만 하면 들어가는 구역은 없다.
            Assert.IsFalse(ExtractionTable.ArriveOnTouch("0-1", tutorialDone: false));
            Assert.IsFalse(ExtractionTable.ArriveOnTouch("0-1", tutorialDone: true));
            Assert.IsFalse(ExtractionTable.ArriveOnTouch("0-2", tutorialDone: false));
            Assert.AreEqual(5f, ExtractionTable.ChannelSeconds);

            var channel = new ExtractionChannel(ExtractionTable.TouchSeconds);
            Assert.AreEqual(ExtractionStep.Completed, channel.Tick(true, 0.02f));
        }
    }
}
