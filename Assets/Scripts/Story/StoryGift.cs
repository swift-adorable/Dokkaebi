using UnityEngine;

/// <summary>
/// 0-2의 큰 요괴가 품은 첫 구슬 (결정 2-79). 이 적이 쓰러지면 시체 전리품에 화염 핵심 구슬을 넣고 사라진다.
/// 풀로 돌아간 몸이 다음에 일반 적으로 나올 때 다시 주지 않게, 한 번 주면 컴포넌트를 지운다.
/// </summary>
public class StoryGift : MonoBehaviour
{
    public ItemDefinition Item;
    public string EventId;

    public void Deliver(CorpseController corpse)
    {
        if (Item != null && corpse != null)
        {
            // 칸이 가득 차 있으면 하나를 비워서라도 넣는다 — 이 구슬은 0장의 약속이다.
            if (!corpse.Loot.TryPut(Item))
            {
                corpse.Loot.Clear();
                corpse.Loot.TryPut(Item);
            }

            StoryManager.Progress.See(EventId);
        }

        Destroy(this);
    }
}
