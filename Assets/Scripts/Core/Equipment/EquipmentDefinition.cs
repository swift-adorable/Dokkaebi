using System;
using UnityEngine;

/// <summary>옵션 하나. (종류, 값) 쌍이다.</summary>
[Serializable]
public struct EquipmentStat
{
    public EquipmentStatType type;
    public float value;

    public EquipmentStat(EquipmentStatType type, float value)
    {
        this.type = type;
        this.value = value;
    }
}

/// <summary>
/// 착용 장비 정의. ItemDefinition을 상속해 같은 인벤토리에 들어간다.
///
/// 상속으로 둔 이유 — 인자 · 장비 · 전리품이 한 가방을 공유해야
/// "화력을 챙길까, 전리품 공간을 남길까"라는 결정이 성립한다.
/// (docs/Blob_Equipment_System.md 1절)
/// </summary>
[CreateAssetMenu(fileName = "Equipment", menuName = "Blob/Equipment Definition")]
public class EquipmentDefinition : ItemDefinition
{
    [Header("Equipment")]
    [Tooltip("들어갈 슬롯.")]
    [SerializeField] private EquipmentSlot slot = EquipmentSlot.Head;

    [Tooltip("옵션 목록. 음수 옵션이 곧 대가다.")]
    [SerializeField] private EquipmentStat[] stats = new EquipmentStat[0];

    [Header("Set")]
    [Tooltip("세트 계열 이름. 비어 있으면 세트에 속하지 않는다.")]
    [SerializeField] private string setFamily = string.Empty;

    [Header("Imprint")]
    [Tooltip("각인 계열. 같은 계열 + 같은 티어는 중복 장착할 수 없다.")]
    [SerializeField] private string imprintFamily = string.Empty;

    [Tooltip("각인이 주는 상태이상 면역. 저항형 II 이상에만 있다.")]
    [SerializeField] private StatusEffectType immunity = StatusEffectType.None;

    public EquipmentSlot Slot => slot;
    public EquipmentStat[] Stats => stats;
    public string SetFamily => setFamily;
    public string ImprintFamily => imprintFamily;
    public StatusEffectType Immunity => immunity;

    /// <summary>이 장비가 가진 옵션 값. 없으면 0.</summary>
    public float GetStat(EquipmentStatType type)
    {
        float total = 0f;

        for (int i = 0; i < stats.Length; i++)
        {
            if (stats[i].type == type)
                total += stats[i].value;
        }

        return total;
    }

    /// <summary>음수 옵션을 하나라도 가졌는지. 「강한 장비에는 대가가 붙는다」 검증에 쓴다.</summary>
    public bool HasDrawback
    {
        get
        {
            for (int i = 0; i < stats.Length; i++)
            {
                if (stats[i].value < 0f)
                    return true;
            }

            return false;
        }
    }
}
