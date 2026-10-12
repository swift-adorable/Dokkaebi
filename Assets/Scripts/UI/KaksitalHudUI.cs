using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【각시탈 표시】 (결정 2-103) — 구미호가 가까이 있으면 화면 위 가운데에 탈 모양이 떨리며 나타난다.
/// 글은 없다. 구미호가 판에 나올 때 StoryRaidDirector가 만든다. 아트 작업 때 다시 그린다.
/// </summary>
public class KaksitalHudUI : MonoBehaviour
{
    private static KaksitalHudUI instance;

    private Image mask;
    private Transform player;
    private float findTimer;
    private float alpha;

    public static KaksitalHudUI EnsureInstance()
    {
        if (instance != null)
            return instance;

        Canvas canvas = UIFactory.CreateCanvas("KaksitalCanvas (Runtime)", 805);
        instance = canvas.gameObject.AddComponent<KaksitalHudUI>();
        instance.Build(canvas);
        return instance;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build(Canvas canvas)
    {
        var go = new GameObject("Kaksital", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(56f, 72f);
        rect.anchoredPosition = new Vector2(0f, -96f);

        mask = go.GetComponent<Image>();
        mask.sprite = UISprites.Of(UISprites.Glyph.Mask);
        mask.preserveAspect = false;
        mask.raycastTarget = false;
        mask.color = new Color(0.98f, 0.93f, 0.86f, 0f);   // 분 바른 흰 탈
        go.AddComponent<Outline>().effectColor = new Color(0.75f, 0.12f, 0.12f, 0.9f);   // 연지 빛 테두리
        go.SetActive(false);
    }

    private void Update()
    {
        bool show = ShouldShow();

        alpha = Mathf.MoveTowards(alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 3f);

        bool visible = alpha > 0.001f;
        if (mask.gameObject.activeSelf != visible)
            mask.gameObject.SetActive(visible);

        if (!visible)
            return;

        Color c = mask.color;
        c.a = alpha;
        mask.color = c;

        // 떨림 — 탈이 스스로 떤다.
        float t = Time.unscaledTime;
        mask.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 18f) * 6f * alpha);
    }

    private bool ShouldShow()
    {
        if (SceneFlow.InBunker)
            return false;

        Health gumiho = KaksitalRule.Gumiho;
        bool alive = gumiho != null && gumiho.isActiveAndEnabled && !gumiho.IsDead;
        if (!alive)
            return false;

        if (player == null)
        {
            findTimer -= Time.unscaledDeltaTime;
            if (findTimer > 0f)
                return false;
            findTimer = 0.5f;

            var dokkaebi = FindAnyObjectByType<DokkaebiController>(FindObjectsInactive.Exclude);
            player = dokkaebi != null ? dokkaebi.transform : null;
            if (player == null)
                return false;
        }

        Vector3 d = gumiho.transform.position - player.position;
        d.y = 0f;

        return KaksitalRule.ShouldNotice(KaksitalRule.Has(StoryManager.Progress), true, d.magnitude);
    }
}
