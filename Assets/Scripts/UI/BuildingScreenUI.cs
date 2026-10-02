using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 건물 목록 — 설계도 테이블에서 연다. (로드맵 8-K)
///
/// 덕코프의 건물 화면처럼 【지금 지을 수 있는 건물과 필요한 재료】를 보여 주고,
/// 재료가 충분하면 항목이 초록이 된다 [확인됨 — 위키 가이드].
///
/// 한 줄에 버튼 하나 — 상태가 버튼 이름을 정한다.
///   가지지 않음 → 「짓기」 (골드 · 재료를 치르고 배치 모드로)
///   가졌지만 놓지 않음 → 「배치」
///   놓음 → 「재활용」 (손실 없이 목록으로)
/// </summary>
public class BuildingScreenUI : MonoBehaviour
{
    private static BuildingScreenUI instance;

    private GameObject panel;
    private Text titleLabel;
    private Text messageLabel;
    private RectTransform list;

    public static bool IsOpen => instance != null && instance.panel != null && instance.panel.activeSelf;

    public static void Open()
    {
        if (instance == null)
        {
            Canvas canvas = UIFactory.CreateCanvas("BuildingScreenCanvas (Runtime)", 1200);
            instance = canvas.gameObject.AddComponent<BuildingScreenUI>();
            instance.Build(canvas);
        }

        instance.messageLabel.text = string.Empty;
        instance.panel.SetActive(true);
        instance.Redraw();
    }

    public static void Close()
    {
        if (instance != null && instance.panel != null)
            instance.panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build(Canvas canvas)
    {
        RectTransform safe = UIFactory.CreateSafeArea(canvas);

        Image shade = UIFactory.CreatePanel("Shade", safe, new Color(0f, 0f, 0f, 0.45f),
            Vector2.zero, Vector2.one, radius: 0);
        shade.raycastTarget = true;
        panel = shade.gameObject;

        Image box = UIFactory.CreateGlass("BuildingBox", shade.transform, UIPalette.Panel,
            new Vector2(0.16f, 0.10f), new Vector2(0.84f, 0.92f), UIFactory.RadiusLarge);

        titleLabel = UIFactory.CreateLabel(box.transform, "건설", 30, FontStyle.Bold,
            new Vector2(0.04f, 0.90f), new Vector2(0.75f, 0.98f),
            TextAnchor.MiddleLeft, UIPalette.TextOnGlass);

        UIFactory.CreateButton(box.transform, "닫기",
            new Vector2(0.80f, 0.905f), new Vector2(0.96f, 0.975f),
            UIPalette.Subtle, Close, 24);

        messageLabel = UIFactory.CreateLabel(box.transform, string.Empty, 22, FontStyle.Normal,
            new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.08f),
            TextAnchor.MiddleCenter, UIPalette.TextAccent);

        list = UIFactory.CreateRegion("List", box.transform,
            new Vector2(0.03f, 0.09f), new Vector2(0.97f, 0.89f));

        panel.SetActive(false);
    }

    private void Redraw()
    {
        titleLabel.text = $"건설 · {PassiveManager.EnsureInstance().Gold:N0}골드";

        UIFactory.ClearChildren(list);

        IReadOnlyList<BuildingDefinition> all = BuildingTable.All;
        float rowHeight = 1f / Mathf.Max(4, all.Count);

        for (int i = 0; i < all.Count; i++)
        {
            float top = 1f - i * rowHeight;
            DrawRow(all[i], new Vector2(0f, top - rowHeight + 0.01f), new Vector2(1f, top - 0.01f));
        }
    }

    private void DrawRow(BuildingDefinition definition, Vector2 min, Vector2 max)
    {
        BuildingState state = BuildingManager.State;
        BuildError error = BuildingManager.CanBuild(definition);

        bool owned = state.Owns(definition.Id);
        bool placed = state.IsPlaced(definition.Id);
        bool affordable = !owned && error == BuildError.None;

        // 덕코프처럼 【지을 수 있으면 초록】.
        Color rowColor = affordable ? UIPalette.Glassify(UIPalette.Gain, 0.28f) : UIPalette.Row;

        Image row = UIFactory.CreatePanel($"Row_{definition.Id}", list, rowColor, min, max);

        UIFactory.CreateLabel(row.transform, definition.Name, 26, FontStyle.Bold,
            new Vector2(0.03f, 0.62f), new Vector2(0.70f, 0.95f),
            TextAnchor.MiddleLeft, UIPalette.TextOnGlass);

        UIFactory.CreateLabel(row.transform, definition.Description, 18, FontStyle.Normal,
            new Vector2(0.03f, 0.36f), new Vector2(0.72f, 0.62f),
            TextAnchor.MiddleLeft, UIPalette.TextDim);

        UIFactory.CreateLabel(row.transform, owned ? StatusLine(placed) : CostLine(definition),
            18, FontStyle.Normal,
            new Vector2(0.03f, 0.05f), new Vector2(0.72f, 0.36f),
            TextAnchor.MiddleLeft, UIPalette.Text);

        string label;
        Color color;
        UnityEngine.Events.UnityAction action;

        if (!owned)
        {
            label = "짓기";
            color = affordable ? UIPalette.Action : UIPalette.Subtle;
            action = () => OnBuild(definition);
        }
        else if (!placed)
        {
            label = "배치";
            color = UIPalette.Action;
            action = () => OnPlace(definition);
        }
        else
        {
            label = "재활용";
            color = UIPalette.Subtle;
            action = () => OnRecycle(definition);
        }

        UIFactory.CreateButton(row.transform, label,
            new Vector2(0.76f, 0.22f), new Vector2(0.97f, 0.78f), color, action, 26);
    }

    private static string StatusLine(bool placed)
        => placed ? "지음 · 놓여 있음" : "지음 · 놓지 않음 — 「배치」를 누르십시오";

    /// <summary>「100골드 · 쇠붙이 3/6 · 숯 0/1 · 필요: 작업대」. 모자란 것은 붉게.</summary>
    private static string CostLine(BuildingDefinition definition)
    {
        var sb = new StringBuilder();

        int gold = PassiveManager.EnsureInstance().Gold;
        PlayerInventory inventory = PlayerInventory.EnsureInstance();
        ItemCatalog catalog = ItemCatalog.Load();

        if (definition.Gold > 0)
            sb.Append(Colored($"{definition.Gold:N0}골드", gold >= definition.Gold));

        foreach (MaterialCost cost in definition.Materials)
        {
            int have = BuildingState.CountIn(inventory.Stash, inventory.Bag, cost.ItemId);
            string name = catalog != null && catalog.Find(cost.ItemId) != null
                ? catalog.Find(cost.ItemId).DisplayName
                : cost.ItemId;

            if (sb.Length > 0)
                sb.Append(" · ");

            sb.Append(Colored($"{name} {have}/{cost.Count}", have >= cost.Count));
        }

        foreach (string required in definition.RequiredBuildings)
        {
            BuildingDefinition other = BuildingTable.Find(required);
            bool ok = BuildingManager.State.Owns(required);

            sb.Append(" · ");
            sb.Append(Colored($"필요: {(other != null ? other.Name : required)}", ok));
        }

        return sb.ToString();
    }

    private static string Colored(string text, bool ok)
        => ok ? text : $"<color=#EB7361>{text}</color>";

    // ────────────────────────────────── 누르기

    private void OnBuild(BuildingDefinition definition)
    {
        BuildError error = BuildingManager.Build(definition);

        if (error != BuildError.None)
        {
            messageLabel.text = BuildingState.Explain(error);
            return;
        }

        // 값을 치른 순간 저장한다 — 배치 도중에 꺼도 지은 것이 남는다.
        SaveManager.Commit("건설");

        OnPlace(definition);
    }

    private void OnPlace(BuildingDefinition definition)
    {
        Close();

        if (BunkerBuildings.Instance != null)
            BunkerBuildings.Instance.BeginPlacing(definition);
    }

    private void OnRecycle(BuildingDefinition definition)
    {
        BuildingManager.Recycle(definition.Id);
        SaveManager.Commit("건물 재활용");

        messageLabel.text = $"「{definition.Name}」 — 목록으로 돌아왔습니다.";
        Redraw();
    }
}
