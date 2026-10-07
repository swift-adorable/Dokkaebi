using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 새로 시작할 때 프롤로그 앞에 한 번 뜨는 난이도 고르기 — 보통 · 악몽 · 지옥 (결정 2-69 · 2-77).
/// 【한 번 고르면 바꿀 수 없다】. 고른 즉시 저장한다. 이미 골랐으면 뜨지 않는다.
/// 모양은 임시다 — 화면은 레이어 · 아트 작업 때 다시 만든다.
/// </summary>
public class DifficultyPickerUI : MonoBehaviour
{
    private Action onDone;

    /// <summary>아직 고르지 않았으면 띄우고, 고른 뒤 onDone. 이미 골랐으면 바로 onDone.</summary>
    public static void ShowIfNeeded(Action onDone)
    {
        if (DifficultyManager.IsChosen)
        {
            onDone?.Invoke();
            return;
        }

        // 이야기 장면(1400)보다 위.
        Canvas canvas = UIFactory.CreateCanvas("DifficultyPickerCanvas (Runtime)", 1500);
        var ui = canvas.gameObject.AddComponent<DifficultyPickerUI>();
        ui.onDone = onDone;
        ui.Build(canvas);
    }

    private void Build(Canvas canvas)
    {
        RectTransform safe = UIFactory.CreateSafeArea(canvas);
        Image dim = UIFactory.CreatePanel("Dim", safe, UIPalette.Dim, Vector2.zero, Vector2.one, 0);

        UIFactory.CreateLabel(dim.transform, "난이도를 고른다", 40, FontStyle.Bold,
            new Vector2(0.1f, 0.80f), new Vector2(0.9f, 0.92f), TextAnchor.MiddleCenter);
        UIFactory.CreateLabel(dim.transform, "한 번 고르면 바꿀 수 없다.", 24, FontStyle.Normal,
            new Vector2(0.1f, 0.74f), new Vector2(0.9f, 0.80f), TextAnchor.MiddleCenter, UIPalette.TextAccent);

        DifficultyLevel[] all = DifficultyTable.All;

        for (int i = 0; i < all.Length; i++)
        {
            DifficultyLevel level = all[i];
            float top = 0.70f - i * 0.21f;

            Button button = UIFactory.CreateButton(dim.transform, DifficultyTable.NameOf(level),
                new Vector2(0.2f, top - 0.11f), new Vector2(0.8f, top), UIPalette.Action, () => Pick(level), 32);

            UIFactory.CreateLabel(dim.transform, DifficultyTable.DescriptionOf(level), 22, FontStyle.Normal,
                new Vector2(0.2f, top - 0.18f), new Vector2(0.8f, top - 0.11f), TextAnchor.MiddleCenter, UIPalette.TextDim);
        }
    }

    private void Pick(DifficultyLevel level)
    {
        DifficultyManager.Choose(level);
        SaveManager.Commit("난이도");
        GameLogger.Log($"[Difficulty] {DifficultyTable.NameOf(level)} — 골랐습니다.");

        Action done = onDone;
        Destroy(gameObject);
        done?.Invoke();
    }
}
