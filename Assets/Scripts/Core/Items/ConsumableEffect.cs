using UnityEngine;

/// <summary>
/// 소모품 하나가 하는 일. ItemDefinition이 들고 있는 값 묶음이다.
/// (docs/Blob_Consumable_System.md 8절)
///
/// 【전용 효과 필드를 계속 늘리지 않는다.】 강화·방호의 「이동 +25%」 같은
/// 것은 상태이상으로 표현하기로 이미 정해 두었다(8절). 그런데 지금
/// StatusEffectType에는 해로운 상태 9종뿐이라 이로운 상태를 담을 자리가
/// 없다. 그 축을 새로 여는 것은 상태이상 표·HUD 색·위험 판정을 전부
/// 건드리는 일이므로 따로 한다.
///
/// 그래서 이번에 담는 것은 **새 축이 필요 없는 셋**이다 —
/// 회복(체력) · 해제(기존 상태 제거) · 음료와 음식(수분·에너지).
/// 강화와 방호는 분류만 있고 값은 비어 있다.
/// </summary>
[System.Serializable]
public class ConsumableEffect
{
    [Tooltip("분류. 같은 분류는 덮어쓴다는 규칙이 여기 걸린다.")]
    [SerializeField] private ConsumableCategory category = ConsumableCategory.Restore;

    [Min(0)]
    [Tooltip("회복하는 체력. 부식 중이면 실제로는 절반만 들어간다.")]
    [SerializeField] private int heal = 0;

    [Min(0f)]
    [Tooltip("채우는 수분.")]
    [SerializeField] private float water = 0f;

    [Min(0f)]
    [Tooltip("채우는 에너지.")]
    [SerializeField] private float energy = 0f;

    [Min(0f)]
    [Tooltip("대가로 태우는 수분. 덕코프의 주사약은 거의 전부 여기를 쓴다.")]
    [SerializeField] private float waterCost = 0f;

    [Min(0f)]
    [Tooltip("대가로 태우는 에너지.")]
    [SerializeField] private float energyCost = 0f;

    [Tooltip("푸는 상태이상 하나. None이면 해제 효과가 없다.")]
    [SerializeField] private StatusEffectType cure = StatusEffectType.None;

    public ConsumableCategory Category => category;

    public int Heal => Mathf.Max(0, heal);
    public float Water => Mathf.Max(0f, water);
    public float Energy => Mathf.Max(0f, energy);
    public float WaterCost => Mathf.Max(0f, waterCost);
    public float EnergyCost => Mathf.Max(0f, energyCost);
    public StatusEffectType Cure => cure;

    /// <summary>해제 효과가 있는가.</summary>
    public bool Cures => cure != StatusEffectType.None;

    /// <summary>
    /// 아무 값도 없는 껍데기인가.
    ///
    /// 강화·방호는 아직 담을 축이 없어 전부 여기에 걸린다.
    /// 「사용」 줄을 띄우기 전에 이것으로 걸러 낸다 — 눌러도 아무 일이
    /// 없는 버튼은 고장 난 것과 구분되지 않는다.
    /// </summary>
    public bool IsEmpty => Heal == 0 && Water <= 0f && Energy <= 0f && !Cures;
}
