using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 칸을 넘는 글자를 「앞부분… 」으로 자른다.
///
/// 【글자를 줄이지 않고 자르는 이유】
/// 전에는 자동 축소(best fit)를 걸었다. 잘리지는 않지만 긴 이름만 글자가
/// 작아져서, 같은 줄의 칸들이 제각각 다른 크기로 보였다. 「면제배갑
/// (중간)」만 유독 작으면 그 칸이 덜 중요해 보인다.
///
/// 이름 전체는 어차피 상세에서 본다. 칸에서는 「무엇인지 구분되는 만큼」이면
/// 충분하므로, 글자 크기를 지키고 뒤를 자른다.
///
/// 【Unity의 Text에는 말줄임이 없다.】 직접 재 보고 한 글자씩 줄인다.
/// 이름이 길어야 스무 자 남짓이라 반복은 짧다.
/// </summary>
[RequireComponent(typeof(Text))]
public class EllipsisLabel : MonoBehaviour
{
    private const string Ellipsis = "…";

    private Text label;
    private string source;

    /// <summary>자르기 전의 원문. 칸이 넓어지면 여기서 다시 복원한다.</summary>
    public void SetText(string value)
    {
        source = value ?? string.Empty;

        Apply();
    }

    private void Awake()
    {
        Cache();
    }

    private void OnEnable()
    {
        Apply();
    }

    // 레이아웃이 확정되는 시점이 Build 직후가 아니라서, 칸 크기가 정해질 때
    // 다시 맞춘다. 화면 비율이 바뀌어도 같은 경로로 다시 맞는다.
    private void OnRectTransformDimensionsChange()
    {
        Apply();
    }

    private void Cache()
    {
        if (label == null)
            label = GetComponent<Text>();

        if (source == null && label != null)
            source = label.text;
    }

    private void Apply()
    {
        Cache();

        if (label == null || string.IsNullOrEmpty(source))
            return;

        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.resizeTextForBestFit = false;

        float max = ((RectTransform)transform).rect.width;

        // 아직 레이아웃이 잡히지 않았다. 잡히면 다시 불린다.
        if (max <= 1f)
            return;

        if (Width(source) <= max)
        {
            label.text = source;
            return;
        }

        for (int length = source.Length - 1; length > 0; length--)
        {
            string candidate = source.Substring(0, length) + Ellipsis;

            if (Width(candidate) > max)
                continue;

            label.text = candidate;
            return;
        }

        label.text = Ellipsis;
    }

    private float Width(string text)
    {
        TextGenerationSettings settings = label.GetGenerationSettings(Vector2.zero);

        settings.horizontalOverflow = HorizontalWrapMode.Overflow;
        settings.verticalOverflow = VerticalWrapMode.Overflow;

        return label.cachedTextGeneratorForLayout.GetPreferredWidth(text, settings)
               / Mathf.Max(0.0001f, label.pixelsPerUnit);
    }
}
