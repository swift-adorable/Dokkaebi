using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>장 맵 표 (결정 2-86 · ZoneMapTable) — 자리가 막힌 곳 · 바닥 밖 · 다른 구역에 있지 않은가.</summary>
    public class ZoneMapTests
    {
        private static IEnumerable<ChapterMap> Maps => ZoneMapTable.All;

        [Test]
        public void 장0_맵이_있고_씬_이름이_맞다()
        {
            ChapterMap map = ZoneMapTable.Of(0);
            Assert.IsNotNull(map);
            Assert.AreEqual("Chapter0", map.SceneName);
            Assert.IsTrue(ZoneMapTable.IsMapScene("Chapter0"));
            Assert.IsFalse(ZoneMapTable.IsMapScene("SampleScene"));
            Assert.IsFalse(ZoneMapTable.IsMapScene("Chapter9"), "표에 없는 장");
        }

        [Test]
        public void 장의_구역마다_땅_출발_길목_두_곳이_있다()
        {
            foreach (ChapterMap map in Maps)
            foreach (ZoneDefinition zone in StoryTable.Chapter(map.Chapter).Zones)
            {
                Assert.IsTrue(map.AreaOf(zone.Id).HasValue, $"{zone.Id} 땅");
                Assert.AreEqual(1, map.AnchorsOf(MapAnchorKind.Start, zone.Id).Count, $"{zone.Id} 출발");
                Assert.AreEqual(2, map.AnchorsOf(MapAnchorKind.Extraction, zone.Id).Count, $"{zone.Id} 길목");
            }
        }

        [Test]
        public void 자리는_자기_구역_땅_안이고_막힌_덩어리에_있지_않다()
        {
            foreach (ChapterMap map in Maps)
            foreach (MapAnchor a in map.Anchors)
            {
                string label = $"{a.Kind} {a.ZoneId} {a.Position}";
                Assert.IsTrue(map.InBounds(a.Position, 2f), label + " — 바닥 밖");
                Assert.AreEqual(a.ZoneId, map.ZoneAt(a.Position), label + " — 다른 구역");

                // 길목은 원(2.5m)이 통째로 비어 있어야 버틸 수 있다.
                float radius = a.Kind == MapAnchorKind.Extraction ? ExtractionTable.Radius : 1f;
                Assert.IsFalse(map.IsBlocked(a.Position, radius), label + " — 덩어리 안");
            }
        }

        [Test]
        public void 길목은_출발_자리에서_멀다()
        {
            foreach (ChapterMap map in Maps)
            foreach (ZoneArea area in map.Areas)
            {
                Vector3 start = map.AnchorsOf(MapAnchorKind.Start, area.ZoneId)[0].Position;

                foreach (MapAnchor e in map.AnchorsOf(MapAnchorKind.Extraction, area.ZoneId))
                    Assert.Greater(Vector3.Distance(start, e.Position), ExtractionTable.MinDistance,
                        $"{area.ZoneId} 길목 {e.Position}");
            }
        }

        [Test]
        public void 금줄은_있는_구역을_막고_구역끼리_겹치지_않는다()
        {
            foreach (ChapterMap map in Maps)
            {
                foreach (MapGate gate in map.Gates)
                    Assert.IsTrue(map.AreaOf(gate.ZoneId).HasValue, gate.ZoneId);

                for (int i = 0; i < map.Areas.Length; i++)
                for (int j = i + 1; j < map.Areas.Length; j++)
                    Assert.IsFalse(map.Areas[i].Bounds.Overlaps(map.Areas[j].Bounds),
                        $"{map.Areas[i].ZoneId} · {map.Areas[j].ZoneId}");
            }
        }

        [Test]
        public void 적은_닫힌_구역과_덩어리_안에는_나오지_않는다()
        {
            ChapterMap map = ZoneMapTable.Of(0);
            System.Func<string, bool> only01 = z => z == "0-1";

            Assert.IsTrue(map.CanStand(new Vector3(-72f, 0f, -30f), only01), "0-1 출발 자리");
            Assert.IsFalse(map.CanStand(new Vector3(40f, 0f, 0f), only01), "0-2가 닫혀 있다");
            Assert.IsTrue(map.CanStand(new Vector3(40f, 0f, 0f), z => true));
            Assert.IsFalse(map.CanStand(new Vector3(-73f, 0f, -40f), z => true), "헛간 안");
            Assert.IsTrue(map.CanStand(new Vector3(-21f, 0f, 10f), z => true), "냇물은 막히지 않는다");
            Assert.IsFalse(map.CanStand(new Vector3(200f, 0f, 0f), z => true), "바닥 밖");
        }
    }
}
