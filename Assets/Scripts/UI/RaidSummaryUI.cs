using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【철수 결산 창】 (결정 2-142) — 철수해 소굴에 돌아오면 한 번 뜬다. 무엇을 얼마나 가져왔는지 · 얼마나 걸렸는지 · 몇을 쓰러뜨렸는지 · 창고 총가치.
/// 이야기 창 · 다른 화면이 떠 있으면 그것이 닫힐 때까지 기다린다. 기록은 RaidLedger.
/// </summary>
public class RaidSummaryUI : MonoBehaviour
{
    private static RaidSummaryUI instance;
    private GameObject panel;
    private Text body;
    private Text gainLabel;
    private float waitAfterLoad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (instance != null)
            return;
        Canvas canvas = UIFactory.CreateCanvas("RaidSummaryCanvas (Runtime)", 1450);
        DontDestroyOnLoad(canvas.gameObject);
        instance = canvas.gameObject.AddComponent<RaidSummaryUI>();
        instance.Build(canvas);

        EnemyController.Killed -= OnKilled;
        EnemyController.Killed += OnKilled;
    }

    private static void OnKilled(EnemyController enemy) => RaidLedger.CountKill();

    /// <summary>들고 있는 것(가방 + 착용 11칸)의 가치.</summary>
    public static int CarriedValue()
    {
        if (!PlayerInventory.HasInstance)
            return 0;
        PlayerInventory inventory = PlayerInventory.Instance;
        int total = RaidLedger.ValueOf(inventory.Bag.Stacks);
        EquipmentLoadout loadout = inventory.Loadout;
        if (loadout != null)
            for (int i = 0; i <= (int)EquipmentSlot.Ammo2; i++)
            {
                ItemStack s = loadout.Get((EquipmentSlot)i);
                if (s?.Definition != null && !s.IsEmpty)
                    total += s.Definition.BaseValue * s.Count;
            }
        return total;
    }

    private void Build(Canvas canvas)
    {
        RectTransform safe = UIFactory.CreateSafeArea(canvas);

        Image shade = UIFactory.CreatePanel("Shade", safe, new Color(0f, 0f, 0f, 0.5f), Vector2.zero, Vector2.one, radius: 0);
        shade.raycastTarget = true;
        panel = shade.gameObject;
        GamePause.Register(panel);

        Image box = UIFactory.CreateGlass("SummaryBox", shade.transform, UIPalette.Panel,
            new Vector2(0.30f, 0.22f), new Vector2(0.70f, 0.78f), UIFactory.RadiusLarge);

        UIFactory.CreateLabel(box.transform, "철수 결산", 44, FontStyle.Bold,
            new Vector2(0.06f, 0.80f), new Vector2(0.94f, 0.95f), TextAnchor.MiddleCenter, UIPalette.TextOnGlass);

        gainLabel = UIFactory.CreateLabel(box.transform, string.Empty, 50, FontStyle.Bold,
            new Vector2(0.06f, 0.62f), new Vector2(0.94f, 0.80f), TextAnchor.MiddleCenter, UIPalette.Gain);

        body = UIFactory.CreateLabel(box.transform, string.Empty, 26, FontStyle.Normal,
            new Vector2(0.08f, 0.26f), new Vector2(0.92f, 0.62f), TextAnchor.MiddleLeft, UIPalette.TextOnGlass);
        body.supportRichText = true;

        UIFactory.CreateButton(box.transform, "확인", new Vector2(0.25f, 0.06f), new Vector2(0.75f, 0.20f),
            UIPalette.Action, Hide, 30);

        panel.SetActive(false);
    }

    private void Update()
    {
        if (panel.activeSelf || RaidLedger.Pending == null)
            return;

        if (!SceneFlow.InBunker || GamePause.IsHeld || StoryDialogueUI.IsShowing)
        {
            waitAfterLoad = 0.6f;   // 소굴이 막 열렸을 때 이야기 창이 먼저 뜰 틈을 준다
            return;
        }

        waitAfterLoad -= Time.unscaledDeltaTime;
        if (waitAfterLoad > 0f)
            return;

        Show(RaidLedger.Pending.Value);
        RaidLedger.ClearPending();
    }

    private void Show(RaidLedger.Summary s)
    {
        int gain = s.Gain;
        gainLabel.text = $"{(gain >= 0 ? "+" : "−")}{Mathf.Abs(gain):N0}엽전";
        gainLabel.color = gain >= 0 ? UIPalette.Gain : UIPalette.Warning;

        string zone = StoryTable.Zone(s.ZoneId)?.Name ?? s.ZoneId ?? "구역";
        int stash = PlayerInventory.HasInstance ? PlayerInventory.Instance.Stash.TotalValue : 0;

        body.text =
            $"다녀온 곳  <b>{zone}</b>\n"
            + $"걸린 시간  <b>{RaidLedger.FormatTime(s.Seconds)}</b>\n"
            + $"쓰러뜨림  <b>{s.Kills}</b>\n"
            + $"들고 온 것  <b>{s.EndValue:N0}엽전</b>  (출발 {s.StartValue:N0})\n"
            + $"창고 총가치  <b>{stash:N0}엽전</b>";

        panel.SetActive(true);
    }

    private void Hide() => panel.SetActive(false);
}
