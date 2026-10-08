using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【임시】 약탕 화면 (결정 2-97) — 달이기 · 샘가. 약탕간 옆 약탕 앞에서 누르면 연다.
/// 떠 있는 동안 게임이 멈춘다(결정 2-92). 모양은 레이어 · 아트 작업 때 다시 만든다.
/// </summary>
public class ApothecaryUI : MonoBehaviour
{
    private static ApothecaryUI instance;

    private const float RowHeight = 96f;
    private const float RowGap = 8f;

    private const int BrewTab = 0;
    private const int SpringsideTab = 1;

    private ItemCatalog catalog;
    private Text title;
    private Text status;
    private UIFactory.ScrollList list;
    private readonly List<Image> tabs = new();
    private int shown = BrewTab;

    public static void Open(int tab = BrewTab)
    {
        if (instance != null)
        {
            instance.Show(tab);
            return;
        }

        // 가방 화면(1000)보다 위.
        Canvas canvas = UIFactory.CreateCanvas("ApothecaryCanvas (Runtime)", 1200);
        instance = canvas.gameObject.AddComponent<ApothecaryUI>();
        instance.Build(canvas);
        instance.Show(tab);
    }

    public static void Close()
    {
        if (instance != null)
            Destroy(instance.gameObject);
    }

    /// <summary>샘가 탭으로 연다 (검증 도구).</summary>
    public static void OpenSpringside() => Open(SpringsideTab);

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

        string[] names = { "달이기", ApothecaryTable.SpringsideName };
        for (int i = 0; i < names.Length; i++)
        {
            int captured = i;
            float x = 0.04f + i * 0.17f;
            Button tab = UIFactory.CreateButton(box.transform, names[i],
                new Vector2(x, 0.80f), new Vector2(x + 0.16f, 0.88f), UIPalette.Header, () => Show(captured), 24);
            tabs.Add(tab.GetComponent<Image>());
        }

        list = UIFactory.CreateScrollList("ApothecaryList", box.transform,
            new Vector2(0.04f, 0.09f), new Vector2(0.96f, 0.785f));

        status = UIFactory.CreateLabel(box.transform, string.Empty, 20, FontStyle.Normal,
            new Vector2(0.04f, 0.01f), new Vector2(0.96f, 0.08f), TextAnchor.MiddleCenter, UIPalette.TextAccent);
    }

    private void Show(int tab)
    {
        shown = tab;
        Refresh();
    }

    private void Refresh()
    {
        title.text = $"{ApothecaryTable.PotName} — 약탕간" + (ApothecaryManager.Built ? string.Empty : "  (약탕간을 지어야 한다)");

        for (int i = 0; i < tabs.Count; i++)
            tabs[i].color = i == shown ? UIPalette.SlotSelected : UIPalette.Header;

        UIFactory.ClearChildren(list.Content);
        float y = 0f;

        if (shown == SpringsideTab)
            FillSpringside(ref y);
        else
            FillBrews(ref y);

        list.Content.sizeDelta = new Vector2(0f, y);
    }

    private void FillBrews(ref float y)
    {
        AddTheme("약재로 약을 달인다 — 재료는 창고 먼저, 만든 것은 창고로", ref y);

        foreach (CraftRecipe recipe in ApothecaryTable.Brews)
        {
            Image row = UIFactory.CreatePanel("Row", list.Content, UIPalette.Row, Vector2.zero, Vector2.zero);
            Place(row.rectTransform, ref y, RowHeight);

            string head = recipe.OutputCount > 1 ? $"{NameOf(recipe.OutputId)} ×{recipe.OutputCount}" : NameOf(recipe.OutputId);
            string effect = catalog?.Find(recipe.OutputId)?.Description ?? string.Empty;

            Text label = UIFactory.CreateLabel(row.transform,
                $"<b>{head}</b>  <size=16><color=#A8A08C>{effect}</color></size>\n<size=18>재료: {CostLine(recipe.Inputs)}</size>",
                22, FontStyle.Normal, new Vector2(0.02f, 0f), new Vector2(0.78f, 1f), TextAnchor.MiddleLeft);
            label.supportRichText = true;

            Button button = UIFactory.CreateButton(row.transform, "달이기", new Vector2(0.80f, 0.15f), new Vector2(0.98f, 0.85f),
                UIPalette.Action, () => Act(ApothecaryManager.Brew(recipe), $"{Josa.EulReul(NameOf(recipe.OutputId))} 달였다 — 창고에 넣었다."), 22);
            button.interactable = ApothecaryTable.CanBrew(recipe, ApothecaryManager.Built, WorkbenchManager.CountOf) == CraftError.None;
        }
    }

    private void FillSpringside(ref float y)
    {
        AddTheme($"판이 끝날 때마다 약재 {ApothecaryTable.HerbsPerNight}개 (최대 {ApothecaryTable.HerbCap}) · 가끔 열린 장의 장 재료", ref y);

        SpringsideState side = ApothecaryManager.Springside;

        if (side.IsEmpty)
        {
            AddLine(ApothecaryManager.Built ? "아직 모인 것이 없다. 파밍을 마치고 오면 모여 있다." : "약탕간을 지어 두면 모이기 시작한다.", ref y);
            return;
        }

        foreach (MaterialCost c in side.Pending)
            AddLine($"{NameOf(c.ItemId)} ×{c.Count}", ref y, UIPalette.TextAccent);

        Image row = UIFactory.CreatePanel("Collect", list.Content, UIPalette.Row, Vector2.zero, Vector2.zero);
        Place(row.rectTransform, ref y, RowHeight);
        UIFactory.CreateButton(row.transform, "다 거두기", new Vector2(0.30f, 0.15f), new Vector2(0.70f, 0.85f),
            UIPalette.Action, OnCollect, 24);
    }

    private void OnCollect()
    {
        int taken = ApothecaryManager.Collect(out bool leftSome);
        status.text = taken == 0
            ? "창고에도 가방에도 자리가 없다."
            : leftSome ? "거두었다 — 자리가 없는 것은 샘가에 남겼다." : "샘가의 것을 다 거두어 창고에 넣었다.";
        Refresh();
    }

    private void Act(CraftError error, string done)
    {
        status.text = error == CraftError.None ? done : ApothecaryTable.Explain(error);
        Refresh();
    }

    private void AddTheme(string text, ref float y)
    {
        Text theme = UIFactory.CreateLabel(list.Content, text, 20, FontStyle.Bold, Vector2.zero, Vector2.zero,
            TextAnchor.MiddleLeft, UIPalette.TextAccent);
        Place(theme.rectTransform, ref y, 44f);
    }

    private void AddLine(string text, ref float y, Color? color = null)
    {
        Text line = UIFactory.CreateLabel(list.Content, text, 22, FontStyle.Normal, Vector2.zero, Vector2.zero,
            TextAnchor.MiddleLeft, color ?? UIPalette.TextDim);
        Place(line.rectTransform, ref y, 44f);
    }

    /// <summary>「약재 3/2 · 쌀 0/1」. 모자란 것은 붉게.</summary>
    private string CostLine(MaterialCost[] costs)
    {
        var sb = new StringBuilder();

        foreach (MaterialCost c in costs)
        {
            int have = WorkbenchManager.CountOf(c.ItemId);

            if (sb.Length > 0)
                sb.Append(" · ");

            string text = $"{NameOf(c.ItemId)} {have}/{c.Count}";
            sb.Append(have >= c.Count ? text : $"<color=#EB7361>{text}</color>");
        }

        return sb.ToString();
    }

    private string NameOf(string id) => catalog?.Find(id)?.DisplayName ?? IngredientTable.NameOf(id) ?? id;

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
