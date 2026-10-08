using UnityEngine;

/// <summary>
/// 맵 덩어리가 쓰는 레이어 (결정 2-89). 「장 맵 굽기」가 TagManager에 이름을 적고 덩어리에 붙인다.
/// </summary>
public static class GameLayers
{
    /// <summary>화살 · 시야를 막는 키 큰 덩어리 (건물 · 대숲 · 바위 · 장승 · 담장 …).</summary>
    public const int Obstacle = 8;

    /// <summary>걸음만 막는 낮은 덩어리 (좌판 · 수풀 · 우물 · 금줄) — 화살 · 시야는 넘어간다.</summary>
    public const int LowCover = 9;

    public const string ObstacleName = "Obstacle";
    public const string LowCoverName = "LowCover";

    /// <summary>화살 · 시야를 막는 레이어.</summary>
    public const int ShotBlockMask = 1 << Obstacle;
}

/// <summary>
/// 【덩어리 규칙】 (결정 2-89 · 【임시 — 바꿀 수 있다】) — 무엇이 걸음 · 화살 · 시야를 막는가.
///
///   · 걸음: 막히는 덩어리 전부 (MapBlock.Solid — 냇물 · 꽃밭만 지나간다)
///   · 화살 · 시야: 키 큰 것만. 낮은 것(좌판 · 수풀 · 우물 · 장 끝 금줄)과 구역 사이 금줄은 넘어간다
///   · 폭발 · 범위 스킬은 아직 벽을 보지 않는다 (ExplosionsIgnoreWalls)
/// 【바꾸는 법】 BlocksShots만 고치고 「장 맵 굽기」를 다시 돌린다 — 탄 · 적 시야는 레이어만 본다.
/// </summary>
public static class ObstacleRules
{
    /// <summary>폭발 · 범위 스킬이 벽 너머에도 닿는다 (지금은 그렇다 — 나중에 바꿀 자리).</summary>
    public const bool ExplosionsIgnoreWalls = true;

    /// <summary>이 덩어리가 화살 · 시야를 막는가.</summary>
    public static bool BlocksShots(MapBlockKind kind) => kind switch
    {
        MapBlockKind.Boundary => true,
        MapBlockKind.Bamboo => true,
        MapBlockKind.Rock => true,
        MapBlockKind.Building => true,
        MapBlockKind.Tree => true,
        MapBlockKind.Fence => true,
        MapBlockKind.Post => true,
        _ => false,   // Bush · Stall · Well · Geumjul (낮다) · Water · Flowerbed (막지 않는다)
    };

    /// <summary>덩어리가 앉을 레이어. 막히지 않는 것은 Default(0).</summary>
    public static int LayerFor(MapBlock block)
        => !block.Solid ? 0 : BlocksShots(block.Kind) ? GameLayers.Obstacle : GameLayers.LowCover;

    /// <summary>구역 사이 금줄(MapGate)은 새끼줄 — 걸음만 막는다.</summary>
    public static int GateLayer => GameLayers.LowCover;
}
