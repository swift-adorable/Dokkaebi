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
///
/// 【틀은 가게와 같다】 (결정 2-123 · StationFrame) — 왼쪽 목록, 가운데 영감(본문 대사), 오른쪽 내 가방.
/// </summary>
public class BuildingScreenUI : MonoBehaviour
{
    private static BuildingScreenUI instance;

    private const float RowHeight = 176f;
    private const float RowGap = 8f;

    private StationFrame frame;
    private Text titleLabel;
    private Text messageLabel;
    private RectTransform list;

    public static bool IsOpen => instance != null && instance.frame != null && instance.frame.IsOpen;

    public static void Open()
    {
        StoryManager.ShowSystemNoteOnce(StoryManager.NoteBuilding);   // 결정 2-112

        if (instance == null)
        {
            Canvas canvas = UIFactory.CreateCanvas("BuildingScreenCanvas (Runtime)", 1200);
            instance = canvas.gameObject.AddComponent<BuildingScreenUI>();
            instance.Build(canvas);
        }

        instance.messageLabel.text = string.Empty;
        instance.StartCoroutine(instance.frame.Show(instance.Redraw));
    }

    public static void Close()
    {
        if (instance != null && instance.frame != null)
            instance.frame.Hide();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build(Canvas canvas)
    {
        frame = StationFrame.Build(canvas, Close);
        frame.UseCounterAsNote(true);
        frame.SetKeeper(KeeperTable.Of(Station.Building));
        titleLabel = frame.Title;
        messageLabel = frame.Status;
        list = frame.List.Content;
    }

    private void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    private void Redraw()
    {
        if (!frame.IsOpen)
            return;

        ItemActionMenu.Close();
        titleLabel.text = "건설";
        frame.SetGold(PassiveManager.EnsureInstance().Gold);
        frame.DrawTabs(new[] { "설계도" }, 0, null);
        frame.DrawBag(null, StationFrame.DetailOnly);
        frame.CounterNote.text = "재료는 창고와 가방에서 함께 센다";

        UIFactory.ClearChildren(list);

        IReadOnlyList<BuildingDefinition> all = BuildingTable.All;
        list.sizeDelta = new Vector2(0f, all.Count * (RowHeight + RowGap));
        float rowHeight = 1f / Mathf.Max(1, all.Count);

        for (int i = 0; i < all.Count; i++)
        {
            float top = 1f - i * rowHeight;
            DrawRow(all[i], new Vector2(0f, top - rowHeight), new Vector2(1f, top));
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
        Color rowColor = affordable ? UIPalette.Glassify(UIPalette.Gain, 0.28f) : StationFrame.RowColor;

        Image row = UIFactory.CreatePanel($"Row_{definition.Id}", list, rowColor, min, max);
        row.rectTransform.offsetMin = new Vector2(4f, RowGap * 0.5f);
        row.rectTransform.offsetMax = new Vector2(-4f, -RowGap * 0.5f);
        UIFactory.CreateOutline(row, StationFrame.RowEdge, UIFactory.Radius, 1);

        UIFactory.CreateLabel(row.transform, definition.Name, 24, FontStyle.Bold,
            new Vector2(0.04f, 0.72f), new Vector2(0.74f, 0.95f),
            TextAnchor.MiddleLeft, UIPalette.TextOnGlass);

        UIFactory.CreateLabel(row.transform, definition.Description, 17, FontStyle.Normal,
            new Vector2(0.04f, 0.30f), new Vector2(0.74f, 0.72f),
            TextAnchor.UpperLeft, UIPalette.TextDim);

        Text cost = UIFactory.CreateLabel(row.transform, owned ? StatusLine(placed) : CostLine(definition),
            17, FontStyle.Normal,
            new Vector2(0.04f, 0.05f), new Vector2(0.74f, 0.30f),
            TextAnchor.MiddleLeft, UIPalette.Text);
        cost.supportRichText = true;

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

        StationFrame.Pill(row.transform, label,
            new Vector2(0.77f, 0.32f), new Vector2(0.97f, 0.68f), color, action, 24);
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
            sb.Append(Colored($"{definition.Gold:N0}엽전", gold >= definition.Gold));

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
