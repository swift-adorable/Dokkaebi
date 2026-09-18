using UnityEngine;

/// <summary>
/// 이동 방향을 대신 정해 주는 두뇌. EnemyMovement가 자기 FixedUpdate 안에서 물어본다.
///
/// 두뇌가 이동에게 "이쪽으로 가라"고 밀어 넣지 않고 이동이 물어보는 구조인 이유 —
/// Unity는 컴포넌트 간 FixedUpdate 순서를 보장하지 않는다. 밀어 넣는 방식이면
/// 어떤 프레임에는 한 틱 늦은 방향으로 움직여 적이 미세하게 떨린다.
/// </summary>
public interface IEnemySteering
{
    /// <summary>
    /// 이번 물리 틱의 이동 방향을 돌려준다.
    /// false를 돌려주면 EnemyMovement가 기본 추격(직선)으로 돌아간다.
    /// </summary>
    bool TryGetSteering(Vector3 toTarget, float distance,
                        out Vector3 direction, out float speedScale);
}
