using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【임시】 요리 화면 — 잡화 가게 옆 부뚜막에서 재료로 음식을 만든다 (결정 2-64 · 2-71 · CookingTable).
/// 부뚜막 앞에서 누르면 연다. 모양은 레이어 · 아트 작업 때 다시 만든다.
/// </summary>
public class CookingUI : MonoBehaviour
{
    private static CookingUI instance;

    private ItemCatalog catalog;
    private readonly List<(Recipe recipe, Text label, Button button)> rows = new();
    private Text status;

    public static void Open()
    {
        if (instance != null)
        {
            instance.Refresh();
            return;
        }

        // 가방 화면(1000)보다 위.
        Canvas canvas = UIFactory.CreateCanvas("CookingCanvas (Runtime)", 1200);
        instance = canvas.gameObject.AddComponent<CookingUI>();
        instance.Build(canvas);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build(Canvas canvas)
    {
        catalog = ItemCatalog.Load();

        RectTransform safe = UIFactory.CreateSafeArea(canvas);
        Image dim = UIFactory.CreatePanel("Dim", safe, UIPalette.Dim, Vector2.zero, Vector2.one, 0);
        Image box = UIFactory.CreatePanel("Box", dim.transform, UIPalette.Panel,
            new Vector2(0.12f, 0.04f), new Vector2(0.88f, 0.96f));

        UIFactory.CreateLabel(box.transform, "요리 — 부뚜막", 30, FontStyle.Bold,
            new Vector2(0.05f, 0.88f), new Vector2(0.75f, 0.98f), TextAnchor.MiddleLeft);
        UIFactory.CreateButton(box.transform, "닫기", new Vector2(0.78f, 0.88f), new Vector2(0.96f, 0.97f),
            UIPalette.Header, () => Destroy(gameObject), 22);

        // 효과 크기로 나눈 탭 (결정 2-73) — 작음 8 · 보통 9 · 큼 5. 한 탭에 아홉 줄까지 들어간다.
        string[] tabNames = { "작음", "보통", "큼" };
        for (int t = 0; t < tabNames.Length; t++)
        {
            CookTier tier = (CookTier)t;
            float x = 0.05f + t * 0.20f;
            UIFactory.CreateButton(box.transform, tabNames[t], new Vector2(x, 0.78f), new Vector2(x + 0.18f, 0.86f),
                UIPalette.Header, () => ShowTier(tier), 22);
        }

        IReadOnlyList<Recipe> all = CookingTable.All;
        var perTier = new int[3];

        for (int i = 0; i < all.Count; i++)
        {
            Recipe recipe = all[i];
            int index = perTier[(int)recipe.Tier]++;
            float top = 0.76f - index * 0.075f;

            Text label = UIFactory.CreateLabel(box.transform, string.Empty, 19, FontStyle.Normal,
                new Vector2(0.05f, top - 0.07f), new Vector2(0.76f, top), TextAnchor.MiddleLeft);
            Button button = UIFactory.CreateButton(box.transform, "만들기",
                new Vector2(0.78f, top - 0.065f), new Vector2(0.96f, top - 0.005f), UIPalette.Action, () => Cook(recipe), 19);

            rows.Add((recipe, label, button));
        }

        status = UIFactory.CreateLabel(box.transform, string.Empty, 20, FontStyle.Normal,
            new Vector2(0.05f, 0.0f), new Vector2(0.95f, 0.07f), TextAnchor.MiddleCenter, UIPalette.TextAccent);

        ShowTier(CookTier.Small);
    }

    private static bool Unlocked => CookingTable.IsUnlocked(BuildingManager.IsOpen);

    private void Refresh()
    {
        PlayerInventory inv = PlayerInventory.EnsureInstance();

        foreach ((Recipe recipe, Text label, Button button) in rows)
        {
            var parts = new List<string>();

            foreach (MaterialCost c in recipe.Inputs)
                parts.Add($"{NameOf(c.ItemId)} {c.Count} ({CookingTable.CountOf(c.ItemId, id => BuildingState.CountIn(inv.Stash, inv.Bag, id))})");

            label.text = $"{NameOf(recipe.OutputId)}  ←  {string.Join(" · ", parts)}";
            button.interactable = CookingTable.CanCook(recipe,
                id => BuildingState.CountIn(inv.Stash, inv.Bag, id), Unlocked) == CookError.None;
        }

        if (!Unlocked)
            status.text = CookingTable.Explain(CookError.Locked);
    }

    private void Cook(Recipe recipe)
    {
        PlayerInventory inv = PlayerInventory.EnsureInstance();
        CookError error = CookingTable.Cook(recipe, inv.Stash, inv.Bag, catalog?.Find(recipe.OutputId), Unlocked);

        status.text = error == CookError.None ? $"{Josa.EulReul(NameOf(recipe.OutputId))} 만들었다." : CookingTable.Explain(error);

        if (error == CookError.None)
        {
            inv.RefreshCapacity();
            ExchangeWindowUI.RefreshIfOpen();
            SaveManager.Commit("요리");
        }

        Refresh();
    }

    private void ShowTier(CookTier tier)
    {
        foreach ((Recipe recipe, Text label, Button button) in rows)
        {
            bool on = recipe.Tier == tier;
            label.gameObject.SetActive(on);
            button.gameObject.SetActive(on);
        }

        Refresh();
    }

    /// <summary>묶음(산적의 고기)은 카탈로그에 없다 — IngredientTable이 이름을 준다.</summary>
    private string NameOf(string id)
        => IngredientTable.IsTag(id) ? IngredientTable.NameOf(id) : catalog?.Find(id)?.DisplayName ?? id;
}
