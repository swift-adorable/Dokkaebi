using System;
using UnityEngine;

/// <summary>
/// 수분 · 에너지를 실제로 흘리고 페널티를 물린다.
/// (docs/Dokkaebi_Survival_System.md · 결정 2-32)
///
/// 【계산은 SurvivalState가 한다.】 여기는 시간을 넘겨주고, 나온 피해를
/// Health에 물리고, 이동·회복 배율을 바깥에 연결하는 일만 한다.
///
/// 【판이 돌 때만 준다.】 벙커·가방 화면·일시정지에서는 흐르지 않는다.
/// 「짐을 정리하는 동안 목이 마르는」 것은 압박이 아니라 방해다.
/// (Survival_System 6절 「판 밖에서는 줄지 않는다」)
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerSurvival : MonoBehaviour
{
    private static PlayerSurvival instance;

    private readonly SurvivalState state = new();

    private Health health;
    private PlayerLoadout loadout;

    /// <summary>지난 프레임의 페널티 상태. 바뀐 순간에만 장비 갱신을 부른다.</summary>
    private bool lastDehydrated;
    private bool lastStarving;

    public static bool HasInstance => instance != null;

    public static PlayerSurvival Instance => instance;

    /// <summary>
    /// 플레이어에게 이 컴포넌트를 보장한다. 【씬 배치를 강제하지 않는다.】
    ///
    /// 플레이어가 프리팹이 아니라 씬 오브젝트라서, 씬을 직접 고치면
    /// 씬 파일에 충돌이 생기고 테스트 씬마다 다시 붙여야 한다.
    /// 다른 화면들(InventoryScreenUI · SurvivalHudUI)과 같은 방식이다.
    /// </summary>
    public static PlayerSurvival EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<PlayerSurvival>(FindObjectsInactive.Include);

        if (instance != null)
            return instance;

        var player = FindAnyObjectByType<DokkaebiController>(FindObjectsInactive.Include);

        if (player == null)
            return null;

        instance = player.gameObject.AddComponent<PlayerSurvival>();

        return instance;
    }

    public SurvivalState State => state;

    /// <summary>수분·에너지가 바뀔 때마다. 화면이 구독한다.</summary>
    public event Action OnChanged;

    private void Awake()
    {
        instance = this;

        health = GetComponent<Health>();
        loadout = GetComponent<PlayerLoadout>();

        // 궂은 날 · 막이 (결정 2-91) — 플레이어에 하나.
        if (!TryGetComponent(out PlayerWeather _))
            gameObject.AddComponent<PlayerWeather>();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (!IsRaidRunning())
            return;

        // 과중량 단계가 소모율을 태운다 — 더 들수록 판이 짧아진다.
        float multiplier = SurvivalTable.DrainMultiplier(
            PlayerInventory.EnsureInstance().Encumbrance);

        // 날씨 — 폭염 · 독안개는 수분, 추위는 에너지를 더 태운다 (결정 2-61 · PlayerWeather).
        int damage = state.Tick(Time.deltaTime, multiplier, PlayerWeather.WaterDrainScale, PlayerWeather.EnergyDrainScale);

        if (damage > 0 && health != null && !health.IsDead)
            health.TakeDamage(damage);

        SyncPenalties();

        OnChanged?.Invoke();
    }

    /// <summary>
    /// 지금 판이 돌고 있는가. GameManager가 없으면(테스트 씬 등) 돈다고 본다.
    /// </summary>
    private static bool IsRaidRunning()
        => !SceneFlow.InBunker && (!GameManager.HasInstance || GameManager.Instance.IsPlaying);

    /// <summary>
    /// 이동 배율은 PlayerLoadout이 한꺼번에 적용한다(SpeedScale의 주인이 하나여야 한다).
    /// 그래서 여기서는 【바뀐 순간에만】 다시 계산하라고 알린다.
    /// 매 프레임 Refresh를 부르면 장비 합산을 초당 60번 하게 된다.
    /// </summary>
    private void SyncPenalties()
    {
        if (lastDehydrated == state.IsDehydrated && lastStarving == state.IsStarving)
            return;

        lastDehydrated = state.IsDehydrated;
        lastStarving = state.IsStarving;

        if (loadout != null)
            loadout.Refresh();
    }

    // ── 바깥에서 부르는 입구 ──────────────────────────────────────────

    /// <summary>음료·음식이 채운다. 8단계의 소모품이 쓸 입구다.</summary>
    public void Restore(float water, float energy)
    {
        state.Restore(water, energy);

        SyncPenalties();

        OnChanged?.Invoke();
    }

    /// <summary>강화 소모품의 대가. 「버프를 쓸수록 물이 급해진다」.</summary>
    public void Drain(float water, float energy)
    {
        state.Drain(water, energy);

        SyncPenalties();

        OnChanged?.Invoke();
    }

    /// <summary>파밍할 때 가득 채운다. 판이 시작부터 불리하면 안 된다.</summary>
    public void Refill()
    {
        state.Refill();

        SyncPenalties();

        OnChanged?.Invoke();
    }
}
