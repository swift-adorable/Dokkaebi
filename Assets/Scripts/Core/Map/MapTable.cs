/// <summary>
/// 【지도 · 미니맵 · 마커 수치】 (결정 2-90 · [임시값]).
/// </summary>
public static class MapTable
{
    /// <summary>안개 한 칸의 변 (m). 작을수록 가장자리가 고와지고 세이브가 커진다.</summary>
    public const float FogCell = 4f;

    /// <summary>이만큼 둘레가 「가 본 땅」으로 밝혀진다 (m).</summary>
    public const float RevealRadius = 18f;

    /// <summary>안개를 걷는 간격 (초).</summary>
    public const float RevealInterval = 0.25f;

    /// <summary>지도 그림의 해상도 — 1m에 몇 화소.</summary>
    public const int PixelsPerMeter = 2;

    /// <summary>미니맵이 보여 주는 반경 (m) — 플레이어가 가운데.</summary>
    public const float MinimapRange = 36f;

    /// <summary>장마다 찍을 수 있는 마커 수 — 지도가 낙서장이 되지 않게 (Map_System 2절 6번).</summary>
    public const int MaxMarkers = 5;

    /// <summary>전체 지도에서 마커를 지우려고 누를 때 이만큼 가까우면 그 마커 (m).</summary>
    public const float MarkerPickRadius = 5f;

    /// <summary>두 번 누르기로 보는 간격 (초).</summary>
    public const float DoubleTapSeconds = 0.35f;

    /// <summary>길목 기둥의 가운데 높이 (m) — 「눈에 들어왔는가」를 이 점으로 본다.</summary>
    public const float PillarSightHeight = 4f;

    /// <summary>길목을 발견했는지 보는 간격 (초).</summary>
    public const float DiscoverInterval = 0.2f;
}
