using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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

    /// <summary>지금 메뉴가 붙어 있는 칸. 같은 칸을 다시 누르면 닫는다.</summary>
    private RectTransform owner;

    /// <summary>
    /// 방금 이 칸 때문에 닫혔다 — 이번 프레임에는 다시 열지 않는다.
    ///
    /// 【토글이 안 먹던 이유】 덮개가 클릭을 뒤로 넘기는 구조라,
    /// 같은 칸을 눌러도 「닫기 → 그 칸이 다시 연다」가 한 프레임에 일어났다.
    /// 닫기 직전의 주인을 기억해 두고, 그 클릭이 낳은 열기 요청만 흘려보낸다.
    /// </summary>
    private static RectTransform suppressed;
    private static int suppressedFrame = -1;

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

    /// <summary>
    /// 메뉴 뒤를 덮는 판. 【누르면 메뉴를 닫고, 그 클릭을 뒤로 넘긴다.】
    ///
    /// 그냥 Button으로 두면 클릭이 여기서 끝난다. 메뉴가 떠 있는 동안
    /// 옆 칸을 누르면 닫히기만 하고, 그 칸을 다시 눌러야 메뉴가 떴다.
    /// 아이템을 하나씩 훑어보는 조작이 두 배로 느려진다.
    ///
    /// 닫은 뒤 같은 자리를 다시 레이캐스트해서, 밑에 있던 것에게
    /// 클릭을 그대로 전달한다. 칸이었으면 그 칸의 메뉴가 바로 뜨고,
    /// 빈 곳이었으면 아무 일도 일어나지 않는다 — 닫히기만 한다.
    /// </summary>
    private class PassThroughShade : MonoBehaviour, IPointerClickHandler
    {
        private static readonly List<RaycastResult> Results = new();

        public void OnPointerClick(PointerEventData eventData)
        {
            // 닫기 직전의 주인을 기억한다. 그 칸이 이 클릭으로 다시 열려고 하면
            // 「같은 칸을 다시 누른 것」이므로 열지 않는다 — 그것이 토글이다.
            RectTransform previous = instance != null ? instance.owner : null;

            // 먼저 닫는다. 이 판이 꺼져야 아래가 레이캐스트에 잡힌다.
            Close();

            suppressed = previous;
            suppressedFrame = Time.frameCount;

            if (EventSystem.current == null)
                return;

            Results.Clear();

            EventSystem.current.RaycastAll(eventData, Results);

            for (int i = 0; i < Results.Count; i++)
            {
                GameObject target = Results[i].gameObject;

                if (target == null)
                    continue;

                // 맨 위에 있는 것 하나에만 넘긴다. 여럿에게 주면
                // 겹쳐 있는 패널이 한 번의 터치로 둘 다 반응한다.
                ExecuteEvents.ExecuteHierarchy(
                    target, eventData, ExecuteEvents.pointerClickHandler);

                break;
            }

            // 넘긴 클릭이 끝났다. 다음 클릭은 평소대로 열린다.
            suppressed = null;
        }
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
        GamePause.Register(shade);   // 떠 있는 동안 게임이 멈춘다 (결정 2-92)

        // 【닫고 나서 그 클릭을 뒤로 넘긴다.】 Button으로 두면 클릭이 여기서
        // 끝나 버려서, 메뉴가 떠 있는 동안 다른 칸을 눌러도 닫히기만 했다.
        // 두 번 눌러야 옆 칸의 메뉴가 뜨는 것은 목록을 훑는 조작을 막는다.
        shade.AddComponent<PassThroughShade>();

        // 【앵커를 한가운데에 둔다.】
        // ScreenPointToLocalPointInRectangle이 돌려주는 값은 **부모 한가운데를
        // 원점으로 하는 좌표**다. 앵커가 왼쪽 아래(0,0)면 그 값을 그대로
        // anchoredPosition에 넣는 순간 화면 절반만큼 어긋난다 —
        // 메뉴가 엉뚱한 곳에 뜨던 원인이 이것이었다.
        box = UIFactory.CreateRegion("Menu", shade.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        // 왼쪽 위 모서리를 기준점으로 삼는다. 칸의 위쪽에 맞춰 내려 그린다.
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

        // 【같은 칸을 다시 누르면 닫는다.】 열고 닫는 데 쓰는 버튼이
        // 칸 자신이면, 메뉴를 치우려고 빈 곳을 찾을 필요가 없다.
        //
        // 덮개가 이미 닫은 뒤에 이 호출이 온다. 그래서 IsOpen이 아니라
        // 「방금 이 칸 때문에 닫혔는가」를 본다.
        if (cell == suppressed && Time.frameCount == suppressedFrame)
        {
            suppressed = null;
            return;
        }

        ItemActionMenu menu = EnsureInstance();

        if (IsOpen && menu.owner == cell)
        {
            Close();
            return;
        }

        menu.Show(cell, entries);
    }

    public static void Close()
    {
        if (instance == null)
            return;

        instance.owner = null;

        if (instance.shade != null)
            instance.shade.SetActive(false);
    }

    private void Show(RectTransform cell, IReadOnlyList<Entry> entries)
    {
        owner = cell;

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
    /// 오른쪽에 띄우면 거의 항상 화면 밖이다. 왼쪽이 모자라면 오른쪽으로,
    /// 그것도 모자라면 화면 안으로 끌어당긴다 — 안 보이는 것보다 겹치는 편이 낫다.
    ///
    /// 좌표는 전부 **부모(shade) 한가운데를 원점으로 하는 국소 좌표**다.
    /// box의 앵커도 한가운데라 값을 그대로 넣으면 된다. 원점이 어긋나면
    /// 메뉴가 화면 절반만큼 밀려난다.
    /// </summary>
    private void Place(RectTransform cell, float height)
    {
        Canvas.ForceUpdateCanvases();

        var parent = (RectTransform)box.parent;

        // 칸의 네 귀퉁이를 화면 좌표로 받는다. 캔버스가 달라도 이 값은 통한다.
        var corners = new Vector3[4];
        cell.GetWorldCorners(corners);

        // 0: 좌하 · 1: 좌상 · 2: 우상 · 3: 우하
        Camera camera = CameraFor(cell);

        Vector2 leftTop = RectTransformUtility.WorldToScreenPoint(camera, corners[1]);
        Vector2 rightTop = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);

        Camera self = CameraFor(parent);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent, leftTop, self, out Vector2 localLeft);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent, rightTop, self, out Vector2 localRight);

        float x = localLeft.x - ButtonWidth - CellGap;
        float y = localLeft.y;

        Rect area = parent.rect;

        // 왼쪽이 모자라면 오른쪽으로 넘긴다.
        if (x < area.xMin)
            x = localRight.x + CellGap;

        // 오른쪽도 모자라면 화면 안으로 끌어당긴다.
        if (x + ButtonWidth > area.xMax)
            x = area.xMax - ButtonWidth;

        x = Mathf.Max(x, area.xMin);

        // 아래로 넘치면 위로 올리고, 위로 넘치면 내린다.
        y = Mathf.Min(y, area.yMax);
        y = Mathf.Max(y, area.yMin + height);

        box.anchoredPosition = new Vector2(x, y);
    }

    /// <summary>
    /// 이 사각형이 속한 캔버스의 카메라. Overlay 캔버스는 null이어야 하고,
    /// Camera·World 캔버스는 그 카메라를 넘겨야 좌표가 맞는다.
    /// </summary>
    private static Camera CameraFor(RectTransform rect)
    {
        Canvas canvas = rect.GetComponentInParent<Canvas>();

        if (canvas == null)
            return null;

        canvas = canvas.rootCanvas;

        return canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;
    }

    // ────────────────────────────────── 사이드 메뉴 만들기

    /// <summary>
    /// 【상자(전리품·창고·상점) 쪽 칸.】 아직 내 물건이 아니다.
    ///
    /// 덕코프의 줄 이름을 그대로 쓴다 — 줍기 · 장비 · 사용.
    /// 「마커」는 넣지 않는다: 위키에 그 버튼의 설명이 없고(확인 불가),
    /// Dokkaebi에는 지도가 없어 표시할 곳도 없다. (지도가 생기면 다시 본다)
    /// 「상세보기」는 덕코프에 없지만 더한다 — 모바일은 올려놓아 설명을
    /// 볼 수 없어서, 읽을 길이 버튼밖에 없다.
    /// </summary>
    public static List<Entry> ForContainerItem(
        ItemDefinition definition,
        Action take, Action equip, Action use, Action detail, string takeLabel = "줍기")
    {
        var entries = new List<Entry>(4);

        if (definition == null)
            return entries;

        // 가장 흔한 일을 맨 위에. 손가락이 칸에서 가장 가까운 줄이다.
        entries.Add(new Entry(takeLabel, UIPalette.Action, take));

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

    /// <summary>
    /// 【가방 칸.】 내 물건이다 — 끼우고, 걸고, 버릴 수 있다.
    ///
    /// 「상세보기」를 맨 아래에 두는 이유 — 가방에 있는 것은 대개 이미
    /// 무엇인지 안다. 자주 하는 일이 위로 온다.
    /// </summary>
    public static List<Entry> ForBagItem(
        ItemDefinition definition,
        Action equip, Action use, Action quick, Action discard, Action detail,
        Entry? exchange = null)
    {
        var entries = new List<Entry>(5);

        if (definition == null)
            return entries;

        // 【창고 · 상점이 열려 있으면 그 일이 맨 위다.】 그 패널을 연 이유가 그것이다.
        if (exchange.HasValue)
            entries.Add(exchange.Value);

        if (equip != null && (IsEquipment(definition.Kind) || definition.IsSkillGem))
            entries.Add(new Entry("장착", UIPalette.Action, equip));

        if (use != null && definition.Kind == ItemKind.Consumable)
            entries.Add(new Entry("사용", UIPalette.Gain, use));

        if (quick != null && definition.Kind == ItemKind.Consumable)
            entries.Add(new Entry("퀵슬롯", UIPalette.SlotEquippable, quick));

        entries.Add(new Entry("상세보기", UIPalette.Subtle, detail));

        // 되돌릴 수 없는 일은 맨 아래. 손가락이 가장 먼 줄이다.
        if (discard != null)
            entries.Add(new Entry("버리기", UIPalette.Warning, discard));

        return entries;
    }

    /// <summary>
    /// 【착용 중인 장비 슬롯 · 꽂힌 젬.】 벗는 것과 보는 것만 할 수 있다.
    /// </summary>
    public static List<Entry> ForEquippedItem(string removeLabel, Action remove, Action detail)
    {
        return new List<Entry>(2)
        {
            new Entry(removeLabel, UIPalette.Action, remove),
            new Entry("상세보기", UIPalette.Subtle, detail)
        };
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
