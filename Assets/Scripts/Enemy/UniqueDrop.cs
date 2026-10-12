using UnityEngine;

/// <summary>
/// 다시 나온 중간 보스가 걸친 고유 장비 (결정 2-109). 쓰러지면 시체 전리품에 넣고 표식을 지운다 —
/// 풀로 돌아간 몸이 다음에 일반 적으로 나올 때 다시 떨어뜨리지 않게.
/// </summary>
public class UniqueDrop : MonoBehaviour
{
    public string ItemId;

    public void Deliver(CorpseController corpse)
    {
        ItemDefinition item = ItemCatalog.Load()?.Find(ItemId);
        if (item != null && corpse != null)
            corpse.PutGuaranteed(item);

        Destroy(this);
    }
}
