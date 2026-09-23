using System.Collections.Generic;

/// <summary>
/// 상점의 남은 재고. MonoBehaviour 없는 순수 클래스다.
///
/// 【파밍이 한 번 끝날 때마다 가득 채운다.】 (결정 2-35)
/// 덕코프의 재고 갱신 주기는 위키에 없다 [확인 불가]. 실시간으로 채우면
/// 「기다리면 된다」가 되고, 모바일에서 실시간 대기는 금지다(Progression 2절).
/// 파밍 단위로 채우면 「이번에 몇 개를 들고 갈까」가 파밍마다 새로 생긴다.
/// </summary>
public class ShopState
{
    private readonly Dictionary<string, int> remaining = new();

    /// <summary>남은 개수. 표에 없는 아이템이면 0.</summary>
    public int Remaining(string itemId)
        => itemId != null && remaining.TryGetValue(itemId, out int count) ? count : 0;

    /// <summary>표의 최대 재고로 전부 채운다.</summary>
    public void Restock(IReadOnlyList<ShopEntry> table)
    {
        remaining.Clear();

        if (table == null)
            return;

        for (int i = 0; i < table.Count; i++)
            remaining[table[i].ItemId] = table[i].MaxStock;
    }

    /// <summary>하나를 뺀다. 남은 것이 없으면 false.</summary>
    public bool TryConsume(string itemId)
    {
        int count = Remaining(itemId);

        if (count <= 0)
            return false;

        remaining[itemId] = count - 1;
        return true;
    }

    /// <summary>
    /// 세이브에서 되살린다. 【표에 없는 줄은 버리고, 최대 재고를 넘는 값은 깎는다.】
    /// 표를 고친 뒤 옛 세이브를 열면 생긴다. 저장에 없는 줄은 가득 찬 것으로 본다 —
    /// 새로 들어온 상품이 0개로 시작하면 다음 파밍까지 살 수 없다.
    /// </summary>
    public void Restore(IReadOnlyList<ShopEntry> table, IReadOnlyList<SavedStock> saved)
    {
        Restock(table);

        if (table == null || saved == null)
            return;

        for (int i = 0; i < saved.Count; i++)
        {
            SavedStock row = saved[i];

            if (row == null || string.IsNullOrEmpty(row.id) || !remaining.ContainsKey(row.id))
                continue;

            int max = remaining[row.id];
            remaining[row.id] = row.remaining < 0 ? 0 : row.remaining > max ? max : row.remaining;
        }
    }

    /// <summary>저장할 줄들. 표 순서대로 쓴다.</summary>
    public List<SavedStock> Capture(IReadOnlyList<ShopEntry> table)
    {
        var rows = new List<SavedStock>();

        if (table == null)
            return rows;

        for (int i = 0; i < table.Count; i++)
            rows.Add(new SavedStock { id = table[i].ItemId, remaining = Remaining(table[i].ItemId) });

        return rows;
    }
}
