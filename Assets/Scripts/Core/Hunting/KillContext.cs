/// <summary>
/// 처치 순간의 사정. (조건부 드롭은 결정 2-127로 뺐다 — 남은 쓰임: 누가 쓰러뜨렸나 · 기록)
/// </summary>
public struct KillContext
{
    /// <summary>마지막 타격이 치명타였는가.</summary>
    public bool killedByCritical;

    /// <summary>죽는 순간 동결 상태였는가.</summary>
    public bool frozenAtDeath;

    /// <summary>
    /// 【살아 있는 동안 한 번이라도】 점화된 적이 있는가.
    ///
    /// 죽는 순간만 보면 안 된다 — 불을 붙였다가 꺼진 뒤에 죽여도
    /// 「점화를 걸지 않고」가 되어 버린다. 조건이 거짓말이 된다.
    /// </summary>
    public bool everIgnited;

    /// <summary>플레이어가 쓰러뜨렸는가 — 마지막으로 때린 쪽이 플레이어 (결정 2-93 · 전리품 표시).</summary>
    public bool killedByPlayer;
}
