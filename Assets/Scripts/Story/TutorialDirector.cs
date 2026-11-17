using System.Collections.Generic;
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
    private int socketChanges;
    private SocketedBuild watchedBuild;
    private bool ackShowing;

    // 결정 2-107 — 먹기 · 회복 · 창고 꺼내기는 한 번 하면 그 단계가 끝난다(단계가 바뀌어도 지우지 않는다).
    private bool drankOrAte;
    private bool usedHeal;
    private bool stashTaken;

    private void OnEnable()
    {
        EnemyController.Killed += OnKilled;
        ExchangeWindowUI.CorpseOpened += OnCorpseOpened;
        ExchangeWindowUI.StashTaken += OnStashTaken;
        PlayerConsumables.Used += OnConsumableUsed;
    }

    private void OnDisable()
    {
        EnemyController.Killed -= OnKilled;
        ExchangeWindowUI.CorpseOpened -= OnCorpseOpened;
        ExchangeWindowUI.StashTaken -= OnStashTaken;
        PlayerConsumables.Used -= OnConsumableUsed;
        WatchBuild(null);
    }

    private void OnKilled(EnemyController _) => killsThisStep++;
    private void OnCorpseOpened() => corpseLooted = true;
    private void OnStashTaken() => stashTaken = true;
    private void OnSocketChanged() => socketChanges++;

    private void OnConsumableUsed(ItemDefinition _, ConsumableOutcome outcome)
    {
        if (outcome.Water > 0f || outcome.Energy > 0f)
            drankOrAte = true;
        if (outcome.Heal > 0)
            usedHeal = true;
    }

    private void WatchBuild(SocketedBuild build)
    {
        if (watchedBuild == build)
            return;
        if (watchedBuild != null)
            watchedBuild.OnChanged -= OnSocketChanged;
        watchedBuild = build;
        if (watchedBuild != null)
            watchedBuild.OnChanged += OnSocketChanged;
    }

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

        WatchBuild(SkillManager.HasInstance ? SkillManager.Instance.Build : null);

        TutorialFacts facts = Gather(progress);
        int index = TutorialTable.Evaluate(progress, facts, out int completed);

        if (index != currentIndex)
        {
            // 단계가 바뀌면 그 단계에서 센 것을 비운다 (잡귀 셋 · 시체 뒤지기).
            killsThisStep = 0;
            corpseLooted = false;
            socketChanges = 0;

            if (completed > 0 && currentIndex >= 0)
                TutorialHudUI.EnsureInstance().Flash();

            // 튜토리얼인 줄 알게 (결정 2-138) — 처음 뜰 때 소개 한 줄, 그 뒤 단계가 바뀔 때마다 가운데에 「튜토리얼 · 할 일」.
            if (index >= 0)
            {
                string what = TutorialTable.Steps[index].Text;
                if (currentIndex < 0 && index == 0)
                    StoryDialogueUI.ShowBanner("튜토리얼 — 0장에서 싸우고 줍고 철수하는 법을 익힌다 · 할 일은 오른쪽 위 초록 줄", 4f);
                else
                    StoryDialogueUI.ShowBanner($"튜토리얼 · {what}", 2.5f);
            }

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

        // 본문 「(UI 튜토리얼 / …)」 글 — 힌트가 비었으면 그 글을 힌트로 쓴다 (결정 2-107).
        string note = step.Note != null ? LoreTable.BodyOf(StoryManager.Lore, LoreKind.Tutorial, step.Note) : string.Empty;
        string hint = string.IsNullOrEmpty(step.Hint) ? note : step.Hint;

        TutorialHudUI.EnsureInstance().Show(index + 1, TutorialTable.Steps.Count,
            step.Text + progressText, hint, step.Place == TutorialPlace.Bunker ? "소굴" : "구역");

        // 안내 창 단계 — 그 자리(소굴 · 구역)에 있고 다른 창이 없을 때 본문 글을 한 번 띄운다.
        if (step.Check == TutorialCheck.Acknowledged && !ackShowing && !StoryDialogueUI.IsShowing
            && (step.Place == TutorialPlace.Bunker) == SceneFlow.InBunker)
        {
            ackShowing = true;
            string id = step.Id;
            StoryDialogueUI.Show(step.Text, new List<string> { string.IsNullOrEmpty(note) ? step.Text : note }, () =>
            {
                ackShowing = false;
                StoryManager.Progress.See(TutorialTable.AckEvent(id));
            });
        }
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
            SocketChangesThisStep = socketChanges,
            DrankOrAte = drankOrAte,
            UsedHeal = usedHeal,
            StashTaken = stashTaken,
        };

        if (PlayerInventory.HasInstance)
        {
            PlayerInventory inventory = PlayerInventory.Instance;
            EquipmentLoadout loadout = inventory.Loadout;

            f.WeaponEquipped = loadout.WeaponAt(0) != null || loadout.WeaponAt(1) != null;
            f.AmmoLoaded = loadout.LoadedAt(0) > 0 || loadout.LoadedAt(1) > 0;
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
