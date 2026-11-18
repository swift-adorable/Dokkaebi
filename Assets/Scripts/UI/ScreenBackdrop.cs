using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【창 뒤 흐림】 (2026-11-18 사용자 「어떤 화면을 띄우면 뒤에 배경은 아주 살짝만 블러 처리를 하던지해서 새로 띄워진 화면과 분리감을」)
///   · 창이 열리면(GamePause.IsHeld) 그 순간의 게임 화면을 카메라로 한 장 찍는다 — HUD(화면 덮개 캔버스)는 찍히지 않는다
///   · 작게 줄였다가(1/4) 쌍선형으로 늘려 살짝 흐리게, 조금 어둡게 — 창 캔버스(1000~) 바로 아래 · HUD(800~950) 위에 깐다
///   · 창이 떠 있는 동안 게임은 멈춰 있어 한 장으로 충분하다(휴대폰에도 가볍다). 닫히면 걷는다
/// </summary>
public class ScreenBackdrop : MonoBehaviour
{
    public const int SortingOrder = 990;
    /// <summary>얼마나 줄였다 늘리나 — 클수록 더 흐리다. 4 = 아주 살짝.</summary>
    public static int Downscale = 4;
    /// <summary>흐린 배경의 밝기 (1 = 그대로).</summary>
    public static float Brightness = 0.82f;

    private static ScreenBackdrop instance;
    private RawImage image;
    private RenderTexture blurred;
    private bool shown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (instance != null)
            return;
        Canvas canvas = UIFactory.CreateCanvas("ScreenBackdropCanvas (Runtime)", SortingOrder);
        DontDestroyOnLoad(canvas.gameObject);
        instance = canvas.gameObject.AddComponent<ScreenBackdrop>();
        var go = new GameObject("Blur", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        instance.image = go.AddComponent<RawImage>();
        instance.image.raycastTarget = false;   // 누름은 창이 받는다 — 흐림은 보이기만
        instance.image.color = new Color(Brightness, Brightness, Brightness, 1f);
        go.SetActive(false);
    }

    private void LateUpdate()
    {
        bool want = GamePause.IsHeld;
        if (want == shown)
            return;
        shown = want;
        if (want)
            Capture();
        image.gameObject.SetActive(want && image.texture != null);
    }

    private void Capture()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        int w = Mathf.Max(1, Screen.width / 2), h = Mathf.Max(1, Screen.height / 2);
        RenderTexture full = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.Default);
        RenderTexture before = cam.targetTexture;
        cam.targetTexture = full;
        cam.Render();
        cam.targetTexture = before;

        // 반씩 줄여 가며 쌍선형으로 — 한 번에 줄이면 지글거린다. 찍은 것이 이미 화면의 1/2 이라 Downscale 이 될 때까지 더 줄인다.
        RenderTexture current = full;
        for (int scale = 2; scale < Downscale; scale *= 2)
        {
            RenderTexture next = RenderTexture.GetTemporary(Mathf.Max(1, current.width / 2), Mathf.Max(1, current.height / 2), 0, RenderTextureFormat.Default);
            next.filterMode = FilterMode.Bilinear;
            Graphics.Blit(current, next);
            if (current != full)
                RenderTexture.ReleaseTemporary(current);
            current = next;
        }

        if (blurred == null || blurred.width != current.width || blurred.height != current.height)
        {
            if (blurred != null)
                blurred.Release();
            blurred = new RenderTexture(current.width, current.height, 0, RenderTextureFormat.Default) { filterMode = FilterMode.Bilinear, name = "ScreenBackdrop" };
        }
        Graphics.Blit(current, blurred);
        if (current != full)
            RenderTexture.ReleaseTemporary(current);
        RenderTexture.ReleaseTemporary(full);

        image.texture = blurred;
        image.color = new Color(Brightness, Brightness, Brightness, 1f);
    }

    private void OnDestroy()
    {
        if (blurred != null)
            blurred.Release();
        if (instance == this)
            instance = null;
    }
}
