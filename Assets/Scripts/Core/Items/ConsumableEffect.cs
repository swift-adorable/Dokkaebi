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

    [Min(1)]
    [Tooltip("몇 중첩을 덜어 내는가. 덕코프의 「출혈 2중첩 제거」가 이것이다.")]
    [SerializeField] private int cureStacks = 99;

    [Min(0)]
    [Tooltip("한 번 쓸 때 닳는 내구도. 0이면 한 번 쓰고 사라지는 물건이다.")]
    [SerializeField] private int useCost = 0;

    [Min(0f)]
    [Tooltip("쓰는 데 걸리는 시간(초). 움직이거나 맞으면 중단된다. 0이면 즉시.")]
    [SerializeField] private float castSeconds = 0f;

    [Tooltip("거는 이로운 상태. 지속시간은 상태이상 표가 정한다.")]
    [SerializeField] private StatusEffectType grant = StatusEffectType.None;

    public ConsumableCategory Category => category;

    public int Heal => Mathf.Max(0, heal);
    public float Water => Mathf.Max(0f, water);
    public float Energy => Mathf.Max(0f, energy);
    public float WaterCost => Mathf.Max(0f, waterCost);
    public float EnergyCost => Mathf.Max(0f, energyCost);
    public StatusEffectType Cure => cure;

    /// <summary>덜어 내는 중첩 수. 크게 두면 사실상 전부 제거다.</summary>
    public int CureStacks => Mathf.Max(1, cureStacks);

    /// <summary>
    /// 한 번 쓸 때 닳는 내구도. 0이면 아이템 하나가 통째로 사라진다.
    ///
    /// 【덕코프의 구급상자는 여러 번 쓰는 물건이다.】 소형 125/25 = 5회,
    /// 구급상자 175/25 = 7회, 대형 400/40 = 10회. [확인됨 — 아이템 #15~#17]
    /// 한 번에 다 채우는 물건이 아니라 조금씩 여러 번 쓰는 물건이라,
    /// 「지금 쓸까 아꼈다 쓸까」가 전투 중에 계속 생긴다.
    /// </summary>
    public int UseCost => Mathf.Max(0, useCost);

    /// <summary>내구도를 깎아 쓰는 물건인가.</summary>
    public bool Charged => UseCost > 0;

    /// <summary>
    /// 쓰는 데 걸리는 시간(초).
    ///
    /// 【이것이 회복의 균형추다.】 전투 중에 마실 수 없어야 「빠질까 버틸까」가
    /// 생긴다. 즉시 회복이면 체력은 그냥 자원이고, 아무 때나 채우면 된다.
    ///
    /// 덕코프도 같다 — 나무위키가 구급상자를 두고 「이 등급부터 사용 시간이
    /// 길어져 전투 중에 쓰기 상당히 어렵다」고 적는다. [확인됨]
    /// 다만 **정확한 초는 어디에도 없다.** 아래 값은 우리가 정한 것이다. [불확실]
    /// </summary>
    public float CastSeconds => Mathf.Max(0f, castSeconds);

    /// <summary>
    /// 거는 이로운 상태. None이면 없다.
    ///
    /// 【지속시간을 여기에 두지 않는다.】 상태이상 표(StatusEffectTable)가
    /// 하나의 기준이어야 한다. 소모품마다 제 지속시간을 들고 있으면
    /// 「각성제는 120초인데 표에는 90초」 같은 것이 생긴다.
    /// </summary>
    public StatusEffectType Grant => grant;

    public bool Grants => grant != StatusEffectType.None;

    /// <summary>해제 효과가 있는가.</summary>
    public bool Cures => cure != StatusEffectType.None;

    /// <summary>
    /// 아무 값도 없는 껍데기인가.
    ///
    /// 강화·방호는 아직 담을 축이 없어 전부 여기에 걸린다.
    /// 「사용」 줄을 띄우기 전에 이것으로 걸러 낸다 — 눌러도 아무 일이
    /// 없는 버튼은 고장 난 것과 구분되지 않는다.
    /// </summary>
    public bool IsEmpty => Heal == 0 && Water <= 0f && Energy <= 0f && !Cures && !Grants;
}
