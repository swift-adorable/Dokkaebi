#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 안 검증 패널. (로드맵 6-P)
///
/// 【왜 화면 안에 있어야 하는가】
/// 검증 도구는 에디터 메뉴(MenuItem)로 만들었는데, MenuItem은 빌드에 없다.
/// 이 게임은 모바일이고 실기에서 확인하는 것이 당연한데,
/// 실기에서 장비를 얻을 방법이 하나도 없었다 — 「장비가 지급되지 않는다」의 정체다.
/// Console 창도 없으므로 결과도 여기에 띄운다.
///
/// 개발 빌드와 에디터에서만 컴파일된다. 출시 빌드에는 존재하지 않는다.
/// </summary>
public class PlaytestPanelUI : MonoBehaviour
{
    private static PlaytestPanelUI instance;

    private GameObject panel;
    private Text output;

    private static readonly (string label, System.Func<string> action)[] Actions =
    {
        ("티어1 한 벌",   PlaytestActions.GiveStarterKit),
        ("티어6 한 벌",   PlaytestActions.GiveEndgameKit),
        ("무기 6종",      PlaytestActions.GiveAllWeapons),
        ("대표 각인 4종", PlaytestActions.GiveKeyImprints),
        ("각인 24종",     PlaytestActions.GiveAllImprints),
        ("검증용 젬 4종", PlaytestActions.GiveChecklistGems),
        ("Core 젬 전부",  PlaytestActions.GiveCoreGems),
        ("Support 젬",    PlaytestActions.GiveSupportGems),
        ("가방 채우기",   PlaytestActions.FillBag),
        ("가방 비우기",   PlaytestActions.ClearBag),
        ("현재 능력치",   PlaytestActions.DumpStats),
        ("현재 젬 빌드",  PlaytestActions.DumpBuild),
        ("냉각 6 → 동결", () => PlaytestActions.StackOnNearest(StatusEffectType.Chill, 6)),
        ("감전 6 → 마비", () => PlaytestActions.StackOnNearest(StatusEffectType.Shock, 6)),
        ("중독 10 → 부식", () => PlaytestActions.StackOnNearest(StatusEffectType.Poison, 10))
    };

    private const int Columns = 3;

    public static PlaytestPanelUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<PlaytestPanelUI>(FindObjectsInactive.Include);

        if (instance != null)
            return instance;

        // 가방 화면(1000)보다 위에 둔다. 가방을 열어 둔 채로도 지급할 수 있어야 한다.
        Canvas canvas = UIFactory.CreateCanvas("PlaytestCanvas (Runtime)", 1200);

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

        panel = UIFactory.CreatePanel("PlaytestPanel", safe, UIPalette.Panel,
            new Vector2(0.12f, 0.06f), new Vector2(0.88f, 0.94f)).gameObject;

        var box = (RectTransform)panel.transform;

        UIFactory.CreatePanel("Header", box, UIPalette.Header,
            new Vector2(0f, 0.92f), new Vector2(1f, 1f));

        UIFactory.CreateLabel(box, "검증 도구 (개발 빌드 전용)", 32, FontStyle.Bold,
            new Vector2(0.03f, 0.92f), new Vector2(0.75f, 1f), TextAnchor.MiddleLeft);

        UIFactory.CreateButton(box, "닫기",
            new Vector2(0.78f, 0.925f), new Vector2(0.97f, 0.995f),
            UIPalette.Subtle, () => panel.SetActive(false), 26);

        BuildButtons(box);

        UIFactory.CreatePanel("OutputBack", box, UIPalette.SlotLocked,
            new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.26f));

        output = UIFactory.CreateLabel(box, "버튼을 누르면 결과가 여기에 나옵니다.", 24,
            FontStyle.Normal, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.25f),
            TextAnchor.UpperLeft, UIPalette.TextAccent);

        panel.SetActive(false);
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
                UIPalette.Action, () => Run(entry.action), 24);
        }
    }

    /// <summary>씬의 「DEBUG」 버튼이 부른다. (DebugUIManager)</summary>
    public void Toggle()
    {
        if (panel == null)
            return;

        panel.SetActive(!panel.activeSelf);
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
