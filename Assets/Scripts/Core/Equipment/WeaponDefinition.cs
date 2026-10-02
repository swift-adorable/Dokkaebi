using UnityEngine;

/// <summary>
/// 무기 정의. 장비의 한 종류이므로 EquipmentDefinition을 상속한다.
///
/// 【무기는 기본값만 정한다】 — 속성도 투사체 행동도 붙이지 않는다.
/// 속성은 핵심 젬, 궤도는 보조 젬의 몫이다. (전투 3층 구조)
/// (docs/Dokkaebi_Equipment_System.md 2절 / Dokkaebi_Skill_System.md 1절)
///
/// 방어 관통을 여기에 필드로 두지 않고 stats의 ArmourPenetration으로 두는 이유 —
/// 부착물과 각인도 같은 축을 건드리므로 합산 경로가 하나여야 한다.
/// </summary>
[CreateAssetMenu(fileName = "Weapon", menuName = "Dokkaebi/Weapon Definition")]
public class WeaponDefinition : EquipmentDefinition
{
    [Header("Weapon Base")]
    [Tooltip("기본 피해. 모든 피해 계산의 시작점이다. (티어 1 = 10)")]
    [Min(1f)]
    [SerializeField] private float baseDamage = CombatConstants.BaseWeaponDamage;

    [Tooltip("발사 간격(초). 작을수록 연사가 빠르다. (티어 1 = 0.40)")]
    [Min(0.01f)]
    [SerializeField] private float fireInterval = CombatConstants.BaseFireInterval;

    [Tooltip("유효 사거리(m). 절반 이내는 ×1.0, 절반 초과는 ×0.5, 초과하면 소멸.")]
    [Min(1f)]
    [SerializeField] private float effectiveRange = CombatConstants.BaseEffectiveRange;

    [Tooltip("투사체 속도(m/s).")]
    [Min(1f)]
    [SerializeField] private float projectileSpeed = 20f;

    [Header("Attachments")]
    [Tooltip("부착 슬롯 수(0~6). 티어를 가르는 축이다.")]
    [Range(0, 6)]
    [SerializeField] private int attachmentSlots = 0;

    [Header("Identity")]
    [Tooltip("계열 이름. 급조 / 전기 / 화학 / 중화기 / 정밀 / 종결")]
    [SerializeField] private string weaponFamily = string.Empty;

    public float BaseDamage => baseDamage;
    public float FireInterval => fireInterval;
    public float EffectiveRange => effectiveRange;
    public float ProjectileSpeed => projectileSpeed;
    public int AttachmentSlots => attachmentSlots;
    public string WeaponFamily => weaponFamily;

    /// <summary>초당 피해. 보정 없는 기본값이다. 티어 비교에 쓴다.</summary>
    public float BaseDps => baseDamage / Mathf.Max(0.01f, fireInterval);
}
