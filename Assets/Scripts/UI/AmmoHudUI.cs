using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【임시】 화살통 · 탄창 HUD (결정 2-80) — 화면 아래 가운데 한 줄. 채우는 중 · 멘 때만 뜬다.
///   「화살통 채우는 중 ■■□□」 / 「환목궁을 멨다 — 맨손」
/// 남은 수는 퀵슬롯 1 · 2(무기 칸)가 보여 준다 (결정 2-81).
/// 아트 작업 때 다시 그린다.
/// </summary>
public class AmmoHudUI : MonoBehaviour
{
    private static AmmoHudUI instance;

    private Text label;
    private bool hiddenByScreen;

    public static AmmoHudUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        Canvas canvas = UIFactory.CreateCanvas("AmmoHudCanvas (Runtime)", 806);
        instance = canvas.gameObject.AddComponent<AmmoHudUI>();
        instance.Build(canvas);
        return instance;
    }

    public static void SetHiddenByScreen(bool hidden)
    {
        if (instance == null)
            return;

        instance.hiddenByScreen = hidden;
        instance.label.gameObject.SetActive(!hidden);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build(Canvas canvas)
    {
        label = UIFactory.CreateLabel(canvas.transform, "", 24, FontStyle.Bold,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), TextAnchor.MiddleCenter, UIPalette.TextOnGlass);
        label.rectTransform.pivot = new Vector2(0.5f, 0f);
        label.rectTransform.sizeDelta = new Vector2(640f, 34f);
        label.rectTransform.anchoredPosition = new Vector2(0f, 190f);   // 퀵슬롯 줄 위
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.raycastTarget = false;
        label.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.75f);
    }

    public void Show(WeaponDefinition weapon, EquipmentLoadout loadout, AmmoFeed feed, int inBag)
    {
        if (hiddenByScreen)
            return;

        if (weapon == null)
        {
            label.text = string.Empty;
            return;
        }

        string holder = weapon.KindInfo.HolderName;

        switch (feed.State)
        {
            case AmmoFeedState.Reloading:
                label.color = UIPalette.TextAccent;
                label.text = $"{holder} 채우는 중 {Bar(feed.Progress)}";
                break;

            case AmmoFeedState.Slinging:
            case AmmoFeedState.Slung:
                label.color = UIPalette.Warning;
                label.text = $"{Josa.EulReul(weapon.DisplayName)} 멨다 — 맨손 · 탄 없음";
                break;

            case AmmoFeedState.Switching:
                label.color = UIPalette.TextDim;
                label.text = $"{Josa.EulReul(weapon.DisplayName)} 드는 중";
                break;

            default:
                label.text = string.Empty;
                break;
        }
    }

    private static string Bar(float progress)
    {
        int filled = Mathf.Clamp(Mathf.RoundToInt(progress * 8f), 0, 8);
        return new string('■', filled) + new string('□', 8 - filled);
    }
}
