using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【임시】 장부방 화면 (결정 2-97 · 2-98) — 장 지도 · 처치 기록 · 가게 재고 · 모은 글(조각 · 방). 장부방 앞에서 누르면 연다.
/// 떠 있는 동안 게임이 멈춘다(결정 2-92). 모양은 레이어 · 아트 작업 때 다시 만든다.
/// </summary>
public class LedgerUI : MonoBehaviour
{
    private static LedgerUI instance;

    private const float RowGap = 8f;

    private const int MapsTab = 0;
    private const int KillsTab = 1;
    private const int ShopsTab = 2;
    private const int NoticesTab = 3;

    private Text title;
    private Text status;
    private UIFactory.ScrollList list;
    private readonly List<Image> tabs = new();
    private int shown = MapsTab;

    public static void Open(int tab = MapsTab)
    {
        if (instance != null)
        {
            instance.Show(tab);
            return;
        }

        Canvas canvas = UIFactory.CreateCanvas("LedgerCanvas (Runtime)", 1200);
        instance = canvas.gameObject.AddComponent<LedgerUI>();
        instance.Build(canvas);
        instance.Show(tab);
    }

    public static void Close()
    {
        if (instance != null)
            Destroy(instance.gameObject);
    }

    /// <summary>처치 기록 탭으로 연다 (검증 도구).</summary>
    public static void OpenKills() => Open(KillsTab);

    /// <summary>가게 재고 탭으로 연다 (검증 도구).</summary>
    public static void OpenShops() => Open(ShopsTab);

    /// <summary>모은 글 탭으로 연다 (검증 도구).</summary>
    public static void OpenWritings() => Open(NoticesTab);

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build(Canvas canvas)
    {
        RectTransform safe = UIFactory.CreateSafeArea(canvas);
        GamePause.Register(canvas.gameObject);   // 떠 있는 동안 게임이 멈춘다 (결정 2-92)
        Image dim = UIFactory.CreatePanel("Dim", safe, UIPalette.Dim, Vector2.zero, Vector2.one, 0);
        Image box = UIFactory.CreatePanel("Box", dim.transform, UIPalette.Panel,
            new Vector2(0.1f, 0.04f), new Vector2(0.9f, 0.96f));

        title = UIFactory.CreateLabel(box.transform, string.Empty, 30, FontStyle.Bold,
            new Vector2(0.04f, 0.89f), new Vector2(0.78f, 0.98f), TextAnchor.MiddleLeft);
        UIFactory.CreateButton(box.transform, "닫기", new Vector2(0.8f, 0.9f), new Vector2(0.96f, 0.97f),
            UIPalette.Header, () => Destroy(gameObject), 22);

        string[] names = { LedgerTable.MapsTab, LedgerTable.KillsTab, LedgerTable.ShopsTab, LedgerTable.NoticesTab };
        for (int i = 0; i < names.Length; i++)
        {
            int captured = i;
            float x = 0.04f + i * 0.17f;
            Button tab = UIFactory.CreateButton(box.transform, names[i],
                new Vector2(x, 0.80f), new Vector2(x + 0.16f, 0.88f), UIPalette.Header, () => Show(captured), 22);
            tabs.Add(tab.GetComponent<Image>());
        }

        list = UIFactory.CreateScrollList("LedgerList", box.transform,
            new Vector2(0.04f, 0.09f), new Vector2(0.96f, 0.785f));

        status = UIFactory.CreateLabel(box.transform, string.Empty, 20, FontStyle.Normal,
            new Vector2(0.04f, 0.01f), new Vector2(0.96f, 0.08f), TextAnchor.MiddleCenter, UIPalette.TextAccent);
    }

    private void Show(int tab)
    {
        shown = tab;
        Refresh();
    }

    private void Refresh()
    {
        title.text = $"{ShopTable.LedgerRoomName}  ·  {LedgerManager.Gold:N0}엽전"
            + (LedgerManager.Built ? string.Empty : "  (장부방을 지어야 한다)");

        for (int i = 0; i < tabs.Count; i++)
            tabs[i].color = i == shown ? UIPalette.SlotSelected : UIPalette.Header;

        UIFactory.ClearChildren(list.Content);
        float y = 0f;

        switch (shown)
        {
            case KillsTab:   FillKills(ref y); break;
            case ShopsTab:   FillShops(ref y); break;
            case NoticesTab: FillNotices(ref y); break;
            default:         FillMaps(ref y); break;
        }

        list.Content.sizeDelta = new Vector2(0f, y);
    }

    // ── 장 지도 ──────────────────────────────────────────────────────

    private void FillMaps(ref float y)
    {
        AddTheme("엽전으로 사면 그 장의 안개가 다 걷힌다 — 철수 지점은 여전히 찾아야 한다", ref y);

        StoryProgress progress = StoryManager.Progress;

        foreach (ChapterDefinition chapter in StoryTable.Chapters)
        {
            int number = chapter.Number;
            FogGrid fog = MapMemory.Fog(number);
            if (fog == null)
                continue;

            bool open = progress != null && progress.IsChapterOpen(number);
            int total = fog.Width * fog.Height;
            int percent = total > 0 ? Mathf.FloorToInt(100f * fog.RevealedCount / total) : 0;

            string state = !open ? "아직 열리지 않았다"
                : LedgerTable.IsFullyRevealed(fog) ? "다 밝혔다"
                : $"가 본 땅 {percent}% · {Colored($"{LedgerTable.MapPrice(number):N0}엽전", LedgerManager.Gold >= LedgerTable.MapPrice(number))}";

            Image row = UIFactory.CreatePanel("Row", list.Content, UIPalette.Row, Vector2.zero, Vector2.zero);
            Place(row.rectTransform, ref y, 90f);

            Text label = UIFactory.CreateLabel(row.transform,
                $"<b>{number}장 · {chapter.Place}</b>\n<size=18>{state}</size>", 22, FontStyle.Normal,
                new Vector2(0.02f, 0f), new Vector2(0.78f, 1f), TextAnchor.MiddleLeft, open ? UIPalette.Text : UIPalette.TextDim);
            label.supportRichText = true;

            Button button = UIFactory.CreateButton(row.transform, "사기", new Vector2(0.80f, 0.15f), new Vector2(0.98f, 0.85f),
                UIPalette.Action, () => OnBuy(number), 22);
            button.interactable = LedgerManager.CanBuyMap(number) == LedgerError.None;
        }
    }

    private void OnBuy(int chapter)
    {
        LedgerError error = LedgerManager.BuyMap(chapter);
        status.text = error == LedgerError.None ? $"{chapter}장 지도를 샀다 — 안개가 걷혔다." : LedgerTable.Explain(error);
        Refresh();
    }

    // ── 처치 기록 ────────────────────────────────────────────────────

    private void FillKills(ref float y)
    {
        KillRecord kills = LedgerManager.Kills;
        AddTheme($"내가 쓰러뜨린 적 {kills.Total:N0} — 많이 잡을수록 더 안다 (1 · 5 · 15 · 30)", ref y);

        for (int a = 0; a <= (int)EnemyArchetype.Wraith; a++)
        {
            var archetype = (EnemyArchetype)a;
            int count = kills.Count(archetype);
            int level = LedgerTable.InfoLevel(count);
            var lines = new List<string>();

            if (level == 0)
            {
                lines.Add("<b>???</b>");
                lines.Add("<size=18>아직 쓰러뜨린 적이 없다.</size>");
            }
            else
            {
                EnemyArchetypeStats stats = EnemyArchetypeTable.Of(archetype);
                var byRarity = new List<string>();
                for (int r = 0; r < EnemyRarityTable.Count; r++)
                {
                    int n = kills.Count(archetype, (EnemyRarity)r);
                    if (n > 0) byRarity.Add($"{LedgerTable.RarityName((EnemyRarity)r)} {n}");
                }

                lines.Add($"<b>{EnemyArchetypeTable.Name(archetype)}</b>  <size=18>쓰러뜨림 {count} ({string.Join(" · ", byRarity)})</size>");

                if (level >= 2)
                    lines.Add($"<size=18>요구하는 답 — {LedgerTable.AnswerName(stats.answer)}</size>");

                if (level >= 3)
                    lines.Add($"<size=18>받는 피해 — {ResistLine(stats.resistances)}</size>");

                if (level >= 4)
                    lines.Add($"<size=18>체력 {stats.health} · 피해 {stats.damage} · 방어 {stats.armour:0.#} · 관통 {stats.armourPenetration} (일반 등급)</size>");
            }

            int next = LedgerTable.ToNextLevel(count);
            if (next > 0 && level > 0)
                lines.Add($"<size=16><color=#A8A08C>{next}마리 더 잡으면 더 안다</color></size>");

            float height = 34f + lines.Count * 26f;
            Image row = UIFactory.CreatePanel("Row", list.Content, UIPalette.Row, Vector2.zero, Vector2.zero);
            Place(row.rectTransform, ref y, height);

            Text label = UIFactory.CreateLabel(row.transform, string.Join("\n", lines), 22, FontStyle.Normal,
                new Vector2(0.02f, 0f), new Vector2(0.98f, 1f), TextAnchor.MiddleLeft, level > 0 ? UIPalette.Text : UIPalette.TextDim);
            label.supportRichText = true;
        }
    }

    private static string ResistLine(ElementalResistances r)
        => $"물리 ×{r.physical:0.##} · 화염 ×{r.fire:0.##} · 냉기 ×{r.cold:0.##} · 번개 ×{r.lightning:0.##} · 카오스 ×{r.chaos:0.##}";

    // ── 가게 재고 (결정 2-98) ────────────────────────────────────────

    private void FillShops(ref float y)
    {
        AddTheme($"판이 끝날 때마다 가게마다 「오늘의 물건」 {ShopDailyTable.Slots}칸이 바뀐다 — 여기서 엽전으로 지금 채우거나 다시 뽑는다", ref y);

        ItemCatalog catalog = ItemCatalog.Load();

        foreach (ShopKind kind in ShopTable.All)
        {
            bool open = BuildingManager.IsOpen(LedgerTable.BuildingOf(kind));
            ShopState state = ShopManager.Of(kind);
            var parts = new List<string>();

            foreach (string id in ShopManager.DailyOf(kind))
            {
                string name = catalog?.Find(id)?.DisplayName ?? id;
                int left = state.Remaining(id);
                parts.Add(left > 0 ? $"{name} ×{left}" : $"{name} 품절");
            }

            string daily = parts.Count > 0 ? string.Join(" · ", parts) : "들일 물건이 아직 없다";
            string head = open ? ShopTable.NameOf(kind) : $"{ShopTable.NameOf(kind)} (아직 짓지 않았다)";

            Image row = UIFactory.CreatePanel("Row", list.Content, UIPalette.Row, Vector2.zero, Vector2.zero);
            Place(row.rectTransform, ref y, 120f);

            Text label = UIFactory.CreateLabel(row.transform,
                $"<b>{head}</b>\n<size=18>오늘의 물건 — {daily}</size>", 22, FontStyle.Normal,
                new Vector2(0.02f, 0f), new Vector2(0.62f, 1f), TextAnchor.MiddleLeft, open ? UIPalette.Text : UIPalette.TextDim);
            label.supportRichText = true;

            int refill = ShopDailyTable.RefillPrice(kind);
            Button fill = UIFactory.CreateButton(row.transform, $"지금 채우기 · {refill}엽전",
                new Vector2(0.64f, 0.53f), new Vector2(0.98f, 0.93f), UIPalette.Action,
                () => OnShop(LedgerManager.Refill(kind), $"{ShopTable.NameOf(kind)} 재고를 가득 채웠다."), 18);
            fill.interactable = LedgerManager.CanRefill(kind) == LedgerError.None;

            Button reroll = UIFactory.CreateButton(row.transform, $"오늘의 물건 다시 뽑기 · {ShopDailyTable.RerollPrice}엽전",
                new Vector2(0.64f, 0.07f), new Vector2(0.98f, 0.47f), UIPalette.Subtle,
                () => OnShop(LedgerManager.Reroll(kind), $"{ShopTable.NameOf(kind)}의 오늘의 물건을 다시 들였다."), 18);
            reroll.interactable = LedgerManager.CanReroll(kind) == LedgerError.None;
        }
    }

    private void OnShop(LedgerError error, string done)
    {
        status.text = error == LedgerError.None ? done : LedgerTable.Explain(error);
        Refresh();
    }

    // ── 모은 글 — 기억의 조각 · 방 ───────────────────────────────────

    private void FillNotices(ref float y)
    {
        StoryProgress progress = StoryManager.Progress;

        // 기억의 조각 (결정 2-98) — 본문 글을 그대로 보여 준다
        var pieces = new List<PieceDefinition>();
        foreach (PieceDefinition piece in StoryTable.Pieces)
            if (progress != null && progress.HasPiece(piece.Id))
                pieces.Add(piece);

        AddTheme($"기억의 조각 {pieces.Count} / {StoryTable.Pieces.Count}", ref y);

        if (pieces.Count == 0)
            AddLine("아직 주운 조각이 없다.", ref y);

        foreach (PieceDefinition piece in pieces)
        {
            StoryPassage passage = StoryManager.Passage(StoryTable.PieceEvent(piece.Id));
            string title = passage != null && !string.IsNullOrEmpty(passage.Title) ? passage.Title : $"기억의 조각 {piece.Number}";
            string body = passage != null ? string.Join("\n\n", passage.Pages) : string.Empty;

            int lines = 1;
            foreach (string para in body.Split('\n'))
                lines += 1 + para.Length / 38;

            Image row = UIFactory.CreatePanel("Row", list.Content, UIPalette.Row, Vector2.zero, Vector2.zero);
            Place(row.rectTransform, ref y, 40f + lines * 25f);

            Text label = UIFactory.CreateLabel(row.transform,
                $"<b>{title}</b>  <size=16><color=#A8A08C>{piece.ZoneId}</color></size>\n<size=19>{body}</size>", 21, FontStyle.Normal,
                new Vector2(0.02f, 0f), new Vector2(0.98f, 1f), TextAnchor.UpperLeft);
            label.supportRichText = true;
            label.rectTransform.offsetMin = new Vector2(0f, 8f);
            label.rectTransform.offsetMax = new Vector2(0f, -8f);
        }

        var collected = new List<NoticeDefinition>();

        foreach (NoticeDefinition notice in StoryTable.Notices)
            if (progress != null && progress.HasNotice(notice.Id))
                collected.Add(notice);

        AddTheme($"길에서 모은 방 {collected.Count} / {StoryTable.Notices.Count}", ref y);

        if (collected.Count == 0)
        {
            AddLine("아직 모은 방이 없다.", ref y);
            return;
        }

        foreach (NoticeDefinition notice in collected)
        {
            ZoneDefinition zone = StoryTable.Zone(notice.ZoneId);
            string where = zone != null ? $"{notice.ZoneId} {zone.Name}" : notice.ZoneId;

            Image row = UIFactory.CreatePanel("Row", list.Content, UIPalette.Row, Vector2.zero, Vector2.zero);
            Place(row.rectTransform, ref y, 96f);

            Text label = UIFactory.CreateLabel(row.transform,
                $"<size=16><color=#A8A08C>{where}</color></size>\n「{notice.Text}」", 21, FontStyle.Normal,
                new Vector2(0.02f, 0f), new Vector2(0.98f, 1f), TextAnchor.MiddleLeft);
            label.supportRichText = true;
        }
    }

    // ── 공통 ─────────────────────────────────────────────────────────

    private void AddTheme(string text, ref float y)
    {
        Text theme = UIFactory.CreateLabel(list.Content, text, 20, FontStyle.Bold, Vector2.zero, Vector2.zero,
            TextAnchor.MiddleLeft, UIPalette.TextAccent);
        Place(theme.rectTransform, ref y, 44f);
    }

    private void AddLine(string text, ref float y)
    {
        Text line = UIFactory.CreateLabel(list.Content, text, 22, FontStyle.Normal, Vector2.zero, Vector2.zero,
            TextAnchor.MiddleLeft, UIPalette.TextDim);
        Place(line.rectTransform, ref y, 44f);
    }

    private static string Colored(string text, bool ok) => ok ? text : $"<color=#EB7361>{text}</color>";

    private static void Place(RectTransform rect, ref float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(6f, -(y + height));
        rect.offsetMax = new Vector2(-6f, -y);
        y += height + RowGap;
    }
}
