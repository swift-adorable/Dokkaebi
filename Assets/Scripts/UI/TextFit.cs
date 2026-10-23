using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【글자 맞추기】 (결정 2-135) — 한 줄 글자가 칸에 들어가는 가장 큰 크기를 잰다.
/// Unity의 Best Fit은 줄바꿈을 허용하면 두 줄로 접어 버리고(「9.6 / 30.0\nkg」 · 「패시\n브」),
/// 막으면 넘친다. 여기서는 접지 않고 폭에 맞춰 글자 크기만 줄인다.
/// </summary>
public static class TextFit
{
    private static readonly TextGenerator Generator = new();

    /// <summary>text를 한 줄로 width × height 안에 넣는 가장 큰 크기 (min ~ max).</summary>
    public static int OneLine(Text label, string text, float width, float height, int max, int min)
    {
        if (string.IsNullOrEmpty(text) || width <= 1f)
            return max;

        TextGenerationSettings settings = label.GetGenerationSettings(new Vector2(width, height));
        settings.horizontalOverflow = HorizontalWrapMode.Overflow;
        settings.verticalOverflow = VerticalWrapMode.Overflow;
        settings.resizeTextForBestFit = false;
        settings.scaleFactor = 1f;

        for (int size = max; size > min; size--)
        {
            settings.fontSize = size;
            float w = Generator.GetPreferredWidth(text, settings);
            float h = Generator.GetPreferredHeight(text, settings);
            if (w <= width && h <= height * 1.05f)
                return size;
        }
        return min;
    }

    /// <summary>줄바꿈을 허용하고 width × height 안에 들어가는 가장 큰 크기 (min ~ max).</summary>
    public static int Block(Text label, string text, float width, float height, int max, int min)
    {
        if (string.IsNullOrEmpty(text) || width <= 1f || height <= 1f)
            return max;

        TextGenerationSettings settings = label.GetGenerationSettings(new Vector2(width, height));
        settings.horizontalOverflow = HorizontalWrapMode.Wrap;
        settings.verticalOverflow = VerticalWrapMode.Overflow;
        settings.resizeTextForBestFit = false;
        settings.scaleFactor = 1f;

        for (int size = max; size > min; size--)
        {
            settings.fontSize = size;
            if (Generator.GetPreferredHeight(text, settings) <= height * 1.02f)
                return size;
        }
        return min;
    }
}

/// <summary>
/// 여러 줄 글자 — 줄은 접되, 칸 높이를 넘으면 크기를 줄인다 (결정 2-135).
/// Unity Best Fit은 세로 넘침이 Overflow면 줄이지 않고, Truncate면 넘친 줄을 통째로 지운다.
/// </summary>
public class FitTextBlock : MonoBehaviour
{
    public int Max;
    public int Min = 12;

    private Text label;
    private string lastText;
    private Vector2 lastSize;

    public static Text Apply(Text label, int min = 12)
    {
        // ??는 Unity의 가짜 null(에디터의 GetComponent)을 못 거른다 — TryGetComponent로 묻는다.
        if (!label.TryGetComponent(out FitTextBlock fit))
            fit = label.gameObject.AddComponent<FitTextBlock>();
        fit.Max = label.fontSize;
        fit.Min = Mathf.Min(min, label.fontSize);
        label.resizeTextForBestFit = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        return label;
    }

    private void LateUpdate()
    {
        if (label == null)
            label = GetComponent<Text>();
        Vector2 size = label.rectTransform.rect.size;
        if (label.text == lastText && size == lastSize)
            return;
        lastText = label.text;
        lastSize = size;
        label.fontSize = TextFit.Block(label, label.text, size.x, size.y, Max, Min);
    }
}

/// <summary>한 줄 글자 — 칸 폭에 맞춰 크기를 줄인다(접지 않는다). 글이나 칸이 바뀔 때만 다시 잰다.</summary>
public class OneLineText : MonoBehaviour
{
    public int Max;
    public int Min = 12;

    private Text label;
    private string lastText;
    private Vector2 lastSize;

    public static Text Apply(Text label, int min = 12)
    {
        // ??는 Unity의 가짜 null(에디터의 GetComponent)을 못 거른다 — TryGetComponent로 묻는다.
        if (!label.TryGetComponent(out OneLineText fit))
            fit = label.gameObject.AddComponent<OneLineText>();
        fit.Max = label.fontSize;
        fit.Min = Mathf.Min(min, label.fontSize);
        label.resizeTextForBestFit = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        return label;
    }

    private void LateUpdate()
    {
        if (label == null)
            label = GetComponent<Text>();
        Vector2 size = label.rectTransform.rect.size;
        if (label.text == lastText && size == lastSize)
            return;
        lastText = label.text;
        lastSize = size;
        label.fontSize = TextFit.OneLine(label, label.text, size.x, size.y, Max, Min);
    }
}

/// <summary>
/// 【나란한 버튼은 글자 크기를 맞춘다】 (결정 2-135) — 같은 부모 아래 버튼(탭 줄 · 아래 버튼 줄)의 글자를
/// 가장 빠듯한 버튼이 들어가는 크기 하나로 맞춘다. 「장비 · 스킬 · 패시브 · 도감」에서 「패시브」만 작아지던 것.
/// UIFactory.CreateButton이 부모에 저절로 붙인다.
/// </summary>
public class UniformButtonText : MonoBehaviour
{
    /// <summary>버튼 글자의 원래 크기(글자 배율 적용) — 줄였다가도 다시 키울 수 있게 따로 둔다.</summary>
    public sealed class Base : MonoBehaviour
    {
        public int Max;
    }

    private string lastKey;
    private readonly List<(Text label, int max)> labels = new();

    private void LateUpdate()
    {
        labels.Clear();
        var key = new System.Text.StringBuilder();
        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeSelf || !child.TryGetComponent(out Button _))
                continue;
            Text label = child.GetComponentInChildren<Text>();
            if (label == null || !label.TryGetComponent(out Base b))
                continue;
            labels.Add((label, b.Max));
            Vector2 s = label.rectTransform.rect.size;
            // 탭 줄은 누를 때마다 새로 그린다 — 글이 같아도 글자(Text)가 새것이면 다시 잰다.
            key.Append(label.GetInstanceID()).Append(':').Append(label.text).Append('|').Append(Mathf.RoundToInt(s.x)).Append('x').Append(Mathf.RoundToInt(s.y)).Append(';');
        }

        string k = key.ToString();
        if (k == lastKey || labels.Count == 0)
            return;
        lastKey = k;

        int size = int.MaxValue;
        foreach ((Text label, int max) in labels)
        {
            Vector2 s = label.rectTransform.rect.size;
            size = Mathf.Min(size, TextFit.OneLine(label, label.text, s.x - 8f, s.y, max, Mathf.Max(12, max / 2)));
        }

        foreach ((Text label, int max) in labels)
            label.fontSize = Mathf.Min(max, size);
    }
}
