using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【임시】 구슬 고르기 화면 (결정 2-75). 가방의 고르지 않은 구슬(핵심 · 보조 · 정신력)을 원하는 구슬로 바꾼다.
/// 한 번 고르면 되돌릴 수 없다. 모양은 레이어 · 아트 작업 때 다시 만든다.
/// </summary>
public class GemPickerUI : MonoBehaviour
{
    private const int PerPage = 7;

    private static GemPickerUI instance;

    private ItemDefinition blank;
    private Action onDone;
    private List<SkillDefinition> candidates = new();
    private int page;

    private RectTransform box;
    private readonly List<GameObject> rows = new();
    private Text pageLabel;
    private Text status;

    public static void Open(ItemDefinition blankGem, Action done)
    {
        if (blankGem == null || !blankGem.IsBlankGem || !SkillManager.HasInstance)
            return;

        if (instance != null)
            Destroy(instance.gameObject);

        // 가방 화면(1000)보다 위.
        Canvas canvas = UIFactory.CreateCanvas("GemPickerCanvas (Runtime)", 1250);
        instance = canvas.gameObject.AddComponent<GemPickerUI>();
        instance.blank = blankGem;
        instance.onDone = done;
        instance.candidates = SkillManager.Instance.CutCandidates(blankGem);
        instance.Build(canvas);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build(Canvas canvas)
    {
        RectTransform safe = UIFactory.CreateSafeArea(canvas);
        Image dim = UIFactory.CreatePanel("Dim", safe, UIPalette.Dim, Vector2.zero, Vector2.one, 0);
        box = UIFactory.CreatePanel("Box", dim.transform, UIPalette.Panel,
            new Vector2(0.12f, 0.06f), new Vector2(0.88f, 0.94f)).rectTransform;

        UIFactory.CreateLabel(box, $"{blank.DisplayName} — 무엇으로 고를까", 28, FontStyle.Bold,
            new Vector2(0.04f, 0.89f), new Vector2(0.78f, 0.98f), TextAnchor.MiddleLeft);
        UIFactory.CreateButton(box, "닫기", new Vector2(0.80f, 0.90f), new Vector2(0.96f, 0.97f),
            UIPalette.Header, () => Destroy(gameObject), 22);

        UIFactory.CreateLabel(box, "한 번 고르면 바꿀 수 없다.", 18, FontStyle.Normal,
            new Vector2(0.04f, 0.84f), new Vector2(0.96f, 0.89f), TextAnchor.MiddleLeft, UIPalette.TextAccent);

        UIFactory.CreateButton(box, "◀", new Vector2(0.04f, 0.08f), new Vector2(0.16f, 0.15f),
            UIPalette.Header, () => Turn(-1), 22);
        UIFactory.CreateButton(box, "▶", new Vector2(0.84f, 0.08f), new Vector2(0.96f, 0.15f),
            UIPalette.Header, () => Turn(1), 22);
        pageLabel = UIFactory.CreateLabel(box, string.Empty, 20, FontStyle.Normal,
            new Vector2(0.18f, 0.08f), new Vector2(0.82f, 0.15f), TextAnchor.MiddleCenter);

        status = UIFactory.CreateLabel(box, string.Empty, 20, FontStyle.Normal,
            new Vector2(0.04f, 0.0f), new Vector2(0.96f, 0.07f), TextAnchor.MiddleCenter, UIPalette.TextAccent);

        Draw();
    }

    private int Pages => Mathf.Max(1, Mathf.CeilToInt(candidates.Count / (float)PerPage));

    private void Turn(int delta)
    {
        page = Mathf.Clamp(page + delta, 0, Pages - 1);
        Draw();
    }

    private void Draw()
    {
        foreach (GameObject go in rows)
            if (go != null) Destroy(go);
        rows.Clear();

        int level = PlayerStats.HasInstance ? PlayerStats.Instance.Level : 1;

        for (int i = 0; i < PerPage; i++)
        {
            int index = page * PerPage + i;
            if (index >= candidates.Count)
                break;

            SkillDefinition skill = candidates[index];
            float top = 0.82f - i * 0.095f;

            string lv = skill.RequiredLevel > level ? $"  (Lv{skill.RequiredLevel} — 지금은 끼울 수 없다)" : $"  (Lv{skill.RequiredLevel})";
            Text label = UIFactory.CreateLabel(box, $"{skill.DisplayName}{lv}\n{skill.Description}", 17, FontStyle.Normal,
                new Vector2(0.04f, top - 0.09f), new Vector2(0.76f, top), TextAnchor.MiddleLeft);
            Button button = UIFactory.CreateButton(box, "고르기",
                new Vector2(0.78f, top - 0.08f), new Vector2(0.96f, top - 0.01f), UIPalette.Action, () => Pick(skill), 20);

            rows.Add(label.gameObject);
            rows.Add(button.gameObject);
        }

        pageLabel.text = candidates.Count == 0 ? "고를 수 있는 구슬이 없다." : $"{page + 1} / {Pages}";
    }

    private void Pick(SkillDefinition skill)
    {
        GemCutError error = SkillManager.Instance.CutGem(blank, skill);

        if (error != GemCutError.None)
        {
            status.text = GemCutting.Explain(error);
            return;
        }

        SaveManager.Commit("구슬 고르기");
        onDone?.Invoke();
        Destroy(gameObject);
    }
}
