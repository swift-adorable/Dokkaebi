using UnityEngine;

/// <summary>
/// 본문 「(시스템 설명 / 새김패 · 가방 무게)」를 그 일이 처음 생길 때 한 번 띄운다 (결정 2-112).
/// 거점 건축 · 요리는 그 화면을 열 때 띄운다(BuildingScreenUI · CookingUI). StoryDirector 옆에 붙는다.
/// </summary>
public class SystemNoteDirector : MonoBehaviour
{
    private float timer;

    private void Update()
    {
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f)
            return;
        timer = 1f;

        if (!PlayerInventory.HasInstance || StoryDialogueUI.IsShowing)
            return;

        StoryProgress p = StoryManager.Progress;
        PlayerInventory inventory = PlayerInventory.Instance;

        if (!p.HasSeen($"note_{StoryManager.NoteImprint}") && HoldsImprint(inventory))
            StoryManager.ShowSystemNoteOnce(StoryManager.NoteImprint);
        else if (!p.HasSeen($"note_{StoryManager.NoteWeight}") && inventory.Encumbrance != EncumbranceLevel.Normal)
            StoryManager.ShowSystemNoteOnce(StoryManager.NoteWeight);
    }

    private static bool HoldsImprint(PlayerInventory inventory)
    {
        foreach (ItemStack s in inventory.Bag.Stacks)
            if (s?.Definition != null && s.Definition.Kind == ItemKind.Imprint)
                return true;

        return inventory.Loadout.Get(EquipmentSlot.ImprintA) != null || inventory.Loadout.Get(EquipmentSlot.ImprintB) != null;
    }
}
