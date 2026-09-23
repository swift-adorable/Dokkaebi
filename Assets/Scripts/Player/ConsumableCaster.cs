using System;
using UnityEngine;

/// <summary>
/// 소모품을 쓰는 동안의 시간을 잰다. (docs/Blob_Consumable_System.md 6절)
///
/// 【시전 시간이 회복의 균형추다.】 전투 중에 마실 수 없어야
/// 「빠질까 버틸까」가 생긴다. 즉시 회복이면 체력은 그냥 자원이 되고,
/// 아무 때나 채우면 되므로 교전을 끊을 이유가 없어진다.
///
/// 【중단 조건을 둘로 둔다 — 움직임과 피격.】
/// 움직임만 보면 가만히 서서 총을 맞으며 대형 구급상자를 다 쓸 수 있다.
/// 피격만 보면 쏘면서 걸어 다니며 회복할 수 있다. 둘 다 막아야
/// 「안전한 자리를 먼저 찾는다」가 행동이 된다.
///
/// 중단되면 **아무것도 닳지 않는다.** 시간만 잃는다 —
/// 실패에 아이템까지 잃으면 전투 중에는 아예 시도하지 않게 된다.
/// </summary>
public class ConsumableCaster : MonoBehaviour
{
    private static ConsumableCaster instance;

    /// <summary>지금 쓰고 있는 칸. null이면 쉬는 중이다.</summary>
    private ItemStack stack;

    private float elapsed;
    private float duration;

    private PlayerMovement movement;
    private Health health;

    private int healthAtStart;

    /// <summary>끝났을 때(성공·중단 모두) 결과 문장을 받는 곳.</summary>
    private Action<string> report;

    public static bool HasInstance => instance != null;

    /// <summary>쓰는 중인가. 화면이 진행 막대를 그릴지 정할 때 본다.</summary>
    public static bool IsCasting => instance != null && instance.stack != null;

    /// <summary>0~1. 쓰는 중이 아니면 0이다.</summary>
    public static float Progress =>
        IsCasting && instance.duration > 0f
            ? Mathf.Clamp01(instance.elapsed / instance.duration)
            : 0f;

    /// <summary>지금 쓰고 있는 물건의 이름. 막대 옆에 적는다.</summary>
    public static string CastingName =>
        IsCasting ? instance.stack.Definition.DisplayName : string.Empty;

    public static ConsumableCaster EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<ConsumableCaster>(FindObjectsInactive.Include);

        if (instance != null)
            return instance;

        instance = new GameObject("ConsumableCaster (Runtime)").AddComponent<ConsumableCaster>();

        return instance;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    /// <summary>
    /// 쓰기 시작한다. 시전 시간이 0이면 그 자리에서 끝낸다.
    ///
    /// 【이미 쓰는 중이면 새 것으로 바꾸지 않는다.】 두 번 누르면 앞의 것이
    /// 버려지는데, 급할 때 연타하는 것이 사람의 습관이라 그 손실이 크다.
    /// </summary>
    public void Begin(ItemStack target, Action<string> onDone)
    {
        if (target?.Definition == null)
        {
            onDone?.Invoke("쓸 물건이 없습니다.");
            return;
        }

        if (stack != null)
        {
            onDone?.Invoke($"「{stack.Definition.DisplayName}」를 쓰는 중입니다.");
            return;
        }

        float seconds = target.Definition.Consumable?.CastSeconds ?? 0f;

        if (seconds <= 0f)
        {
            onDone?.Invoke(PlayerConsumables.Use(target));
            return;
        }

        // 쓸 수 없는 물건이면 시간을 끌 이유가 없다. 먼저 판정한다.
        string blocked = PlayerConsumables.Blocked(target);

        if (blocked != null)
        {
            onDone?.Invoke(blocked);
            return;
        }

        stack = target;
        duration = seconds;
        elapsed = 0f;
        report = onDone;

        movement = FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Exclude);
        health = FindPlayerHealth();
        healthAtStart = health != null ? health.Current : 0;
    }

    /// <summary>바깥에서 끊는다. 화면이 닫히거나 죽었을 때.</summary>
    public static void Cancel(string reason)
    {
        if (!IsCasting)
            return;

        instance.Finish(reason);
    }

    private void Update()
    {
        if (stack == null)
            return;

        // 가방에서 사라졌다 — 버렸거나 창고로 옮겼다.
        if (stack.IsEmpty)
        {
            Finish("물건이 없어져 중단했습니다.");
            return;
        }

        if (health == null || health.IsDead)
        {
            Finish("중단했습니다.");
            return;
        }

        if (health.Current < healthAtStart)
        {
            Finish($"맞아서 중단했습니다 — {CastingName}은 그대로 남아 있습니다.");
            return;
        }

        if (movement != null && movement.MoveDirection.sqrMagnitude > 0.0001f)
        {
            Finish($"움직여서 중단했습니다 — {CastingName}은 그대로 남아 있습니다.");
            return;
        }

        elapsed += Time.deltaTime;

        if (elapsed < duration)
            return;

        ItemStack done = stack;

        // 먼저 비워야 Use 안에서 다시 들어오는 일이 없다.
        ItemStack target = done;
        Action<string> callback = report;

        stack = null;
        report = null;

        callback?.Invoke(PlayerConsumables.Use(target));
    }

    private void Finish(string message)
    {
        Action<string> callback = report;

        stack = null;
        report = null;

        callback?.Invoke(message);
    }

    private static Health FindPlayerHealth()
    {
        var player = FindAnyObjectByType<BlobController>(FindObjectsInactive.Exclude);

        return player != null ? player.GetComponent<Health>() : null;
    }
}
