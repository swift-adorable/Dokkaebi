using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【기억 연출】 (결정 2-111 · 본문 945 「(기억 연출) 아홉 조각의 장면이 빠르게 겹친다 — …」) — 어두운 화면에
/// 본문이 적은 장면 조각을 하나씩 빠르게 겹쳐 띄운다. 그림은 아트 작업 때 — 지금은 글만. 누르면 건너뛴다.
/// </summary>
public class MemoryMontageUI : MonoBehaviour
{
    private const float FrameSeconds = 1.1f;

    private Text label;
    private Action done;
    private bool skip;

    public static void Play(IList<string> frames, Action onDone)
    {
        if (frames == null || frames.Count == 0)
        {
            onDone?.Invoke();
            return;
        }

        Canvas canvas = UIFactory.CreateCanvas("MemoryMontageCanvas (Runtime)", 1450);
        var ui = canvas.gameObject.AddComponent<MemoryMontageUI>();
        ui.done = onDone;
        ui.Build(canvas);
        ui.StartCoroutine(ui.Run(new List<string>(frames)));
    }

    private void Build(Canvas canvas)
    {
        GamePause.Register(canvas.gameObject);
        Image back = UIFactory.CreatePanel("Back", canvas.transform, new Color(0.02f, 0.02f, 0.03f, 0.96f), Vector2.zero, Vector2.one, 0);
        var button = back.gameObject.AddComponent<Button>();
        button.targetGraphic = back;
        button.onClick.AddListener(() => skip = true);

        label = UIFactory.CreateLabel(back.transform, string.Empty, 34, FontStyle.Normal,
            new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.7f), TextAnchor.MiddleCenter, new Color(0.95f, 0.9f, 0.8f));
        label.raycastTarget = false;
    }

    private IEnumerator Run(List<string> frames)
    {
        foreach (string frame in frames)
        {
            if (skip)
                break;

            label.text = frame;
            float t = 0f;
            while (t < FrameSeconds && !skip)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(Mathf.Min(t / 0.25f, (FrameSeconds - t) / 0.25f));
                label.color = new Color(0.95f, 0.9f, 0.8f, a);
                yield return null;
            }
        }

        Action callback = done;
        Destroy(gameObject);
        callback?.Invoke();
    }
}
