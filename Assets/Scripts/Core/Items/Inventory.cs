using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 가방. 장비 · 젬 · 전리품이 전부 여기에 들어간다.
///
/// 두 개의 독립된 자원으로 제한된다.
///   가방 칸 — 초반의 병목
///   최대 소지 중량 — kg. 중반 이후의 진짜 병목
///
/// 두 축을 분리한 이유 — 가방마다 배분이 달라서 "이번 파밍에 무엇을 노리는가"에 따라
/// 가방 선택이 갈린다. 고가 중량물이냐, 소형 재료 다수냐.
/// (docs/Dokkaebi_Equipment_System.md 5-1절)
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

    /// <summary>가방 칸 수.</summary>
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
    /// "느려지지만 들고 갈 수는 있다"여야 철수 판단이 생긴다.
    /// </summary>
    public EncumbranceLevel Encumbrance => WeightCalculator.Evaluate(TotalWeight, weightLimit);

    public bool IsOverweight => Encumbrance != EncumbranceLevel.Normal;

    /// <summary>
    /// 담을 수 있는지. 칸 수만 본다. 무게는 막지 않는다.
    ///
    /// 겹칠 수 있는 아이템은 기존 칸에 들어가므로 칸을 쓰지 않을 수 있다.
    /// 젬은 칸에 잡히지 않으므로(ItemDefinition.IsCargo) 늘 담을 수 있다.
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

        return newStacks * definition.SlotCost <= FreeSlots;
    }

    /// <summary>담는다. 실제로 담긴 개수를 반환한다. 부분 담기를 허용한다.</summary>
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
            if (FreeSlots < definition.SlotCost)
                break;

            int amount = Mathf.Min(remaining, definition.StackMax);

            stacks.Add(new ItemStack(definition, amount, durability));

            remaining -= amount;
        }

        return count - remaining;
    }

    /// <summary>
    /// 담는다. 이미 만들어진 개체(내구도 유지)를 그대로 넣는다.
    ///
    /// 【겹치는 아이템은 먼저 기존 칸에 합친다.】
    /// 예전에는 무조건 새 칸에 붙였다. 그래서 전리품 창에서 쇠붙이를 세 번 주우면
    /// 스택 상한이 20인데도 세 칸을 잡아먹었다. 가방이 금방 차서
    /// 가방 설계(칸 수·무게)가 전부 헛돌았다.
    ///
    /// 【전부 아니면 전혀】 계약은 그대로다. 먼저 CanAdd로 전량이 들어가는지
    /// 확인한 뒤에만 옮기기 시작한다. 그래야 전리품 한 칸이 반만 옮겨져
    /// "무엇을 가져왔는지 화면만 보고 알 수 없는" 상태가 생기지 않는다.
    /// (LootContainer.TryTakeTo의 주석과 같은 이유다)
    /// </summary>
    public bool TryAddStack(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty)
            return false;

        if (!stack.Definition.IsStackable)
        {
            // 내구도가 있는 물건은 애초에 겹치지 않는다. 개체를 그대로 넣는다.
            if (FreeSlots < stack.TotalSlots)
                return false;

            stacks.Add(stack);

            return true;
        }

        if (!CanAdd(stack.Definition, stack.Count))
            return false;

        // 1) 기존 칸의 여유부터 채운다.
        for (int i = 0; i < stacks.Count && !stack.IsEmpty; i++)
            stacks[i].Merge(stack);

        // 2) 남은 것은 새 칸으로. CanAdd가 자리를 보장했다.
        while (!stack.IsEmpty)
        {
            if (FreeSlots < stack.Definition.SlotCost)
                return false;

            int amount = Mathf.Min(stack.Count, stack.Definition.StackMax);

            stacks.Add(new ItemStack(stack.Definition, amount));

            stack.Take(amount);
        }

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

    /// <summary>
    /// 이 개체를 통째로 받을 수 있는지. 【TryAddStack이 성공할지와 같은 답이다.】
    /// 겹치는 것은 기존 칸의 여유까지 센다.
    /// </summary>
    public bool CanAccept(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty)
            return false;

        if (!stack.Definition.IsStackable)
            return FreeSlots >= stack.TotalSlots;

        return CanAdd(stack.Definition, stack.Count);
    }

    /// <summary>
    /// 한 칸을 다른 인벤토리로 옮긴다. 창고 ↔ 가방이 이 길을 쓴다.
    ///
    /// 【전부 아니면 전혀】 — 받는 쪽에 자리가 없으면 아무것도 움직이지 않는다.
    /// 먼저 검사하고, 빼고, 넣는다. 넣기가 어긋나면 도로 넣는다.
    /// 이 함수는 아이템을 만들지도 없애지도 않는다.
    /// </summary>
    public static bool MoveStack(Inventory from, Inventory to, ItemStack stack)
    {
        if (from == null || to == null || from == to || stack == null || stack.IsEmpty)
            return false;

        if (!to.CanAccept(stack))
            return false;

        if (!from.RemoveStack(stack))
            return false;

        if (to.TryAddStack(stack))
            return true;

        from.TryAddStack(stack);
        return false;
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
    /// 철수에 실패했을 때 잃는 것을 비운다.
    ///
    /// 각인만 남는다. 플레이어가 배울 규칙은 하나여야 하므로 예외를 늘리지 않는다.
    /// 젬도 장비와 똑같이 잃는다. (docs/Dokkaebi_Progression_System.md 6절)
    /// </summary>
    public int DropOnDeath(int safeSlots = 0)
    {
        int lost = 0;

        // 패시브 「사망해도 지키는 가방 칸 +n」 — 가방 맨 앞 n칸(젬이 아닌 물건)은 남는다 (Audit A9).
        // 【사망 규칙의 유일한 예외다】 (Passive_System 5절).
        var safe = SafeStacks(safeSlots);

        for (int i = stacks.Count - 1; i >= 0; i--)
        {
            if (stacks[i].Definition != null && stacks[i].Definition.SurvivesDeath)
                continue;

            if (safe != null && safe.Contains(stacks[i]))
                continue;

            lost += stacks[i].Count;
            stacks.RemoveAt(i);
        }

        return lost;
    }

    /// <summary>가방 맨 앞 n칸 — 젬이 아닌 묶음을 앞에서부터 n개. 없으면 null.</summary>
    public HashSet<ItemStack> SafeStacks(int safeSlots)
    {
        if (safeSlots <= 0)
            return null;

        var safe = new HashSet<ItemStack>();

        for (int i = 0; i < stacks.Count && safe.Count < safeSlots; i++)
        {
            if (stacks[i].Definition != null && !stacks[i].Definition.IsSkillGem)
                safe.Add(stacks[i]);
        }

        return safe;
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
