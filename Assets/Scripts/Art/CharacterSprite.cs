using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 【도트 캐릭터 그림】 (결정 2-129) — PixelLab에서 만든 8방향 그림(서 있기 1장 · 걷기 8장)을 나무 · 돌처럼
/// 카메라 쪽으로 세운 판(기울기 ArtCatalog.Pitch)에 붙여 그린다. 빛 · 그림자는 나무 판과 같은 재질을 쓴다.
/// 원래 몸(캡슐)은 그리지 않는다 — 충돌 · 물리는 그대로. 그림 판은 돌지 않고 8방향 그림을 고른다 —
/// 쏘는 동안은 조준 쪽, 아니면 걷는 쪽(결정 2-132). 움직이면 걷기, 멈추면 서 있기.
/// 그림: Resources/Characters/{이름}/idle_{방향}.png · walk_{방향}_{0~7}.png (68×68, 아래 11칸이 발 밑 여백).
/// 주인공(어린 도깨비)은 씬이 열릴 때 저절로 입힌다.
/// </summary>
public class CharacterSprite : MonoBehaviour
{
    public const string PlayerSet = "Dokkaebi";

    /// <summary>방향 이름 — 북(화면 위)에서 시계 방향.</summary>
    public static readonly string[] Directions = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };

    public const int CanvasPixels = 68;
    public const int FeetPadPixels = 11;     // 68칸 그림에서 발 밑 빈칸
    public const int BodyPixels = 47;        // 발끝 ~ 머리끝
    public const int WalkFrames = 8;

    [Tooltip("발끝 ~ 머리끝 실제 키(m). 원래 캡슐은 2m.")]
    [SerializeField] private float height = 1.8f;
    [Tooltip("이동 속도 5m/s일 때 걷기 그림 초당 장수.")]
    [SerializeField] private float walkFps = 10f;

    private sealed class Set
    {
        public Texture2D[] Idle = new Texture2D[8];
        public Texture2D[,] Walk = new Texture2D[8, WalkFrames];
        public bool Complete;
    }

    private static readonly Dictionary<string, Set> Sets = new();
    private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    private static Mesh quad;

    private Set set;
    private Transform card;
    private MeshRenderer cardRenderer;
    private MeshRenderer behindRenderer;   // 나무 · 집 뒤에 가려지면 비치는 실루엣 (결정 2-130)
    private MaterialPropertyBlock block;
    private Renderer bodyRenderer;
    private Collider bodyCollider;
    private Rigidbody body;
    private PlayerWeapon weapon;

    /// <summary>쏜 뒤 이만큼은 조준 쪽을 본다 — 연사 사이에 걷는 쪽으로 휙휙 돌지 않게.</summary>
    private const float AttackFacingSeconds = 0.6f;
    private float walkClock;
    private Texture2D shown;

    /// <summary>그림 판 렌더러 (점검용).</summary>
    public Renderer CardRenderer => cardRenderer;

    public int Direction { get; private set; } = 4;
    public bool Walking { get; private set; }

    // ── 주인공에 저절로 입히기 ──────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        DressPlayers();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => DressPlayers();

    public static void DressPlayers()
    {
        if (!Has(PlayerSet))
            return;
        foreach (PlayerMovement p in FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (!p.TryGetComponent(out CharacterSprite _))
                p.gameObject.AddComponent<CharacterSprite>().Use(PlayerSet);
    }

    public static bool Has(string name) => Load(name).Complete;

    private static Set Load(string name)
    {
        if (Sets.TryGetValue(name, out Set s))
            return s;
        s = new Set();
        bool ok = true;
        for (int d = 0; d < 8; d++)
        {
            s.Idle[d] = Resources.Load<Texture2D>($"Characters/{name}/idle_{Directions[d]}");
            ok &= s.Idle[d] != null;
            for (int f = 0; f < WalkFrames; f++)
            {
                s.Walk[d, f] = Resources.Load<Texture2D>($"Characters/{name}/walk_{Directions[d]}_{f}");
                ok &= s.Walk[d, f] != null;
            }
        }
        s.Complete = ok;
        Sets[name] = s;
        return s;
    }

    // ── 몸 ────────────────────────────────────────────────────────

    public void Use(string name)
    {
        set = Load(name);
        if (!set.Complete)
        {
            GameLogger.Warning($"[CharacterSprite] 「{name}」 그림이 모자라 입히지 않는다.");
            enabled = false;
            return;
        }

        body = GetComponent<Rigidbody>();
        weapon = GetComponent<PlayerWeapon>();
        bodyCollider = GetComponent<Collider>();
        if (TryGetComponent(out bodyRenderer))
            bodyRenderer.forceRenderingOff = true;   // 켜짐 표시는 남긴다 — 다른 코드가 몸을 끄면 그림도 숨는다

        Transform figure = transform.Find(PlaceholderFigure.FigureName);
        if (figure != null)
            figure.gameObject.SetActive(false);

        BuildCard();
        Show(set.Idle[Direction]);
    }

    private void BuildCard()
    {
        var go = new GameObject("~CharacterSprite");
        card = go.transform;
        card.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = Quad();
        cardRenderer = go.AddComponent<MeshRenderer>();
        cardRenderer.sharedMaterial = CardMaterial();
        cardRenderer.shadowCastingMode = ShadowCastingMode.On;
        cardRenderer.receiveShadows = true;
        block = new MaterialPropertyBlock();

        Material behind = BehindMaterial();
        if (behind != null)
        {
            var ghost = new GameObject("~Behind");
            ghost.transform.SetParent(card, false);
            ghost.AddComponent<MeshFilter>().sharedMesh = Quad();
            behindRenderer = ghost.AddComponent<MeshRenderer>();
            behindRenderer.sharedMaterial = behind;
            behindRenderer.shadowCastingMode = ShadowCastingMode.Off;
            behindRenderer.receiveShadows = false;
        }

        float metersPerPixel = height / BodyPixels;
        float size = CanvasPixels * metersPerPixel;
        card.localScale = Vector3.one;   // 실제 크기는 LateUpdate에서 뿌리 크기를 되돌려 맞춘다
        cardSize = size;
        feetDrop = FeetPadPixels * metersPerPixel;
    }

    private float cardSize;
    private float feetDrop;

    private void OnDestroy()
    {
        if (bodyRenderer != null)
            bodyRenderer.forceRenderingOff = false;
    }

    private void LateUpdate()
    {
        if (card == null)
            return;

        // 몸을 끄면(사망 · 숨기기 등) 그림도 숨는다.
        cardRenderer.enabled = bodyRenderer == null || bodyRenderer.enabled;
        if (behindRenderer != null)
            behindRenderer.enabled = cardRenderer.enabled;

        Vector3 v = body != null ? body.linearVelocity : Vector3.zero;
        float speed = new Vector2(v.x, v.z).magnitude;
        Walking = speed > 0.3f;

        // 방향 (결정 2-132) — 1. 공격하는 방향: 막 쏜 동안은 조준(몸이 바라보는 쪽)
        //                    2. 움직이는 방향: 쏘지 않고 걸으면 걷는 쪽
        //                    둘 다 아니면 마지막 방향 그대로. 북 = 화면 위(+z).
        bool attacking = weapon != null && Time.time - weapon.LastShotTime < AttackFacingSeconds;
        Vector3 f = attacking ? transform.forward : Walking ? new Vector3(v.x, 0f, v.z) : Vector3.zero;
        if (f.x * f.x + f.z * f.z > 0.0001f)
        {
            float angle = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            Direction = ((Mathf.RoundToInt(angle / 45f) % 8) + 8) % 8;
        }

        if (Walking)
        {
            walkClock += Time.deltaTime * walkFps * Mathf.Clamp(speed / 5f, 0.6f, 1.6f);
            Show(set.Walk[Direction, (int)walkClock % WalkFrames]);
        }
        else
        {
            walkClock = 0f;
            Show(set.Idle[Direction]);
        }

        // 판 — 몸의 회전 · 크기와 무관하게 카메라 쪽으로 세운다. 발끝이 땅에 닿게.
        float ground = bodyCollider != null ? bodyCollider.bounds.min.y : transform.position.y;
        Quaternion tilt = Quaternion.Euler(ArtCatalog.Pitch, 0f, 0f);
        Vector3 up = tilt * Vector3.up;
        card.SetPositionAndRotation(new Vector3(transform.position.x, ground, transform.position.z) - up * feetDrop, tilt);
        Vector3 s = transform.lossyScale;
        card.localScale = new Vector3(cardSize / Mathf.Max(0.001f, s.x), cardSize / Mathf.Max(0.001f, s.y), 1f / Mathf.Max(0.001f, s.z));
    }

    private void Show(Texture2D tex)
    {
        if (tex == null || tex == shown)
            return;
        shown = tex;
        cardRenderer.GetPropertyBlock(block);
        block.SetTexture(BaseMap, tex);
        cardRenderer.SetPropertyBlock(block);
        if (behindRenderer != null)
            behindRenderer.SetPropertyBlock(block);
    }

    private static Material behindMaterial;

    /// <summary>가려졌을 때만 비치는 실루엣 재질. 셰이더는 Resources/Shaders에 두어 빌드에 늘 들어간다.</summary>
    private static Material BehindMaterial()
    {
        if (behindMaterial != null)
            return behindMaterial;
        Shader shader = Resources.Load<Shader>("Shaders/CharacterBehind");
        if (shader == null || !shader.isSupported)
            return null;
        behindMaterial = new Material(shader) { name = "CharacterBehind" };
        return behindMaterial;
    }

    // ── 공유 부품 ──────────────────────────────────────────────────

    /// <summary>아래 가운데가 원점인 1×1 판. 앞이 -z(카메라 쪽).</summary>
    private static Mesh Quad()
    {
        if (quad != null)
            return quad;
        quad = new Mesh { name = "CharacterCard" };
        quad.SetVertices(new List<Vector3> { new(-0.5f, 0f, 0f), new(0.5f, 0f, 0f), new(-0.5f, 1f, 0f), new(0.5f, 1f, 0f) });
        quad.SetUVs(0, new List<Vector2> { new(0f, 0f), new(1f, 0f), new(0f, 1f), new(1f, 1f) });
        quad.SetNormals(new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
        quad.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
        quad.RecalculateBounds();
        return quad;
    }

    private static Material material;

    /// <summary>
    /// 나무 판과 같은 재질(빛 받음 · 알파 잘라내기 · 양면)을 복사해 쓴다 — 빌드에 같은 셰이더 변형이 이미 들어 있다.
    /// 아트 꾸러미가 없으면 URP Lit에 같은 설정을 건다.
    /// </summary>
    private static Material CardMaterial()
    {
        if (material != null)
            return material;

        Material source = null;
        ArtKit kit = ArtKit.Load();
        if (kit != null)
            foreach (ArtKit.Entry e in kit.Entries)
                if (e.Key != null && e.Key.StartsWith("S:") && e.Material != null)
                {
                    source = e.Material;
                    break;
                }

        if (source != null)
            material = new Material(source) { name = "CharacterCard" };
        else
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "CharacterCard" };
            material.SetFloat("_AlphaClip", 1f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", 0f);
            material.SetFloat("_Smoothness", 0f);
            material.renderQueue = (int)RenderQueue.AlphaTest;
        }
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Cutoff", 0.5f);
        return material;
    }
}
