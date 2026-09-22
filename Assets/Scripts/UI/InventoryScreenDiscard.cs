using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버리기 팝업. (로드맵 6-P)
///
/// 【왜 팝업인가】
/// 「1개 버리기 / 전부 버리기」 두 버튼으로는 15개 중 7개를 버릴 수 없다.
/// 과중량은 "몇 kg만 덜어내면 되는가"의 문제라 개수를 고를 수 있어야 한다.
///
/// 【왜 확인을 받는가】
/// 땅에 떨어지는 월드 아이템이 아직 없어서 버리기는 곧 삭제다.
/// 한 번의 오터치로 최고 티어 장비가 사라지면 그 판의 검증이 끝난다.
/// 월드 드롭이 생기면 이 팝업은 「몇 개를 내려놓을까」로 뜻만 바뀐다.
///
/// 구성 — 상단: 아이콘·이름·보유 개수 / 중앙: 슬라이더 + 직접 입력 / 하단: 취소·버리기
/// </summary>
public partial class InventoryScreenUI
{
    private GameObject discardPopup;
    private ItemStack discardTarget;
    private Slider discardSlider;
    private InputField discardField;
    private Text discardCountLabel;

    /// <summary>슬라이더와 입력칸이 서로를 다시 부르는 것을 막는다.</summary>
    private bool discardSyncing;

    private int discardAmount = 1;

    private bool IsDiscardOpen => discardPopup != null;

    // ── 열고 닫기 ─────────────────────────────────────────────────────

    private void OpenDiscardPopup(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty || stack.Definition == null)
            return;

        CloseDiscardPopup();
        CloseItemDetail();

        discardTarget = stack;
        discardAmount = stack.Count;

        BuildDiscardPopup();

        // 【덮개 위로 버튼이 솟는 것을 막는다.】
        // 세 버튼은 패널 밖에 있어서 덮개가 가리지 못한다.
        hudSuppressed = true;
        RefreshHudVisibility();
    }

    private void CloseDiscardPopup()
    {
        if (discardPopup != null)
            Destroy(discardPopup);

        discardPopup = null;
        discardTarget = null;
        discardSlider = null;
        discardField = null;
        discardCountLabel = null;

        hudSuppressed = false;
        RefreshHudVisibility();
    }

    // ── 구성 ──────────────────────────────────────────────────────────

    private void BuildDiscardPopup()
    {
        ItemDefinition definition = discardTarget.Definition;

        // 뒷면을 덮는 판. 팝업 밖을 눌러도 아래 UI가 눌리지 않게 막는다.
        // 패널은 바깥 여백만큼 안으로 들어와 있다. 0~1로 두면 그 테두리
        // 한 줄이 덮이지 않아, 그 자리를 누르면 뒤 UI가 눌린다.
        Image shade = UIFactory.CreatePanel("DiscardShade", panel.transform,
            UIPalette.Dim, new Vector2(-0.2f, -0.2f), new Vector2(1.2f, 1.2f));

        shade.raycastTarget = true;

        discardPopup = shade.gameObject;

        // 덮개는 화면 끝까지 넘겼으므로, 팝업 자체는 패널 크기로 되돌린 뒤 잡는다.
        // 그러지 않으면 덮개가 커진 만큼 팝업도 같이 커진다.
        RectTransform frame = UIFactory.CreateRegion("Frame", discardPopup.transform,
            new Vector2(1f / 7f, 1f / 7f), new Vector2(6f / 7f, 6f / 7f));

        RectTransform box = UIFactory.CreateRegion("Box", frame,
            new Vector2(0.30f, 0.26f), new Vector2(0.70f, 0.74f));

        UIFactory.CreatePanel("Back", box, UIPalette.Panel, Vector2.zero, Vector2.one);

        BuildDiscardHeader(box, definition);
        BuildDiscardAmount(box);
        BuildDiscardFooter(box);

        SyncDiscardWidgets(discardAmount);
    }

    /// <summary>상단 — 아이콘 · 이름 · 보유 개수.</summary>
    private void BuildDiscardHeader(RectTransform box, ItemDefinition definition)
    {
        UIFactory.CreatePanel("Header", box, UIPalette.Header,
            new Vector2(0f, 0.74f), new Vector2(1f, 1f));

        Image icon = UIFactory.CreatePanel("Icon", box, UIPalette.ForItem(definition.Kind),
            new Vector2(0.05f, 0.775f), new Vector2(0.22f, 0.965f));

        // 아트가 들어오면 색 판 대신 그린다. 지금은 종류 색이 아이콘 역할을 한다.
        if (definition.Icon != null)
        {
            icon.sprite = definition.Icon;
            icon.color = Color.white;
            icon.preserveAspect = true;
        }

        UIFactory.CreateLabel(box, definition.DisplayName, 37, FontStyle.Bold,
            new Vector2(0.26f, 0.87f), new Vector2(0.95f, 0.965f), TextAnchor.LowerLeft);

        UIFactory.CreateLabel(box,
            $"보유 {discardTarget.Count}개    {definition.Weight * discardTarget.Count:0.0} kg",
            28, FontStyle.Normal,
            new Vector2(0.26f, 0.775f), new Vector2(0.95f, 0.865f), TextAnchor.UpperLeft,
            UIPalette.TextAccent);
    }

    /// <summary>중앙 — 0 ~ 보유 개수 슬라이더와 직접 입력칸.</summary>
    private void BuildDiscardAmount(RectTransform box)
    {
        UIFactory.CreateLabel(box, "버릴 개수", 28, FontStyle.Normal,
            new Vector2(0.05f, 0.58f), new Vector2(0.60f, 0.68f), TextAnchor.MiddleLeft,
            UIPalette.TextDim);

        discardCountLabel = UIFactory.CreateLabel(box, string.Empty, 28, FontStyle.Bold,
            new Vector2(0.60f, 0.58f), new Vector2(0.95f, 0.68f), TextAnchor.MiddleRight,
            UIPalette.TextAccent);

        discardSlider = UIFactory.CreateIntSlider(box,
            new Vector2(0.05f, 0.38f), new Vector2(0.68f, 0.56f),
            0, discardTarget.Count, discardAmount);

        discardSlider.onValueChanged.AddListener(v => SyncDiscardWidgets((int)v));

        discardField = UIFactory.CreateIntField(box,
            new Vector2(0.74f, 0.38f), new Vector2(0.95f, 0.56f), discardAmount);

        // onEndEdit이 아니라 onValueChanged를 쓴다 — 치는 동안 슬라이더가 따라와야
        // "지금 몇 개를 버리려는 중인지"가 한눈에 보인다.
        discardField.onValueChanged.AddListener(HandleDiscardFieldChanged);
    }

    /// <summary>하단 — 취소 · 버리기.</summary>
    private void BuildDiscardFooter(RectTransform box)
    {
        UIFactory.CreateButton(box, "취소",
            new Vector2(0.05f, 0.06f), new Vector2(0.48f, 0.22f),
            UIPalette.Subtle, CloseDiscardPopup);

        UIFactory.CreateButton(box, "버리기",
            new Vector2(0.52f, 0.06f), new Vector2(0.95f, 0.22f),
            UIPalette.Warning, ConfirmDiscard);
    }

    // ── 값 동기화 ─────────────────────────────────────────────────────

    private void HandleDiscardFieldChanged(string raw)
    {
        // 빈 칸은 지우는 중이다. 0으로 되돌리면 글자를 못 친다.
        if (string.IsNullOrEmpty(raw))
            return;

        if (!int.TryParse(raw, out int parsed))
            return;

        SyncDiscardWidgets(parsed);
    }

    private void SyncDiscardWidgets(int amount)
    {
        if (discardSyncing || discardTarget == null)
            return;

        discardSyncing = true;

        discardAmount = Mathf.Clamp(amount, 0, discardTarget.Count);

        if (discardSlider != null)
            discardSlider.SetValueWithoutNotify(discardAmount);

        if (discardField != null && discardField.text != discardAmount.ToString())
            discardField.SetTextWithoutNotify(discardAmount.ToString());

        if (discardCountLabel != null)
        {
            float weight = discardTarget.Definition.Weight * discardAmount;

            discardCountLabel.text = $"{discardAmount} / {discardTarget.Count}    −{weight:0.0} kg";
        }

        discardSyncing = false;
    }

    // ── 실행 ──────────────────────────────────────────────────────────

    private void ConfirmDiscard()
    {
        if (discardTarget == null || discardAmount <= 0)
        {
            CloseDiscardPopup();
            return;
        }

        ItemStack target = discardTarget;
        int amount = discardAmount;
        string name = target.Definition.DisplayName;

        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        // 통째로 버리는 경우와 개수만 줄이는 경우를 나눈다.
        // Take만 쓰면 0개짜리 빈 칸이 남는다.
        if (amount >= target.Count)
        {
            bag.RemoveStack(target);
            selected = null;
        }
        else
        {
            target.Take(amount);
        }

        CloseDiscardPopup();

        PlayerInventory.Instance.RefreshCapacity();

        selectedSlot = null;

        ShowToast($"「{name}」 {amount}개를 버렸습니다.");

        Refresh();
    }
}
