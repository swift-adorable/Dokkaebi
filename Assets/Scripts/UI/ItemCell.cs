using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 격자 한 칸의 생김새. 【가방·전리품·창고·상점이 전부 이것을 쓴다.】
///
/// 【왜 따로 빼는가】
/// 원래는 가방 화면 안에만 있었다. 전리품 창이 생기면서 같은 칸을 다시
/// 그리게 됐는데, 두 벌을 두면 한쪽만 고쳐져 「가방에서는 내구도가 보이는데
/// 전리품에서는 안 보인다」 같은 일이 생긴다. 칸의 생김새는 한 곳에서만 정한다.
///
/// 누를 때 무슨 일이 일어나는지는 부르는 쪽이 정한다 —
/// 가방에서는 상세를 열고, 전리품에서는 줍는다.
/// </summary>
public static class ItemCell
{
    /// <summary>
    /// 칸 하나를 그린다. stack이 null이면 빈 칸이 된다.
    /// </summary>
    /// <param name="chosen">지금 고른 칸인지. 파랗게 밝힌다.</param>
    public static Image Draw(
        string name, Transform parent, Vector2 min, Vector2 max,
        ItemStack stack, bool chosen, UnityAction onClick)
    {
        bool empty = stack?.Definition == null;

        Color kind = empty ? UIPalette.Slot : UIPalette.ForItem(stack.Definition.Kind);

        Color body = empty
            ? UIPalette.Inset
            : chosen
                ? UIPalette.SlotSelected
                : UIPalette.Glassify(kind, 0.34f);

        Image cell = UIFactory.CreatePanel(name, parent, body, min, max);

        // 빈 칸에는 광택을 얹지 않는다. 격자 전체가 번들거려 아이템이 묻힌다.
        UIFactory.CreateOutline(cell,
            empty ? UIPalette.EdgeSoft
                  : chosen ? UIPalette.Brighten(UIPalette.SlotSelected, 0.22f)
                           : UIPalette.Glassify(UIPalette.Brighten(kind, 0.34f), 0.70f),
            UIFactory.Radius,
            chosen ? 3 : 2);

        var button = cell.gameObject.AddComponent<Button>();
        button.targetGraphic = cell;
        button.interactable = !empty;

        if (onClick != null)
            button.onClick.AddListener(onClick);

        if (empty)
            return cell;

        // 종류 도형. 가운데에 크게 깔아 배경처럼 쓴다 —
        // 글자를 읽기 전에 「무기인가 재료인가」가 먼저 들어온다.
        // 실제 그림이 생기면 definition.Icon이 이 자리를 대신한다.
        Image glyph = UIFactory.CreatePanel("Glyph", cell.transform, UIPalette.GlyphTint,
            new Vector2(0.20f, 0.16f), new Vector2(0.80f, 0.76f), radius: 0);

        glyph.raycastTarget = false;
        glyph.preserveAspect = true;

        if (stack.Definition.Icon != null)
        {
            glyph.sprite = stack.Definition.Icon;
            glyph.color = Color.white;
        }
        else
        {
            glyph.sprite = UISprites.Of(UISprites.GlyphFor(stack.Definition.Kind));
        }

        // 이름은 좌상단. 반투명 검정 띠를 깔아 어떤 칸 색 위에서도 흰 글자가 읽히게 한다.
        Image strip = UIFactory.CreatePanel("NameStrip", cell.transform, UIPalette.NameStrip,
            new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.96f), radius: 6);

        strip.raycastTarget = false;

        Text nameLabel = UIFactory.CreateLabel(strip.transform, stack.Definition.DisplayName,
            19, FontStyle.Bold, new Vector2(0.06f, 0f), new Vector2(0.94f, 1f),
            TextAnchor.MiddleLeft, Color.white);

        WrapName(nameLabel, 18);

        // 개수는 우하단. 겹칠 수 있는 물건에만 뜬다 —
        // 1개짜리에 「1」을 붙이면 잡음이다.
        if (stack.Count > 1)
        {
            UIFactory.CreateBadge(cell.transform, stack.Count.ToString(),
                new Vector2(0.56f, 0.06f), new Vector2(0.96f, 0.30f), 22,
                Color.white);
        }

        DrawDurabilityBar(cell.transform, stack);

        return cell;
    }

    /// <summary>
    /// 이름이 칸을 넘으면 두 줄까지 접고, 그래도 넘치면 글자를 줄인다.
    /// 자르지 않는 이유 — Unity Text는 한 줄이 칸보다 크면 그 줄을 아예 그리지 않는다.
    /// </summary>
    public static void WrapName(Text label, int size)
    {
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = Mathf.Max(11, Mathf.RoundToInt(size * 0.7f));
        label.resizeTextMaxSize = size;
    }

    /// <summary>내구도 막대. 내구도가 없는 물건에는 그리지 않는다.</summary>
    public static void DrawDurabilityBar(Transform cell, ItemStack stack)
    {
        if (stack.Definition == null || !stack.Definition.HasDurability)
            return;

        if (stack.MaxDurability <= 0)
            return;

        float ratio = Mathf.Clamp01(stack.Durability / (float)stack.MaxDurability);

        UIFactory.CreatePanel("DurTrack", cell, UIPalette.NameStrip,
            new Vector2(0.06f, 0.035f), new Vector2(0.50f, 0.085f), radius: 3);

        Color color = stack.IsBroken
            ? UIPalette.Warning
            : stack.IsWorn
                ? UIPalette.Cost
                : UIPalette.Gain;

        UIFactory.CreatePanel("DurFill", cell, color,
            new Vector2(0.06f, 0.035f),
            new Vector2(0.06f + (0.50f - 0.06f) * ratio, 0.085f), radius: 3);
    }
}
