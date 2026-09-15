using UnityEngine;

/// <summary>
/// 플레이어 입력 소스 추상화.
///
/// BlobController는 "누가 입력을 주는지" 모른 채 이 인터페이스만 바라본다.
/// 덕분에 에디터에서는 키보드/마우스, 실기기에서는 가상 조이스틱으로
/// 구현체만 교체하면 되고 게임 로직은 한 줄도 바뀌지 않는다. (DIP)
/// </summary>
public interface IPlayerInputSource
{
    /// <summary>이동 방향. 크기 0~1. (x: 좌우, y: 앞뒤)</summary>
    Vector2 MoveInput { get; }

    /// <summary>조준 방향. 크기 0~1. 크기가 0이면 조준 입력 없음. (x: 좌우, y: 앞뒤)</summary>
    Vector2 AimInput { get; }

    /// <summary>사격 유지 여부.</summary>
    bool ShootHeld { get; }

    /// <summary>이번 프레임에 대시가 눌렸는지.</summary>
    bool DashPressed { get; }

    /// <summary>이번 프레임에 흡수가 눌렸는지.</summary>
    bool AbsorbPressed { get; }

    /// <summary>입력 소스 활성/비활성 전환.</summary>
    void SetActive(bool isActive);
}
