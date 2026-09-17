using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 가방. 장비 · 인자 · 전리품이 전부 여기에 들어간다.
///
/// 두 개의 독립된 자원으로 제한된다.
///   적재 공간 — 칸 수.  초반의 병목
///   최대 소지 중량 — kg. 중반 이후의 진짜 병목
///
/// 두 축을 분리한 이유 — 가방마다 배분이 달라서 "이번 출격에 무엇을 노리는가"에 따라
/// 가방 선택이 갈린다. 고가 중량물이냐, 소형 재료 다수냐.
/// (docs/Blob_Equipment_System.md 5-1절)
///
/// MonoBehaviour 의존이 없는 순수 클래스다. EditMode 테스트 대상.
/// </summary>
public class Inventory
{
    private readonly List<ItemStack> stacks = new();

    private int slotCapacity;
    private float weightLimit;

    public Inventory(int slots, float weight)
    {
        slotCapacity = Mathf.Max(0, slots);
        weightLimit = Mathf.Max(0f, weight);
    }

    public IReadOnlyList<ItemStack> Stacks => stacks;

    /// <summary>적재 공간(칸 수).</summary>
    public int SlotCapacity
    {
        get => slotCapacity;
        set => slotCapacity = Mathf.Max(0, value);
    }

    /// <summary>최대 소지 중량(kg).</summary>
    public float WeightLimit
    {
        get => weightLimit;
        set => weightLimit = Mathf.Max(0f, value);
    }

    /// <summary>사용 중인 칸 수.</summary>
    public int UsedSlots
    {
        get
        {
            int used = 0;

            for (int i = 0; i < stacks.Count; i++)
                used += stacks[i].TotalSlots;

            return used;
        }
    }

    public int FreeSlots => Mathf.Max(0, slotCapacity - UsedSlots);

    /// <summary>현재 총 무게(kg).</summary>
    public float TotalWeight
    {
        get
        {
            float total = 0f;

            for (int i = 0; i < stacks.Count; i++)
                total += stacks[i].TotalWeight;

            return total;
        }
    }

    /// <summary>
    /// 과중량 단계.
    ///
    /// 무게는 상한을 넘어도 【담을 수 있다】. 넘으면 느려질 뿐이다.
    /// 덕코프도 그렇다 — 무거운 걸 주웠을 때 "못 줍는다"가 아니라
    /// "느려지지만 들고 갈 수는 있다"여야 추출 판단이 생긴다.
    /// </summary>
    public EncumbranceLevel Encumbrance => WeightCalculator.Evaluate(TotalWeight, weightLimit);

    public bool IsOverweight => Encumbrance != EncumbranceLevel.Normal;

    /// <summary>
    /// 담을 수 있는지. 칸 수만 본다. 무게는 막지 않는다.
    ///
    /// 겹칠 수 있는 아이템은 기존 칸에 들어가므로 칸을 쓰지 않을 수 있다.
    /// </summary>
    public bool CanAdd(ItemDefinition definition, int count = 1)
    {
        if (definition == null || count <= 0)
            return false;

        int remaining = count;

        // 먼저 기존 칸의 여유부터 센다.
        if (definition.IsStackable)
        {
            for (int i = 0; i < stacks.Count && remaining > 0; i++)
            {
                if (stacks[i].Definition == definition)
                    remaining -= stacks[i].FreeSpace;
            }
        }

        if (remaining <= 0)
            return true;

        int newStacks = Mathf.CeilToInt(remaining / (float)definition.StackMax);

        return newStacks * definition.SlotSize <= FreeSlots;
    }

    /// <summary>담는다. 실제로 담긴 개수를 반환한다. 부분 적재를 허용한다.</summary>
    public int TryAdd(ItemDefinition definition, int count = 1, int durability = -1)
    {
        if (definition == null || count <= 0)
            return 0;

        int remaining = count;

        // 1) 기존 칸에 겹친다.
        if (definition.IsStackable)
        {
            for (int i = 0; i < stacks.Count && remaining > 0; i++)
            {
                ItemStack stack = stacks[i];

                if (stack.Definition != definition || stack.FreeSpace <= 0)
                    continue;

                var incoming = new ItemStack(definition, Mathf.Min(remaining, definition.StackMax));

                remaining -= stack.Merge(incoming);
            }
        }

        // 2) 남은 것은 새 칸에 담는다.
        while (remaining > 0)
        {
            if (FreeSlots < definition.SlotSize)
                break;

            int amount = Mathf.Min(remaining, definition.StackMax);

            stacks.Add(new ItemStack(definition, amount, durability));

            remaining -= amount;
        }

        return count - remaining;
    }

    /// <summary>담는다. 이미 만들어진 개체(내구도 유지)를 그대로 넣는다.</summary>
    public bool TryAddStack(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty)
            return false;

        if (FreeSlots < stack.TotalSlots)
            return false;

        stacks.Add(stack);

        return true;
    }

    /// <summary>뺀다. 실제로 뺀 개수를 반환한다.</summary>
    public int Remove(ItemDefinition definition, int count = 1)
    {
        if (definition == null || count <= 0)
            return 0;

        int removed = 0;

        for (int i = stacks.Count - 1; i >= 0 && removed < count; i--)
        {
            ItemStack stack = stacks[i];

            if (stack.Definition != definition)
                continue;

            removed += stack.Take(count - removed);

            if (stack.IsEmpty)
                stacks.RemoveAt(i);
        }

        return removed;
    }

    /// <summary>특정 개체를 뺀다. 소켓에 끼울 때 쓴다.</summary>
    public bool RemoveStack(ItemStack stack)
    {
        return stack != null && stacks.Remove(stack);
    }

    public int CountOf(ItemDefinition definition)
    {
        if (definition == null)
            return 0;

        int total = 0;

        for (int i = 0; i < stacks.Count; i++)
        {
            if (stacks[i].Definition == definition)
                total += stacks[i].Count;
        }

        return total;
    }

    public bool Contains(ItemDefinition definition) => CountOf(definition) > 0;

    /// <summary>
    /// 추출에 실패했을 때 잃는 것을 비운다.
    ///
    /// 각인만 남는다. 유저가 배울 규칙은 하나여야 하므로 예외를 늘리지 않는다.
    /// 인자도 장비와 똑같이 잃는다. (docs/Blob_Progression_System.md 6절)
    /// </summary>
    public int DropOnDeath()
    {
        int lost = 0;

        for (int i = stacks.Count - 1; i >= 0; i--)
        {
            if (stacks[i].Definition != null && stacks[i].Definition.SurvivesDeath)
                continue;

            lost += stacks[i].Count;
            stacks.RemoveAt(i);
        }

        return lost;
    }

    public void Clear() => stacks.Clear();

    /// <summary>총 가치. 손익 결산 UI가 쓴다. (Master_Prompt 8-1절)</summary>
    public int TotalValue
    {
        get
        {
            int total = 0;

            for (int i = 0; i < stacks.Count; i++)
            {
                if (stacks[i].Definition != null)
                    total += stacks[i].Definition.BaseValue * stacks[i].Count;
            }

            return total;
        }
    }
}
