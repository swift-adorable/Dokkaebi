using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 칸 옆에 붙는 작은 메뉴. 【누른 칸 바로 옆에서 할 일을 고른다.】
///
/// 【왜 바로 실행하지 않는가】
/// 전리품 칸을 한 번 누르면 바로 줍게 해 뒀었다. 빠르긴 한데,
/// 「줍기」 말고는 아무것도 할 수 없다. 총을 주울지, 탄약만 뺄지,
/// 무엇인지 먼저 볼지를 고를 수 없으면 창이 **버튼 하나짜리 목록**이 된다.
///
/// 【왜 화면 가운데 팝업이 아닌가】
/// 덕코프는 누른 칸 **옆에** 띄운다. 가운데로 띄우면 눈이 칸에서 팝업으로,
/// 다시 칸으로 오가야 한다. 옆에 있으면 「이 칸의 메뉴」라는 것이 자리로 드러난다.
/// 모바일에서는 손가락이 칸 위에 있으므로 이동 거리도 짧다.
///
/// 【무엇을 넣을지는 부르는 쪽이 정한다.】 이 클래스는 자리와 모양만 맡는다.
/// 가방에서 누른 것과 전리품에서 누른 것은 할 수 있는 일이 다르다.
/// </summary>
public class ItemActionMenu : MonoBehaviour
{
    /// <summary>메뉴 한 줄.</summary>
    public readonly struct Entry
    {
        public readonly string Label;
        public readonly Color Color;
        public readonly Action Action;
        public readonly bool Enabled;

        public Entry(string label, Color color, Action action, bool enabled = true)
        {
            Label = label;
            Color = color;
            Action = action;
            Enabled = enabled;
        }
    }

    // ── 치수 (캔버스 픽셀) ────────────────────────────────────────────

    private const float ButtonWidth = 118f;
    private const float ButtonHeight = 54f;
    private const float ButtonGap = 6f;

    /// <summary>칸과 메뉴 사이 틈.</summary>
    private const float CellGap = 8f;

    private static ItemActionMenu instance;

    private GameObject shade;
    private RectTransform box;

    public static bool IsOpen => instance != null && instance.shade != null
                                 && instance.shade.activeSelf;

    private static ItemActionMenu EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<ItemActionMenu>(FindObjectsInactive.Include);

        if (instance != null)
            return instance;

        // 어떤 패널보다도 위. 패널 옆으로 삐져나와야 하므로 잘리면 안 된다.
        Canvas canvas = UIFactory.CreateCanvas("ItemActionMenuCanvas (Runtime)", 1200);

        instance = canvas.gameObject.AddComponent<ItemActionMenu>();
        instance.Build(canvas);

        return instance;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private RectTransform safeArea;

    private void Build(Canvas canvas)
    {
        safeArea = UIFactory.CreateSafeArea(canvas);

        // 바깥을 누르면 닫힌다. 모바일에서 가장 기대되는 동작이다.
        // 보이지 않는 판이라 게임 화면을 가리지 않는다.
        Image blocker = UIFactory.CreatePanel("Shade", safeArea,
            new Color(0f, 0f, 0f, 0.001f), Vector2.zero, Vector2.one, radius: 0);

        shade = blocker.gameObject;

        var shadeButton = shade.AddComponent<Button>();
        shadeButton.targetGraphic = blocker;
        shadeButton.transition = Selectable.Transition.None;
        shadeButton.onClick.AddListener(Close);

        box = UIFactory.CreateRegion("Menu", shade.transform, Vector2.zero, Vector2.zero);
        box.pivot = new Vector2(0f, 1f);

        shade.SetActive(false);
    }

    // ────────────────────────────────── 열고 닫기

    /// <summary>
    /// 칸 옆에 연다.
    /// </summary>
    /// <param name="cell">누른 칸. 이 칸의 오른쪽에 붙인다.</param>
    public static void Open(RectTransform cell, IReadOnlyList<Entry> entries)
    {
        if (cell == null || entries == null || entries.Count == 0)
            return;

        ItemActionMenu menu = EnsureInstance();

        menu.Show(cell, entries);
    }

    public static void Close()
    {
        if (instance != null && instance.shade != null)
            instance.shade.SetActive(false);
    }

    private void Show(RectTransform cell, IReadOnlyList<Entry> entries)
    {
        shade.SetActive(true);

        UIFactory.ClearChildren(box);

        float height = entries.Count * ButtonHeight + (entries.Count - 1) * ButtonGap;

        box.sizeDelta = new Vector2(ButtonWidth, height);

        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];

            GameObject buttonObject = UIFactory.CreateChild($"Action_{i}", box);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, 0f);
            rect.offsetMax = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(0f, ButtonHeight);
            rect.anchoredPosition = new Vector2(0f, -i * (ButtonHeight + ButtonGap));

            var image = buttonObject.AddComponent<Image>();
            image.color = entry.Color;
            image.sprite = UISprites.Rounded(UIFactory.Radius);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = entry.Enabled;

            Action captured = entry.Action;

            button.onClick.AddListener(() =>
            {
                Close();

                captured?.Invoke();
            });

            UIFactory.CreateOutline(image, UIPalette.Rim, UIFactory.Radius, 2);

            UIFactory.CreateLabel(buttonObject.transform, entry.Label, 23, FontStyle.Bold,
                new Vector2(0.06f, 0f), new Vector2(0.94f, 1f), TextAnchor.MiddleCenter);
        }

        Place(cell, height);
    }

    /// <summary>
    /// 칸의 왼쪽 위에 맞춰 놓는다. 화면 밖으로 나가면 반대편으로 넘긴다.
    ///
    /// 【왼쪽을 기본으로 둔다.】 전리품 패널이 화면 오른쪽에 붙어 있어서
    /// 오른쪽에 띄우면 거의 항상 화면 밖이다.
    /// </summary>
    private void Place(RectTransform cell, float height)
    {
        Canvas.ForceUpdateCanvases();

        // 칸의 네 귀퉁이를 화면 좌표로 받는다. 캔버스가 달라도 이 값은 통한다.
        var corners = new Vector3[4];
        cell.GetWorldCorners(corners);

        // 0: 좌하 · 1: 좌상 · 2: 우상 · 3: 우하
        Vector2 leftTop = corners[1];
        Vector2 rightTop = corners[2];

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            safeArea, leftTop, null, out Vector2 localLeft);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            safeArea, rightTop, null, out Vector2 localRight);

        float x = localLeft.x - ButtonWidth - CellGap;
        float y = localLeft.y;

        Rect area = safeArea.rect;

        // 왼쪽이 모자라면 오른쪽으로 넘긴다.
        if (x < area.xMin)
            x = localRight.x + CellGap;

        // 오른쪽도 모자라면 칸 위에 겹쳐 둔다 — 안 보이는 것보다 낫다.
        if (x + ButtonWidth > area.xMax)
            x = area.xMax - ButtonWidth;

        // 아래로 넘치면 위로 올린다.
        if (y - height < area.yMin)
            y = area.yMin + height;

        box.anchoredPosition = new Vector2(x, y);
    }

    // ────────────────────────────────── 차림표 만들기

    /// <summary>
    /// 아이템 성격에 맞는 줄을 고른다. 【컨테이너(전리품·창고·상점) 쪽 칸용.】
    ///
    /// 덕코프의 줄 이름을 그대로 쓴다 — 장비 · 사용 · 탄약 제거.
    /// 「마커」는 넣지 않는다: 위키에 그 버튼의 설명이 없고(확인 불가),
    /// Blob에는 지도가 없어 표시할 곳도 없다.
    /// 「상세보기」는 덕코프에 없지만 더한다 — 모바일은 마우스를 올려
    /// 설명을 볼 수 없어서, 읽을 길이 버튼밖에 없다.
    /// </summary>
    public static List<Entry> ForContainerItem(
        ItemDefinition definition,
        Action take, Action equip, Action use, Action detail)
    {
        var entries = new List<Entry>(4);

        if (definition == null)
            return entries;

        // 가장 흔한 일을 맨 위에. 손가락이 칸에서 가장 가까운 줄이다.
        entries.Add(new Entry("줍기", UIPalette.Action, take));

        // 【바로 착용·사용은 「줍고 나서」가 아니라 그 자리에서 된다.】
        // 덕코프도 그렇다. 가방이 꽉 찼을 때 총 하나를 바꿔 끼우려고
        // 무언가를 먼저 버려야 하면 그게 곧 막힘이다.
        if (equip != null && IsEquipment(definition.Kind))
            entries.Add(new Entry("장착", UIPalette.SlotEquippable, equip));

        if (use != null && definition.Kind == ItemKind.Consumable)
            entries.Add(new Entry("사용", UIPalette.Gain, use));

        entries.Add(new Entry("상세보기", UIPalette.Subtle, detail));

        return entries;
    }

    private static bool IsEquipment(ItemKind kind)
    {
        switch (kind)
        {
            case ItemKind.Weapon:
            case ItemKind.Armour:
            case ItemKind.Backpack:
            case ItemKind.Imprint:
                return true;

            default:
                return false;
        }
    }
}
