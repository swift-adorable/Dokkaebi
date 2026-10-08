using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 장 맵 표 (결정 2-86 · 2-87 · ZoneMapTable) — 자리가 막힌 곳 · 바닥 밖 · 다른 구역에 있지 않은가,
    /// 금줄이 닫힌 구역으로 가는 길을 다 막는가, 열린 구역의 자리에 걸어서 다 닿는가.
    /// </summary>
    public class ZoneMapTests
    {
        private static IEnumerable<ChapterMap> Maps => ZoneMapTable.All;

        /// <summary>보스 자리는 몸 둘 · 셋이 옆으로 늘어서도 덩어리에 걸리지 않게 이만큼 비어 있어야 한다.</summary>
        private const float BossClearance = 4f;

        private static int OrderOf(ChapterMap map, string zoneId)
            => System.Array.FindIndex(StoryTable.Chapter(map.Chapter).Zones, z => z.Id == zoneId);

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
        public void 장0부터_6장까지_맵이_다_있다()
        {
            for (int chapter = 0; chapter <= StoryTable.LastChapter; chapter++)
            {
                ChapterMap map = ZoneMapTable.Of(chapter);
                Assert.IsNotNull(map, $"{chapter}장");
                Assert.AreEqual($"Chapter{chapter}", map.SceneName);
                Assert.IsTrue(ZoneMapTable.IsMapScene(map.SceneName));
            }

            Assert.AreEqual(StoryTable.LastChapter + 1, ZoneMapTable.All.Count);
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
        public void 보스_자리는_그_구역_보스_수만큼_있다()
        {
            foreach (ChapterMap map in Maps)
            {
                if (map.Chapter == 0)
                    continue;

                foreach (ZoneDefinition zone in StoryTable.Chapter(map.Chapter).Zones)
                    Assert.AreEqual(StoryTable.BossesIn(zone.Id).Count, map.AnchorsOf(MapAnchorKind.Boss, zone.Id).Count,
                        $"{zone.Id} 보스 자리");
            }
        }

        [Test]
        public void 조각_자리는_조각이_있는_구역에만_하나씩_있다()
        {
            foreach (ChapterMap map in Maps)
            foreach (ZoneDefinition zone in StoryTable.Chapter(map.Chapter).Zones)
            {
                int expected = StoryTable.PieceIn(zone.Id) != null ? 1 : 0;
                List<MapAnchor> spots = map.AnchorsOf(MapAnchorKind.Piece, zone.Id);
                Assert.AreEqual(expected, spots.Count, $"{zone.Id} 조각 자리");

                foreach (MapAnchor a in spots)
                    Assert.IsFalse(string.IsNullOrEmpty(a.Label), $"{zone.Id} 조각 자리 이름");
            }
        }

        [Test]
        public void 장1부터_구역마다_방_자리_둘과_열매_나무_자리_셋이_있다()
        {
            foreach (ChapterMap map in Maps)
            {
                if (map.Chapter == 0)
                    continue;

                foreach (ZoneDefinition zone in StoryTable.Chapter(map.Chapter).Zones)
                {
                    Assert.GreaterOrEqual(map.AnchorsOf(MapAnchorKind.Pickup, zone.Id).Count, 2, $"{zone.Id} 방 자리");
                    Assert.GreaterOrEqual(map.AnchorsOf(MapAnchorKind.BerryTree, zone.Id).Count, BerryTree.MaxTrees,
                        $"{zone.Id} 열매 나무 자리");
                }
            }
        }

        [Test]
        public void 자리는_자기_구역_땅_안이고_막힌_덩어리에_있지_않다()
        {
            foreach (ChapterMap map in Maps)
            foreach (MapAnchor a in map.Anchors)
            {
                string label = $"{map.Chapter}장 {a.Kind} {a.ZoneId} {a.Position} {a.Label}";
                Assert.IsTrue(map.InBounds(a.Position, 2f), label + " — 바닥 밖");

                string zone = map.ZoneAt(a.Position);

                // 조각 · 표지는 본문이 정한 자리라 앞 구역 땅일 수 있다(3-2 조각 여섯 = 3-1 부엌) — 그 구역이 열릴 때 이미 열린 땅.
                if (a.Kind is MapAnchorKind.Piece or MapAnchorKind.Landmark)
                {
                    Assert.IsNotNull(zone, label + " — 구역 밖");
                    Assert.LessOrEqual(OrderOf(map, zone), OrderOf(map, a.ZoneId), label + " — 뒤 구역 땅");
                }
                else
                {
                    Assert.AreEqual(a.ZoneId, zone, label + " — 다른 구역");
                }

                // 길목은 원(2.5m)이 통째로 비어 있어야 버틸 수 있다 · 보스는 몸들이 옆으로 늘어선다.
                float radius = a.Kind switch
                {
                    MapAnchorKind.Extraction => ExtractionTable.Radius,
                    MapAnchorKind.Boss => BossClearance,
                    _ => 1f,
                };
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
                {
                    Assert.IsTrue(map.AreaOf(gate.ZoneId).HasValue, gate.ZoneId);
                    Assert.Greater(OrderOf(map, gate.ZoneId), 0, $"{gate.ZoneId} — 첫 구역은 막지 않는다");
                }

                for (int i = 0; i < map.Areas.Length; i++)
                for (int j = i + 1; j < map.Areas.Length; j++)
                    Assert.IsFalse(map.Areas[i].Bounds.Overlaps(map.Areas[j].Bounds),
                        $"{map.Areas[i].ZoneId} · {map.Areas[j].ZoneId}");
            }
        }

        /// <summary>
        /// 구역이 하나씩 열릴 때마다 첫 구역 출발 자리에서 걸어 본다 (0.5m 칸 · 몸 반지름 0.5m).
        /// 닫힌 구역의 땅에는 닿지 않아야 하고(금줄이 길을 다 막는다), 열린 구역의 자리에는 다 닿아야 한다.
        /// 걸어서 닿는 땅은 어느 구역에든 속해야 한다(금줄 자리는 빼고).
        /// </summary>
        [Test]
        public void 금줄이_닫힌_구역으로_가는_길을_다_막고_열린_구역의_자리에는_다_닿는다()
        {
            var failures = new List<string>();

            foreach (ChapterMap map in Maps)
            {
                ZoneDefinition[] zones = StoryTable.Chapter(map.Chapter).Zones;
                var grid = new WalkGrid(map);
                Vector3 start = map.AnchorsOf(MapAnchorKind.Start, zones[0].Id)[0].Position;

                for (int k = 0; k < zones.Length; k++)
                {
                    var open = new HashSet<string>();
                    for (int i = 0; i <= k; i++)
                        open.Add(zones[i].Id);

                    bool[,] seen = grid.Flood(start, open);
                    var leaks = new HashSet<string>();
                    int noZone = 0;

                    for (int x = 0; x < grid.Nx; x++)
                    for (int z = 0; z < grid.Nz; z++)
                    {
                        if (!seen[x, z])
                            continue;

                        Vector3 p = grid.Center(x, z);
                        string zone = map.ZoneAt(p);

                        if (zone == null)
                        {
                            if (!grid.NearGate(p, 1.5f))
                                noZone++;
                        }
                        else if (!open.Contains(zone))
                        {
                            leaks.Add(zone);
                        }
                    }

                    if (leaks.Count > 0)
                        failures.Add($"{zones[k].Id}까지 열림 — 닫힌 {string.Join(", ", leaks)}에 걸어 들어간다");

                    if (noZone > 0)
                        failures.Add($"{zones[k].Id}까지 열림 — 구역 없는 땅 {noZone}칸");

                    foreach (MapAnchor a in map.Anchors)
                        if (open.Contains(a.ZoneId) && !grid.Reached(seen, a.Position))
                            failures.Add($"{zones[k].Id}까지 열림 — {a.Kind} {a.ZoneId} {a.Position} {a.Label}에 닿지 않는다");

                    // 다 열렸을 때 — 구역 땅인데 걸어서 닿지 않는 곳(금줄 너머 등)이 없어야 적이 갇혀 나오지 않는다.
                    if (k == zones.Length - 1)
                    {
                        int pockets = grid.CountUnreached(seen);
                        if (pockets > 0)
                            failures.Add($"{map.Chapter}장 — 구역 땅인데 닿지 않는 곳 {pockets}칸 (구역을 줄이거나 메운다)");
                    }
                }
            }

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void 비밀_통로는_1장부터_장마다_하나이고_출구는_이전_장에_하나다()
        {
            foreach (ChapterMap map in Maps)
            {
                int secrets = 0, exits = 0;

                foreach (MapAnchor a in map.Anchors)
                {
                    if (a.Kind == MapAnchorKind.Secret) secrets++;
                    if (a.Kind == MapAnchorKind.SecretExit) exits++;

                    if (a.Kind is MapAnchorKind.Secret or MapAnchorKind.SecretExit)
                        Assert.IsFalse(string.IsNullOrEmpty(a.Label), $"{map.Chapter}장 {a.Kind} 이름");
                }

                Assert.AreEqual(map.Chapter == 0 ? 0 : 1, secrets, $"{map.Chapter}장 통로 입구");
                Assert.AreEqual(map.Chapter == StoryTable.LastChapter ? 0 : 1, exits, $"{map.Chapter}장 통로 출구");

                if (map.Chapter > 0)
                {
                    MapAnchor? exit = SecretPassageRule.ExitOf(map.Chapter);
                    Assert.IsTrue(exit.HasValue, $"{map.Chapter}장 통로가 닿는 곳");
                    Assert.AreEqual(map.Chapter - 1, StoryTable.Zone(exit.Value.ZoneId).Chapter, "이전 장으로 간다");
                }
            }

            Assert.IsFalse(SecretPassageRule.ExitOf(0).HasValue, "0장에는 이전 장이 없다");
        }

        [Test]
        public void 비밀_통로는_입구_구역을_끝낸_뒤에만_나타난다()
        {
            var p = new StoryProgress();
            Assert.IsFalse(SecretPassageRule.IsOpen(p, 1), "현무 전");
            Assert.IsFalse(SecretPassageRule.IsOpen(p, 0), "0장에는 통로가 없다");

            foreach (BossDefinition b in StoryTable.Bosses)
                if (b.ZoneId.StartsWith("1-"))
                    p.Defeat(b.Id);

            Assert.IsTrue(SecretPassageRule.IsOpen(p, 1), "1-3 현무 뒤 — 우물");
            Assert.IsFalse(SecretPassageRule.IsOpen(p, 2), "2-3 강철이 전");

            p.Defeat("gangcheori");
            Assert.IsTrue(SecretPassageRule.IsOpen(p, 2), "2-3 강철이 뒤 — 상류 강바닥");

            Assert.AreEqual("우물", SecretPassageRule.PromptOf(SecretPassageRule.EntranceOf(1).Value));
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

            ChapterMap ch6 = ZoneMapTable.Of(6);
            Assert.IsTrue(ch6.CanStand(new Vector3(0f, 0f, 0f), z => true), "꽃밭은 막히지 않는다");
            Assert.IsFalse(ch6.CanStand(new Vector3(0f, 0f, 62f), z => true), "가장 오래된 금줄 위");
            Assert.IsFalse(ch6.CanStand(new Vector3(0f, 0f, 66f), z => true), "결계 너머는 구역이 아니다");
        }

        /// <summary>걸어 보기용 칸 — 덩어리 · 바닥 밖은 막힘, 금줄은 그 구역이 닫혀 있을 때만 막힘.</summary>
        private sealed class WalkGrid
        {
            private const float Cell = 0.5f;
            private const float Body = 0.5f;

            private readonly ChapterMap map;
            private readonly bool[,] solid;
            private readonly List<string>[,] gates;

            public readonly int Nx;
            public readonly int Nz;

            public WalkGrid(ChapterMap map)
            {
                this.map = map;
                Nx = Mathf.CeilToInt(map.HalfSize.x * 2f / Cell);
                Nz = Mathf.CeilToInt(map.HalfSize.y * 2f / Cell);
                solid = new bool[Nx, Nz];
                gates = new List<string>[Nx, Nz];

                for (int x = 0; x < Nx; x++)
                for (int z = 0; z < Nz; z++)
                {
                    Vector3 p = Center(x, z);
                    solid[x, z] = !map.InBounds(p, Body) || map.IsBlocked(p, Body);

                    foreach (MapGate g in map.Gates)
                        if (g.Block.Contains(p, Body))
                            (gates[x, z] ??= new List<string>()).Add(g.ZoneId);
                }
            }

            public Vector3 Center(int x, int z)
                => new(-map.HalfSize.x + (x + 0.5f) * Cell, 0f, -map.HalfSize.y + (z + 0.5f) * Cell);

            private Vector2Int Index(Vector3 p)
                => new(Mathf.FloorToInt((p.x + map.HalfSize.x) / Cell), Mathf.FloorToInt((p.z + map.HalfSize.y) / Cell));

            public bool NearGate(Vector3 p, float radius)
            {
                foreach (MapGate g in map.Gates)
                    if (g.Block.Contains(p, radius))
                        return true;

                return false;
            }

            /// <summary>구역 땅 · 막히지 않음 · 금줄 자리 아님 — 그런데 닿지 않은 칸 수.</summary>
            public int CountUnreached(bool[,] seen)
            {
                int count = 0;

                for (int x = 0; x < Nx; x++)
                for (int z = 0; z < Nz; z++)
                    if (!seen[x, z] && !solid[x, z] && gates[x, z] == null && map.ZoneAt(Center(x, z)) != null)
                        count++;

                return count;
            }

            public bool[,] Flood(Vector3 from, HashSet<string> open)
            {
                var seen = new bool[Nx, Nz];
                var queue = new Queue<Vector2Int>();
                Vector2Int s = Index(from);
                seen[s.x, s.y] = true;
                queue.Enqueue(s);

                Vector2Int[] steps = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

                while (queue.Count > 0)
                {
                    Vector2Int c = queue.Dequeue();

                    foreach (Vector2Int step in steps)
                    {
                        Vector2Int n = c + step;

                        if (n.x < 0 || n.y < 0 || n.x >= Nx || n.y >= Nz || seen[n.x, n.y] || solid[n.x, n.y])
                            continue;

                        List<string> g = gates[n.x, n.y];
                        if (g != null && !g.TrueForAll(open.Contains))
                            continue;

                        seen[n.x, n.y] = true;
                        queue.Enqueue(n);
                    }
                }

                return seen;
            }

            /// <summary>자리 칸이나 그 둘레 칸에 닿았는가.</summary>
            public bool Reached(bool[,] seen, Vector3 p)
            {
                Vector2Int c = Index(p);

                for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    int x = c.x + dx, z = c.y + dz;
                    if (x >= 0 && z >= 0 && x < Nx && z < Nz && seen[x, z])
                        return true;
                }

                return false;
            }
        }
    }
}
