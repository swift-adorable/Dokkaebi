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

    private const float TopBarBottom = 0.90f;
    private const float ColumnTop = TopBarBottom;
    private const float ColumnBottom = 0.10f;
    private const float FooterTop = ColumnBottom;

    /// <summary>좌우 두 칸의 경계. 양쪽이 반 칸씩 물러나 사이가 한 칸이 된다.</summary>
    private const float ColumnSplit = 0.5f;

    /// <summary>
    /// 중량 막대가 카드 안에서 차지하는 가로 범위.
    ///
    /// 【글자 자리와 겹치지 않게 끊는다.】
    /// 0.085에서 시작하던 막대가 「소지 중량」 글자(0.045~0.20) 위로
    /// 올라와 글자를 반쯤 덮고 있었다. 세 칸은 서로 넘지 않는다.
    ///   0.04~0.22 글자 · 0.25~0.62 막대 · 0.64~0.96 숫자
    /// </summary>
    private const float WeightBarLeft = 0.25f;
    private const float WeightBarRight = 0.62f;

    /// <summary>중량 막대의 위아래 여백. 아래줄 높이 기준 비율이다.</summary>
    private const float WeightBarInset = 0.24f;

    /// <summary>
    /// 가방 격자의 열 수.
    ///
    /// 장비 탭은 폭을 다 쓰므로 여덟 열, 스킬 탭은 절반이라 여섯 열이다.
    /// 열 수를 고정해 두면 넓은 쪽에서 칸이 주먹만 해진다.
    /// </summary>
    private int BagColumns => UsesFullWidth ? 8 : 6;

    /// <summary>이 탭이 좌측 칸에 폭을 다 주는가. 소켓판이 있는 스킬 탭만 나눠 쓴다.</summary>
    private bool UsesFullWidth => tab != Tab.Socket;

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

    /// <summary>
    /// 칸(장비 슬롯 · 가방 칸) 안쪽 여백. 【캔버스 픽셀】이다.
    /// 칸은 가로세로 비가 제각각이라 비율로 주면 위아래만 얇아진다.
    /// </summary>
    private const float SlotPad = 12f;

    private static InventoryScreenUI instance;

    private GameObject panel;
    private GameObject toggleButton;
    private Text toggleLabel;

    /// <summary>끼울 수 있는 젬이 있을 때 가방 버튼에 붙는 점.</summary>
    private Image socketDot;

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

    /// <summary>위·아래 줄. 바깥 여백은 panel이 이미 들여 놨다.</summary>
    private RectTransform topBar;
    private RectTransform footer;

    private RectTransform quickSlots;

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

    /// <summary>「장비」 머리글. 스킬 탭에서는 통째로 꺼진다.</summary>
    private Text equipTitleLabel;

    /// <summary>가방 격자 뒤에 깔린 눌린 면. 장비 칸을 끄면 같이 위로 늘어난다.</summary>
    private Image bagWell;

    private Text bagTitleLabel;
    private Text weightLabel;
    private Image weightFill;

    private readonly List<Button> tabButtons = new();
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
    }

    private void HandleExternalChange()
    {
        RefreshToggle();

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

        BuildToggleButton(safeArea);

        // 【바깥 테두리는 여기 한 번만 준다.】
        // 전에는 줄마다 0f / 1f로 붙여 놓고 안쪽만 여백을 줬더니,
        // 화면 끝에는 여백이 없고 안쪽만 떠 있는 모양이 됐다.
        RectTransform panelRect = UIFactory.CreateRegion(
            "Panel", safeArea, Vector2.zero, Vector2.one);

        UIFactory.Inset(panelRect, UIFactory.Gap);

        panel = panelRect.gameObject;

        // 덮개는 패널 안에 둔다. 패널이 꺼지면 같이 꺼져야 한다.
        // 안전 영역 밖까지 덮으려고 앵커를 넉넉히 넘긴다.
        Image dim = UIFactory.CreatePanel("Dim", panel.transform, UIPalette.Dim,
            new Vector2(-0.2f, -0.2f), new Vector2(1.2f, 1.2f));

        dim.raycastTarget = true;

        float half = UIFactory.Gap * 0.5f;

        topBar = UIFactory.CreateSlice("TopBar", panel.transform,
            new Vector2(0f, TopBarBottom), Vector2.one, bottom: half);

        RectTransform body = UIFactory.CreateSlice("Body", panel.transform,
            new Vector2(0f, ColumnBottom), new Vector2(1f, ColumnTop),
            bottom: half, top: half);

        footer = UIFactory.CreateSlice("Footer", panel.transform,
            Vector2.zero, new Vector2(1f, FooterTop), top: half);

        BuildTopBar();
        BuildLeftColumn(body);

        rightPanel = UIFactory.CreateSlice("Right", body,
            new Vector2(ColumnSplit, 0f), Vector2.one, left: half);

        BuildBottomBar();
        BuildToast();

        panel.SetActive(false);
    }

    /// <summary>항상 떠 있는 「가방」 버튼. 빈 소켓이 있으면 색으로 알린다.</summary>
    private void BuildToggleButton(Transform parent)
    {
        toggleButton = UIFactory.CreateChild("InventoryToggle", parent);

        var rect = toggleButton.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(220f, 96f);
        rect.anchoredPosition = new Vector2(-8f, -8f);

        var image = toggleButton.AddComponent<Image>();
        image.color = UIPalette.Header;
        image.sprite = UISprites.Rounded(UIFactory.RadiusLarge);
        image.type = Image.Type.Sliced;

        var button = toggleButton.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(Toggle);

        UIFactory.CreateOutline(image, UIPalette.Rim, UIFactory.RadiusLarge, 2);

        toggleLabel = UIFactory.CreateLabel(toggleButton.transform, "가방", 35, FontStyle.Bold,
            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

        // 빈 소켓 알림 점. 버튼 좌상단 모서리에 걸친다.
        socketDot = UIFactory.CreatePanel("SocketDot", toggleButton.transform,
            UIPalette.TextAccent, new Vector2(-0.08f, 0.62f), new Vector2(0.30f, 1.16f),
            radius: 14);

        UIFactory.CreateLabel(socketDot.transform, "0", 24, FontStyle.Bold,
            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, new Color(0.08f, 0.08f, 0.1f));

        socketDot.gameObject.SetActive(false);
    }

    private void BuildTopBar()
    {
        // 크레딧 — 스크린샷의 좌상단 화폐 표시 자리.
        Image purse = UIFactory.CreateGlass("Credits", topBar, UIPalette.Header,
            Vector2.zero, new Vector2(0.185f, 1f), UIFactory.RadiusLarge);

        creditLabel = UIFactory.CreateLabel(purse.transform, "₡ 0", 35, FontStyle.Bold,
            new Vector2(0.06f, 0f), new Vector2(0.94f, 1f), TextAnchor.MiddleRight,
            UIPalette.TextAccent);

        // 탭 — 스크린샷의 상단 중앙 아이콘 줄. 아트 전이라 글자로 둔다.
        // 우상단은 「가방」 토글이 먹는다. 탭 줄의 중심을 그만큼 왼쪽으로 민다.
        float width = 0.115f;
        float gap = 0.01f;
        float total = TabNames.Length * width + (TabNames.Length - 1) * gap;
        float startX = 0.46f - total * 0.5f;

        tabButtons.Clear();

        for (int i = 0; i < TabNames.Length; i++)
        {
            float x = startX + i * (width + gap);
            int captured = i;

            Button button = UIFactory.CreateButton(topBar, TabNames[i],
                new Vector2(x, 0f), new Vector2(x + width, 1f),
                UIPalette.Inset, () => SelectTab((Tab)captured), 32, UIFactory.RadiusLarge);

            tabButtons.Add(button);
        }
    }

    // ── 좌측 칸의 세로 배치 ───────────────────────────────────────────
    // 장비 탭은 「장비 머리글 · 장비 8칸 · 가방 머리글 · 가방 격자」 네 단이고,
    // 스킬 탭은 장비 두 단을 접어 젬 목록이 위까지 올라온다.
    //
    // 【장비 칸을 조금 키웠다.】
    // 0.63~0.905(27.5%)에서 0.60~0.905(30.5%)로 늘린다. 장비 탭이 폭을
    // 다 쓰게 되면서 칸이 가로로 두 배가 되므로, 「진압용 중장갑 (중간)」
    // 같은 긴 이름이 두 줄로 들어간다.

    private const float EquipTitleBottom = 0.915f;
    private const float EquipGridTop = 0.905f;
    private const float EquipGridBottom = 0.60f;
    private const float BagTitleTop = 0.595f;
    private const float BagTitleBottom = 0.525f;
    private const float BagTop = 0.515f;

    /// <summary>장비 칸을 접었을 때 — 젬 목록이 쓰는 범위.</summary>
    private const float GemTitleTop = 0.985f;
    private const float GemTitleBottom = 0.925f;
    private const float GemListTop = 0.915f;

    /// <summary>장비 칸을 보일지에 맞춰 좌측 칸의 세로 배치를 바꾼다.</summary>
    private void LayoutLeftColumn(bool showEquipment)
    {
        equipTitleLabel.gameObject.SetActive(showEquipment);
        equipmentGrid.gameObject.SetActive(showEquipment);

        float titleTop = showEquipment ? 0.985f : GemTitleTop;
        float titleBottom = showEquipment ? BagTitleBottom : GemTitleBottom;
        float listTop = showEquipment ? BagTop : GemListTop;

        var title = bagTitleLabel.rectTransform;
        title.anchorMin = new Vector2(0f, titleBottom);
        title.anchorMax = new Vector2(1f, showEquipment ? BagTitleTop : titleTop);
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

        equipTitleLabel = UIFactory.CreateLabel(content, "장비", 30, FontStyle.Bold,
            new Vector2(0f, EquipTitleBottom), new Vector2(1f, 0.985f),
            TextAnchor.MiddleLeft, UIPalette.TextDim);

        equipmentGrid = UIFactory.CreateRegion("Equipment", content,
            new Vector2(0f, EquipGridBottom), new Vector2(1f, EquipGridTop));

        bagTitleLabel = UIFactory.CreateLabel(content, "가방", 30, FontStyle.Bold,
            new Vector2(0f, BagTitleBottom), new Vector2(1f, BagTitleTop),
            TextAnchor.MiddleLeft, UIPalette.TextDim);

        // 가방 격자 뒤에 한 단계 눌린 면을 깔아 깊이를 준다.
        bagWell = UIFactory.CreatePanel("BagWell", content, UIPalette.Inset,
            Vector2.zero, new Vector2(1f, BagTop), UIFactory.Radius);

        bagViewport = UIFactory.CreateSlice("Bag", content,
            Vector2.zero, new Vector2(1f, BagTop),
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

        RectTransform weightCard = UIFactory.CreateSlice("WeightCard", footer,
            Vector2.zero, new Vector2(0.40f, 1f), right: half);

        UIFactory.CreateGlass("Back", weightCard, UIPalette.Panel,
            Vector2.zero, Vector2.one, UIFactory.RadiusLarge);

        UIFactory.CreateLabel(weightCard, "소지 중량", 23, FontStyle.Normal,
            new Vector2(0.04f, 0f), new Vector2(0.22f, 1f),
            TextAnchor.MiddleLeft, UIPalette.TextDim);

        UIFactory.CreatePanel("WeightTrack", weightCard, UIPalette.Inset,
            new Vector2(WeightBarLeft, WeightBarInset),
            new Vector2(WeightBarRight, 1f - WeightBarInset), radius: 6);

        weightFill = UIFactory.CreatePanel("WeightFill", weightCard, UIPalette.Action,
            new Vector2(WeightBarLeft, WeightBarInset),
            new Vector2(WeightBarLeft, 1f - WeightBarInset), radius: 6);

        weightLabel = UIFactory.CreateLabel(weightCard, string.Empty, 24, FontStyle.Bold,
            new Vector2(0.64f, 0f), new Vector2(0.96f, 1f),
            TextAnchor.MiddleRight, UIPalette.TextDim);

        // 퀵슬롯 1~8 — 하단 중앙.
        quickSlots = UIFactory.CreateSlice("QuickSlots", footer,
            new Vector2(0.40f, 0f), new Vector2(0.86f, 1f), left: half, right: half);

        // 「닫기」도 반 칸 물러난다 — 퀵슬롯과의 간격이 다른 경계와 같아진다.
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

    private void BuildToast()
    {
        Image back = UIFactory.CreatePanel("Toast", panel.transform, UIPalette.Header,
            new Vector2(0.18f, FooterTop + 0.02f), new Vector2(0.82f, FooterTop + 0.11f),
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
        RefreshTabs();
        RefreshCredits();

        // 【탭마다 필요한 만큼만 쓴다.】
        //   장비  — 좌측만. 상세는 눌렀을 때 가운데에 뜨므로 우측이 놀 이유가 없다.
        //   스킬  — 좌 젬 목록 · 우 소켓판. 둘을 같이 봐야 어디에 끼울지 정한다.
        //   패시브 — 우측만. 트리를 넓게 펴야 한다.
        bool showLeft = tab != Tab.Passive;
        bool showRight = tab != Tab.Bag;

        float half = UIFactory.Gap * 0.5f;

        leftColumn.gameObject.SetActive(showLeft);
        quickSlots.gameObject.SetActive(showLeft);
        rightPanel.gameObject.SetActive(showRight);

        if (showLeft)
        {
            // 우측이 없으면 폭을 다 쓴다. 앵커만 옮기면 오른쪽에 반 칸이
            // 남으므로 물러났던 여백도 같이 되돌린다.
            leftColumn.anchorMax = new Vector2(showRight ? ColumnSplit : 1f, 1f);
            leftColumn.offsetMax = new Vector2(showRight ? -half : 0f, 0f);

            LayoutLeftColumn(tab != Tab.Socket);

            if (tab != Tab.Socket)
                RefreshEquipment();

            RefreshBag();
            RefreshQuickSlots();
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

            if (tab == Tab.Socket)
                DrawSocketPanel();
            else
                DrawPassivePanel();
        }

        RefreshToggle();
    }

    private void RefreshTabs()
    {
        for (int i = 0; i < tabButtons.Count; i++)
        {
            if (tabButtons[i] == null)
                continue;

            var image = tabButtons[i].targetGraphic as Image;

            if (image != null)
                image.color = (Tab)i == tab ? UIPalette.Action : UIPalette.Inset;

            // 고른 탭만 글자를 밝힌다. 색만으로는 작은 화면에서 구분이 약하다.
            var label = tabButtons[i].GetComponentInChildren<Text>();

            if (label != null)
                label.color = (Tab)i == tab ? UIPalette.Text : UIPalette.TextDim;
        }
    }

    private void RefreshCredits()
    {
        int amount = PassiveManager.HasInstance ? PassiveManager.Instance.Credits : 0;

        creditLabel.text = $"₡ {amount:N0}";
    }

    private void RefreshToggle()
    {
        if (toggleLabel == null || !SkillManager.HasInstance)
            return;

        int free = SkillManager.Instance.Build.FreeSocketCount;
        int gems = SkillManager.Instance.GetGemsInBag().Count;

        bool canSocket = free > 0 && gems > 0;

        // 【글자로 두 줄을 쓰지 않는다.】
        // 「가방 / 빈 소켓 1」은 버튼 안에서 무슨 뜻인지 읽히지 않는다.
        // 끼울 수 있는 젬이 있다는 신호는 점 하나면 충분하다.
        toggleLabel.text = "가방";
        toggleLabel.color = UIPalette.Text;

        if (socketDot != null)
        {
            socketDot.gameObject.SetActive(canSocket);

            if (socketDot.transform.childCount > 0)
            {
                var count = socketDot.transform.GetChild(0).GetComponent<Text>();

                if (count != null)
                    count.text = free.ToString();
            }
        }
    }

    // ────────────────────────────────── 좌측 — 장비

    private void RefreshEquipment()
    {
        UIFactory.ClearChildren(equipmentGrid);

        EquipmentLoadout loadout = PlayerInventory.EnsureInstance().Loadout;

        var slots = (EquipmentSlot[])System.Enum.GetValues(typeof(EquipmentSlot));

        for (int i = 0; i < slots.Length; i++)
        {
            UIFactory.GetCellAnchors(i, 4, 2, 0.008f, out Vector2 min, out Vector2 max);

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
                UIFactory.CreateLabel(cell.transform, EquipmentSlotName(slots[i]), 21,
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
                stack.Definition.DisplayName, 22, FontStyle.Bold,
                new Vector2(0.05f, 0f), new Vector2(0.95f, 1f),
                TextAnchor.MiddleLeft, Color.white);

            FitName(equipName, 22);

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

        int hidden = 0;

        foreach (ItemStack stack in bag.Stacks)
        {
            if (BelongsToTab(stack))
                bagStacks.Add(stack);
            else
                hidden++;
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

        bagTitleLabel.text = $"가방 ({bag.UsedSlots}/{bag.SlotCapacity})"
            + (hidden > 0 ? $"    젬 {hidden}개는 스킬 탭에" : string.Empty);

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
        bool empty = stack?.Definition == null;
        bool chosen = !empty && stack == selected;

        Color kind = empty ? UIPalette.Slot : UIPalette.ForItem(stack.Definition.Kind);

        Color body = empty
            ? UIPalette.Inset
            : chosen
                ? UIPalette.SlotSelected
                : UIPalette.Glassify(kind, 0.34f);

        Image cell = UIFactory.CreatePanel(name, parent, body, min, max);

        // 빈 칸에는 광택을 얹지 않는다. 격자 전체가 번들거려 아이템이 묻힌다.
        UIFactory.CreateOutline(cell,
            empty ? UIPalette.EdgeSoft
                  : chosen ? UIPalette.Brighten(UIPalette.SlotSelected, 0.22f)
                           : UIPalette.Glassify(UIPalette.Brighten(kind, 0.34f), 0.70f),
            UIFactory.Radius,
            chosen ? 3 : 2);

        var button = cell.gameObject.AddComponent<Button>();
        button.targetGraphic = cell;
        button.interactable = !empty;

        ItemStack captured = stack;
        button.onClick.AddListener(() => SelectStack(captured));

        if (empty)
            return;

        // 종류 도형. 가운데에 크게 깔아 배경처럼 쓴다 —
        // 글자를 읽기 전에 「무기인가 재료인가」가 먼저 들어온다.
        // 실제 그림이 생기면 definition.Icon이 이 자리를 대신한다.
        Image glyph = UIFactory.CreatePanel("Glyph", cell.transform, UIPalette.GlyphTint,
            new Vector2(0.20f, 0.16f), new Vector2(0.80f, 0.76f), radius: 0);

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

        // 이름은 좌상단. 반투명 검정 띠를 깔아 어떤 칸 색 위에서도 흰 글자가 읽히게 한다.
        Image strip = UIFactory.CreatePanel("NameStrip", cell.transform, UIPalette.NameStrip,
            new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.96f), radius: 6);

        strip.raycastTarget = false;

        Text nameLabel = UIFactory.CreateLabel(strip.transform, stack.Definition.DisplayName,
            19, FontStyle.Bold, new Vector2(0.06f, 0f), new Vector2(0.94f, 1f),
            TextAnchor.MiddleLeft, Color.white);

        FitName(nameLabel, 19);

        // 개수는 우하단. 겹칠 수 있는 물건에만 뜬다 —
        // 1개짜리에 「1」을 붙이면 잡음이다.
        if (stack.Count > 1)
        {
            UIFactory.CreateBadge(cell.transform, stack.Count.ToString(),
                new Vector2(0.56f, 0.06f), new Vector2(0.96f, 0.30f), 22,
                Color.white);
        }

        DrawDurabilityBar(cell.transform, stack);
    }

    /// <summary>
    /// 이름이 칸을 넘지 않게 맞춘다.
    ///
    /// 두 줄까지 접고, 그래도 안 들어가면 글자를 줄인다.
    /// 최소 크기를 원래의 3분의 2로 묶어 둔다 — 그 아래로 내려가면
    /// 읽히지가 않아서 이름을 적은 의미가 없다.
    /// </summary>
    private static void FitName(Text label, int size)
    {
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = Mathf.Max(12, Mathf.RoundToInt(size * 0.67f));
        label.resizeTextMaxSize = size;
    }

    /// <summary>내구도 막대. 없는 아이템에는 그리지 않는다.</summary>
    private static void DrawDurabilityBar(Transform cell, ItemStack stack)
    {
        if (stack.Definition == null || !stack.Definition.HasDurability)
            return;

        if (stack.MaxDurability <= 0)
            return;

        float ratio = Mathf.Clamp01(stack.Durability / (float)stack.MaxDurability);

        UIFactory.CreatePanel("DurTrack", cell, UIPalette.NameStrip,
            new Vector2(0.06f, 0.035f), new Vector2(0.50f, 0.085f), radius: 3);

        Color color = stack.IsBroken
            ? UIPalette.Warning
            : stack.IsWorn
                ? UIPalette.Cost
                : UIPalette.Gain;

        UIFactory.CreatePanel("DurFill", cell, color,
            new Vector2(0.06f, 0.035f),
            new Vector2(0.06f + (0.50f - 0.06f) * ratio, 0.085f), radius: 3);
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

    private void RefreshQuickSlots()
    {
        UIFactory.ClearChildren(quickSlots);

        for (int i = 0; i < 8; i++)
        {
            UIFactory.GetCellAnchors(i, 8, 1, 0.006f, out Vector2 min, out Vector2 max);

            Image cell = UIFactory.CreatePanel($"Quick_{i}", quickSlots,
                UIPalette.Inset, min, max);

            UIFactory.CreateOutline(cell, UIPalette.EdgeSoft, UIFactory.Radius, 2);

            UIFactory.CreateLabel(cell.transform, (i + 1).ToString(), 21, FontStyle.Normal,
                new Vector2(0.1f, 0.05f), new Vector2(0.88f, 0.4f),
                TextAnchor.LowerRight, UIPalette.TextDim);
        }
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
    private void DrawStatRow(int index, float top, string label, string value, Color valueColor)
    {
        const float RowHeight = 0.052f;
        const float Gap = 0.006f;

        float bottom = top - (index + 1) * RowHeight - index * Gap;

        Image row = UIFactory.CreatePanel($"Row_{index}", detailContent,
            index % 2 == 0 ? UIPalette.Row : UIPalette.RowAlt,
            new Vector2(0f, bottom), new Vector2(1f, bottom + RowHeight), radius: 6);

        UIFactory.CreateLabel(row.transform, label, 24, FontStyle.Normal,
            new Vector2(0.04f, 0f), new Vector2(0.60f, 1f), TextAnchor.MiddleLeft,
            UIPalette.TextDim);

        UIFactory.CreateLabel(row.transform, value, 24, FontStyle.Bold,
            new Vector2(0.60f, 0f), new Vector2(0.96f, 1f), TextAnchor.MiddleRight,
            valueColor);
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

        for (int i = 0; i < lines.Count; i++)
        {
            int split = lines[i].IndexOf(':');

            string label = split > 0 ? lines[i].Substring(0, split) : lines[i];
            string value = split > 0 ? lines[i].Substring(split + 1).Trim() : string.Empty;

            DrawStatRow(i, StatTop, label, value, UIPalette.Text);
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
