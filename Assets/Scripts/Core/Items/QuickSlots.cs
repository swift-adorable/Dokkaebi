using System.Collections.Generic;

/// <summary>
/// 퀵슬롯 8칸 — 화면 하단에 늘 떠 있는 줄이 읽는 자리.
///
/// 【칸이 아이템을 「가지고」 있지 않다.】
/// 가방에 있는 것을 가리키기만 한다. 그래서 퀵슬롯에 넣어도 가방 칸이
/// 줄지 않고, 무게도 두 번 세지 않는다. 가방에서 사라진 것(다 쓰거나
/// 버리거나 창고에 넣은 것)은 Prune이 조용히 지운다.
///
/// MonoBehaviour 의존이 없는 순수 클래스다. EditMode 테스트 대상.
/// </summary>
public class QuickSlots
{
    public const int Count = 8;

    /// <summary>
    /// 1 · 2번은 무기 두 자루 자리다 (결정 2-81 — 덕코프처럼). 물건은 3 ~ 8번(0부터 세어 2 ~ 7)에만 건다.
    /// </summary>
    public const int FirstItemSlot = 2;

    public static bool IsItemSlot(int index) => index >= FirstItemSlot && index < Count;

    /// <summary>비어 있음.</summary>
    public const int None = -1;

    private readonly ItemStack[] slots = new ItemStack[Count];

    /// <summary>이 칸에 걸린 것. 비었으면 null.</summary>
    public ItemStack Get(int index)
    {
        return index < 0 || index >= Count ? null : slots[index];
    }

    /// <summary>이 물건이 걸린 칸 번호. 없으면 None.</summary>
    public int IndexOf(ItemStack stack)
    {
        if (stack == null)
            return None;

        for (int i = 0; i < Count; i++)
        {
            if (ReferenceEquals(slots[i], stack))
                return i;
        }

        return None;
    }

    /// <summary>
    /// 이 칸에 건다.
    ///
    /// 【같은 물건이 두 칸에 걸리지 않게 한다.】
    /// 1번과 5번에 같은 주사약이 걸려 있으면, 하나를 다 썼을 때
    /// 남은 칸이 없는 것을 가리키게 된다. 옮기는 것으로 처리한다.
    /// </summary>
    public void Assign(int index, ItemStack stack)
    {
        if (!IsItemSlot(index))
            return;

        if (stack == null)
        {
            slots[index] = null;
            return;
        }

        int previous = IndexOf(stack);

        if (previous == index)
        {
            // 같은 칸을 다시 고르면 뺀다 — 넣기와 빼기가 같은 동작이다.
            slots[index] = null;
            return;
        }

        if (previous != None)
            slots[previous] = null;

        slots[index] = stack;
    }

    public void Clear(int index)
    {
        if (index >= 0 && index < Count)
            slots[index] = null;
    }

    public void ClearAll()
    {
        for (int i = 0; i < Count; i++)
            slots[i] = null;
    }

    /// <summary>
    /// 가방에 없는 것을 지운다. 다시 그리기 전에 부른다.
    ///
    /// 【가리키는 것이 사라졌는지 여기서만 판단한다.】
    /// 버리기 · 착용 · 소진 등 가방을 건드리는 곳마다 퀵슬롯을 챙기게 하면
    /// 한 군데만 빠져도 「없는 것을 가리키는 칸」이 남는다.
    /// </summary>
    public void Prune(Inventory bag)
    {
        for (int i = 0; i < Count; i++)
        {
            ItemStack stack = slots[i];

            if (stack == null)
                continue;

            if (stack.IsEmpty || bag == null || !Holds(bag.Stacks, stack))
                slots[i] = null;
        }
    }

    private static bool Holds(IReadOnlyList<ItemStack> stacks, ItemStack stack)
    {
        for (int i = 0; i < stacks.Count; i++)
        {
            if (ReferenceEquals(stacks[i], stack))
                return true;
        }

        return false;
    }
}
