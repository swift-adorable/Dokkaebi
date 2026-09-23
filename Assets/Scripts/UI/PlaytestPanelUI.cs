#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 안 검증 패널. (로드맵 6-P)
///
/// 【왜 화면 안에 있어야 하는가】
/// 검증 도구는 에디터 메뉴(MenuItem)로 만들었는데, MenuItem은 빌드에 없다.
/// 이 게임은 모바일이고 실제 기기에서 확인하는 것이 당연한데,
/// 실제 기기에서 장비를 얻을 방법이 하나도 없었다 — 「장비가 지급되지 않는다」의 정체다.
/// Console 창도 없으므로 결과도 여기에 띄운다.
///
/// 개발 빌드와 에디터에서만 컴파일된다. 출시 빌드에는 존재하지 않는다.
/// </summary>
public class PlaytestPanelUI : MonoBehaviour
{
    private static PlaytestPanelUI instance;

    private GameObject panel;
    private GameObject toggle;
    private Text output;

    private static readonly (string label, System.Func<string> action)[] Actions =
    {
        ("티어1 한 벌",   PlaytestActions.GiveStarterKit),
        ("티어6 한 벌",   PlaytestActions.GiveEndgameKit),
        ("무기 6종",      PlaytestActions.GiveAllWeapons),
        ("각인 전부",     PlaytestActions.GiveAllImprints),
        ("젬 전부",       PlaytestActions.GiveAllGems),
        ("수분·에너지 절반", PlaytestActions.HalveSurvival),
        ("탈수·허기 즉시",   PlaytestActions.EmptySurvival),
        ("수분·에너지 가득", PlaytestActions.RefillSurvival),
        ("각성 Lv +1",    PlaytestActions.RaiseAwakeningLevel),
        ("계정 Lv +1",    PlaytestActions.RaiseAccountLevel),
        ("겹치는 재료",   PlaytestActions.GiveStackables),
        ("가방 채우기",   PlaytestActions.FillBag),
        ("가방 비우기",   PlaytestActions.ClearBag),
        ("체력 회복",     PlaytestActions.HealSelf),
        ("적 생성 시작",  PlaytestActions.StartSpawning),
        ("적 생성 정지",  PlaytestActions.StopSpawning),
        ("모든 적 제거",  PlaytestActions.KillAllEnemies),
        ("현재 능력치",   PlaytestActions.DumpStats),
        ("현재 젬 빌드",  PlaytestActions.DumpBuild),
        ("냉각 6 → 동결", () => PlaytestActions.StackOnNearest(StatusEffectType.Chill, 6)),
        ("감전 6 → 마비", () => PlaytestActions.StackOnNearest(StatusEffectType.Shock, 6)),
        ("중독 10 → 부식", () => PlaytestActions.StackOnNearest(StatusEffectType.Poison, 10)),

        // 위 셋은 【가장 가까운 적】에게 건다. 화면 좌상단의 상태이상 줄은
        // 【내】 상태를 보여 주므로, 적에게 걸어서는 줄이 뜨지 않는다.
        // 그 줄을 확인하려면 아래 둘을 쓴다.
        ("내게 기본 6종",     PlaytestActions.StackAllOnSelf),
        ("내게 위험 상태 3종", PlaytestActions.CriticalOnSelf),
        ("내게 냉각 6 → 동결", () => PlaytestActions.StackOnSelf(StatusEffectType.Chill, 6)),
        ("내 상태이상 해제",  PlaytestActions.ClearStatusOnSelf)
    };

    private const int Columns = 3;

    public static PlaytestPanelUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<PlaytestPanelUI>(FindObjectsInactive.Include);

        if (instance != null)
            return instance;

        // 【버튼은 가방 화면(1000)보다 아래, 패널은 맨 위.】
        //
        // 버튼을 위에 두었더니 좌상단 크레딧 카드를 덮었다. 개발용 버튼이
        // 게임 UI를 가리면 정작 확인해야 할 것을 못 본다. 그래서 캔버스는
        // 900에 두고, 가방을 열면 버튼은 그 뒤로 숨는다 — 지급 메뉴가 끝나면
        // 가방이 자동으로 열리므로 흐름에는 지장이 없다.
        //
        // 패널은 다르다. 열려 있는 동안에는 【무엇보다 위】여야 한다.
        // 가방·전리품 위에서 아이템을 지급하고 결과 줄을 읽는 도구인데
        // 그 판들 뒤에 깔리면 눌리지도, 읽히지도 않는다.
        // 그래서 패널만 자기 캔버스로 2000에 올린다 (BuildPanelCanvas).
        Canvas canvas = UIFactory.CreateCanvas("PlaytestCanvas (Runtime)", 900);

        instance = canvas.gameObject.AddComponent<PlaytestPanelUI>();
        instance.Build(canvas.transform);

        return instance;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build(Transform root)
    {
        // 화면 UI는 안전 영역 안에만 둔다. 노치와 홈 인디케이터를 피한다.
        RectTransform safe = UIFactory.CreateSafeArea(root.GetComponent<Canvas>());

        BuildToggle(safe);

        // 패널은 뒤가 비치면 글자가 안 읽힌다. 유리 중에서도 짙은 쪽을 쓴다.
        panel = UIFactory.CreateGlass("PlaytestPanel", safe,
            new Color(0.10f, 0.13f, 0.19f, 0.88f),
            new Vector2(0.12f, 0.06f), new Vector2(0.88f, 0.94f),
            UIFactory.RadiusLarge).gameObject;

        BuildPanelCanvas(panel);

        var box = (RectTransform)panel.transform;

        UIFactory.CreatePanel("Header", box, UIPalette.Header,
            new Vector2(0f, 0.92f), new Vector2(1f, 1f), UIFactory.RadiusLarge);

        UIFactory.CreateLabel(box, "디버그 도구 (개발 빌드 전용)", 32, FontStyle.Bold,
            new Vector2(0.03f, 0.92f), new Vector2(0.75f, 1f), TextAnchor.MiddleLeft);

        UIFactory.CreateButton(box, "닫기",
            new Vector2(0.79f, 0.928f), new Vector2(0.97f, 0.992f),
            UIPalette.Subtle, () => SetOpen(false), 26, UIFactory.Radius);

        BuildButtons(box);

        Image well = UIFactory.CreatePanel("OutputBack", box, UIPalette.Inset,
            new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.26f), UIFactory.Radius);

        UIFactory.CreateOutline(well, UIPalette.EdgeSoft, UIFactory.Radius, 2);

        output = UIFactory.CreateLabel(box, "버튼을 누르면 결과가 여기에 나옵니다.", 25,
            FontStyle.Normal, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.25f),
            TextAnchor.UpperLeft, UIPalette.TextAccent);

        panel.SetActive(false);
    }

    /// <summary>
    /// 패널만 따로 맨 위로 올린다.
    ///
    /// 【부모 캔버스를 통째로 올리지 않는 이유】 그러면 닫혀 있을 때도
    /// 디버그 버튼이 가방·전리품 위에 떠서 게임 화면을 가린다. 자식에
    /// Canvas를 붙이고 overrideSorting을 켜면 그 가지만 순서를 새로 잡는다.
    ///
    /// 【GraphicRaycaster를 같이 붙여야 한다.】 overrideSorting을 켠 순간
    /// 이 가지는 부모 캔버스의 레이캐스터가 아니라 자기 것을 쓴다.
    /// 빼먹으면 화면에는 맨 위에 보이는데 버튼이 하나도 안 눌린다.
    /// (상세 팝업 1100에서 같은 것을 빼먹어 한 번 겪었다)
    ///
    /// 2000은 지금 쓰는 가장 높은 값(버리기 창 1110)보다 넉넉히 위다.
    /// </summary>
    private const int PanelSortingOrder = 2000;

    private static void BuildPanelCanvas(GameObject target)
    {
        var canvas = target.AddComponent<Canvas>();

        canvas.overrideSorting = true;
        canvas.sortingOrder = PanelSortingOrder;

        target.AddComponent<UnityEngine.UI.GraphicRaycaster>();
    }

    /// <summary>
    /// 검증 버튼. 씬의 DEBUG 버튼을 대신한다.
    ///
    /// 씬 오브젝트가 아니라 여기서 만드는 이유 —
    /// 이 파일 전체가 `UNITY_EDITOR || DEVELOPMENT_BUILD`로 감싸여 있어서
    /// **출시 빌드에는 컴파일조차 되지 않는다.** 씬에 놓인 버튼은 그 보장을 못 받는다.
    /// 안전 영역 안에 두므로 노치·홈 인디케이터에도 걸리지 않는다.
    /// </summary>
    /// <summary>
    /// 화면 버튼줄의 자리·크기. 【InventoryScreenUI와 같은 값이어야 한다.】
    /// 두 캔버스가 나뉘어 있어 한쪽만 고치면 줄이 어긋난다.
    /// </summary>
    private const float ButtonWidth = 132f;
    private const float ButtonHeight = 62f;
    private const float ButtonMargin = 10f;

    private void BuildToggle(Transform safe)
    {
        Image image = UIFactory.CreatePanel("DebugToggle", safe, UIPalette.Warning,
            Vector2.zero, Vector2.zero, UIFactory.RadiusLarge);

        toggle = image.gameObject;

        // 【왼쪽 아래.】 오른쪽 위는 장비·스킬·패시브가, 왼쪽 위는 생존
        // 게이지와 상태이상 줄이 쓴다. 개발용 버튼을 그 줄에 섞으면
        // 출시 화면과 개발 화면이 같아 보인다. 남은 구석에 두면
        // 「이건 게임의 일부가 아니다」가 자리로 드러난다.
        //
        // 가방을 열면 이 버튼은 가방 판(1000) 뒤로 숨는다. 이 캔버스가
        // 900이기 때문이며, 의도한 것이다 — 지급이 끝나면 가방이 자동으로
        // 열리므로 흐름에 지장이 없다.
        var rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
        rect.anchoredPosition = new Vector2(ButtonMargin, ButtonMargin);

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(Toggle);

        UIFactory.CreateOutline(image, UIPalette.Rim, UIFactory.RadiusLarge, 2);

        // 【이름은 「디버그」.】 「검증」은 QA가 하는 일처럼 읽혀서,
        // 이것이 개발 빌드에만 있는 도구라는 사실이 드러나지 않았다.
        UIFactory.CreateLabel(image.transform, "디버그", 28, FontStyle.Bold,
            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
    }

    private void BuildButtons(RectTransform box)
    {
        RectTransform grid = UIFactory.CreateRegion("Buttons", box,
            new Vector2(0.03f, 0.28f), new Vector2(0.97f, 0.90f));

        int rows = Mathf.CeilToInt(Actions.Length / (float)Columns);

        for (int i = 0; i < Actions.Length; i++)
        {
            UIFactory.GetCellAnchors(i, Columns, rows, 0.008f,
                out Vector2 min, out Vector2 max);

            (string label, System.Func<string> action) entry = Actions[i];

            UIFactory.CreateButton(grid, entry.label, min, max,
                UIPalette.Action, () => Run(entry.action), 25);
        }
    }

    /// <summary>씬의 「DEBUG」 버튼이 부른다. (DebugUIManager)</summary>
    public void Toggle()
    {
        if (panel == null)
            return;

        SetOpen(!panel.activeSelf);
    }

    /// <summary>
    /// 패널을 켜고 끈다. 【화면 버튼도 함께 치운다.】
    ///
    /// 장비·스킬·패시브 세 버튼은 가방 캔버스(1000) 밖의 화면 UI라
    /// 이 패널(900) 위로 그대로 떠올라 오른쪽 위 칸을 덮었다.
    /// 여기 한 곳에서만 여닫아, 어느 경로로 닫아도 버튼이 되돌아온다.
    /// </summary>
    private void SetOpen(bool open)
    {
        panel.SetActive(open);

        InventoryScreenUI.SetHudSuppressed(open);
    }

    /// <summary>
    /// 가방 화면이 열리고 닫힐 때 불린다.
    ///
    /// 【왜 필요한가】
    /// 검증 캔버스를 가방(1000)보다 아래(900)로 내렸는데, 가방 판이 반투명이라
    /// 뒤에 켜져 있던 검증 패널의 글자가 그대로 **비쳐 보였다.**
    /// 두 화면의 글자가 겹쳐 무엇도 읽히지 않았다.
    /// 뒤에 있으면 어차피 누를 수도 없으니, 열려 있을 때는 통째로 감춘다.
    /// </summary>
    public static void SetHiddenByScreen(bool hidden)
    {
        if (instance == null)
            return;

        if (instance.panel != null && hidden && instance.panel.activeSelf)
            instance.SetOpen(false);

        if (instance.toggle != null)
            instance.toggle.SetActive(!hidden);
    }

    private void Run(System.Func<string> action)
    {
        string message;

        // 도구가 게임을 죽이면 안 된다. 예외도 결과의 일부로 보여 준다.
        try
        {
            message = action();
        }
        catch (System.Exception e)
        {
            message = $"실패: {e.GetType().Name} — {e.Message}";
        }

        if (output != null)
            output.text = message;

        Debug.Log("[Playtest] " + message);

        InventoryScreenUI.RefreshIfOpen();
    }
}
#endif
