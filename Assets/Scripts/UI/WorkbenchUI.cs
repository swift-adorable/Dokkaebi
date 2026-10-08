using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【임시】 작업대 화면 (결정 2-95) — 단계 탭 Ⅰ ~ Ⅳ · 단계 올리기 · 견본 바치기 · 만들기.
/// 작업대 앞에서 누르면 연다. 떠 있는 동안 게임이 멈춘다(결정 2-92). 모양은 레이어 · 아트 작업 때 다시 만든다.
/// </summary>
public class WorkbenchUI : MonoBehaviour
{
    private static WorkbenchUI instance;

    private const float RowHeight = 96f;
    private const float RowGap = 8f;

    private ItemCatalog catalog;
    private Text title;
    private Text upgradeLine;
    private Button upgradeButton;
    private Text status;
    private UIFactory.ScrollList list;
    private readonly List<Image> tabs = new();
    private int shownStage = 1;

    public static void Open()
    {
        if (instance != null)
        {
            instance.Refresh();
            return;
        }

        // 가방 화면(1000)보다 위.
        Canvas canvas = UIFactory.CreateCanvas("WorkbenchCanvas (Runtime)", 1200);
        instance = canvas.gameObject.AddComponent<WorkbenchUI>();
        instance.Build(canvas);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build(Canvas canvas)
    {
        catalog = ItemCatalog.Load();

        RectTransform safe = UIFactory.CreateSafeArea(canvas);
        GamePause.Register(canvas.gameObject);   // 떠 있는 동안 게임이 멈춘다 (결정 2-92)
        Image dim = UIFactory.CreatePanel("Dim", safe, UIPalette.Dim, Vector2.zero, Vector2.one, 0);
        Image box = UIFactory.CreatePanel("Box", dim.transform, UIPalette.Panel,
            new Vector2(0.1f, 0.04f), new Vector2(0.9f, 0.96f));

        title = UIFactory.CreateLabel(box.transform, string.Empty, 30, FontStyle.Bold,
            new Vector2(0.04f, 0.89f), new Vector2(0.78f, 0.98f), TextAnchor.MiddleLeft);
        UIFactory.CreateButton(box.transform, "닫기", new Vector2(0.8f, 0.9f), new Vector2(0.96f, 0.97f),
            UIPalette.Header, () => Destroy(gameObject), 22);

        for (int stage = 1; stage <= WorkbenchTable.MaxStage; stage++)
        {
            int captured = stage;
            float x = 0.04f + (stage - 1) * 0.12f;
            Button tab = UIFactory.CreateButton(box.transform, WorkbenchTable.StageName(stage),
                new Vector2(x, 0.80f), new Vector2(x + 0.11f, 0.88f), UIPalette.Header, () => ShowStage(captured), 26);
            tabs.Add(tab.GetComponent<Image>());
        }

        upgradeLine = UIFactory.CreateLabel(box.transform, string.Empty, 19, FontStyle.Normal,
            new Vector2(0.53f, 0.80f), new Vector2(0.80f, 0.88f), TextAnchor.MiddleRight, UIPalette.TextDim);
        upgradeLine.supportRichText = true;
        upgradeButton = UIFactory.CreateButton(box.transform, "단계 올리기", new Vector2(0.81f, 0.80f),
            new Vector2(0.96f, 0.88f), UIPalette.Action, OnUpgrade, 20);

        list = UIFactory.CreateScrollList("WorkbenchList", box.transform,
            new Vector2(0.04f, 0.09f), new Vector2(0.96f, 0.785f));

        status = UIFactory.CreateLabel(box.transform, string.Empty, 20, FontStyle.Normal,
            new Vector2(0.04f, 0.01f), new Vector2(0.96f, 0.08f), TextAnchor.MiddleCenter, UIPalette.TextAccent);

        ShowStage(Mathf.Clamp(WorkbenchManager.State.Stage, 1, WorkbenchTable.MaxStage));
    }

    private void ShowStage(int stage)
    {
        shownStage = stage;
        Refresh();
    }

    private void Refresh()
    {
        WorkbenchState state = WorkbenchManager.State;
        int stage = state.Stage;

        title.text = $"작업대 {WorkbenchTable.StageName(stage)}  ·  {WorkbenchManager.Gold:N0}엽전";

        for (int i = 0; i < tabs.Count; i++)
            tabs[i].color = i + 1 == shownStage ? UIPalette.SlotSelected
                : i + 1 <= stage ? UIPalette.Header : UIPalette.SlotLocked;

        // 단계 올리기 줄
        if (stage >= WorkbenchTable.MaxStage)
        {
            upgradeLine.text = "마지막 단계다";
            upgradeButton.interactable = false;
        }
        else
        {
            StageCost cost = WorkbenchTable.CostToReach(stage + 1);
            upgradeLine.text = $"{WorkbenchTable.StageName(stage + 1)}로: {CostLine(cost.Materials, cost.Gold)}";
            upgradeButton.interactable = state.CanUpgrade(WorkbenchManager.Built, WorkbenchManager.Gold,
                WorkbenchManager.CountOf) == CraftError.None;
        }

        FillList();
    }

    private void FillList()
    {
        UIFactory.ClearChildren(list.Content);
        WorkbenchState state = WorkbenchManager.State;
        float y = 0f;

        Text theme = UIFactory.CreateLabel(list.Content, $"{WorkbenchTable.StageName(shownStage)} — {WorkbenchTable.StageTheme(shownStage)}"
            + (state.Stage < shownStage ? "   (작업대를 이 단계로 올려야 한다)" : string.Empty),
            22, FontStyle.Bold, Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft,
            state.Stage < shownStage ? UIPalette.TextDim : UIPalette.TextAccent);
        Place(theme.rectTransform, ref y, 44f);

        foreach (CraftRecipe recipe in WorkbenchTable.InStage(shownStage))
            AddRow(recipe, ref y);

        list.Content.sizeDelta = new Vector2(0f, y);
    }

    private void AddRow(CraftRecipe recipe, ref float y)
    {
        WorkbenchState state = WorkbenchManager.State;
        bool unlocked = state.IsUnlocked(recipe);

        Image row = UIFactory.CreatePanel("Row", list.Content, UIPalette.Row, Vector2.zero, Vector2.zero);
        Place(row.rectTransform, ref y, RowHeight);

        string head = recipe.OutputCount > 1 ? $"{NameOf(recipe.OutputId)} ×{recipe.OutputCount}" : NameOf(recipe.OutputId);
        string line = unlocked
            ? $"재료: {CostLine(recipe.Inputs, recipe.Gold)}"
            : $"바칠 것: {CostLine(recipe.UnlockCost, 0)}";

        Text label = UIFactory.CreateLabel(row.transform, $"<b>{head}</b>\n<size=18>{line}</size>", 22, FontStyle.Normal,
            new Vector2(0.02f, 0f), new Vector2(0.78f, 1f), TextAnchor.MiddleLeft);
        label.supportRichText = true;

        bool canAct;
        string verb;
        UnityEngine.Events.UnityAction action;

        if (unlocked)
        {
            verb = "만들기";
            canAct = state.CanCraft(recipe, WorkbenchManager.Built, WorkbenchManager.Gold, WorkbenchManager.CountOf) == CraftError.None;
            action = () => Act(WorkbenchManager.Craft(recipe), $"{Josa.EulReul(NameOf(recipe.OutputId))} 만들었다 — 창고에 넣었다.");
        }
        else
        {
            verb = "바치기";
            canAct = state.CanUnlock(recipe, WorkbenchManager.Built, WorkbenchManager.CountOf) == CraftError.None;
            action = () => Act(WorkbenchManager.Unlock(recipe), $"{NameOf(recipe.OutputId)} 만드는 법을 열었다.");
        }

        Button button = UIFactory.CreateButton(row.transform, verb, new Vector2(0.80f, 0.15f), new Vector2(0.98f, 0.85f),
            unlocked ? UIPalette.Action : UIPalette.Subtle, action, 22);
        button.interactable = canAct;
    }

    private void Act(CraftError error, string done)
    {
        status.text = error == CraftError.None ? done : WorkbenchState.Explain(error);
        Refresh();
    }

    private void OnUpgrade()
    {
        CraftError error = WorkbenchManager.Upgrade();
        status.text = error == CraftError.None
            ? $"작업대를 {WorkbenchTable.StageName(WorkbenchManager.State.Stage)}로 올렸다 — {WorkbenchTable.StageTheme(WorkbenchManager.State.Stage)}"
            : WorkbenchState.Explain(error);

        if (error == CraftError.None)
            shownStage = WorkbenchManager.State.Stage;

        Refresh();
    }

    /// <summary>「120엽전 · 쇠붙이 3/6 · 숯 0/1」. 모자란 것은 붉게.</summary>
    private string CostLine(MaterialCost[] costs, int gold)
    {
        var sb = new StringBuilder();

        if (gold > 0)
            sb.Append(Colored($"{gold:N0}엽전", WorkbenchManager.Gold >= gold));

        foreach (MaterialCost c in costs)
        {
            int have = WorkbenchManager.CountOf(c.ItemId);

            if (sb.Length > 0)
                sb.Append(" · ");

            sb.Append(Colored($"{NameOf(c.ItemId)} {have}/{c.Count}", have >= c.Count));
        }

        return sb.ToString();
    }

    private static string Colored(string text, bool ok) => ok ? text : $"<color=#EB7361>{text}</color>";

    private string NameOf(string id) => catalog?.Find(id)?.DisplayName ?? id;

    private static void Place(RectTransform rect, ref float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(6f, -(y + height));
        rect.offsetMax = new Vector2(-6f, -y);
        y += height + RowGap;
    }
}
