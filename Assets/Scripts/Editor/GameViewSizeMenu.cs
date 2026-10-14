using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 【개발용】 게임 화면 크기를 바꾼다 (결정 2-124 — UI 비율 점검). 16:9 · 20:9(휴대폰) · 4:3(태블릿).
/// Unity가 게임 화면 크기를 바꾸는 공개 API를 주지 않아 반사로 GameViewSizes에 고정 해상도를 넣고 고른다.
/// 원격 작업에서 화면 찍기(PlayCaptureMenu)와 같이 쓴다. 빌드에는 없다(Editor).
/// </summary>
public static class GameViewSizeMenu
{
    private const string TagKey = "Dokkaebi.GameViewSize";

    /// <summary>마지막으로 고른 화면 크기 (찍은 그림 이름에 붙인다). 고른 적이 없으면 "기본".</summary>
    public static string Tag => SessionState.GetString(TagKey, "기본");

    [MenuItem("Dokkaebi/Debug/화면 크기 16대9 (1920×1080)")]
    public static void Wide() => Set(1920, 1080, "Dokkaebi 16:9");

    [MenuItem("Dokkaebi/Debug/화면 크기 20대9 휴대폰 (2400×1080)")]
    public static void Phone() => Set(2400, 1080, "Dokkaebi 20:9");

    [MenuItem("Dokkaebi/Debug/화면 크기 4대3 태블릿 (2048×1536)")]
    public static void Tablet() => Set(2048, 1536, "Dokkaebi 4:3");

    private static void Set(int width, int height, string label)
    {
        SessionState.SetString(TagKey, $"{width}x{height}");
        Assembly editor = typeof(Editor).Assembly;
        Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
        Type sizeType = editor.GetType("UnityEditor.GameViewSize");
        Type sizeKind = editor.GetType("UnityEditor.GameViewSizeType");
        Type gameViewType = editor.GetType("UnityEditor.GameView");

        if (sizesType == null || sizeType == null || sizeKind == null || gameViewType == null)
        {
            Debug.LogWarning("[GameViewSize] 이 Unity에서는 게임 화면 크기를 바꿀 수 없다.");
            return;
        }

        object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance")?.GetValue(null);
        object groupType = sizesType.GetProperty("currentGroupType")?.GetValue(sizes);
        object group = sizesType.GetMethod("GetGroup")?.Invoke(sizes, new[] { groupType });
        if (group == null)
            return;

        Type groupClass = group.GetType();
        int total = (int)groupClass.GetMethod("GetTotalCount").Invoke(group, null);
        int index = -1;

        for (int i = 0; i < total; i++)
        {
            object s = groupClass.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
            if ((string)sizeType.GetProperty("baseText").GetValue(s) == label)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            ConstructorInfo ctor = sizeType.GetConstructor(new[] { sizeKind, typeof(int), typeof(int), typeof(string) });
            object fixedRes = Enum.ToObject(sizeKind, 1);   // FixedResolution
            groupClass.GetMethod("AddCustomSize").Invoke(group, new[] { ctor.Invoke(new[] { fixedRes, width, height, (object)label }) });
            index = (int)groupClass.GetMethod("GetTotalCount").Invoke(group, null) - 1;
        }

        EditorWindow view = EditorWindow.GetWindow(gameViewType);
        PropertyInfo selected = gameViewType.GetProperty("selectedSizeIndex",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (selected != null)
            selected.SetValue(view, index);
        else
            gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.Invoke(view, new object[] { index, null });

        view.Repaint();
        Debug.Log($"[GameViewSize] {label} ({width} × {height})");
    }
}
