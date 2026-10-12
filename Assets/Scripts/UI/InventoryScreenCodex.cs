using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 가방 화면의 「도감」 탭 (결정 2-106) — 본문의 「(세계관 도감 / …)」 일곱 장.
/// 그 이야기 토막을 본 뒤에 열리고, 글은 본문 그대로다(LoreTable). 열리지 않은 장은 이름 없이 「아직 모른다」.
/// 패시브 탭처럼 화면을 통째로 쓰고, 나가는 길은 오른쪽 위 「닫기」 하나다.
/// </summary>
public partial class InventoryScreenUI
{
    private void DrawCodexPanel()
    {
        StoryProgress progress = StoryManager.Progress;

        int total = 0, open = 0;
        foreach (LoreEntry e in StoryManager.Lore)
        {
            if (e.Kind != LoreKind.Codex)
                continue;
            total++;
            if (LoreTable.IsUnlocked(e, progress))
                open++;
        }

        RectTransform tabs = UIFactory.CreateRegion("ScreenTabs", rightContent,
            new Vector2(0f, HeaderLine + 0.01f), new Vector2(0.48f, 1f));
        DrawScreenTabs(tabs);

        UIFactory.CreateLabel(rightContent, $"도감  {open} / {total}", 30, FontStyle.Bold,
            new Vector2(0.50f, HeaderLine), new Vector2(0.86f, 1f), TextAnchor.MiddleLeft, UIPalette.TextOnGlass);

        UIFactory.CreateButton(rightContent, "닫기", new Vector2(0.89f, HeaderLine), Vector2.one,
            UIPalette.Subtle, Close, 26);

        UIFactory.ScrollList list = UIFactory.CreateScrollList("CodexList", rightContent,
            Vector2.zero, new Vector2(1f, HeaderLine - 0.02f));

        float y = 0f;

        foreach (LoreEntry e in StoryManager.Lore)
        {
            if (e.Kind != LoreKind.Codex)
                continue;

            bool unlocked = LoreTable.IsUnlocked(e, progress);
            string text = unlocked
                ? $"<b>{e.Title}</b>\n<size=21>{e.Body.Replace(" · ", "\n· ")}</size>"
                : "<b>???</b>\n<size=21><color=#8C8676>아직 모른다.</color></size>";

            int lines = 2;
            if (unlocked)
                foreach (string part in e.Body.Split('·'))
                    lines += 1 + part.Length / 44;

            Image row = UIFactory.CreatePanel("Row", list.Content, UIPalette.Row, Vector2.zero, Vector2.zero);
            float height = 24f + lines * 27f;

            RectTransform rect = row.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(6f, -(y + height));
            rect.offsetMax = new Vector2(-6f, -y);
            y += height + 8f;

            Text label = UIFactory.CreateLabel(row.transform, text, 24, FontStyle.Normal,
                new Vector2(0.02f, 0f), new Vector2(0.98f, 1f), TextAnchor.UpperLeft);
            label.supportRichText = true;
            label.rectTransform.offsetMin = new Vector2(0f, 8f);
            label.rectTransform.offsetMax = new Vector2(0f, -8f);
        }

        list.Content.sizeDelta = new Vector2(0f, y);
    }
}
