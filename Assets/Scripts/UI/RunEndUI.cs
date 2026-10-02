using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 사망 화면. (로드맵 8-J · 감사 A12 해결)
///
/// 이 화면이 없던 동안에는 죽으면 시간이 멈춘 채 아무것도 뜨지 않았다 —
/// 디버그 「체력 회복」 말고는 빠져나갈 길이 없었다. 이제 벙커로 돌아간다.
///
/// 【무엇을 잃고 무엇이 남는지를 적는다.】 덕코프도 사망 뒤 결산을 보여 준다.
/// 저장은 이 화면이 뜨기 전에 이미 끝났다 (DokkaebiController.HandleDied) —
/// 여기서 앱을 꺼도 결과는 같다.
/// </summary>
public class RunEndUI : MonoBehaviour
{
    private static RunEndUI instance;

    private GameObject panel;
    private Text body;
    private Text buttonLabel;

    public static void ShowDeath(int lostCount)
    {
        if (instance == null)
        {
            Canvas canvas = UIFactory.CreateCanvas("RunEndCanvas (Runtime)", 1500);
            instance = canvas.gameObject.AddComponent<RunEndUI>();
            instance.Build(canvas);
        }

        instance.body.text =
            $"가방 · 장비 {lostCount}점을 잃었습니다.\n"
            + "레벨 · 엽전 · 패시브 · 새김패 · 창고는 남습니다.";

        instance.buttonLabel.text = SceneFlow.HasBunker ? "소굴로 돌아가기" : "다시 시작";

        instance.panel.SetActive(true);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build(Canvas canvas)
    {
        RectTransform safe = UIFactory.CreateSafeArea(canvas);

        // 뒤를 덮는다 — 멈춘 게임 화면을 더 조작할 일이 없다.
        Image shade = UIFactory.CreatePanel("Shade", safe, new Color(0f, 0f, 0f, 0.6f),
            Vector2.zero, Vector2.one, radius: 0);
        shade.raycastTarget = true;

        panel = shade.gameObject;

        Image box = UIFactory.CreateGlass("RunEndBox", shade.transform, UIPalette.Panel,
            new Vector2(0.30f, 0.28f), new Vector2(0.70f, 0.72f), UIFactory.RadiusLarge);

        UIFactory.CreateLabel(box.transform, "사망", 52, FontStyle.Bold,
            new Vector2(0.06f, 0.70f), new Vector2(0.94f, 0.94f),
            TextAnchor.MiddleCenter, UIPalette.Warning);

        body = UIFactory.CreateLabel(box.transform, string.Empty, 26, FontStyle.Normal,
            new Vector2(0.06f, 0.34f), new Vector2(0.94f, 0.70f),
            TextAnchor.MiddleCenter, UIPalette.TextOnGlass);

        Button button = UIFactory.CreateButton(box.transform, "소굴로 돌아가기",
            new Vector2(0.18f, 0.08f), new Vector2(0.82f, 0.28f),
            UIPalette.Action, OnReturn, 30);

        buttonLabel = button.GetComponentInChildren<Text>();

        panel.SetActive(false);
    }

    private void OnReturn()
    {
        panel.SetActive(false);
        SceneFlow.ReturnToBunker();
    }
}
