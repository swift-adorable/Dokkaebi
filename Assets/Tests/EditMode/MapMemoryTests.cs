using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>지도 · 미니맵 · 마커 (결정 2-90) — 가 본 땅 · 마커 · 지도 그림 · 퀘스트 구역.</summary>
    public class MapMemoryTests
    {
        [SetUp]
        public void SetUp() => MapMemory.Reset();

        [TearDown]
        public void TearDown() => MapMemory.Reset();

        [Test]
        public void 가_본_땅은_둘레만_밝혀진다()
        {
            var fog = new FogGrid(new Vector2(80f, 45f));
            Assert.AreEqual(40, fog.Width);
            Assert.AreEqual(23, fog.Height);
            Assert.IsFalse(fog.IsRevealedAt(Vector3.zero));

            Assert.IsTrue(fog.Reveal(Vector3.zero, MapTable.RevealRadius));
            Assert.IsTrue(fog.IsRevealedAt(Vector3.zero));
            Assert.IsTrue(fog.IsRevealedAt(new Vector3(10f, 0f, 0f)));
            Assert.IsFalse(fog.IsRevealedAt(new Vector3(40f, 0f, 0f)), "반경 밖");
            Assert.IsFalse(fog.Reveal(Vector3.zero, MapTable.RevealRadius), "같은 자리는 새로 밝힐 것이 없다");
        }

        [Test]
        public void 가_본_땅은_세이브로_돌아오고_크기가_다르면_버린다()
        {
            var fog = new FogGrid(new Vector2(80f, 45f));
            fog.Reveal(new Vector3(-60f, 0f, 20f), 12f);
            string data = fog.Export();

            var back = new FogGrid(new Vector2(80f, 45f));
            Assert.IsTrue(back.Import(data));
            Assert.AreEqual(fog.RevealedCount, back.RevealedCount);
            Assert.IsTrue(back.IsRevealedAt(new Vector3(-60f, 0f, 20f)));

            var other = new FogGrid(new Vector2(55f, 85f));
            Assert.IsFalse(other.Import(data), "다른 크기의 맵");
            Assert.AreEqual(0, other.RevealedCount);
            Assert.IsFalse(other.Import("깨진 글"), "base64가 아니다");
        }

        [Test]
        public void 마커는_장마다_다섯까지이고_가까이_누르면_지운다()
        {
            for (int i = 0; i < MapTable.MaxMarkers; i++)
                Assert.AreEqual(MarkerError.None, MapMemory.AddMarker(1, new Vector2(i * 10f, 0f)));

            Assert.AreEqual(MarkerError.Full, MapMemory.AddMarker(1, new Vector2(0f, 30f)));
            Assert.AreEqual(MarkerError.None, MapMemory.AddMarker(2, Vector2.zero), "장마다 따로");
            Assert.AreEqual(MarkerError.NoMap, MapMemory.AddMarker(9, Vector2.zero));

            Assert.IsFalse(MapMemory.RemoveMarkerNear(1, new Vector2(5f, 20f), MapTable.MarkerPickRadius), "멀다");
            Assert.IsTrue(MapMemory.RemoveMarkerNear(1, new Vector2(21f, 1f), MapTable.MarkerPickRadius));
            Assert.AreEqual(MapTable.MaxMarkers - 1, MapMemory.Markers(1).Count);
            Assert.AreEqual(MarkerError.None, MapMemory.AddMarker(1, new Vector2(0f, 30f)), "지우면 다시 찍는다");
        }

        [Test]
        public void 지도_기억은_세이브로_돌아온다()
        {
            MapMemory.Fog(3).Reveal(new Vector3(0f, 0f, 80f), 20f);
            MapMemory.AddMarker(3, new Vector2(12f, -30f));
            MapMemory.AddMarker(5, new Vector2(-40f, 60f));
            int revealed = MapMemory.Fog(3).RevealedCount;

            List<SavedMap> saved = MapMemory.Capture();
            string json = JsonUtility.ToJson(new SaveData { maps = saved });

            MapMemory.Reset();
            MapMemory.Restore(JsonUtility.FromJson<SaveData>(json).maps);

            Assert.AreEqual(revealed, MapMemory.Fog(3).RevealedCount);
            Assert.AreEqual(new Vector2(12f, -30f), MapMemory.Markers(3)[0]);
            Assert.AreEqual(1, MapMemory.Markers(5).Count);
            Assert.AreEqual(0, MapMemory.Fog(4).RevealedCount, "안 가 본 장");
        }

        [Test]
        public void 지도_그림은_맵_크기이고_닫힌_구역은_어둡고_닫힌_금줄은_밝다()
        {
            ChapterMap map = ZoneMapTable.Of(1);
            Color32[] open = MapRaster.Render(map, z => true);
            Color32[] onlyFirst = MapRaster.Render(map, z => z == "1-1");

            int w = MapRaster.WidthOf(map);
            Assert.AreEqual(w * MapRaster.HeightOf(map), open.Length);
            Assert.AreEqual(110 * MapTable.PixelsPerMeter, w);

            // 1-3 우물 남쪽 빈 땅 (0, 46) — 닫히면 어둡다.
            int i = Index(map, new Vector3(0f, 0f, 46f));
            Assert.Greater(open[i].g, onlyFirst[i].g);

            // 1-2로 가는 큰길 길목 (0, -25) — 닫혀 있으면 금줄 색.
            int gate = Index(map, new Vector3(0f, 0f, -25f));
            Assert.AreEqual(MapRaster.GateColor, onlyFirst[gate]);
            Assert.AreNotEqual(MapRaster.GateColor, open[gate]);
        }

        [Test]
        public void 지도_좌표는_왕복한다()
        {
            ChapterMap map = ZoneMapTable.Of(4);
            var world = new Vector3(-37f, 0f, 22f);
            Vector3 back = MapRaster.WorldOfUv(map, MapRaster.UvOf(map, world));
            Assert.AreEqual(world.x, back.x, 0.001f);
            Assert.AreEqual(world.z, back.z, 0.001f);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), MapRaster.UvOf(map, Vector3.zero));
        }

        [Test]
        public void 퀘스트_목표가_가리키는_구역()
        {
            Assert.AreEqual("2-3", QuestZones.ZoneOf(new QuestObjective("", QuestCondition.ClearZone, "2-3")));
            Assert.AreEqual("1-1", QuestZones.ZoneOf(new QuestObjective("", QuestCondition.DefeatBoss, "yagwanggwi")));
            Assert.AreEqual("4-4", QuestZones.ZoneOf(new QuestObjective("", QuestCondition.Seen, StoryTable.EnterEvent("4-4"))));
            Assert.IsNull(QuestZones.ZoneOf(new QuestObjective("", QuestCondition.Seen, StoryTable.NightEvent(1))), "밤은 구역이 아니다");
            Assert.IsNull(QuestZones.ZoneOf(new QuestObjective("", QuestCondition.Build, "smithy")));
            Assert.IsNull(QuestZones.ZoneOf(null));

            List<string> start = QuestZones.InChapter(new QuestTracker(new StoryProgress()), 0);
            Assert.IsNotNull(start);
        }

        private static int Index(ChapterMap map, Vector3 world)
        {
            int x = Mathf.FloorToInt((world.x + map.HalfSize.x) * MapTable.PixelsPerMeter);
            int y = Mathf.FloorToInt((world.z + map.HalfSize.y) * MapTable.PixelsPerMeter);
            return y * MapRaster.WidthOf(map) + x;
        }
    }
}
