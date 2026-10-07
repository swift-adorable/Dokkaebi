using UnityEngine;

/// <summary>
/// 【임시】 0장 튜토리얼 가이드를 돌린다 (결정 2-85 · TutorialTable). 씬마다 StoryDirector 옆에 붙는다.
/// 0.25초마다 지금 상태(TutorialFacts)를 모아 판정하고, 화면 위 가운데 안내(TutorialHudUI)를 바꾼다.
/// 다 끝나면 「tut_done」을 적고 안내를 치운다.
/// </summary>
public class TutorialDirector : MonoBehaviour
{
    private const float PollInterval = 0.25f;

    private float timer;
    private int currentIndex = -2;
    private int killsThisStep;
    private bool corpseLooted;

    private void OnEnable()
    {
        EnemyController.Killed += OnKilled;
        ExchangeWindowUI.CorpseOpened += OnCorpseOpened;
    }

    private void OnDisable()
    {
        EnemyController.Killed -= OnKilled;
        ExchangeWindowUI.CorpseOpened -= OnCorpseOpened;
    }

    private void OnKilled(EnemyController _) => killsThisStep++;
    private void OnCorpseOpened() => corpseLooted = true;

    private void Update()
    {
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f)
            return;

        timer = PollInterval;

        StoryProgress progress = StoryManager.Progress;

        if (TutorialTable.IsDone(progress) || ChapterZeroTable.TutorialDone(progress))
        {
            TutorialHudUI.Clear();
            enabled = false;
            return;
        }

        // 새 게임은 난이도 · 프롤로그가 먼저다 — 그동안은 안내를 띄우지 않는다.
        if (!progress.HasSeen(StoryTable.PrologueEvent))
            return;

        TutorialFacts facts = Gather(progress);
        int index = TutorialTable.Evaluate(progress, facts, out int completed);

        if (index != currentIndex)
        {
            // 단계가 바뀌면 그 단계에서 센 것을 비운다 (잡귀 셋 · 시체 뒤지기).
            killsThisStep = 0;
            corpseLooted = false;

            if (completed > 0 && currentIndex >= 0)
                TutorialHudUI.EnsureInstance().Flash();

            currentIndex = index;
        }

        if (index < 0)
        {
            StoryDialogueUI.ShowBanner("튜토리얼을 마쳤다 — 이제 주운 장비는 빈 자리에 바로 들어간다.", 3f);
            TutorialHudUI.Clear();
            enabled = false;
            return;
        }

        TutorialStep step = TutorialTable.Steps[index];
        string progressText = step.Check == TutorialCheck.Kills ? $" ({Mathf.Min(killsThisStep, step.Count)}/{step.Count})" : string.Empty;

        TutorialHudUI.EnsureInstance().Show(index + 1, TutorialTable.Steps.Count,
            step.Text + progressText, step.Hint, step.Place == TutorialPlace.Bunker ? "소굴" : "구역");
    }

    private TutorialFacts Gather(StoryProgress p)
    {
        var f = new TutorialFacts
        {
            BundleOpened = p.HasSeen(ChapterZeroTable.BundleEvent),
            Arrived = p.HasSeen(ChapterZeroTable.ArrivalEvent),
            Entered02 = p.HasSeen(StoryTable.EnterEvent(ChapterZeroTable.GiftZone)),
            GiftDropped = p.HasSeen(ChapterZeroTable.GiftEvent),
            Extracted02 = p.HasSeen(TutorialTable.Extracted02Event),
            KillsThisStep = killsThisStep,
            CorpseLooted = corpseLooted,
        };

        if (PlayerInventory.HasInstance)
        {
            PlayerInventory inventory = PlayerInventory.Instance;
            EquipmentLoadout loadout = inventory.Loadout;

            f.WeaponEquipped = loadout.WeaponAt(0) != null || loadout.WeaponAt(1) != null;
            f.StashUsed = inventory.Stash.Stacks.Count > 0;

            foreach (ItemStack s in inventory.Bag.Stacks)
            {
                ItemDefinition d = s?.Definition;
                if (d == null)
                    continue;

                if (d.Kind == ItemKind.Weapon)
                    f.WeaponInHand = true;

                if (d.IsSkillGem && d.Skill != null && d.Skill.Category == SkillCategory.Core)
                    f.CoreGemOwned = true;
            }
        }

        if (SkillManager.HasInstance)
            f.CoreSocketed = SkillManager.Instance.Build.HasCore;

        return f;
    }
}
