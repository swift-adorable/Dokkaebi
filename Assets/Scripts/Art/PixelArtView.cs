using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【픽셀 캐릭터 — HD-2D 시험】 (결정 2-115) — 배경은 3D 그대로, 캐릭터(플레이어 · 적)만 저해상도로 그려 도트처럼 보이게 한다.
///   · 캐릭터의 보이는 몸은 「PixelChar」 층의 복제본이 맡고(PixelCharacter), 원본은 그림자만 남긴다 — 그림자는 3D 바닥에 그대로 진다
///   · 픽셀 카메라가 주 카메라와 같은 자리 · 화각으로 그 층만 작은 그림(RenderTexture)에 그리고, 화면 맨 아래 UI로 점 필터 확대해 얹는다
///   · 캐릭터 키가 화면에서 TargetCharacterPixels(기본 48) 칸이 되도록 확대 배율을 고른다
/// 【시험 단계의 한계】 캐릭터가 늘 배경 위에 그려진다(지붕 · 벽 뒤에 가려지지 않는다). 머리 위 글자 · 막대는 캐릭터 그림 아래에 놓인다.
/// 켜고 끄기는 개발용 메뉴 「Dokkaebi/Art/픽셀 캐릭터 켜기 · 끄기」. 기본은 꺼짐.
/// </summary>
public class PixelArtView : MonoBehaviour
{
    public const string LayerName = "PixelChar";
    private const string MaterialPath = "Art/PixelComposite";
    private const float ScanSeconds = 0.5f;

    /// <summary>화면에서 캐릭터 키가 몇 칸이 될지.</summary>
    public static int TargetCharacterPixels = 48;

    /// <summary>확대 배율의 하한 — 화면이 작아도 도트가 보이게.</summary>
    public static int MinScale = 2;

    /// <summary>
    /// 몸을 카메라 반대쪽으로 젖히는 정도(카메라가 내려다보는 각도에 곱한다) — 위에서 내려다봐도 도트 그림처럼 앞모습이 보이게.
    /// 1이면 몸이 시선과 직각(완전히 그림처럼), 0이면 서 있는 그대로. 원본(그림자)은 젖히지 않는다.
    /// </summary>
    public static float LeanFactor = 0.85f;

    /// <summary>이번 프레임 몸을 눕히는 회전(세계 기준). 꺼져 있으면 회전 없음.</summary>
    public static Quaternion Lean { get; private set; } = Quaternion.identity;

    /// <summary>캐릭터의 대략적인 키(m) — 확대 배율 계산용. 지금 몸은 캡슐 2m.</summary>
    public static float CharacterHeight = 2f;

    private static PixelArtView instance;

    public static bool Enabled => instance != null && instance.isActiveAndEnabled;

    /// <summary>지금 확대 배율(화면 픽셀 ÷ 도트 한 칸).</summary>
    public static int Scale => instance != null ? instance.scale : 0;

    public static int Layer => LayerMask.NameToLayer(LayerName);

    private Camera main;
    private Camera pixelCamera;
    private RenderTexture texture;
    private Canvas canvas;
    private RawImage image;
    private Material composite;
    private int scale;
    private float nextScan;
    private readonly List<PixelCharacter> attached = new();

    public static void SetEnabled(bool on)
    {
        if (on)
        {
            if (Layer < 0)
            {
                Debug.LogError($"[PixelArt] 「{LayerName}」 층이 없다 — 메뉴 「Dokkaebi/Art/픽셀 층 · 재료 준비」를 먼저 실행한다.");
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[PixelArt] 주 카메라가 없다.");
                return;
            }

            if (instance == null)
                instance = cam.gameObject.AddComponent<PixelArtView>();
            instance.enabled = true;
        }
        else if (instance != null)
        {
            Destroy(instance);
            instance = null;
        }
    }

    /// <summary>
    /// 화면 높이 · 화각 · 캐릭터까지의 거리로 확대 배율을 고른다 — 순수 계산(테스트가 읽는다).
    /// </summary>
    public static int ScaleFor(int screenHeight, float verticalFov, float distance, float characterHeight, int targetPixels)
    {
        float worldHeight = 2f * distance * Mathf.Tan(verticalFov * 0.5f * Mathf.Deg2Rad);
        if (worldHeight <= 0f || characterHeight <= 0f || targetPixels <= 0)
            return 1;

        float rows = targetPixels * worldHeight / characterHeight;
        return Mathf.Clamp(Mathf.RoundToInt(screenHeight / rows), 1, 12);
    }

    /// <summary>카메라가 내려다보는 각도 × factor만큼 카메라 오른쪽 축으로 뒤로 젖히는 회전 — 순수 계산(테스트가 읽는다).</summary>
    public static Quaternion LeanFor(Vector3 cameraForward, Vector3 cameraRight, float factor)
    {
        float pitch = Mathf.Asin(Mathf.Clamp(-cameraForward.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        return Quaternion.AngleAxis(pitch * factor, cameraRight);
    }

    private void OnEnable()
    {
        main = GetComponent<Camera>();
        main.cullingMask &= ~(1 << Layer);

        var go = new GameObject("PixelCamera");
        go.transform.SetParent(transform, false);
        pixelCamera = go.AddComponent<Camera>();
        pixelCamera.cullingMask = 1 << Layer;
        pixelCamera.clearFlags = CameraClearFlags.SolidColor;
        pixelCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        pixelCamera.allowMSAA = false;
        pixelCamera.allowHDR = false;
        pixelCamera.depth = main.depth - 1;

        var canvasGo = new GameObject("PixelCanvas");
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1000;   // 모든 HUD 아래
        image = new GameObject("PixelImage").AddComponent<RawImage>();
        image.transform.SetParent(canvasGo.transform, false);
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Material source = Resources.Load<Material>(MaterialPath);
        if (source != null)
            composite = new Material(source);
        else
        {
            Shader shader = Shader.Find("Dokkaebi/PixelComposite");
            if (shader != null) composite = new Material(shader);
        }
        image.material = composite;

        scale = 0;
        nextScan = 0f;
        Debug.Log("[PixelArt] 픽셀 캐릭터 켬");
    }

    private void OnDisable()
    {
        Lean = Quaternion.identity;
        if (main != null)
            main.cullingMask |= 1 << Layer;

        foreach (PixelCharacter c in attached)
            if (c != null)
                Destroy(c);
        attached.Clear();

        if (pixelCamera != null) Destroy(pixelCamera.gameObject);
        if (canvas != null) Destroy(canvas.gameObject);
        if (texture != null) { texture.Release(); Destroy(texture); }
        if (composite != null) Destroy(composite);
        texture = null;
        Debug.Log("[PixelArt] 픽셀 캐릭터 끔");
    }

    private void LateUpdate()
    {
        if (main == null || pixelCamera == null)
            return;

        pixelCamera.fieldOfView = main.fieldOfView;
        pixelCamera.orthographic = main.orthographic;
        pixelCamera.orthographicSize = main.orthographicSize;
        pixelCamera.nearClipPlane = main.nearClipPlane;
        pixelCamera.farClipPlane = main.farClipPlane;

        Transform player = QuestSpotDirector.Player;
        float distance = player != null ? Vector3.Distance(main.transform.position, player.position) : 15f;
        int wanted = main.orthographic
            ? Mathf.Clamp(Mathf.RoundToInt(Screen.height / (TargetCharacterPixels * 2f * main.orthographicSize / CharacterHeight)), 1, 12)
            : ScaleFor(Screen.height, main.fieldOfView, distance, CharacterHeight, TargetCharacterPixels);

        wanted = Mathf.Max(MinScale, wanted);

        // 내려다보는 각도만큼(× LeanFactor) 카메라 오른쪽 축으로 뒤로 젖힌다 — 몸이 시선과 직각에 가까워진다.
        Lean = LeanFor(main.transform.forward, main.transform.right, LeanFactor);

        int w = Mathf.Max(1, Screen.width / wanted);
        int h = Mathf.Max(1, Screen.height / wanted);
        if (texture == null || wanted != scale || texture.width != w || texture.height != h)
        {
            scale = wanted;
            if (texture != null) { pixelCamera.targetTexture = null; texture.Release(); Destroy(texture); }
            texture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                filterMode = FilterMode.Point,
                antiAliasing = 1,
                name = "PixelCharacters"
            };
            pixelCamera.targetTexture = texture;
            image.texture = texture;
        }

        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + ScanSeconds;
            Scan();
        }
    }

    /// <summary>
    /// 【개발용】 지금 화면을 정한 해상도(예: 휴대폰 1920×1080)로 다시 그려 PNG로 남긴다 — 에디터 창이 작아도
    /// 실제 기기에서 도트가 어떻게 보이는지 보려고. UI는 빠진다.
    /// </summary>
    public static string CapturePreview(string path, int width, int height)
    {
        if (instance == null || instance.main == null || instance.pixelCamera == null)
            return null;

        Camera main = instance.main;
        Camera pix = instance.pixelCamera;
        Transform player = QuestSpotDirector.Player;
        float distance = player != null ? Vector3.Distance(main.transform.position, player.position) : 15f;
        int s = Mathf.Max(MinScale, ScaleFor(height, main.fieldOfView, distance, CharacterHeight, TargetCharacterPixels));

        var full = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var small = new RenderTexture(Mathf.Max(1, width / s), Mathf.Max(1, height / s), 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
        {
            filterMode = FilterMode.Point
        };

        RenderTexture oldMain = main.targetTexture, oldPix = pix.targetTexture;
        main.targetTexture = full;
        main.Render();
        pix.targetTexture = small;
        pix.Render();
        main.targetTexture = oldMain;
        pix.targetTexture = oldPix;

        if (instance.composite != null)
            Graphics.Blit(small, full, instance.composite);

        RenderTexture.active = full;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());

        Destroy(tex);
        full.Release(); Destroy(full);
        small.Release(); Destroy(small);
        Debug.Log($"[PixelArt] {width}×{height} · 배율 {s} → {path}");
        return path;
    }

    /// <summary>플레이어 · 적에게 픽셀 몸을 붙인다(새로 나온 것 · 몸이 바뀐 것).</summary>
    private void Scan()
    {
        attached.RemoveAll(c => c == null);

        foreach (PlayerMovement p in FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude))
            Track(PixelCharacter.Ensure(p.gameObject));

        foreach (EnemyIdentity e in FindObjectsByType<EnemyIdentity>(FindObjectsInactive.Exclude))
            Track(PixelCharacter.Ensure(e.gameObject));
    }

    private void Track(PixelCharacter c)
    {
        if (c != null && !attached.Contains(c))
            attached.Add(c);
    }
}
