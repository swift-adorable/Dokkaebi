using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 요리 화면 (결정 2-64 · 2-71 · 2-123) — 잡화 가게 옆 부뚜막에서 재료로 음식을 만든다 (CookingTable).
/// 틀은 가게와 같다(StationFrame) — 왼쪽 탭(작음 · 보통 · 큼) · 줄마다 만들 것 · 재료(가진 수) · 「만들기」,
/// 가운데 부뚜막(주인 없음 — 이름표만) · 앞판에 지금 사정, 오른쪽 내 가방.
/// </summary>
public class CookingUI : MonoBehaviour
{
    private static CookingUI instance;

    private const float RowHeight = 96f;
    private const float RowGap = 8f;

    private static readonly string[] TabNames = { "작음", "보통", "큼" };

    private ItemCatalog catalog;
    private StationFrame frame;
    private CookTier tier = CookTier.Small;

    public static void Open()
    {
        StoryManager.ShowSystemNoteOnce(StoryManager.NoteCooking);   // 결정 2-112

        if (instance != null)
        {
            instance.Refresh();
            return;
        }

        // 가방 화면(1000)보다 위.
        Canvas canvas = UIFactory.CreateCanvas("CookingCanvas (Runtime)", 1200);
        instance = canvas.gameObject.AddComponent<CookingUI>();
        instance.Build(canvas);
        instance.StartCoroutine(instance.frame.Show(instance.Refresh));
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (frame != null && frame.IsOpen && Input.GetKeyDown(KeyCode.Escape))
            Destroy(gameObject);
    }

    private void Build(Canvas canvas)
    {
        catalog = ItemCatalog.Load();

        frame = StationFrame.Build(canvas, () => Destroy(gameObject));
        frame.UseCounterAsNote(true);
        frame.SetKeeper(KeeperTable.Of(Station.Hearth));
        frame.Title.text = $"요리 — {CookingTable.HearthName}";
    }

    private static bool Unlocked => CookingTable.IsUnlocked(BuildingManager.IsOpen);

    private void Refresh()
    {
        if (!frame.IsOpen)
            return;

        ItemActionMenu.Close();
        frame.SetGold(PassiveManager.EnsureInstance().Gold);
        frame.DrawTabs(TabNames, (int)tier, i =>
        {
            tier = (CookTier)i;
            Refresh();
        });
        frame.DrawBag(null, StationFrame.DetailOnly);
        frame.CounterNote.text = Unlocked ? "재료는 창고와 가방에서 함께 센다" : CookingTable.Explain(CookError.Locked);

        PlayerInventory inv = PlayerInventory.EnsureInstance();
        UIFactory.ClearChildren(frame.List.Content);
        float y = 0f;

        foreach (Recipe recipe in CookingTable.All)
        {
            if (recipe.Tier != tier)
                continue;

            var parts = new List<string>();
            foreach (MaterialCost c in recipe.Inputs)
            {
                int have = CookingTable.CountOf(c.ItemId, id => BuildingState.CountIn(inv.Stash, inv.Bag, id));
                string text = $"{NameOf(c.ItemId)} {have}/{c.Count}";
                parts.Add(have >= c.Count ? text : $"<color=#EB7361>{text}</color>");
            }

            Image row = UIFactory.CreatePanel("Row", frame.List.Content, StationFrame.RowColor, Vector2.zero, Vector2.zero);
            Place(row.rectTransform, ref y, RowHeight);

            Text label = UIFactory.CreateLabel(row.transform,
                $"<b>{NameOf(recipe.OutputId)}</b>\n<size=18>재료: {string.Join(" · ", parts)}</size>",
                22, FontStyle.Normal, new Vector2(0.03f, 0f), new Vector2(0.76f, 1f), TextAnchor.MiddleLeft);
            label.supportRichText = true;

            Recipe captured = recipe;
            Button button = StationFrame.Pill(row.transform, "만들기", new Vector2(0.78f, 0.2f), new Vector2(0.97f, 0.8f),
                UIPalette.Action, () => Cook(captured), 22);
            button.interactable = CookingTable.CanCook(recipe,
                id => BuildingState.CountIn(inv.Stash, inv.Bag, id), Unlocked) == CookError.None;
        }

        frame.List.Content.sizeDelta = new Vector2(0f, y);
    }

    private void Cook(Recipe recipe)
    {
        PlayerInventory inv = PlayerInventory.EnsureInstance();
        CookError error = CookingTable.Cook(recipe, inv.Stash, inv.Bag, catalog?.Find(recipe.OutputId), Unlocked);

        frame.Status.text = error == CookError.None ? $"{Josa.EulReul(NameOf(recipe.OutputId))} 만들었다." : CookingTable.Explain(error);

        if (error == CookError.None)
        {
            inv.RefreshCapacity();
            ExchangeWindowUI.RefreshIfOpen();
            SaveManager.Commit("요리");
        }

        Refresh();
    }

    /// <summary>묶음(산적의 고기)은 카탈로그에 없다 — IngredientTable이 이름을 준다.</summary>
    private string NameOf(string id)
        => IngredientTable.IsTag(id) ? IngredientTable.NameOf(id) : catalog?.Find(id)?.DisplayName ?? id;

    private static void Place(RectTransform rect, ref float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(4f, -(y + height));
        rect.offsetMax = new Vector2(-4f, -y);
        y += height + RowGap;
    }
}
