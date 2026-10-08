using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Dokkaebi.Tests
{
    /// <summary>덩어리 규칙 · 맵 레이어 · NavMesh · 길찾기 (결정 2-89).</summary>
    public class ObstacleTests
    {
        [Test]
        public void 키_큰_덩어리만_화살과_시야를_막는다()
        {
            foreach (MapBlockKind k in new[] { MapBlockKind.Boundary, MapBlockKind.Bamboo, MapBlockKind.Rock,
                         MapBlockKind.Building, MapBlockKind.Tree, MapBlockKind.Fence, MapBlockKind.Post })
                Assert.IsTrue(ObstacleRules.BlocksShots(k), k.ToString());

            foreach (MapBlockKind k in new[] { MapBlockKind.Bush, MapBlockKind.Stall, MapBlockKind.Well,
                         MapBlockKind.Geumjul, MapBlockKind.Water, MapBlockKind.Flowerbed })
                Assert.IsFalse(ObstacleRules.BlocksShots(k), k.ToString());
        }

        [Test]
        public void 덩어리_레이어는_막는_것에_따라_나뉜다()
        {
            Assert.AreEqual(GameLayers.Obstacle, ObstacleRules.LayerFor(new MapBlock(MapBlockKind.Building, 0, 0, 4, 4, 4)));
            Assert.AreEqual(GameLayers.LowCover, ObstacleRules.LayerFor(new MapBlock(MapBlockKind.Stall, 0, 0, 4, 1, 4)));
            Assert.AreEqual(0, ObstacleRules.LayerFor(new MapBlock(MapBlockKind.Water, 0, 0, 4, 0.1f, 4)), "냇물은 막지 않는다");
            Assert.AreEqual(GameLayers.LowCover, ObstacleRules.GateLayer, "구역 사이 금줄은 걸음만 막는다");
            Assert.AreEqual(1 << GameLayers.Obstacle, GameLayers.ShotBlockMask);
        }

        [Test]
        public void 맵_레이어_이름이_프로젝트에_있다()
        {
            Assert.AreEqual(GameLayers.ObstacleName, LayerMask.LayerToName(GameLayers.Obstacle), "「장 맵 굽기」를 돌린다");
            Assert.AreEqual(GameLayers.LowCoverName, LayerMask.LayerToName(GameLayers.LowCover));
        }

        [Test]
        public void 장_맵마다_NavMesh가_구워져_있다()
        {
            foreach (ChapterMap map in ZoneMapTable.All)
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<NavMeshData>($"Assets/Data/NavMesh/{map.SceneName}_NavMesh.asset"),
                    $"{map.SceneName} — 「장 맵 굽기」를 돌린다");
        }

        /// <summary>
        /// 구운 씬을 열어 NavMesh로 걸어 본다 — 구역마다 출발 자리에서 그 땅의 자리(길목 · 보스 · 조각 · 방 · 열매 나무 · 통로)로
        /// 끊기지 않은 길이 있는가. 금줄은 넘지 않는다(같은 땅 안에서만 본다).
        /// 옛 주막(1-1)처럼 문으로 돌아 들어가는 자리는 길이 곧은 거리보다 길다 — 덩어리를 돌아간다는 뜻.
        /// </summary>
        [Test]
        public void 구운_맵에서_구역_출발_자리부터_그_땅의_자리까지_길이_있다()
        {
            var failures = new List<string>();
            var path = new NavMeshPath();

            foreach (ChapterMap map in ZoneMapTable.All)
            {
                Scene scene = EditorSceneManager.OpenScene($"Assets/Scenes/{map.SceneName}.unity", OpenSceneMode.Additive);

                try
                {
                    foreach (MapAnchor a in map.Anchors)
                    {
                        if (a.Kind is MapAnchorKind.Start or MapAnchorKind.Landmark)
                            continue;

                        string land = map.ZoneAt(a.Position);
                        List<MapAnchor> starts = map.AnchorsOf(MapAnchorKind.Start, land);
                        if (starts.Count == 0)
                            continue;

                        if (!NavMesh.SamplePosition(starts[0].Position, out NavMeshHit from, 1.5f, NavMesh.AllAreas)
                            || !NavMesh.SamplePosition(a.Position, out NavMeshHit to, 1.5f, NavMesh.AllAreas))
                        {
                            failures.Add($"{map.SceneName} {a.Kind} {a.ZoneId} {a.Position} — NavMesh 위가 아니다");
                            continue;
                        }

                        if (!NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path)
                            || path.status != NavMeshPathStatus.PathComplete)
                            failures.Add($"{map.SceneName} {a.Kind} {a.ZoneId} {a.Position} {a.Label} — 길이 끊겼다 ({path.status})");
                    }

                    if (map.Chapter == 1)
                    {
                        // 옛 주막 부뚜막 밑 조각 — 출발 자리에서 주막 동쪽 문으로 돌아 들어간다.
                        MapAnchor piece = map.AnchorsOf(MapAnchorKind.Piece, "1-1")[0];
                        Vector3 start = map.AnchorsOf(MapAnchorKind.Start, "1-1")[0].Position;
                        NavMesh.SamplePosition(start, out NavMeshHit s, 1.5f, NavMesh.AllAreas);
                        NavMesh.SamplePosition(piece.Position, out NavMeshHit e, 1.5f, NavMesh.AllAreas);
                        NavMesh.CalculatePath(s.position, e.position, NavMesh.AllAreas, path);

                        float length = 0f;
                        Vector3[] corners = path.corners;
                        for (int i = 1; i < corners.Length; i++)
                            length += Vector3.Distance(corners[i - 1], corners[i]);

                        Assert.Greater(corners.Length, 2, "주막 벽을 돌아간다");
                        Assert.Greater(length, Vector3.Distance(s.position, e.position) + 1f, "곧은 거리보다 길다");
                    }
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void NavMesh가_없으면_곧장_간다()
        {
            var pathing = new EnemyPathing();
            Vector3 dir = pathing.Direction(Vector3.zero, new Vector3(10f, 1f, 0f), out bool detour);

            Assert.IsFalse(detour);
            Assert.AreEqual(1f, dir.x, 0.001f);
            Assert.AreEqual(0f, dir.y, 0.001f, "높이는 보지 않는다");
        }
    }
}
