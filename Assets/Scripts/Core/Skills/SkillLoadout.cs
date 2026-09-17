using System.Collections.Generic;

/// <summary>
/// 적재(Loadout) — 도감에서 골라 이번 레이드에 가져가는 스킬 목록. (v5 §1-2)
///
/// MonoBehaviour에 의존하지 않는 순수 클래스다. EditMode 테스트 대상이다.
///
/// 왜 제한이 필요한가:
/// 제한이 없으면 후반부에 60개 풀에서 3장을 뽑게 되어 원하는 빌드가 만들어지지 않는다.
/// 도감을 채울수록 빌드 설계가 어려워지는 역설이 생긴다. 완화 금지. (10-7)
/// </summary>
public class SkillLoadout
{
    /// <summary>적재 슬롯 초기값. (v8 §11)</summary>
    public const int MinSlotCapacity = 6;

    /// <summary>
    /// 적재 슬롯 최대값. 추출 성공 누적으로 확장된다.
    ///
    /// 11의 근거 — 한 런에서 실제 작동 가능한 최대치가
    /// Core 2 + Support 6(소켓 3×2) + Meta 2 + 전령 1 = 11이다.
    /// 11을 넘으면 가져가도 쓸 수 없는 스킬이 생겨 슬롯 확장이 무의미해진다.
    /// </summary>
    public const int MaxSlotCapacity = 11;

    private readonly List<SkillDefinition> entries = new();

    private int slotCapacity = MinSlotCapacity;

    public SkillLoadout(int capacity = MinSlotCapacity)
    {
        SlotCapacity = capacity;
    }

    /// <summary>현재 슬롯 수. 8~14로 강제된다.</summary>
    public int SlotCapacity
    {
        get => slotCapacity;
        set
        {
            int clamped = value < MinSlotCapacity ? MinSlotCapacity
                        : value > MaxSlotCapacity ? MaxSlotCapacity
                        : value;

            slotCapacity = clamped;

            // 슬롯이 줄어들면 초과분을 뒤에서부터 덜어낸다.
            while (entries.Count > slotCapacity)
                entries.RemoveAt(entries.Count - 1);
        }
    }

    public IReadOnlyList<SkillDefinition> Entries => entries;

    public int Count => entries.Count;

    public int FreeSlots => slotCapacity - entries.Count;

    /// <summary>Core를 하나라도 포함하는지. 없으면 공격 수단이 없다. (v5 §11-1)</summary>
    public bool HasCore
    {
        get
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Category == SkillCategory.Core)
                    return true;
            }

            return false;
        }
    }

    /// <summary>레이드에 진입할 수 있는 구성인지.</summary>
    public bool IsValid => entries.Count > 0 && HasCore;

    /// <summary>유저에게 보여줄 검증 실패 사유. 유효하면 빈 문자열.</summary>
    public string ValidationMessage
    {
        get
        {
            if (entries.Count == 0)
                return "적재가 비어 있습니다.";

            if (!HasCore)
                return "핵심 스킬(Core)를 최소 1개 포함해야 합니다.";

            return string.Empty;
        }
    }

    public bool Contains(SkillDefinition definition)
    {
        return definition != null && entries.Contains(definition);
    }

    /// <summary>적재에 추가한다. 중복이거나 슬롯이 없으면 false.</summary>
    public bool TryAdd(SkillDefinition definition)
    {
        if (definition == null)
            return false;

        if (entries.Contains(definition))
            return false;

        if (entries.Count >= slotCapacity)
            return false;

        entries.Add(definition);

        return true;
    }

    public bool Remove(SkillDefinition definition)
    {
        return definition != null && entries.Remove(definition);
    }

    public void Clear()
    {
        entries.Clear();
    }
}
