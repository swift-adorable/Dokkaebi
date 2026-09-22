using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 가방 화면 — 장비 · 가방 · 젬 소켓 · 패시브를 한 화면의 탭으로 묶는다.
/// 배치는 덕코프 스크린샷을 따르고, 【조작은 전부 터치】다.
///
/// 화면 구성
///   상단 중앙 : 탭 (장비 / 스킬 / 패시브)
///   좌상단    : 크레딧
///   장비 탭   : 장비 8슬롯 · 가방 격자 (폭 전체)
///   스킬 탭   : 좌 스킬 젬 목록(분류별) · 우 소켓판
///   패시브 탭 : 계열 트리 (폭 전체)
///   하단 중앙 : 퀵슬롯 1~8
///   하단 좌   : 소지 중량 막대
///
/// 【무엇을 누르든 상세가 뜬다.】
/// 가방의 아이템 · 착용 중인 장비 · 소켓에 꽂힌 젬 — 무엇을 누르든
/// 화면 가운데에 같은 모양의 상세가 뜨고, 장착 · 해제 · 버리기를 거기서 한다.
/// 전에는 「고르고 → 밝아진 자리를 누른다」라는 두 단계였는데,
/// 어디를 눌러야 하는지 화면 아래 한 줄로만 알려 줘서 매번 읽어야 했다.
/// 모바일에는 호버가 없으니 「누르면 그 자리에서 다 한다」가 가장 짧다.
/// </summary>
public partial class InventoryScreenUI : MonoBehaviour
{
    private enum Tab { Bag = 0, Socket = 1, Passive = 2 }

    private static readonly string[] TabNames = { "장비", "스킬", "패시브" };

    // ── 레이아웃 ──────────────────────────────────────────────────────
    // 값은 전부 【안전 영역 기준】의 0~1이다. 노치와 홈 인디케이터는
    // SafeAreaFitter가 이미 잘라 냈으므로 여기서는 화면 전체를 쓴다고 생각해도 된다.
    // 한 곳에 모아 두는 이유 — 흩어져 있으면 한 줄만 옮겨도 겹치는지 알 수 없다.

    // ── 세로 세 줄 ────────────────────────────────────────────────────
    // 위에서부터 탭줄 · 본문 · 아래줄. 값은 【패널 기준】 0~1이다.
    //
    // 【줄 사이 간격을 여기서 빼지 않는 이유】
    // 이전에는 0.90 다음을 0.875에서 시작하는 식으로 경계마다 다른 숫자를
    // 빼 두었다. 그래서 위 간격 0.025 · 아래 간격 0.030처럼 제각각이 됐고,
    // 바깥 테두리는 아예 0이라 줄이 화면 끝에 붙었다.
    // 이제 경계는 맞닿게 두고, 양쪽이 UIFactory.Gap의 절반씩 물러난다.
    // 어느 경계에서나 간격이 정확히 한 칸이고, 바깥도 한 칸이다.
    //
    // 【안내줄(네 번째 줄)을 없앴다.】
    // 본문과 아래줄 사이에 「"…" 착용」 한 줄을 띄우려고 4.2%를 늘 비워 뒀다.
    // 대부분의 순간에는 아무 글자도 없어서 그냥 빈 띠였고, 글자가 있을 때도
    // 이미 끝난 일을 알려 줄 뿐이었다. 꼭 알려야 하는 실패만 잠깐 뜨는
    // 알림으로 바꾸고(ShowToast), 그 줄은 본문이 가져간다.

    // 【상단 줄을 없앴다.】
    // 크레딧 하나 때문에 화면 위 10%를 통째로 비워 두고 있었다.
    // 크레딧은 패널 안 머리글 오른쪽으로 들어갔고, 본문이 그 자리를 가져간다.
    private const float ColumnTop = 1f;
    private const float ColumnBottom = 0.10f;
    private const float FooterTop = ColumnBottom;

    /// <summary>
    /// 장비·스킬 화면이 차지하는 폭.
    ///
    /// 【화면을 다 덮지 않는다.】
    /// 가방을 열었다고 바깥이 사라지면, 지금 어디에 서 있고 무엇이 다가오는지가
    /// 보이지 않는다. 추출 루팅에서 가방을 여는 순간은 대개 안전하지 않은
    /// 순간이라 「짐을 보면서 바깥도 본다」가 성립해야 한다.
    ///
    /// 0.55에서 0.38로 줄인다 — 절반이 넘으면 「한쪽에 치우쳤다」로 읽히지 않는다.
    /// 그만큼 칸이 좁아지므로 긴 이름은 말줄임으로 자른다.
    /// </summary>
    private const float SidePanelRight = 0.38f;

    /// <summary>패시브 화면만 폭을 다 쓴다. 트리를 접으면 볼 수가 없다.</summary>
    private const float ColumnSplit = 0.5f;

    /// <summary>
    /// 중량 막대가 카드 안에서 차지하는 가로 범위.
    ///
    /// 【글자 자리와 겹치지 않게 끊는다.】
    /// 0.085에서 시작하던 막대가 「소지 중량」 글자(0.045~0.20) 위로
    /// 올라와 글자를 반쯤 덮고 있었다. 세 칸은 서로 넘지 않는다.
    ///   0.04~0.22 글자 · 0.25~0.62 막대 · 0.64~0.96 숫자
    /// </summary>
    private const float WeightBarLeft = 0.27f;
    private const float WeightBarRight = 0.60f;

    /// <summary>중량 막대의 위아래 여백. 아래줄 높이 기준 비율이다.</summary>
    private const float WeightBarInset = 0.24f;

    /// <summary>
    /// 가방 격자의 열 수.
    ///
    /// 장비 탭은 폭을 다 쓰므로 여덟 열, 스킬 탭은 절반이라 여섯 열이다.
    /// 열 수를 고정해 두면 넓은 쪽에서 칸이 주먹만 해진다.
    /// </summary>
    /// <summary>
    /// 가방 격자의 열 수.
    ///
    /// 일곱 열은 칸이 너무 잘아 이름이 두 글자도 안 들어갔다.
    /// 다섯 열이면 한 칸이 140px 남짓이라 이름이 읽힌다.
    /// </summary>
    private const int BagColumns = 5;

    /// <summary>
    /// 한 번에 보이는 행. 【칸 크기의 기준】이다.
    ///
    /// 용량이 늘면 칸이 작아지는 것이 아니라 내용물이 길어지고 스크롤이 생긴다.
    /// 6-N 이후 실사용 상한이 65칸(기본 20 + 장비 37 + 패시브 8)이라
    /// 고정 격자로는 29칸이 보이지도 눌리지도 않았다. (Blob_Audit.md F1)
    /// 모바일에서 터치 목표 크기를 지키는 유일한 방법이 스크롤이다.
    /// </summary>
    private const int BagVisibleRows = 6;

    /// <summary>칸 사이 여백. 부모 기준 정규화 값이다.</summary>
    private const float BagCellPadding = 0.006f;

    /// <summary>칸과 칸 사이 여백(px). 가로세로가 같아 보이려면 픽셀로 줘야 한다.</summary>
    private const float CellGapPixels = 8f;

    /// <summary>
    /// 칸(장비 슬롯 · 가방 칸) 안쪽 여백. 【캔버스 픽셀】이다.
    /// 칸은 가로세로 비가 제각각이라 비율로 주면 위아래만 얇아진다.
    /// </summary>
    private const float SlotPad = 12f;

    private static InventoryScreenUI instance;

    /// <summary>
    /// 오른쪽 위 세 버튼(장비·스킬·패시브)을 가릴 이유가 있는가.
    ///
    /// 버리기 팝업·검증 패널처럼 화면을 덮는 것이 떠 있을 때 켠다.
    /// 세 버튼은 패널 밖에 있어서, 덮개를 깔아도 그 위에 그대로 떠 있었다.
    /// </summary>
    private bool hudSuppressed;

    private GameObject panel;

    /// <summary>끼울 수 있는 젬이 있을 때 가방 버튼에 붙는 점.</summary>
    private Image socketDot;

    /// <summary>뒤를 어둡게 덮는 판. 패시브 화면에서만 켠다.</summary>
    private Image dim;

    private RectTransform leftColumn;
    private RectTransform equipmentGrid;
    private RectTransform bagViewport;
    private RectTransform bagGrid;
    private ScrollRect bagScroll;
    private RectTransform rightPanel;

    /// <summary>
    /// 우측 패널의 【안쪽】. 바탕(Back)은 rightPanel을 가득 채우고,
    /// 글자와 칸은 여기에 붙는다. 이 둘을 나눠 두지 않으면
    /// 바탕까지 같이 줄어들어 패널 사이에 빈 틈이 생긴다.
    /// </summary>
    private RectTransform rightContent;

    /// <summary>아래 줄. 바깥 여백은 panel이 이미 들여 놨다.</summary>
    private RectTransform footer;

    /// <summary>본문 영역. 패시브에서는 아래줄 자리까지 내려간다.</summary>
    private RectTransform bodyRegion;


    /// <summary>안전 영역 컨테이너. 화면 UI는 전부 이 아래에 붙는다.</summary>
    private RectTransform safeArea;

    /// <summary>
    /// 지금 고른 【착용 중인】 장비의 자리. 가방에서 고른 것(selected)과 구분한다.
    ///
    /// 나눠 두는 이유 — 해제는 이제 두 단계다. 슬롯을 누르면 고르기만 하고,
    /// 우측 행동 줄의 「해제」를 눌러야 실제로 벗는다.
    /// 예전에는 빈손으로 슬롯을 누르면 즉시 벗겨져서, 무엇을 끼웠는지
    /// 확인하려고 누른 것만으로 장비가 가방으로 돌아갔다.
    /// </summary>
    private EquipmentSlot? selectedSlot;

    private Text creditLabel;

    /// <summary>위 단의 머리글 — 「장비」 또는 「각성 Lv.n」.</summary>
    private Text equipTitleLabel;

    /// <summary>머리글 오른쪽의 보조 정보 — 「다음 개방 Lv.3 (소켓 1)」.</summary>
    private Text topInfoLabel;

    /// <summary>가방 격자 뒤에 깔린 눌린 면. 장비 칸을 끄면 같이 위로 늘어난다.</summary>
    private Image bagWell;

    private Text bagTitleLabel;
    private Text weightLabel;
    private Image weightFill;

    /// <summary>소지 중량 카드. 패시브 화면에서는 끈다.</summary>
    private RectTransform weightCard;

    private readonly List<ItemStack> bagStacks = new();

    private Tab tab = Tab.Bag;

    /// <summary>가방에서 고른 칸. 우측 패널과 소켓 장착이 이것을 본다.</summary>
    private ItemStack selected;

    public bool IsOpen => panel != null && panel.activeSelf;

    public static InventoryScreenUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<InventoryScreenUI>(FindObjectsInactive.Include);

        if (instance != null)
            return instance;

        Canvas canvas = UIFactory.CreateCanvas("InventoryScreenCanvas (Runtime)", 1000);

        instance = canvas.gameObject.AddComponent<InventoryScreenUI>();
        instance.Build(canvas.transform);

        return instance;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        if (SkillManager.HasInstance)
        {
            SkillManager.Instance.OnBuildChanged -= HandleExternalChange;
            SkillManager.Instance.OnGemGained -= HandleGemGained;
            SkillManager.Instance.OnEquipRejected -= HandleEquipRejected;
        }
    }

    private void Start()
    {
        SkillManager manager = SkillManager.EnsureInstance();

        manager.OnBuildChanged += HandleExternalChange;
        manager.OnGemGained += HandleGemGained;
        manager.OnEquipRejected += HandleEquipRejected;

        RefreshToggle();
        RefreshQuickSlots();
    }

    private void HandleExternalChange()
    {
        RefreshToggle();
        RefreshQuickSlots();

        if (IsOpen)
            Refresh();
    }

    private void HandleGemGained(SkillDefinition skill) => HandleExternalChange();

    private void HandleEquipRejected(SkillDefinition skill, SocketError error)
    {
        ShowToast(SocketErrorText.Describe(error));
    }

    // ────────────────────────────────── 생성

    private void Build(Transform parent)
    {
        safeArea = UIFactory.CreateSafeArea(parent.GetComponent<Canvas>());

        // 【바깥 테두리는 여기 한 번만 준다.】
        // 전에는 줄마다 0f / 1f로 붙여 놓고 안쪽만 여백을 줬더니,
        // 화면 끝에는 여백이 없고 안쪽만 떠 있는 모양이 됐다.
        RectTransform panelRect = UIFactory.CreateRegion(
            "Panel", safeArea, Vector2.zero, Vector2.one);

        UIFactory.Inset(panelRect, UIFactory.Gap);

        panel = panelRect.gameObject;

        // 덮개는 패널 안에 둔다. 패널이 꺼지면 같이 꺼져야 한다.
        // 안전 영역 밖까지 덮으려고 앵커를 넉넉히 넘긴다.
        // 어둡게 덮을지는 탭이 정한다. (Refresh에서 색만 바꾼다)
        // 장비·스킬은 한쪽에만 뜨므로 나머지를 가리면
        // 「바깥을 보면서 짐을 본다」가 성립하지 않는다.
        dim = UIFactory.CreatePanel("Dim", panel.transform, UIPalette.Dim,
            new Vector2(-0.2f, -0.2f), new Vector2(1.2f, 1.2f));

        dim.raycastTarget = true;

        float half = UIFactory.Gap * 0.5f;

        bodyRegion = UIFactory.CreateSlice("Body", panel.transform,
            new Vector2(0f, ColumnBottom), new Vector2(1f, ColumnTop),
            bottom: half, top: half);

        RectTransform body = bodyRegion;

        footer = UIFactory.CreateSlice("Footer", panel.transform,
            Vector2.zero, new Vector2(1f, FooterTop), top: half);

        BuildLeftColumn(body);

        rightPanel = UIFactory.CreateSlice("Right", body,
            new Vector2(ColumnSplit, 0f), Vector2.one, left: half);

        BuildBottomBar();

        // 【HUD는 패널보다 나중에 만든다.】
        // 먼저 만들면 패널의 덮개가 위에 깔려 눌리지 않는다. 화면이 열려 있는
        // 동안에도 다른 화면으로 바로 건너뛸 수 있어야 하므로 맨 위에 둔다.
        BuildHud(safeArea);
        BuildToast(safeArea);

        panel.SetActive(false);
    }

    // ── 늘 떠 있는 것들 ───────────────────────────────────────────────
    //
    // 【탭 줄을 없애고 버튼을 따로 뒀다.】
    // 전에는 「가방」 버튼 하나로 화면을 열고, 그 안의 탭 줄로 셋을 오갔다.
    // 두 단계라 「스킬을 보려면 먼저 가방을 연다」가 됐다. 이제 세 버튼이
    // 화면에 늘 떠 있고, 누르면 그 화면이 바로 열린다. 같은 버튼을 다시
    // 누르면 닫힌다.

    private const float HudButtonWidth = 150f;
    private const float HudButtonHeight = 88f;
    private const float HudButtonGap = 10f;

    /// <summary>퀵슬롯 한 칸의 변(px). 줄 전체가 화면 가운데 0.30~0.70에 든다.</summary>
    private const float QuickCellSize = 92f;

    private const float QuickCellGap = 8f;

    private readonly List<Button> hudButtons = new();

    private RectTransform quickBar;

    private void BuildHud(Transform parent)
    {
        BuildHudButtons(parent);
        BuildQuickBar(parent);
    }

    private void BuildHudButtons(Transform parent)
    {
        hudButtons.Clear();

        for (int i = 0; i < TabNames.Length; i++)
        {
            GameObject buttonObject = UIFactory.CreateChild($"Hud_{TabNames[i]}", parent);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(HudButtonWidth, HudButtonHeight);

            // 오른쪽부터 역순으로 쌓는다 — 패시브가 가장 오른쪽.
            int fromRight = TabNames.Length - 1 - i;

            rect.anchoredPosition = new Vector2(
                -(10f + fromRight * (HudButtonWidth + HudButtonGap)), -10f);

            var image = buttonObject.AddComponent<Image>();
            image.color = UIPalette.Header;
            image.sprite = UISprites.Rounded(UIFactory.RadiusLarge);
            image.type = Image.Type.Sliced;

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            int captured = i;
            button.onClick.AddListener(() => ToggleTab((Tab)captured));

            UIFactory.CreateOutline(image, UIPalette.Rim, UIFactory.RadiusLarge, 2);

            UIFactory.CreateLabel(buttonObject.transform, TabNames[i], 30, FontStyle.Bold,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            hudButtons.Add(button);

            // 빈 소켓 알림 점은 「스킬」 버튼에만 붙인다.
            if ((Tab)i != Tab.Socket)
                continue;

            socketDot = UIFactory.CreatePanel("SocketDot", buttonObject.transform,
                UIPalette.TextAccent, new Vector2(-0.10f, 0.60f), new Vector2(0.36f, 1.18f),
                radius: 14);

            UIFactory.CreateLabel(socketDot.transform, "0", 22, FontStyle.Bold,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter,
                new Color(0.08f, 0.08f, 0.1f));

            socketDot.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 화면 하단의 퀵슬롯 줄. 【열려 있든 닫혀 있든 같은 자리에 있다.】
    ///
    /// 전에는 가방 화면의 아래줄 안에만 있어서, 정작 쓰는 순간(교전 중)에는
    /// 보이지 않았다. 가방 안에서는 「넣는 자리」, 밖에서는 「쓰는 자리」다.
    /// </summary>
    private void BuildQuickBar(Transform parent)
    {
        GameObject barObject = UIFactory.CreateChild("QuickBar", parent);

        quickBar = barObject.GetComponent<RectTransform>();
        quickBar.anchorMin = new Vector2(0.5f, 0f);
        quickBar.anchorMax = new Vector2(0.5f, 0f);
        quickBar.pivot = new Vector2(0.5f, 0f);

        quickBar.sizeDelta = new Vector2(
            QuickSlots.Count * QuickCellSize + (QuickSlots.Count - 1) * QuickCellGap,
            QuickCellSize);

        quickBar.anchoredPosition = new Vector2(0f, 12f);
    }

    /// <summary>같은 버튼을 다시 누르면 닫는다.</summary>
    private void ToggleTab(Tab next)
    {
        if (IsOpen && tab == next)
        {
            Close();
            return;
        }

        if (IsOpen)
        {
            SelectTab(next);
            return;
        }

        Open((int)next);
    }

    // ── 좌측 칸의 세로 배치 ───────────────────────────────────────────
    // 장비 탭과 스킬 탭이 【같은 네 단】을 쓴다.
    //
    //   장비 : 「장비」      · 장비 8칸 · 「가방 (n/m)」    · 아이템 격자
    //   스킬 : 「각성 Lv.n」 · 소켓판   · 「스킬 젬 (n개)」 · 젬 목록
    //
    // 두 화면의 구조가 같아지면 「위는 끼우는 자리, 아래는 가진 것」이라는
    // 한 가지만 배우면 된다. 소켓판이 장비 8칸보다 한 줄 많아 그만큼 더 준다.

    private const float TopBandTop = 0.905f;
    private const float TitleTop = 0.985f;

    private const float EquipBandBottom = 0.60f;
    private const float SocketBandBottom = 0.52f;

    /// <summary>아래 단 제목의 높이.</summary>
    private const float ListTitleHeight = 0.070f;

    private const float BandGap = 0.010f;

    /// <summary>탭에 맞춰 좌측 칸의 세로 배치를 바꾼다.</summary>
    private void LayoutLeftColumn(bool skillTab)
    {
        float bandBottom = skillTab ? SocketBandBottom : EquipBandBottom;

        float titleTop = bandBottom - BandGap;
        float titleBottom = titleTop - ListTitleHeight;
        float listTop = titleBottom - BandGap;

        // 스킬 탭에서만 「다음 개방」이 뜬다. 그때는 크레딧을 접는다 —
        // 젬을 끼우는 화면에서 돈은 쓸 일이 없다.
        topInfoLabel.gameObject.SetActive(skillTab);
        creditLabel.gameObject.SetActive(!skillTab);

        equipmentGrid.anchorMin = new Vector2(0f, bandBottom);
        equipmentGrid.anchorMax = new Vector2(1f, TopBandTop);
        equipmentGrid.offsetMin = Vector2.zero;
        equipmentGrid.offsetMax = Vector2.zero;

        var title = bagTitleLabel.rectTransform;
        title.anchorMin = new Vector2(0f, titleBottom);
        title.anchorMax = new Vector2(1f, titleTop);
        title.offsetMin = Vector2.zero;
        title.offsetMax = Vector2.zero;

        bagWell.rectTransform.anchorMax = new Vector2(1f, listTop);

        bagViewport.anchorMax = new Vector2(1f, listTop);
        bagViewport.offsetMax = new Vector2(-8f, -8f);
    }

    private void BuildLeftColumn(RectTransform body)
    {
        leftColumn = UIFactory.CreateSlice("Left", body,
            Vector2.zero, new Vector2(ColumnSplit, 1f), right: UIFactory.Gap * 0.5f);

        UIFactory.CreateGlass("Back", leftColumn, UIPalette.Panel,
            Vector2.zero, Vector2.one, UIFactory.RadiusLarge);

        // 바탕은 칸을 가득 채우고, 내용물만 한 칸 들어온다.
        // 그래서 왼쪽 칸의 안쪽 여백과 우측 패널의 안쪽 여백이 같아진다.
        RectTransform content = UIFactory.Inset(
            UIFactory.CreateRegion("Content", leftColumn, Vector2.zero, Vector2.one),
            UIFactory.Gap);

        equipTitleLabel = UIFactory.CreateLabel(content, "장비", 29, FontStyle.Bold,
            new Vector2(0f, TopBandTop + BandGap), new Vector2(0.55f, TitleTop),
            TextAnchor.MiddleLeft, UIPalette.TextDim);

        // 【크레딧이 여기로 들어왔다.】
        // 화면 위 10%를 크레딧 하나 때문에 비워 두고 있었다.
        // 머리글 오른쪽은 어차피 비어 있던 자리다.
        creditLabel = UIFactory.CreateLabel(content, "₡ 0", 28, FontStyle.Bold,
            new Vector2(0.45f, TopBandTop + BandGap), new Vector2(1f, TitleTop),
            TextAnchor.MiddleRight, UIPalette.TextAccent);

        topInfoLabel = UIFactory.CreateLabel(content, string.Empty, 22, FontStyle.Normal,
            new Vector2(0.30f, TopBandTop + BandGap), new Vector2(1f, TitleTop),
            TextAnchor.MiddleRight, UIPalette.TextDim);

        equipmentGrid = UIFactory.CreateRegion("TopBand", content,
            new Vector2(0f, EquipBandBottom), new Vector2(1f, TopBandTop));

        bagTitleLabel = UIFactory.CreateLabel(content, "가방", 29, FontStyle.Bold,
            Vector2.zero, Vector2.one, TextAnchor.MiddleLeft, UIPalette.TextDim);

        // 아래 단 뒤에 한 단계 눌린 면을 깔아 깊이를 준다.
        bagWell = UIFactory.CreatePanel("ListWell", content, UIPalette.Inset,
            Vector2.zero, new Vector2(1f, 0.515f), UIFactory.Radius);

        bagViewport = UIFactory.CreateSlice("List", content,
            Vector2.zero, new Vector2(1f, 0.515f),
            left: 8f, bottom: 8f, right: 8f, top: 8f);

        // 마스크가 없으면 스크롤한 칸이 위쪽 장비 영역을 덮는다.
        bagViewport.gameObject.AddComponent<RectMask2D>();

        // 드래그를 받으려면 레이캐스트 대상이 필요하다. 빈 칸 사이나
        // 마지막 줄 아래를 문질러도 스크롤되게 만드는 투명 판이다.
        var bagCatcher = bagViewport.gameObject.AddComponent<Image>();
        bagCatcher.color = new Color(0f, 0f, 0f, 0f);

        // 내용물은 위를 기준으로 자란다 — 첫 칸의 자리가 용량과 무관하게 같다.
        bagGrid = UIFactory.CreateRegion("BagContent", bagViewport,
            new Vector2(0f, 1f), new Vector2(1f, 1f));
        bagGrid.pivot = new Vector2(0.5f, 1f);
        bagGrid.sizeDelta = Vector2.zero;

        bagScroll = bagViewport.gameObject.AddComponent<ScrollRect>();
        bagScroll.viewport = bagViewport;
        bagScroll.content = bagGrid;
        bagScroll.horizontal = false;
        bagScroll.vertical = true;
        bagScroll.movementType = ScrollRect.MovementType.Elastic;
        bagScroll.elasticity = 0.1f;
        bagScroll.inertia = true;
        bagScroll.decelerationRate = 0.135f;
        bagScroll.scrollSensitivity = 40f;
    }

    private void BuildBottomBar()
    {
        // 소지 중량 — 좌하단. 막대 · 숫자를 한 카드 안에 담는다.
        // 아래줄(footer)은 이미 바깥 여백만큼 들어와 있으므로
        // 여기서는 0~1을 그대로 쓴다. 칸 사이만 반 칸씩 띄운다.
        float half = UIFactory.Gap * 0.5f;

        // 【0.40 → 0.26으로 줄인다.】
        // 가운데의 퀵슬롯 줄(0.30~0.70)과 겹쳐 1·2번 칸을 덮고 있었다.
        weightCard = UIFactory.CreateSlice("WeightCard", footer,
            Vector2.zero, new Vector2(0.26f, 1f), right: half);

        UIFactory.CreateGlass("Back", weightCard, UIPalette.Panel,
            Vector2.zero, Vector2.one, UIFactory.RadiusLarge);

        UIFactory.CreateLabel(weightCard, "중량", 21, FontStyle.Normal,
            new Vector2(0.05f, 0f), new Vector2(0.24f, 1f),
            TextAnchor.MiddleLeft, UIPalette.TextDim);

        UIFactory.CreatePanel("WeightTrack", weightCard, UIPalette.Inset,
            new Vector2(WeightBarLeft, WeightBarInset),
            new Vector2(WeightBarRight, 1f - WeightBarInset), radius: 6);

        weightFill = UIFactory.CreatePanel("WeightFill", weightCard, UIPalette.Action,
            new Vector2(WeightBarLeft, WeightBarInset),
            new Vector2(WeightBarLeft, 1f - WeightBarInset), radius: 6);

        weightLabel = UIFactory.CreateLabel(weightCard, string.Empty, 21, FontStyle.Bold,
            new Vector2(0.62f, 0f), new Vector2(0.96f, 1f),
            TextAnchor.MiddleRight, UIPalette.TextDim);

        // 가운데(0.40~0.86)는 비워 둔다 — 화면에 늘 떠 있는 퀵슬롯 줄이
        // 그 자리에 겹쳐 뜬다. 열려 있든 닫혀 있든 같은 자리에 있게 하려는 것이다.

        // 「닫기」도 반 칸 물러난다 — 간격이 다른 경계와 같아진다.
        RectTransform closeBox = UIFactory.CreateSlice("CloseBox", footer,
            new Vector2(0.86f, 0f), Vector2.one, left: half);

        UIFactory.CreateButton(closeBox, "닫기",
            Vector2.zero, Vector2.one,
            UIPalette.Subtle, Close, 31, UIFactory.RadiusLarge);
    }

    // ────────────────────────────────── 잠깐 뜨는 알림

    /// <summary>
    /// 실패한 이유를 잠깐 띄우는 띠.
    ///
    /// 【자리를 잡아 두지 않는다.】
    /// 전에는 본문과 아래줄 사이에 한 줄을 늘 비워 두고 거기에 적었다.
    /// 대부분의 순간에는 글자가 없어 그냥 빈 띠였다. 이제 아래줄 위에
    /// 겹쳐 떴다가 사라진다 — 배치를 밀지 않는다.
    /// </summary>
    private GameObject toast;

    private Text toastLabel;
    private float toastHideAt;

    private const float ToastSeconds = 2.8f;

    private void BuildToast(Transform parent)
    {
        // 화면이 닫혀 있을 때도 떠야 하므로 패널이 아니라 안전 영역에 붙인다.
        Image back = UIFactory.CreatePanel("Toast", parent, UIPalette.Header,
            new Vector2(0.24f, 0.135f), new Vector2(0.76f, 0.205f),
            UIFactory.RadiusLarge);

        back.raycastTarget = false;

        UIFactory.CreateOutline(back, UIPalette.Rim, UIFactory.RadiusLarge, 2);

        toast = back.gameObject;

        toastLabel = UIFactory.CreateLabel(toast.transform, string.Empty, 25, FontStyle.Bold,
            new Vector2(0.03f, 0f), new Vector2(0.97f, 1f),
            TextAnchor.MiddleCenter, UIPalette.TextAccent);

        toastLabel.raycastTarget = false;

        toast.SetActive(false);
    }

    private void ShowToast(string text)
    {
        if (toast == null)
            return;

        if (string.IsNullOrEmpty(text))
        {
            toast.SetActive(false);
            return;
        }

        toastLabel.text = text;
        toast.SetActive(true);

        // 일시정지 중에도 흐르는 시계를 쓴다 — 이 화면이 열려 있으면
        // timeScale이 0이라 Time.time은 멈춰 있다.
        toastHideAt = Time.unscaledTime + ToastSeconds;
    }

    private void Update()
    {
        if (toast != null && toast.activeSelf && Time.unscaledTime >= toastHideAt)
            toast.SetActive(false);
    }

    // ────────────────────────────────── 열고 닫기

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    public void Open(int tabIndex = -1)
    {
        if (tabIndex >= 0 && tabIndex < TabNames.Length)
            tab = (Tab)tabIndex;

        CloseItemDetail();

        selected = null;
        selectedSlot = null;

        // 활성화가 먼저다 — 꺼진 상태에서는 뷰포트 높이를 읽을 수 없어
        // 스크롤 내용물의 높이가 0으로 잡힌다.
        panel.SetActive(true);

        Refresh();

        if (bagScroll != null)
            bagScroll.verticalNormalizedPosition = 1f;

        if (GameManager.HasInstance)
            GameManager.Instance.OpenSkill();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 검증 패널은 이 화면 뒤에 있다. 켜져 있으면 글자가 비쳐 보인다.
        PlaytestPanelUI.SetHiddenByScreen(true);
#endif
    }

    public void Close()
    {
        CloseDiscardPopup();
        CloseItemDetail();

        ShowToast(string.Empty);

        selected = null;
        selectedSlot = null;

        panel.SetActive(false);

        // 패시브 화면에서 껐던 것을 도로 켠다 — 밖에서는 늘 떠 있어야 한다.
        if (quickBar != null)
            quickBar.gameObject.SetActive(true);

        RefreshHudVisibility();

        RefreshToggle();
        RefreshQuickSlots();

        if (GameManager.HasInstance)
            GameManager.Instance.CloseSkill();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        PlaytestPanelUI.SetHiddenByScreen(false);
#endif
    }

    private void SelectTab(Tab next)
    {
        CloseItemDetail();

        tab = next;
        selected = null;
        selectedSlot = null;

        Refresh();
    }

    // ────────────────────────────────── 다시 그리기

    private void Refresh()
    {
        RefreshCredits();
        RefreshQuickSlots();

        // 【탭마다 필요한 만큼만 쓴다.】
        //   장비 · 스킬 — 왼쪽 55%만. 나머지는 게임 화면이 그대로 비친다.
        //   패시브      — 폭 전체 + 덮개. 트리를 접으면 볼 수가 없다.
        bool passive = tab == Tab.Passive;

        bool showLeft = !passive;
        bool showRight = passive;

        float half = UIFactory.Gap * 0.5f;

        leftColumn.gameObject.SetActive(showLeft);
        rightPanel.gameObject.SetActive(showRight);

        bodyRegion.anchorMin = new Vector2(0f, passive ? 0f : ColumnBottom);
        bodyRegion.offsetMin = new Vector2(0f, passive ? 0f : half);

        // 【덮개는 늘 켜 두고 색만 바꾼다.】
        // 장비·스킬에서는 투명하게 둔다 — 뒤가 그대로 보이면서도,
        // 패널 바깥을 눌렀을 때 그 터치가 조준 조이스틱까지 내려가지 않는다.
        // 덮개를 아예 끄면 짐을 보는 동안 조준이 돌아가 버린다.
        dim.color = passive ? UIPalette.Dim : Color.clear;
        weightCard.gameObject.SetActive(!passive);
        quickBar.gameObject.SetActive(!passive);

        // 【패시브는 화면을 통째로 쓴다.】
        // 트리는 접으면 볼 수가 없다. 위의 화면 버튼과 아래줄까지 접고,
        // 나가는 길은 패널 오른쪽 위의 「닫기」 하나로 둔다.
        RefreshHudVisibility();

        footer.gameObject.SetActive(!passive);

        if (showLeft)
        {
            leftColumn.anchorMax = new Vector2(SidePanelRight, 1f);
            leftColumn.offsetMax = new Vector2(-half, 0f);

            bool skillTab = tab == Tab.Socket;

            LayoutLeftColumn(skillTab);

            UIFactory.ClearChildren(equipmentGrid);

            if (skillTab)
                DrawSocketPanel(equipmentGrid);
            else
                RefreshEquipment();

            RefreshBag();
            RefreshWeight();
        }

        if (showRight)
        {
            rightPanel.anchorMin = new Vector2(showLeft ? ColumnSplit : 0f, 0f);
            rightPanel.offsetMin = new Vector2(showLeft ? half : 0f, 0f);

            UIFactory.ClearChildren(rightPanel);

            // 바탕은 각 Draw가 rightPanel에 가득 깔고, 글자·칸은 여기 붙는다.
            // 지우고 다시 만드는 이유 — ClearChildren이 방금 같이 지웠다.
            UIFactory.CreateGlass("Back", rightPanel, UIPalette.Panel,
                Vector2.zero, Vector2.one, UIFactory.RadiusLarge);

            rightContent = UIFactory.Inset(
                UIFactory.CreateRegion("Content", rightPanel, Vector2.zero, Vector2.one),
                UIFactory.Gap);

            DrawPassivePanel();
        }

        RefreshToggle();
    }

    /// <summary>
    /// 세 버튼을 보일지 한 곳에서 정한다.
    /// 가릴 이유는 둘 — 패시브 화면(화면을 통째로 쓴다)과 덮는 팝업이다.
    /// 흩어 두면 한쪽이 켠 것을 다른 쪽이 모르고 도로 켠다.
    /// </summary>
    private void RefreshHudVisibility()
    {
        // 【닫혀 있으면 늘 보인다.】
        // 패시브 규칙은 「그 화면이 떠 있는 동안」에만 해당한다.
        // tab만 보다가, 패시브를 껐을 때 tab이 그대로라 버튼까지 같이 사라졌다.
        bool show = !hudSuppressed && (!IsOpen || tab != Tab.Passive);

        for (int i = 0; i < hudButtons.Count; i++)
        {
            if (hudButtons[i] != null)
                hudButtons[i].gameObject.SetActive(show);
        }
    }

    /// <summary>
    /// 화면 밖(검증 패널 등)에서 세 버튼을 잠시 감출 때 부른다.
    /// 인스턴스가 없으면 아직 버튼도 없으므로 그냥 돌아간다.
    /// </summary>
    public static void SetHudSuppressed(bool value)
    {
        if (instance == null)
            return;

        instance.hudSuppressed = value;
        instance.RefreshHudVisibility();
    }

    private void RefreshCredits()
    {
        if (creditLabel == null)
            return;

        int amount = PassiveManager.EnsureInstance().Credits;

        creditLabel.text = $"₡ {amount:N0}";
    }

    private void RefreshToggle()
    {
        // 열려 있는 화면의 버튼을 밝힌다. 세 버튼이 늘 떠 있으므로
        // 지금 무엇을 보고 있는지 여기서만 알 수 있다.
        for (int i = 0; i < hudButtons.Count; i++)
        {
            if (hudButtons[i] == null)
                continue;

            bool active = IsOpen && (Tab)i == tab;

            if (hudButtons[i].targetGraphic is Image image)
                image.color = active ? UIPalette.Action : UIPalette.Header;

            var label = hudButtons[i].GetComponentInChildren<Text>();

            if (label != null)
                label.color = active ? UIPalette.Text : UIPalette.TextDim;
        }

        if (socketDot == null || !SkillManager.HasInstance)
            return;

        int free = SkillManager.Instance.Build.FreeSocketCount;
        int gems = SkillManager.Instance.GetGemsInBag().Count;

        // 【글자로 알리지 않는다.】
        // 「스킬 / 빈 소켓 1」은 버튼 안에서 무슨 뜻인지 읽히지 않는다.
        // 끼울 수 있는 젬이 있다는 신호는 점 하나면 충분하다.
        socketDot.gameObject.SetActive(free > 0 && gems > 0);

        if (socketDot.transform.childCount > 0)
        {
            var count = socketDot.transform.GetChild(0).GetComponent<Text>();

            if (count != null)
                count.text = free.ToString();
        }
    }

    // ────────────────────────────────── 좌측 — 장비

    private void RefreshEquipment()
    {
        equipTitleLabel.text = "장비";

        EquipmentLoadout loadout = PlayerInventory.EnsureInstance().Loadout;

        var slots = (EquipmentSlot[])System.Enum.GetValues(typeof(EquipmentSlot));

        // 【가로와 세로 여백을 같게 만든다.】
        // 정규화 여백 0.008 하나를 두 축에 쓰면, 가로로 긴 칸에서는
        // 가로 6px · 세로 2px이 된다. 세로만 답답해 보이던 이유다.
        // 픽셀로 정하고 각 축의 길이로 나눠 환산한다.
        Canvas.ForceUpdateCanvases();

        float gridWidth = Mathf.Max(1f, equipmentGrid.rect.width);
        float gridHeight = Mathf.Max(1f, equipmentGrid.rect.height);

        float padX = CellGapPixels / gridWidth;
        float padY = CellGapPixels / gridHeight;

        for (int i = 0; i < slots.Length; i++)
        {
            UIFactory.GetCellAnchors(i, 4, 2, padX, padY, out Vector2 min, out Vector2 max);

            ItemStack stack = loadout.Get(slots[i]);

            bool isSelectedSlot = selectedSlot == slots[i];

            // 【고른 장비가 들어갈 수 있는 자리를 밝힌다.】
            // 어느 칸에 끼워야 하는지 글로만 알려 주면 8칸을 하나씩 눌러 보게 된다.
            bool canAccept = selected != null
                             && selected.Definition is EquipmentDefinition
                             && loadout.CanEquip(selected, slots[i]);

            Color kind = stack?.Definition != null
                ? UIPalette.ForItem(stack.Definition.Kind)
                : UIPalette.Slot;

            Color body = isSelectedSlot
                ? UIPalette.SlotSelected
                : canAccept
                    ? UIPalette.SlotEquippable
                    : stack == null
                        ? UIPalette.Inset
                        : UIPalette.Glassify(kind, 0.34f);

            Image cell = UIFactory.CreatePanel($"Equip_{slots[i]}", equipmentGrid,
                body, min, max);

            // 낄 수 있는 자리는 테두리까지 밝혀 준다. 바탕색만 바꾸면
            // 어두운 화면에서 「조금 다른 회색」으로만 보인다.
            UIFactory.CreateOutline(cell,
                isSelectedSlot ? UIPalette.Brighten(UIPalette.SlotSelected, 0.22f)
                    : canAccept ? UIPalette.Brighten(UIPalette.SlotEquippable, 0.34f)
                    : stack == null ? UIPalette.EdgeSoft
                    : UIPalette.Brighten(kind),
                UIFactory.Radius,
                isSelectedSlot || canAccept ? 3 : 2);

            // 장비 슬롯도 누를 수 있어야 한다. 이것이 없던 동안에는
            // 에셋 82종을 만들어 놓고 게임에서 입을 방법이 없었다. (docs/Blob_Audit.md A1)
            var slotButton = cell.gameObject.AddComponent<Button>();
            slotButton.targetGraphic = cell;

            EquipmentSlot captured = slots[i];
            slotButton.onClick.AddListener(() => OnEquipSlotClicked(captured));

            // 부위 이름은 항상 작게 위에 남긴다 —
            // 끼고 나면 어느 자리였는지 알 수 없던 문제를 없앤다.
            //
            // 【여백을 비율이 아니라 픽셀로 준다.】
            // 0.07(가로) / 0.05(세로)로 두었더니 칸이 가로로 길어
            // 왼쪽은 14px, 위는 3px이 됐다. 글자가 천장에 붙어 보였다.
            UIFactory.Inset(
                UIFactory.CreateLabel(cell.transform, EquipmentSlotName(slots[i]), 20,
                    FontStyle.Bold, new Vector2(0f, 0.5f), Vector2.one,
                    TextAnchor.UpperLeft, UIPalette.TextDim).rectTransform,
                left: SlotPad, bottom: 0f, right: SlotPad, top: SlotPad);

            if (stack?.Definition == null)
                continue;

            // 장비 이름은 흰 글자 + 반투명 검정 띠. 종류 색이 밝은 칸에서도 읽힌다.
            Image strip = UIFactory.CreatePanel("NameStrip", cell.transform,
                UIPalette.NameStrip,
                new Vector2(0f, 0f), new Vector2(1f, 0.5f), radius: 6);

            UIFactory.Inset(strip.rectTransform,
                left: SlotPad, bottom: SlotPad, right: SlotPad, top: 0f);

            strip.raycastTarget = false;

            // 【긴 이름이 칸을 넘던 자리다.】
            // 「진압용 중장갑 (중간)」처럼 아홉 자가 넘는 이름이 띠 밖으로
            // 삐져나와 옆 칸과 겹쳐 보였다. 두 가지를 같이 건다 —
            // 줄바꿈을 허용해 두 줄까지 접고, 그래도 넘치면 글자를 줄인다.
            // 장비 탭이 폭을 다 쓰게 되면서 칸이 두 배로 넓어졌으므로
            // 실제로 줄어드는 경우는 아주 긴 이름뿐이다.
            Text equipName = UIFactory.CreateLabel(strip.transform,
                stack.Definition.DisplayName, 21, FontStyle.Bold,
                new Vector2(0.05f, 0f), new Vector2(0.95f, 1f),
                TextAnchor.MiddleLeft, Color.white);

            FitName(equipName, 21);

            DrawDurabilityBar(cell.transform, stack);
        }
    }

    private static string EquipmentSlotName(EquipmentSlot slot)
    {
        switch (slot)
        {
            case EquipmentSlot.Weapon:   return "무기";
            case EquipmentSlot.Head:     return "머리";
            case EquipmentSlot.Body:     return "갑옷";
            case EquipmentSlot.Face:     return "얼굴";
            case EquipmentSlot.Ears:     return "이어폰";
            case EquipmentSlot.Backpack: return "가방";
            case EquipmentSlot.ImprintA: return "각인 1";
            case EquipmentSlot.ImprintB: return "각인 2";
            default:                     return slot.ToString();
        }
    }

    // ────────────────────────────────── 좌측 — 가방

    private void RefreshBag()
    {
        UIFactory.ClearChildren(bagGrid);

        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        // 【탭에 맞는 것만 보여 준다.】
        // 스킬 탭에서 방탄복을 고를 일이 없고, 장비 탭에서 젬을 눌러 봐야
        // 「스킬 탭에서 장착」이라는 안내만 다시 나온다. 목록만 길어진다.
        bagStacks.Clear();

        foreach (ItemStack stack in bag.Stacks)
        {
            if (BelongsToTab(stack))
                bagStacks.Add(stack);
        }

        // 부모(=뷰포트)의 실제 크기를 읽기 전에 레이아웃을 확정시킨다.
        Canvas.ForceUpdateCanvases();

        float viewWidth = bagViewport.rect.width;
        float viewHeight = bagViewport.rect.height;

        int columns = BagColumns;

        // 【칸을 정사각형으로 만든다.】
        // 가로를 열 수로 나눈 값이 한 칸의 변이다. 내용물 높이를
        // 「칸 변 × 줄 수」로 잡으면 세로도 같은 길이가 된다.
        float cellSize = viewWidth > 0f ? viewWidth / columns : 0f;

        if (tab == Tab.Socket)
        {
            bagTitleLabel.text = $"스킬 젬 ({bagStacks.Count}개)";
            DrawGemSections(columns, cellSize, viewWidth, viewHeight);
            return;
        }

        // 【「젬 n개는 스킬 탭에」를 적지 않는다.】
        // 스킬 버튼이 화면에 늘 떠 있으니 한 번 눌러 보면 안다.
        // 매번 같은 줄을 읽게 만드는 것이 더 비싸다.
        bagTitleLabel.text = $"가방 ({bag.UsedSlots}/{bag.SlotCapacity})";

        // 【전부 그린다.】 잘라내면 그 칸의 물건은 보이지도 눌리지도 않는다.
        int cells = Mathf.Max(bagStacks.Count, bag.SlotCapacity);

        int needed = Mathf.CeilToInt(cells / (float)columns);

        // 화면을 채울 만큼은 그린다. 빈 칸이 곧 남은 자리라는 표시다.
        int fits = cellSize > 0f ? Mathf.CeilToInt(viewHeight / cellSize) : BagVisibleRows;

        int rows = Mathf.Max(1, Mathf.Max(needed, fits));

        float contentHeight = cellSize * rows;

        bagGrid.sizeDelta = new Vector2(0f, contentHeight);

        // 여백은 부모 기준 정규화 값이다. 가로와 세로의 기준 길이가 다르므로
        // 같은 픽셀 간격을 내려면 각각 따로 환산해야 한다.
        float gap = cellSize * 0.055f;

        float paddingX = viewWidth > 0f ? gap / viewWidth : BagCellPadding;
        float paddingY = contentHeight > 0f ? gap / contentHeight : BagCellPadding;

        for (int i = 0; i < rows * columns; i++)
        {
            UIFactory.GetCellAnchors(i, columns, rows, paddingX, paddingY,
                out Vector2 min, out Vector2 max);

            ItemStack cellStack = i < bagStacks.Count ? bagStacks[i] : null;

            DrawItemCell($"Bag_{i}", bagGrid, min, max, cellStack);
        }
    }

    /// <summary>
    /// 스킬 탭의 젬 목록 — 【분류별로 끊어서】 보여 준다.
    ///
    /// 전에는 43개를 한 덩어리로 늘어놓아서, 어느 것이 핵심이고 어느 것이
    /// 보조인지 하나씩 눌러 봐야 알 수 있었다. 오른쪽 소켓판은 이미
    /// 「핵심 · 소켓 · 발동 · 전령」으로 나뉘어 있으니, 왼쪽도 같은 순서로
    /// 끊어 두면 「이 줄의 젬은 저 줄의 자리에 들어간다」가 바로 읽힌다.
    /// </summary>
    private void DrawGemSections(int columns, float cellSize, float viewWidth, float viewHeight)
    {
        var order = new[]
        {
            SkillCategory.Core, SkillCategory.Support,
            SkillCategory.Meta, SkillCategory.Persistent
        };

        float headerHeight = cellSize * 0.42f;

        // 1차 — 전체 높이를 먼저 잰다. 정규화 좌표를 쓰려면 분모가 있어야 한다.
        float total = 0f;
        var rowsOf = new int[order.Length];

        for (int c = 0; c < order.Length; c++)
        {
            int count = CountGems(order[c]);

            if (count == 0)
                continue;

            rowsOf[c] = Mathf.CeilToInt(count / (float)columns);
            total += headerHeight + rowsOf[c] * cellSize;
        }

        if (total <= 0f)
        {
            bagGrid.sizeDelta = new Vector2(0f, Mathf.Max(1f, viewHeight));

            UIFactory.CreateLabel(bagGrid, "가진 스킬 젬이 없습니다.", 26, FontStyle.Normal,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, UIPalette.TextDim);
            return;
        }

        total = Mathf.Max(total, viewHeight);

        bagGrid.sizeDelta = new Vector2(0f, total);

        float gap = cellSize * 0.055f;
        float padX = viewWidth > 0f ? gap / viewWidth : BagCellPadding;
        float padY = gap / total;

        // 2차 — 위에서부터 쌓는다. y는 【위에서 잰 거리】(px)다.
        float y = 0f;

        for (int c = 0; c < order.Length; c++)
        {
            if (rowsOf[c] == 0)
                continue;

            SkillCategory category = order[c];

            UIFactory.CreateLabel(bagGrid,
                $"{SkillCategoryName(category)} 젬", 23, FontStyle.Bold,
                new Vector2(0.012f, 1f - (y + headerHeight) / total),
                new Vector2(0.99f, 1f - y / total),
                TextAnchor.MiddleLeft, UIPalette.TextAccent);

            y += headerHeight;

            int index = 0;

            for (int i = 0; i < bagStacks.Count; i++)
            {
                if (CategoryOf(bagStacks[i]) != category)
                    continue;

                int column = index % columns;
                int row = index / columns;

                float top = y + row * cellSize;

                var min = new Vector2(
                    column / (float)columns + padX,
                    1f - (top + cellSize) / total + padY);

                var max = new Vector2(
                    (column + 1) / (float)columns - padX,
                    1f - top / total - padY);

                DrawItemCell($"Gem_{category}_{index}", bagGrid, min, max, bagStacks[i]);

                index++;
            }

            y += rowsOf[c] * cellSize;
        }
    }

    private int CountGems(SkillCategory category)
    {
        int count = 0;

        for (int i = 0; i < bagStacks.Count; i++)
        {
            if (CategoryOf(bagStacks[i]) == category)
                count++;
        }

        return count;
    }

    /// <summary>젬이 아니면 유지형으로 몰아 둔다 — 목록에서 사라지지 않게.</summary>
    private static SkillCategory CategoryOf(ItemStack stack)
    {
        return stack?.Definition?.Skill != null
            ? stack.Definition.Skill.Category
            : SkillCategory.Persistent;
    }

    public static string SkillCategoryName(SkillCategory category)
    {
        switch (category)
        {
            case SkillCategory.Core:    return "핵심";
            case SkillCategory.Support: return "보조";
            case SkillCategory.Meta:    return "발동";
            default:                    return "전령";
        }
    }

    /// <summary>
    /// 아이템 한 칸. 가방·전리품이 같은 모양을 쓴다.
    ///
    /// 구성 — 바탕(종류 색을 눌러서) · 윤곽(종류 색을 밝혀서) ·
    ///        이름(아래 정렬) · 개수 배지(우하단) · 내구도 막대(맨 아래)
    ///
    /// 【개수 배지를 알약으로 까는 이유】
    /// 칸 배경색이 종류마다 달라서 글자만 얹으면 밝은 칸에서 숫자가 사라진다.
    /// 어두운 알약 위에 얹으면 어떤 칸에서도 같은 크기로 읽힌다.
    /// </summary>
    private void DrawItemCell(
        string name, Transform parent, Vector2 min, Vector2 max, ItemStack stack)
    {
        // 생김새는 ItemCell이 정한다 — 전리품·창고·상점이 같은 칸을 쓴다.
        // 여기서는 「누르면 상세를 연다」만 정한다.
        ItemStack captured = stack;

        ItemCell.Draw(name, parent, min, max, stack,
            stack != null && stack == selected, () => SelectStack(captured));
    }

    private static void WrapName(Text label, int size) => ItemCell.WrapName(label, size);

    private static void DrawDurabilityBar(Transform cell, ItemStack stack)
        => ItemCell.DrawDurabilityBar(cell, stack);

    /// <summary>
    /// 이름이 칸을 넘으면 「앞부분…」으로 자른다.
    ///
    /// 자동 축소를 쓰지 않는 이유는 EllipsisLabel의 주석에 적어 두었다 —
    /// 긴 이름만 글자가 작아지면 같은 줄의 칸들이 제각각으로 보인다.
    /// </summary>
    private static void FitName(Text label, int size)
    {
        label.fontSize = size;

        label.gameObject.AddComponent<EllipsisLabel>().SetText(label.text);
    }

    /// <summary>
    /// 가방 칸을 눌렀다. 【고르는 것이 아니라 상세를 연다.】
    ///
    /// 전에는 여기서 고르기만 하고, 어디에 넣을지는 밝아진 자리를
    /// 다시 눌러야 했다. 두 단계인 것을 화면 아래 한 줄로만 알려 줘서
    /// 매번 그 줄을 읽어야 했다. 이제 상세 안에서 전부 끝낸다.
    /// </summary>
    private void SelectStack(ItemStack stack) => OpenItemDetail(stack);

    /// <summary>이 칸이 지금 탭에 속하는가.</summary>
    private bool BelongsToTab(ItemStack stack)
    {
        if (stack?.Definition == null)
            return false;

        return tab == Tab.Socket
            ? stack.Definition.IsSkillGem
            : !stack.Definition.IsSkillGem;
    }

    /// <summary>
    /// 퀵슬롯 8칸을 다시 그린다.
    ///
    /// 가방 화면이 열려 있으면 【넣는 자리】 — 누르면 그 칸이 비워진다.
    /// 무엇을 넣을지는 아이템 상세의 「퀵슬롯」 줄에서 고른다.
    /// 닫혀 있으면 【쓰는 자리】다. 다만 소모품 사용은 아직 없다(9단계).
    /// </summary>
    private void RefreshQuickSlots()
    {
        if (quickBar == null)
            return;

        UIFactory.ClearChildren(quickBar);

        PlayerInventory inventory = PlayerInventory.EnsureInstance();
        QuickSlots quick = inventory.Quick;

        quick.Prune(inventory.Bag);

        for (int i = 0; i < QuickSlots.Count; i++)
        {
            UIFactory.GetCellAnchors(i, QuickSlots.Count, 1, 0.006f,
                out Vector2 min, out Vector2 max);

            ItemStack stack = quick.Get(i);

            bool empty = stack?.Definition == null;

            Color kind = empty ? UIPalette.Slot : UIPalette.ForItem(stack.Definition.Kind);

            Image cell = UIFactory.CreatePanel($"Quick_{i}", quickBar,
                empty ? UIPalette.Inset : UIPalette.Glassify(kind, 0.34f), min, max);

            UIFactory.CreateOutline(cell,
                empty ? UIPalette.EdgeSoft : UIPalette.Brighten(kind),
                UIFactory.Radius, 2);

            var button = cell.gameObject.AddComponent<Button>();
            button.targetGraphic = cell;

            int captured = i;
            button.onClick.AddListener(() => OnQuickSlotClicked(captured));

            UIFactory.CreateLabel(cell.transform, (i + 1).ToString(), 20, FontStyle.Normal,
                new Vector2(0.1f, 0.04f), new Vector2(0.9f, 0.34f),
                TextAnchor.LowerRight, UIPalette.TextDim);

            if (empty)
                continue;

            Image glyph = UIFactory.CreatePanel("Glyph", cell.transform, UIPalette.GlyphTint,
                new Vector2(0.22f, 0.28f), new Vector2(0.78f, 0.80f), radius: 0);

            glyph.raycastTarget = false;
            glyph.preserveAspect = true;

            if (stack.Definition.Icon != null)
            {
                glyph.sprite = stack.Definition.Icon;
                glyph.color = Color.white;
            }
            else
            {
                glyph.sprite = UISprites.Of(UISprites.GlyphFor(stack.Definition.Kind));
            }

            if (stack.Count > 1)
            {
                UIFactory.CreateBadge(cell.transform, stack.Count.ToString(),
                    new Vector2(0.50f, 0.66f), new Vector2(0.96f, 0.96f), 20, Color.white);
            }
        }
    }

    private void OnQuickSlotClicked(int index)
    {
        PlayerInventory inventory = PlayerInventory.EnsureInstance();

        if (IsOpen)
        {
            // 가방 안에서는 넣고 빼는 자리다. 여기서는 빼기만 한다 —
            // 넣는 것은 「무엇을」이 있어야 하므로 아이템 상세에서 고른다.
            inventory.Quick.Clear(index);
            RefreshQuickSlots();
            return;
        }

        ItemStack stack = inventory.Quick.Get(index);

        if (stack?.Definition == null)
            return;

        // 【소모품 사용은 아직 없다.】 (docs/Blob_Consumable_System.md · 9단계)
        // 여기에 쓰는 동작을 붙이는 순간 「무엇을 얼마나 회복하는가」를
        // 정하는 셈이 된다. 그건 UI가 정할 일이 아니다.
        ShowToast($"「{stack.Definition.DisplayName}」 — 소모품 사용은 아직 없습니다.");
    }

    private void RefreshWeight()
    {
        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        float ratio = bag.WeightLimit <= 0f ? 0f : bag.TotalWeight / bag.WeightLimit;

        // 막대는 꽉 차면 멈추되 문구는 실제 값을 보여 준다. 무게는 한도를 넘을 수 있다.
        float clamped = Mathf.Clamp01(ratio);

        // 【좌표를 여기서 다시 적지 않는다.】
        // 예전에는 0.10~0.30을 하드코딩해 두었는데, 배치를 바꾸면서
        // 막대는 0.08~0.245로 옮겼고 이 줄만 남아 채움이 어긋났다.
        // 이제 상수에서 계산한다.
        weightFill.rectTransform.anchorMin = new Vector2(WeightBarLeft, WeightBarInset);

        weightFill.rectTransform.anchorMax = new Vector2(
            WeightBarLeft + (WeightBarRight - WeightBarLeft) * clamped, 1f - WeightBarInset);

        weightFill.color = bag.IsOverweight ? UIPalette.Warning : UIPalette.Action;

        weightLabel.text = $"{bag.TotalWeight:0.0} / {bag.WeightLimit:0.0} kg"
            + (bag.IsOverweight ? $"  ({EncumbranceName(bag.Encumbrance)})" : string.Empty);

        weightLabel.color = bag.IsOverweight ? UIPalette.Warning : UIPalette.TextDim;
    }

    private static string EncumbranceName(EncumbranceLevel level)
    {
        switch (level)
        {
            case EncumbranceLevel.Heavy:      return "과중량";
            case EncumbranceLevel.Overloaded: return "심한 과중량";
            case EncumbranceLevel.Immobile:   return "움직일 수 없음";
            default:                          return string.Empty;
        }
    }

    /// <summary>작은 알약 칩. 무게·가치처럼 짧은 수치를 담는다.</summary>
    private void DrawChip(float left, float bottom, float width, string text, Color color)
    {
        Image chip = UIFactory.CreatePanel($"Chip_{text}", detailContent, UIPalette.Inset,
            new Vector2(left, bottom), new Vector2(left + width, bottom + 0.058f), radius: 8);

        UIFactory.CreateOutline(chip, UIPalette.EdgeSoft, 8, 1);

        UIFactory.CreateLabel(chip.transform, text, 24, FontStyle.Bold,
            new Vector2(0.08f, 0f), new Vector2(0.92f, 1f), TextAnchor.MiddleCenter, color);
    }

    /// <summary>
    /// 표 한 줄. 좌측에 이름, 우측에 값.
    ///
    /// 홀짝으로 바탕을 번갈아 까는 이유 — 값이 여러 줄이면
    /// 어느 값이 어느 이름의 것인지 눈이 놓친다.
    /// </summary>
    // ── 수치 표 ───────────────────────────────────────────────────────
    //
    // 【행 높이를 글자에 맞춰 늘린다.】
    // 전에는 모든 행이 같은 높이(0.052)였다. 「대가: 초기 피해가 지연 피해로
    // 전환된다」처럼 값이 길면 글자가 행 밖으로 삐져나와 다음 행과 겹쳤다.
    // 이제 값의 실제 높이를 재서 그만큼 행을 늘린다.

    /// <summary>행 안쪽 위아래 여백(px).</summary>
    private const float StatRowPadPixels = 12f;

    /// <summary>한 줄짜리 행의 최소 높이(px). 손가락이 닿는 크기를 지킨다.</summary>
    private const float StatRowMinPixels = 46f;

    private const float StatRowGap = 0.008f;

    /// <summary>값 칸이 시작하는 가로 위치. 왼쪽은 부제목이 쓴다.</summary>
    private const float StatValueLeft = 0.42f;
    private const float StatValueRight = 0.96f;

    /// <summary>다음 행이 놓일 높이. 표를 그리기 전에 BeginStatRows가 되돌린다.</summary>
    private float statCursor;
    private int statIndex;

    private void BeginStatRows()
    {
        statCursor = StatTop;
        statIndex = 0;
    }

    /// <summary>
    /// 표 한 줄. 왼쪽에 부제목, 오른쪽에 값.
    ///
    /// 홀짝으로 바탕을 번갈아 까는 이유 — 값이 여러 줄이면
    /// 어느 값이 어느 이름의 것인지 눈이 놓친다.
    ///
    /// 값이 한 줄이면 부제목도 세로 가운데에, 여러 줄이면 둘 다 위에 붙인다.
    /// </summary>
    private void DrawStatRow(string label, string value, Color valueColor)
    {
        // 아래 버튼들을 덮기 시작하면 멈춘다. 겹치는 것보다 잘리는 편이 낫다.
        if (statCursor <= ActionRowTop + 0.04f)
            return;

        float host = Mathf.Max(1f, detailContent.rect.height);
        float width = Mathf.Max(1f, detailContent.rect.width);

        Image row = UIFactory.CreatePanel($"Row_{statIndex}", detailContent,
            statIndex % 2 == 0 ? UIPalette.Row : UIPalette.RowAlt,
            new Vector2(0f, statCursor - 0.05f), new Vector2(1f, statCursor), radius: 6);

        Text valueLabel = UIFactory.CreateLabel(row.transform, value, 23, FontStyle.Bold,
            new Vector2(StatValueLeft, 0f), new Vector2(StatValueRight, 1f),
            TextAnchor.UpperRight, valueColor);

        valueLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        valueLabel.verticalOverflow = VerticalWrapMode.Overflow;

        float textHeight = PreferredHeight(
            valueLabel, value, width * (StatValueRight - StatValueLeft));

        float pixels = Mathf.Max(StatRowMinPixels, textHeight + StatRowPadPixels);
        float height = pixels / host;

        row.rectTransform.anchorMin = new Vector2(0f, statCursor - height);
        row.rectTransform.anchorMax = new Vector2(1f, statCursor);
        row.rectTransform.offsetMin = Vector2.zero;
        row.rectTransform.offsetMax = Vector2.zero;

        bool single = pixels <= StatRowMinPixels;

        valueLabel.alignment = single ? TextAnchor.MiddleRight : TextAnchor.UpperRight;

        UIFactory.Inset(valueLabel.rectTransform,
            left: 0f, bottom: 0f, right: 0f, top: single ? 0f : StatRowPadPixels * 0.5f);

        Text nameLabel = UIFactory.CreateLabel(row.transform, label, 23, FontStyle.Normal,
            new Vector2(0.04f, 0f), new Vector2(StatValueLeft - 0.02f, 1f),
            single ? TextAnchor.MiddleLeft : TextAnchor.UpperLeft, UIPalette.TextDim);

        UIFactory.Inset(nameLabel.rectTransform,
            left: 0f, bottom: 0f, right: 0f, top: single ? 0f : StatRowPadPixels * 0.5f);

        statCursor -= height + StatRowGap;
        statIndex++;
    }

    /// <summary>
    /// 이 폭에서 글자가 몇 픽셀 높이가 되는가.
    ///
    /// 레이아웃이 확정되기 전에도 재야 하므로, 폭을 직접 넘겨 계산한다.
    /// </summary>
    private static float PreferredHeight(Text label, string text, float width)
    {
        TextGenerationSettings settings = label.GetGenerationSettings(new Vector2(width, 0f));

        settings.horizontalOverflow = HorizontalWrapMode.Wrap;
        settings.verticalOverflow = VerticalWrapMode.Overflow;

        return label.cachedTextGeneratorForLayout.GetPreferredHeight(text, settings)
               / Mathf.Max(0.0001f, label.pixelsPerUnit);
    }

    private static string ItemKindName(ItemKind kind)
    {
        switch (kind)
        {
            case ItemKind.Weapon:     return "무기";
            case ItemKind.Armour:     return "방어구";
            case ItemKind.Backpack:   return "가방";
            case ItemKind.Imprint:    return "각인";
            case ItemKind.SkillGem:   return "스킬 젬";
            case ItemKind.Consumable: return "소모품";
            case ItemKind.Key:        return "열쇠";
            case ItemKind.Material:   return "재료";
            default:                  return kind.ToString();
        }
    }

    private void DrawGemInfo(SkillDefinition skill)
    {
        string category = SkillCategoryName(skill.Category);

        var lines = new List<string>(5)
        {
            $"분류: {category}",
            $"요구 각성: Lv{skill.RequiredLevel}"
        };

        if (skill.Category == SkillCategory.Support && skill.RequiredTags != SkillTag.None)
            lines.Add($"요구 태그: {skill.RequiredTags.ToKoreanString()}");

        if (skill.Tags != SkillTag.None)
            lines.Add($"태그: {skill.Tags.ToKoreanString()}");

        if (!string.IsNullOrEmpty(skill.CostDescription))
            lines.Add($"대가: {skill.CostDescription}");

        BeginStatRows();

        for (int i = 0; i < lines.Count; i++)
        {
            int split = lines[i].IndexOf(':');

            string label = split > 0 ? lines[i].Substring(0, split) : lines[i];
            string value = split > 0 ? lines[i].Substring(split + 1).Trim() : string.Empty;

            DrawStatRow(label, value, UIPalette.Text);
        }
    }

    /// <summary>
    /// 가방 화면을 열어 한 줄을 띄운다. 검증 도구가 결과를 보이려고 쓴다.
    ///
    /// 【왜 필요한가】 Playtest 메뉴는 Debug.Log로만 결과를 말했다.
    /// 에디터 Console 창을 찾아 띄워 두지 않으면 "아무 일도 안 일어난 것"과
    /// 구분되지 않는다. 결과는 결과가 보이는 곳에 있어야 한다.
    /// </summary>
    public static void ShowBagWithMessage(string message)
    {
        InventoryScreenUI screen = EnsureInstance();

        if (screen == null)
            return;

        if (!screen.IsOpen)
            screen.Open((int)Tab.Bag);
        else
            screen.Refresh();

        screen.ShowToast(message);
    }

    /// <summary>열려 있을 때만 다시 그린다. 가방을 건드린 쪽이 부른다.</summary>
    public static void RefreshIfOpen()
    {
        if (instance != null && instance.IsOpen)
            instance.Refresh();
    }
}
