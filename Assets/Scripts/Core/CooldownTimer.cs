using UnityEngine;

/// <summary>
/// 재사용 대기시간을 관리하는 순수 값 타입.
///
/// MonoBehaviour에 의존하지 않으므로 단위 테스트가 쉽고,
/// 사격 연사 제한과 대시 쿨다운처럼 같은 패턴이 반복되는 곳에서 재사용한다.
/// 시간 값을 인자로 받기 때문에 Time.time / Time.unscaledTime 중 무엇을 쓸지
/// 호출부가 선택할 수 있다.
/// </summary>
[System.Serializable]
public struct CooldownTimer
{
    private float nextReadyTime;

    /// <summary>
    /// 한 번이라도 소비된 적이 있는지.
    ///
    /// struct의 기본값은 nextReadyTime = 0이므로, 이 플래그가 없으면
    /// '아직 쓴 적 없는 타이머'와 '0초에 소비된 타이머'를 구분할 수 없다.
    /// 그 경우 기준 시각이 0보다 작으면 새 타이머가 사용 불가로 판정된다.
    /// </summary>
    private bool hasBeenConsumed;

    /// <summary>지금 사용 가능한지. 한 번도 쓰지 않은 타이머는 항상 사용 가능하다.</summary>
    public bool IsReady(float now) => !hasBeenConsumed || now >= nextReadyTime;

    /// <summary>남은 대기 시간. 사용 가능하면 0.</summary>
    public float RemainingTime(float now)
        => hasBeenConsumed ? Mathf.Max(0f, nextReadyTime - now) : 0f;

    /// <summary>
    /// 사용 가능하면 대기시간을 걸고 true를 반환한다. 불가능하면 아무것도 하지 않고 false.
    /// </summary>
    public bool TryConsume(float now, float cooldown)
    {
        if (!IsReady(now))
            return false;

        // 음수 쿨다운은 '즉시 재사용 가능'으로 취급한다.
        nextReadyTime = now + Mathf.Max(0f, cooldown);
        hasBeenConsumed = true;

        return true;
    }

    /// <summary>대기시간을 즉시 해제한다.</summary>
    public void Reset()
    {
        nextReadyTime = 0f;
        hasBeenConsumed = false;
    }
}
