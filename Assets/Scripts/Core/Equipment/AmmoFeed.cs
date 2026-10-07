/// <summary>무기와 탄의 상태 (결정 2-80).</summary>
public enum AmmoFeedState
{
    /// <summary>무기가 없다 — 맨손. 탄을 쓰지 않는다.</summary>
    Unarmed,
    /// <summary>쏠 수 있다.</summary>
    Ready,
    /// <summary>가방에서 통을 채우는 중 — 못 쏜다.</summary>
    Reloading,
    /// <summary>탄이 다 떨어져 무기를 등에 메는 중 — 못 쏜다.</summary>
    Slinging,
    /// <summary>무기를 등에 멨다 — 맨손으로 싸운다.</summary>
    Slung,
    /// <summary>무기를 바꿔 드는 중 — 못 쏜다 (결정 2-81).</summary>
    Switching,
}

public enum AmmoFeedEvent
{
    None,
    ReloadStarted,
    /// <summary>다 채웠다 — 부른 쪽이 가방에서 통으로 옮긴다.</summary>
    ReloadCompleted,
    /// <summary>탄이 떨어져 무기를 멨다 — 알림.</summary>
    Slung,
    /// <summary>가방에 탄이 생겨 무기를 다시 들었다 — 이어서 채운다.</summary>
    Drawn,
}

/// <summary>
/// 화살통 · 탄창 상태 기계 (결정 2-80). 수를 들고 있지 않는다 — 통 · 가방의 수는 부른 쪽이 매번 넘긴다.
///
///   · 통이 비면 가방에서 저절로 채운다 — 채우는 시간(무기 종류마다) 동안 못 쏜다
///   · 가방에도 없으면 무기를 등에 메고(0.5초) 맨손으로 싸운다 + 알림
///   · 멘 동안 가방에 탄이 생기면 다시 들고 채운다
///   · R(데스크톱)로 덜 찬 통을 채울 수 있다
/// </summary>
public sealed class AmmoFeed
{
    /// <summary>무기를 등에 메는 데 걸리는 시간 (초) [임시값].</summary>
    public const float SlingSeconds = 0.5f;

    /// <summary>무기를 바꿔 드는 데 걸리는 시간 (초) [임시값 — 결정 2-81].</summary>
    public const float SwitchSeconds = 0.4f;

    /// <summary>
    /// 무기를 바꿔 든다 — 바꿔 드는 동안 못 쏘고, 다 들면 새 무기의 통을 보고 다시 정한다.
    /// 채우던 것 · 메던 것은 끊긴다.
    /// </summary>
    public void BeginSwitch()
    {
        State = AmmoFeedState.Switching;
        Duration = Remaining = SwitchSeconds;
    }

    public AmmoFeedState State { get; private set; } = AmmoFeedState.Unarmed;
    public float Remaining { get; private set; }
    public float Duration { get; private set; }

    /// <summary>들고 있는 무기로 쏠 수 있는가 (통에 한 발 이상).</summary>
    public bool CanShootWeapon(int loaded) => State == AmmoFeedState.Ready && loaded > 0;

    /// <summary>맨손 공격을 쓰는가 — 무기가 없거나 멨다.</summary>
    public bool UseUnarmed => State == AmmoFeedState.Unarmed || State == AmmoFeedState.Slung;

    /// <summary>채우기 · 메기 진행 (0~1).</summary>
    public float Progress => Duration <= 0f ? 1f : UnityEngine.Mathf.Clamp01(1f - Remaining / Duration);

    public AmmoFeedEvent Tick(bool hasWeapon, int loaded, int capacity, int inBag,
        float reloadSeconds, float deltaTime, bool reloadRequested = false)
    {
        if (!hasWeapon)
        {
            State = AmmoFeedState.Unarmed;
            Remaining = 0f;
            return AmmoFeedEvent.None;
        }

        switch (State)
        {
            case AmmoFeedState.Unarmed:
                // 방금 무기를 들었다.
                if (loaded > 0)
                {
                    State = AmmoFeedState.Ready;
                    return AmmoFeedEvent.None;
                }

                return inBag > 0 ? BeginReload(reloadSeconds) : BeginSling();

            case AmmoFeedState.Ready:
                if (loaded <= 0)
                    return inBag > 0 ? BeginReload(reloadSeconds) : BeginSling();

                if (reloadRequested && loaded < capacity && inBag > 0)
                    return BeginReload(reloadSeconds);

                return AmmoFeedEvent.None;

            case AmmoFeedState.Reloading:
                if (inBag <= 0)
                {
                    // 채우는 사이 가방의 탄이 사라졌다(버렸다 · 팔았다).
                    if (loaded > 0)
                    {
                        State = AmmoFeedState.Ready;
                        return AmmoFeedEvent.None;
                    }

                    return BeginSling();
                }

                Remaining -= UnityEngine.Mathf.Max(0f, deltaTime);
                if (Remaining > 0f)
                    return AmmoFeedEvent.None;

                State = AmmoFeedState.Ready;
                Remaining = 0f;
                return AmmoFeedEvent.ReloadCompleted;

            case AmmoFeedState.Slinging:
                Remaining -= UnityEngine.Mathf.Max(0f, deltaTime);
                if (Remaining > 0f)
                    return AmmoFeedEvent.None;

                State = AmmoFeedState.Slung;
                Remaining = 0f;
                return AmmoFeedEvent.Slung;

            case AmmoFeedState.Switching:
                Remaining -= UnityEngine.Mathf.Max(0f, deltaTime);
                if (Remaining > 0f)
                    return AmmoFeedEvent.None;

                // 다 들었다 — 방금 무기를 든 것처럼 다시 정한다.
                State = AmmoFeedState.Unarmed;
                Remaining = 0f;
                return Tick(true, loaded, capacity, inBag, reloadSeconds, 0f);

            case AmmoFeedState.Slung:
                if (loaded > 0)
                {
                    State = AmmoFeedState.Ready;
                    return AmmoFeedEvent.Drawn;
                }

                if (inBag > 0)
                {
                    BeginReload(reloadSeconds);
                    return AmmoFeedEvent.Drawn;
                }

                return AmmoFeedEvent.None;
        }

        return AmmoFeedEvent.None;
    }

    /// <summary>씬이 바뀌거나 쓰러졌을 때.</summary>
    public void Reset()
    {
        State = AmmoFeedState.Unarmed;
        Remaining = 0f;
        Duration = 0f;
    }

    private AmmoFeedEvent BeginReload(float seconds)
    {
        State = AmmoFeedState.Reloading;
        Duration = Remaining = UnityEngine.Mathf.Max(0.01f, seconds);
        return AmmoFeedEvent.ReloadStarted;
    }

    private AmmoFeedEvent BeginSling()
    {
        State = AmmoFeedState.Slinging;
        Duration = Remaining = SlingSeconds;
        return AmmoFeedEvent.None;
    }
}
