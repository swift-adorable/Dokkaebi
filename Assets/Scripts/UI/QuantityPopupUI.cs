using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【수량 고르기】 (결정 2-77) — 겹치는 물건을 창고에 넣고 빼거나, 줍거나, 사고팔 때 몇 개인지 고른다.
/// 하나뿐이면 묻지 않고 바로 한다. 버리기 팝업과 같은 꼴(슬라이더 + 직접 입력).
/// </summary>
public class QuantityPopupUI : MonoBehaviour
{
    private static QuantityPopupUI instance;

    private int max;
    private int amount;
    private Func<int, string> describe;
    private Action<int> confirm;

    private Slider slider;
    private InputField field;
    private Text info;
    private bool syncing;

    /// <param name="title">물건 이름</param>
    /// <param name="verb">버튼 말 — 꺼내기 · 넣기 · 줍기 · 사기 · 팔기</param>
    /// <param name="maxCount">고를 수 있는 최대</param>
    /// <param name="describeAmount">n개일 때 붙일 말(값 · 무게). 없으면 null</param>
    public static void Ask(string title, string verb, int maxCount, Func<int, string> describeAmount, Action<int> onConfirm)
    {
        if (maxCount <= 0)
            return;

        if (maxCount == 1)
        {
            onConfirm?.Invoke(1);
            return;
        }

        Close();

        Canvas canvas = UIFactory.CreateCanvas("QuantityPopupCanvas (Runtime)", 1300);
        instance = canvas.gameObject.AddComponent<QuantityPopupUI>();
        instance.max = maxCount;
        instance.amount = maxCount;
        instance.describe = describeAmount;
        instance.confirm = onConfirm;
        instance.Build(canvas, title, verb);
    }

    /// <summary>떠 있는가 — 전리품 창이 뒤지기를 잠시 멈춘다 (결정 2-142).</summary>
    public static bool IsOpen => instance != null;

    public static void Close()
    {
        if (instance != null)
            Destroy(instance.gameObject);
        instance = null;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build(Canvas canvas, string title, string verb)
    {
        RectTransform safe = UIFactory.CreateSafeArea(canvas);
        GamePause.Register(canvas.gameObject);   // 떠 있는 동안 게임이 멈춘다 (결정 2-92)
        Image dim = UIFactory.CreatePanel("Dim", safe, UIPalette.Dim, Vector2.zero, Vector2.one, 0);
        dim.raycastTarget = true;

        // 가게 · 시설 화면과 같은 꾸밈 (결정 2-123) — 짙은 판 · 보라 테 · 알약 단추.
        Image box = UIFactory.CreatePanel("Box", dim.transform, new Color(0.08f, 0.08f, 0.11f, 0.97f),
            new Vector2(0.32f, 0.31f), new Vector2(0.68f, 0.69f), UIFactory.RadiusLarge);
        UIFactory.CreateOutline(box, StationFrame.Accent, UIFactory.RadiusLarge, 3);

        UIFactory.CreateLabel(box.transform, title, 30, FontStyle.Bold,
            new Vector2(0.06f, 0.78f), new Vector2(0.80f, 0.95f), TextAnchor.MiddleLeft);
        UIFactory.CreateButton(box.transform, "✕", new Vector2(0.86f, 0.80f), new Vector2(0.96f, 0.95f),
            new Color(0f, 0f, 0f, 0.001f), Close, 30);

        info = UIFactory.CreateLabel(box.transform, string.Empty, 24, FontStyle.Normal,
            new Vector2(0.06f, 0.60f), new Vector2(0.94f, 0.76f), TextAnchor.MiddleLeft, UIPalette.TextAccent);

        slider = UIFactory.CreateIntSlider(box.transform, new Vector2(0.06f, 0.36f), new Vector2(0.68f, 0.56f), 1, max, amount);
        slider.onValueChanged.AddListener(v => Sync((int)v));

        field = UIFactory.CreateIntField(box.transform, new Vector2(0.74f, 0.36f), new Vector2(0.94f, 0.56f), amount);
        field.onValueChanged.AddListener(raw =>
        {
            if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, out int parsed))
                Sync(parsed);
        });

        StationFrame.Pill(box.transform, "1개", new Vector2(0.06f, 0.08f), new Vector2(0.30f, 0.26f),
            StationFrame.PillDark, () => Sync(1), 24);
        StationFrame.Pill(box.transform, verb, new Vector2(0.34f, 0.08f), new Vector2(0.94f, 0.26f),
            StationFrame.PillAction, Confirm, 26);

        Sync(amount);
    }

    private void Sync(int value)
    {
        if (syncing)
            return;

        syncing = true;
        amount = Mathf.Clamp(value, 1, max);

        slider.SetValueWithoutNotify(amount);
        if (field.text != amount.ToString())
            field.SetTextWithoutNotify(amount.ToString());

        string extra = describe != null ? describe(amount) : string.Empty;
        info.text = string.IsNullOrEmpty(extra) ? $"{amount} / {max}" : $"{amount} / {max}    {extra}";
        syncing = false;
    }

    private void Confirm()
    {
        Action<int> callback = confirm;
        int chosen = amount;
        Close();
        callback?.Invoke(chosen);
    }
}
