using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전리품 창 — 시체·상자를 파밍하면 열린다. 【원하는 것만 집는다.】
///
/// 이전에는 흡수 즉시 전부 가방에 들어갔다. 그러면 추출 루팅의 핵심 결정인
/// 「무엇을 들고 갈 것인가」가 사라진다. 가방은 유한하고 무게는 발을 묶는다.
/// 무엇을 두고 갈지 고르는 순간이 이 장르의 본체다.
///
/// 화면 구성 (덕코프 배치 차용, 조작은 터치)
///   우측 패널 : 「전리품 (n/8)」 · 5×2 격자
///   칸을 누르면 그 아래 설명 카드가 뜬다 — 이름 · 무게 · 설명 · 가치
///   버튼      : 줍기 / 전부 줍기 / 닫기
///   하단      : 다 들었을 때의 무게를 미리 보여 준다
/// </summary>
public class LootWindowUI : MonoBehaviour
{
    private const int Columns = 5;
    private const int Rows = 2;

    private static LootWindowUI instance;

    private GameObject panel;
    private Text titleLabel;
    private Text weightLabel;
    private RectTransform grid;
    private RectTransform detail;
    private Button takeButton;

    private CorpseController source;
    private LootContainer loot;
    private int selectedIndex = -1;

    public bool IsOpen => panel != null && panel.activeSelf;

    /// <summary>창을 보장한다. 씬 배치를 강제하지 않는다.</summary>
    public static LootWindowUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<LootWindowUI>(FindObjectsInactive.Include);

        if (instance != null)
            return instance;

        Canvas canvas = UIFactory.CreateCanvas("LootWindowCanvas (Runtime)", 1100);

        instance = canvas.gameObject.AddComponent<LootWindowUI>();
        instance.Build(canvas.transform);

        return instance;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    // ────────────────────────────────── 생성

    private void Build(Transform parent)
    {
        panel = UIFactory.CreateRegion("Panel", parent, Vector2.zero, Vector2.one).gameObject;

        // 바깥을 눌러도 닫히게 한다. 모바일에서 닫기 버튼만 두면 답답하다.
        Image dim = UIFactory.CreatePanel("Dim", panel.transform, UIPalette.Dim,
            Vector2.zero, Vector2.one);

        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.targetGraphic = dim;
        dimButton.onClick.AddListener(Close);

        // 우측 패널. 스크린샷과 같은 자리다.
        Image window = UIFactory.CreatePanel("Window", panel.transform, UIPalette.Panel,
            new Vector2(0.58f, 0.18f), new Vector2(0.97f, 0.88f));

        UIFactory.CreatePanel("Header", window.transform, UIPalette.Header,
            new Vector2(0f, 0.90f), new Vector2(1f, 1f));

        titleLabel = UIFactory.CreateLabel(window.transform, "전리품", 36, FontStyle.Bold,
            new Vector2(0.04f, 0.90f), new Vector2(0.96f, 1f), TextAnchor.MiddleLeft);

        grid = UIFactory.CreateRegion("Grid", window.transform,
            new Vector2(0.03f, 0.56f), new Vector2(0.97f, 0.88f));

        detail = UIFactory.CreateRegion("Detail", window.transform,
            new Vector2(0.03f, 0.19f), new Vector2(0.97f, 0.53f));

        weightLabel = UIFactory.CreateLabel(window.transform, string.Empty, 24, FontStyle.Normal,
            new Vector2(0.03f, 0.13f), new Vector2(0.97f, 0.18f),
            TextAnchor.MiddleLeft, UIPalette.TextDim);

        takeButton = UIFactory.CreateButton(window.transform, "줍기",
            new Vector2(0.03f, 0.03f), new Vector2(0.35f, 0.12f),
            UIPalette.Action, TakeSelected);

        UIFactory.CreateButton(window.transform, "전부 줍기",
            new Vector2(0.37f, 0.03f), new Vector2(0.69f, 0.12f),
            UIPalette.Action, TakeAll);

        UIFactory.CreateButton(window.transform, "닫기",
            new Vector2(0.71f, 0.03f), new Vector2(0.97f, 0.12f),
            UIPalette.Subtle, Close);

        panel.SetActive(false);
    }

    // ────────────────────────────────── 열고 닫기

    public void Open(CorpseController corpse)
    {
        if (corpse == null)
            return;

        source = corpse;
        loot = corpse.Loot;
        selectedIndex = -1;

        Refresh();

        panel.SetActive(true);

        if (GameManager.HasInstance)
            GameManager.Instance.OpenSkill();
    }

    public void Close()
    {
        panel.SetActive(false);

        // 다 집었으면 시체를 정리한다. 남아 있으면 그대로 두어
        // 나중에 돌아와 마저 집을 수 있게 한다.
        if (source != null && loot != null && loot.IsEmpty)
            DespawnSource();

        source = null;
        loot = null;
        selectedIndex = -1;

        if (GameManager.HasInstance)
            GameManager.Instance.CloseSkill();
    }

    private void DespawnSource()
    {
        var absorber = FindAnyObjectByType<PlayerAbsorber>(FindObjectsInactive.Include);

        if (absorber != null)
            absorber.Despawn(source);
    }

    // ────────────────────────────────── 다시 그리기

    private void Refresh()
    {
        if (loot == null)
            return;

        titleLabel.text = $"전리품 ({loot.UsedSlots}/{loot.Capacity})";

        BuildGrid();
        BuildDetail();
        RefreshWeight();

        takeButton.interactable = selectedIndex >= 0 && loot.Get(selectedIndex) != null;
    }

    private void BuildGrid()
    {
        UIFactory.ClearChildren(grid);

        int cells = Mathf.Min(loot.Capacity, Columns * Rows);

        for (int i = 0; i < cells; i++)
        {
            UIFactory.GetCellAnchors(i, Columns, Rows, 0.008f,
                out Vector2 min, out Vector2 max);

            ItemStack stack = loot.Get(i);

            Color color = stack == null
                ? UIPalette.Slot
                : i == selectedIndex
                    ? UIPalette.SlotSelected
                    : UIPalette.ForItem(stack.Definition.Kind);

            Image cell = UIFactory.CreatePanel($"Slot_{i}", grid, color, min, max);

            var button = cell.gameObject.AddComponent<Button>();
            button.targetGraphic = cell;
            button.interactable = stack != null;

            int captured = i;
            button.onClick.AddListener(() => Select(captured));

            if (stack == null)
                continue;

            UIFactory.CreateLabel(cell.transform, stack.Definition.DisplayName, 22,
                FontStyle.Bold, new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.94f),
                TextAnchor.UpperLeft);

            if (stack.Count > 1)
            {
                UIFactory.CreateLabel(cell.transform, stack.Count.ToString(), 24,
                    FontStyle.Bold, new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.22f),
                    TextAnchor.LowerRight, UIPalette.TextAccent);
            }
        }
    }

    /// <summary>선택한 칸의 설명 카드. 무게를 반드시 보여 준다 — 들지 말지의 근거다.</summary>
    private void BuildDetail()
    {
        UIFactory.ClearChildren(detail);

        ItemStack stack = selectedIndex >= 0 ? loot.Get(selectedIndex) : null;

        if (stack == null)
        {
            UIFactory.CreateLabel(detail, "칸을 눌러 무엇인지 확인하십시오.", 26,
                FontStyle.Normal, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter,
                UIPalette.TextDim);
            return;
        }

        ItemDefinition definition = stack.Definition;

        UIFactory.CreatePanel("Card", detail, UIPalette.Header, Vector2.zero, Vector2.one);

        UIFactory.CreateLabel(detail, definition.DisplayName, 32, FontStyle.Bold,
            new Vector2(0.04f, 0.76f), new Vector2(0.96f, 0.96f), TextAnchor.MiddleLeft);

        float totalWeight = definition.Weight * stack.Count;

        UIFactory.CreateLabel(detail, $"{totalWeight:0.0} kg", 24, FontStyle.Normal,
            new Vector2(0.04f, 0.62f), new Vector2(0.96f, 0.76f), TextAnchor.MiddleLeft,
            UIPalette.TextAccent);

        UIFactory.CreateLabel(detail, definition.Description, 24, FontStyle.Normal,
            new Vector2(0.04f, 0.22f), new Vector2(0.96f, 0.60f), TextAnchor.UpperLeft,
            UIPalette.TextDim);

        string footer = definition.IsSkillGem && definition.Skill != null
            ? $"인자 · {definition.Skill.Category}  Lv{definition.Skill.RequiredLevel}"
            : $"가치 {definition.BaseValue * stack.Count}";

        UIFactory.CreateLabel(detail, footer, 24, FontStyle.Normal,
            new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.20f), TextAnchor.MiddleLeft,
            UIPalette.TextAccent);
    }

    /// <summary>다 들었을 때의 무게를 미리 보여 준다. 추출 판단의 근거다.</summary>
    private void RefreshWeight()
    {
        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        float now = bag.TotalWeight;
        float after = now + loot.TotalWeight;

        bool willOverload = after > bag.WeightLimit;

        weightLabel.text =
            $"소지 {now:0.0} / {bag.WeightLimit:0.0} kg" +
            $"    다 들면 {after:0.0} kg" +
            (willOverload ? "  (과중량)" : string.Empty) +
            $"    빈 칸 {bag.FreeSlots}";

        weightLabel.color = willOverload ? UIPalette.Warning : UIPalette.TextDim;
    }

    // ────────────────────────────────── 조작

    private void Select(int index)
    {
        selectedIndex = selectedIndex == index ? -1 : index;

        Refresh();
    }

    private void TakeSelected()
    {
        if (loot == null || selectedIndex < 0)
            return;

        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        if (!loot.TryTakeTo(selectedIndex, bag))
        {
            // 가방에 자리가 없으면 아이템은 전리품 칸에 그대로 남는다. 사라지지 않는다.
            GameLogger.Log("[LootWindowUI] 가방에 자리가 없습니다.");
            return;
        }

        selectedIndex = -1;

        Refresh();
    }

    private void TakeAll()
    {
        if (loot == null)
            return;

        int moved = loot.TakeAllTo(PlayerInventory.EnsureInstance().Bag);

        GameLogger.Log($"[LootWindowUI] {moved}칸 회수");

        selectedIndex = -1;

        // 다 집었으면 창을 닫는다. 빈 창을 보고 있을 이유가 없다.
        if (loot.IsEmpty)
        {
            Close();
            return;
        }

        Refresh();
    }
}
