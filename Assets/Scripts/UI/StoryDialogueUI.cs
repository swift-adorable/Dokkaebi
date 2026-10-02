using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 이야기 장면을 보여 주는 창. (로드맵 3단계 · 결정 2-47)
///
/// 【컷신을 쓰지 않는다.】 본문 문단을 한 쪽씩 화면 아래 글상자에 띄우고,
/// 누르면 넘긴다. 싸움 장면은 본문에 없다 — 싸우기 전후의 인과와 대사만 있다.
///
/// 【보는 동안 시간을 멈춘다.】 파밍 중에 글을 읽다가 맞으면 안 된다.
/// 「건너뛰기」는 남은 쪽을 다 넘기고 본 것으로 친다.
///
/// 토막이 여러 개 몰리면(보스 뒤 이야기 + 사신패) 차례로 보여 준다.
/// </summary>
public class StoryDialogueUI : MonoBehaviour
{
    private sealed class Request
    {
        public string Title;
        public List<string> Pages;
        public Action Done;
    }

    private static StoryDialogueUI instance;

    private readonly Queue<Request> queue = new();

    private Request current;
    private int page;
    private float previousTimeScale = 1f;

    private GameObject panel;
    private Text title;
    private Text body;
    private Text counter;

    private Text banner;
    private float bannerUntil;

    /// <summary>보고 있는 중인가. 다른 화면이 입력을 받지 않게 할 때 쓴다.</summary>
    public static bool IsShowing => instance != null && instance.current != null;

    public static void Show(string heading, IList<string> pages, Action done)
    {
        if (pages == null || pages.Count == 0)
        {
            done?.Invoke();
            return;
        }

        StoryDialogueUI ui = EnsureInstance();
        ui.queue.Enqueue(new Request { Title = heading ?? string.Empty, Pages = new List<string>(pages), Done = done });

        if (ui.current == null)
            ui.Next();
    }

    /// <summary>시간을 멈추지 않는 짧은 알림 — 「야광귀가 나타났다」.</summary>
    public static void ShowBanner(string text, float seconds = 3f)
    {
        StoryDialogueUI ui = EnsureInstance();
        ui.banner.text = text;
        ui.banner.gameObject.SetActive(true);
        ui.bannerUntil = Time.unscaledTime + seconds;
    }

    private static StoryDialogueUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        Canvas canvas = UIFactory.CreateCanvas("StoryCanvas (Runtime)", 1400);
        instance = canvas.gameObject.AddComponent<StoryDialogueUI>();
        instance.Build(canvas);
        return instance;
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        // 씬이 바뀌며 사라질 때 시간을 멈춘 채 두지 않는다.
        if (current != null)
            Time.timeScale = previousTimeScale;

        instance = null;
    }

    private void Update()
    {
        if (banner.gameObject.activeSelf && Time.unscaledTime > bannerUntil)
            banner.gameObject.SetActive(false);
    }

    private void Build(Canvas canvas)
    {
        RectTransform safe = UIFactory.CreateSafeArea(canvas);

        Image shade = UIFactory.CreatePanel("StoryShade", safe, new Color(0f, 0f, 0f, 0.45f),
            Vector2.zero, Vector2.one, radius: 0);
        shade.raycastTarget = true;

        // 어디를 눌러도 넘어간다 — 엄지가 버튼을 찾지 않아도 된다.
        var tap = shade.gameObject.AddComponent<Button>();
        tap.transition = Selectable.Transition.None;
        tap.onClick.AddListener(OnNext);

        panel = shade.gameObject;

        Image box = UIFactory.CreateGlass("StoryBox", shade.transform, UIPalette.Panel,
            new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.46f), UIFactory.RadiusLarge);

        title = UIFactory.CreateLabel(box.transform, string.Empty, 28, FontStyle.Bold,
            new Vector2(0.05f, 0.82f), new Vector2(0.70f, 0.96f), TextAnchor.MiddleLeft, UIPalette.TextAccent);

        counter = UIFactory.CreateLabel(box.transform, string.Empty, 22, FontStyle.Normal,
            new Vector2(0.70f, 0.82f), new Vector2(0.95f, 0.96f), TextAnchor.MiddleRight, UIPalette.TextDim);

        body = UIFactory.CreateLabel(box.transform, string.Empty, 27, FontStyle.Normal,
            new Vector2(0.05f, 0.20f), new Vector2(0.95f, 0.82f), TextAnchor.UpperLeft, UIPalette.TextOnGlass);
        body.lineSpacing = 1.15f;

        UIFactory.CreateButton(box.transform, "건너뛰기",
            new Vector2(0.52f, 0.04f), new Vector2(0.72f, 0.17f), UIPalette.Subtle, OnSkip, 22);

        UIFactory.CreateButton(box.transform, "다음",
            new Vector2(0.75f, 0.04f), new Vector2(0.95f, 0.17f), UIPalette.Action, OnNext, 24);

        banner = UIFactory.CreateLabel(safe, string.Empty, 30, FontStyle.Bold,
            new Vector2(0.15f, 0.84f), new Vector2(0.85f, 0.94f), TextAnchor.MiddleCenter, UIPalette.TextAccent);
        banner.raycastTarget = false;
        banner.gameObject.SetActive(false);

        panel.SetActive(false);
    }

    private void Next()
    {
        if (queue.Count == 0)
        {
            Close();
            return;
        }

        bool opening = current == null;
        current = queue.Dequeue();
        page = 0;

        if (opening)
        {
            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            ItemActionMenu.Close();
        }

        panel.SetActive(true);
        Render();
    }

    private void Render()
    {
        title.text = current.Title;
        body.text = current.Pages[page];
        counter.text = $"{page + 1} / {current.Pages.Count}";
    }

    private void OnNext()
    {
        if (current == null)
            return;

        page++;

        if (page < current.Pages.Count)
        {
            Render();
            return;
        }

        Finish();
    }

    private void OnSkip()
    {
        if (current != null)
            Finish();
    }

    private void Finish()
    {
        Request done = current;
        Next();
        done.Done?.Invoke();
    }

    private void Close()
    {
        current = null;
        panel.SetActive(false);
        Time.timeScale = previousTimeScale;
    }
}
