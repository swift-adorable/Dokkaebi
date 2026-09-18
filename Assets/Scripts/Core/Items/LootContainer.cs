using UnityEngine;

/// <summary>
/// 전리품 칸. 시체·상자·캐비닛이 전부 이것을 쓴다.
///
/// 【왜 인벤토리(Inventory)와 다른 클래스인가】
/// 가방은 무게와 적재 공간이라는 두 자원으로 제한되고 겹침을 관리한다.
/// 전리품은 그런 것이 없다. 고정된 칸 수에 무엇이 들었는지만 보여 주면 된다.
/// 둘을 한 클래스로 합치면 "전리품 칸에도 무게 제한이 있는가" 같은
/// 답할 필요 없는 질문이 계속 생긴다.
///
/// 【절대 규칙】 가방에 자리가 없으면 아이템은 전리품 칸에 그대로 남는다.
/// 이 클래스는 아이템을 만들지도 없애지도 않는다. 옮길 뿐이다.
/// 인자 소켓(SocketedBuild)과 같은 원칙이다.
///
/// MonoBehaviour 의존이 없는 순수 클래스다. EditMode 테스트 대상.
/// </summary>
public class LootContainer
{
    /// <summary>기본 칸 수. 화면에 「전리품 (3/8)」로 표시되는 그 8이다.</summary>
    public const int DefaultCapacity = 8;

    private readonly ItemStack[] slots;

    public LootContainer(int capacity = DefaultCapacity)
    {
        slots = new ItemStack[Mathf.Max(1, capacity)];
    }

    public int Capacity => slots.Length;

    /// <summary>무언가 들어 있는 칸의 수.</summary>
    public int UsedSlots
    {
        get
        {
            int used = 0;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && !slots[i].IsEmpty)
                    used++;
            }

            return used;
        }
    }

    public bool IsEmpty => UsedSlots == 0;

    public bool IsFull => UsedSlots >= slots.Length;

    /// <summary>해당 칸의 내용. 비었으면 null.</summary>
    public ItemStack Get(int index)
    {
        if (index < 0 || index >= slots.Length)
            return null;

        ItemStack stack = slots[index];

        return stack != null && !stack.IsEmpty ? stack : null;
    }

    /// <summary>첫 번째 빈 칸에 넣는다. 자리가 없으면 false.</summary>
    public bool TryPut(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty)
            return false;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && !slots[i].IsEmpty)
                continue;

            slots[i] = stack;

            return true;
        }

        return false;
    }

    /// <summary>정의로부터 새로 만들어 넣는다.</summary>
    public bool TryPut(ItemDefinition definition, int count = 1, int durability = -1)
    {
        return definition != null
            && count > 0
            && TryPut(new ItemStack(definition, count, durability));
    }

    /// <summary>
    /// 해당 칸을 꺼낸다. 칸이 비고, 꺼낸 개체를 돌려준다.
    /// 호출부가 반드시 어딘가에 넣어야 한다. 버리면 아이템이 사라진다.
    /// </summary>
    public ItemStack Take(int index)
    {
        ItemStack stack = Get(index);

        if (stack == null)
            return null;

        slots[index] = null;

        return stack;
    }

    /// <summary>
    /// 가방으로 옮긴다. 【가방에 자리가 없으면 옮기지 않고 그대로 둔다.】
    /// 부분 적재를 허용하지 않는 이유 — 전리품 한 칸이 반만 옮겨지면
    /// 유저가 무엇을 가져왔는지 화면만 보고 알 수 없다.
    /// </summary>
    public bool TryTakeTo(int index, Inventory bag)
    {
        if (bag == null)
            return false;

        ItemStack stack = Get(index);

        if (stack == null)
            return false;

        if (!bag.TryAddStack(stack))
            return false;

        slots[index] = null;

        return true;
    }

    /// <summary>가능한 만큼 전부 옮긴다. 실제로 옮긴 칸 수를 돌려준다.</summary>
    public int TakeAllTo(Inventory bag)
    {
        if (bag == null)
            return 0;

        int moved = 0;

        for (int i = 0; i < slots.Length; i++)
        {
            if (TryTakeTo(i, bag))
                moved++;
        }

        return moved;
    }

    /// <summary>총 가치. 「이걸 다 들고 가면 얼마인가」를 화면에 보여 줄 때 쓴다.</summary>
    public int TotalValue
    {
        get
        {
            int total = 0;

            for (int i = 0; i < slots.Length; i++)
            {
                ItemStack stack = Get(i);

                if (stack?.Definition != null)
                    total += stack.Definition.BaseValue * stack.Count;
            }

            return total;
        }
    }

    /// <summary>총 무게. 「다 들면 과중량인가」를 미리 보여 줄 때 쓴다.</summary>
    public float TotalWeight
    {
        get
        {
            float total = 0f;

            for (int i = 0; i < slots.Length; i++)
                total += Get(i)?.TotalWeight ?? 0f;

            return total;
        }
    }

    public void Clear()
    {
        for (int i = 0; i < slots.Length; i++)
            slots[i] = null;
    }
}
