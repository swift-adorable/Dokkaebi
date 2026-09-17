using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상태이상 도트를 한곳에서 돌리는 시스템.
///
/// 왜 Health마다 Update를 두지 않는가:
/// 적이 60마리면 Update 콜백이 60개 생기고, 그중 대부분은 상태이상이 없다.
/// 여기서는 【상태가 걸린 대상만】 목록에 들어오므로 보통 몇 개만 돈다.
/// 피해 요청 버퍼도 하나를 재사용해 매 프레임 GC Alloc이 0이다.
///
/// 씬 배치에 의존하지 않는다. EnsureInstance로 필요할 때 생긴다.
/// </summary>
public class StatusEffectSystem : Singleton<StatusEffectSystem>
{
    private readonly List<Health> tracked = new(64);
    private readonly List<DamageRequest> buffer = new(8);

    /// <summary>현재 상태이상을 추적 중인 대상 수. 디버그·테스트용.</summary>
    public int TrackedCount => tracked.Count;

    /// <summary>인스턴스를 보장한다. 씬 배치를 강제하지 않는다.</summary>
    public static StatusEffectSystem EnsureInstance()
    {
        if (HasInstance)
            return Instance;

        var existing = FindAnyObjectByType<StatusEffectSystem>(FindObjectsInactive.Include);

        if (existing != null)
            return existing;

        return new GameObject("StatusEffectSystem (Runtime)").AddComponent<StatusEffectSystem>();
    }

    /// <summary>
    /// 상태가 걸린 대상을 등록한다. 이미 있으면 무시한다.
    /// Health가 상태를 받을 때 스스로 호출한다.
    /// </summary>
    public void Track(Health target)
    {
        if (target == null || tracked.Contains(target))
            return;

        tracked.Add(target);
    }

    public void Untrack(Health target)
    {
        if (target != null)
            tracked.Remove(target);
    }

    private void Update()
    {
        float delta = Time.deltaTime;

        if (delta <= 0f)
            return;

        // 뒤에서부터 도는 이유 — 도트로 죽거나 상태가 끝난 대상을 그 자리에서 뺄 수 있다.
        for (int i = tracked.Count - 1; i >= 0; i--)
        {
            Health target = tracked[i];

            if (target == null || target.IsDead || !target.Status.HasAny)
            {
                tracked.RemoveAt(i);
                continue;
            }

            target.TickStatus(delta, buffer);
        }
    }

    /// <summary>씬 전환·런 종료 시 전부 비운다.</summary>
    public void Clear()
    {
        tracked.Clear();
    }
}
