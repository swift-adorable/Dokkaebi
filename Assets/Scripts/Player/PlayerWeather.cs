using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【궂은 날 · 막이】 (결정 2-60 · 2-61 · 2-64 · 2-91) — 이번 판 날씨가 플레이어에게 하는 일. PlayerSurvival이 붙인다.
///
///   · 한파 — 방한이 모자란 단계만큼 추위가 쌓인다(1단계 = 15분에 100). 중첩당 이동 −0.25% · 쏘기 느림 +0.3% ·
///     에너지 +1.3%, 100이면 동상(1.5초마다 1). 화로 · 모닥불(WarmthSource) 곁에서 초당 5씩 녹는다
///   · 흙비 — 막이 1 모자라면 18초마다 1 · 2 모자라면 2.5초마다 1 (6장 꽃비는 해가 없다)
///   · 폭염 · 독안개 — 수분이 ×1.6 / ×2.4로 준다 (서늘함 한 단계마다 한 칸)
///   · 짙은 안개 — 내 시야가 ×0.7 / ×0.45 (밝히기 한 단계마다 한 칸) — FogOverlayUI가 그린다
/// 막이 단계 = 장비 막이 합(반올림) + 막이 소모품(걸려 있으면 +1).
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerWeather : MonoBehaviour
{
    public static PlayerWeather Current { get; private set; }

    /// <summary>플레이어가 없거나 판이 아니면 1.</summary>
    public static float MoveScale => Current != null ? Current.moveScale : 1f;
    public static float FireIntervalScale => Current != null ? Current.fireIntervalScale : 1f;
    public static float WaterDrainScale => Current != null ? Current.waterScale : 1f;
    public static float EnergyDrainScale => Current != null ? Current.energyScale : 1f;
    public static float SightScale => Current != null ? Current.sightScale : 1f;

    private Health health;
    private float coldStacks;
    private float dustTimer;
    private float frostTimer;

    private float moveScale = 1f;
    private float fireIntervalScale = 1f;
    private float waterScale = 1f;
    private float energyScale = 1f;
    private float sightScale = 1f;

    /// <summary>추위 중첩 (0 ~ 100).</summary>
    public float ColdStacks => coldStacks;

    /// <summary>지금 이 판의 막이 단계와 모자란 단계 — HUD가 읽는다.</summary>
    public int Protection { get; private set; }
    public int Deficit { get; private set; }

    private void Awake()
    {
        health = GetComponent<Health>();
        Current = this;
    }

    private void Start()
    {
        // 판 씬이면 날씨 입자 · 덮개를 띄운다 (결정 2-91).
        if (!SceneFlow.InBunker)
            WeatherEffects.Ensure(transform);
    }

    private void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }

    private void Update()
    {
        bool running = !SceneFlow.InBunker && (!GameManager.HasInstance || GameManager.Instance.IsPlaying);
        RaidWeather weather = RaidManager.HasInstance && running ? RaidManager.Current.weather : RaidWeather.Calm;

        Protection = ProtectionLevel(weather.Protection);
        Deficit = weather.Deficit(Protection);

        float dt = Time.deltaTime;
        bool alive = health != null && !health.IsDead;

        // ── 한파 ────────────────────────────────────────────────────
        coldStacks = WeatherTable.ColdStep(coldStacks, alive ? weather.Hazard : WeatherHazard.None,
            Deficit, WarmthSource.IsNear(transform.position), dt);

        if (coldStacks >= WeatherTable.ColdMaxStacks && alive)
        {
            frostTimer += dt;
            while (frostTimer >= WeatherTable.FrostbiteInterval)
            {
                frostTimer -= WeatherTable.FrostbiteInterval;
                health.TakeDamage(1);
            }
        }
        else
        {
            frostTimer = 0f;
        }

        moveScale = 1f - coldStacks * WeatherTable.ColdMovePerStack;
        fireIntervalScale = 1f + coldStacks * WeatherTable.ColdFireIntervalPerStack;
        energyScale = 1f + coldStacks * WeatherTable.ColdEnergyDrainPerStack;

        // ── 흙비 ────────────────────────────────────────────────────
        if (weather.Hazard == WeatherHazard.Dust && Deficit > 0 && alive)
        {
            float interval = WeatherTable.DustInterval(Deficit);
            dustTimer += dt;
            while (dustTimer >= interval)
            {
                dustTimer -= interval;
                health.TakeDamage(1);
            }
        }
        else
        {
            dustTimer = 0f;
        }

        // ── 폭염 · 독안개 ───────────────────────────────────────────
        waterScale = weather.Hazard == WeatherHazard.Heat ? WeatherTable.WaterDrainScale(Deficit) : 1f;

        // ── 짙은 안개 ───────────────────────────────────────────────
        sightScale = weather.Hazard == WeatherHazard.Fog ? WeatherTable.PlayerSightScale(Deficit) : 1f;
    }

    /// <summary>그 막이의 단계 — 장비 합(반올림) + 소모품(걸려 있으면 +1).</summary>
    public int ProtectionLevel(ProtectionKind kind)
    {
        if (kind == ProtectionKind.None)
            return 0;

        float equipped = PlayerInventory.HasInstance
            ? PlayerInventory.Instance.Loadout.Modifiers.Get(StatOf(kind))
            : 0f;

        int fromItem = health != null && health.Status.Has(StatusOf(kind)) ? 1 : 0;
        return Mathf.Max(0, Mathf.RoundToInt(equipped) + fromItem);
    }

    public static EquipmentStatType StatOf(ProtectionKind kind) => kind switch
    {
        ProtectionKind.Warmth => EquipmentStatType.ProtectWarmth,
        ProtectionKind.Shield => EquipmentStatType.ProtectShield,
        ProtectionKind.Cool => EquipmentStatType.ProtectCool,
        ProtectionKind.Light => EquipmentStatType.ProtectLight,
        _ => EquipmentStatType.None,
    };

    public static StatusEffectType StatusOf(ProtectionKind kind) => kind switch
    {
        ProtectionKind.Warmth => StatusEffectType.GuardWarmth,
        ProtectionKind.Shield => StatusEffectType.GuardShield,
        ProtectionKind.Cool => StatusEffectType.GuardCool,
        ProtectionKind.Light => StatusEffectType.GuardLight,
        _ => StatusEffectType.None,
    };
}
