using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 【적 도트 그림】 (2026-11-17) — 도형 몸 대신 설화 적 팩(vibeforge 「Korean Folklore Enemies」 — 상업용 가능 · AI 학습 금지라
/// 학습에는 안 씀)의 4방향 띠 그림을 주인공처럼 카메라 쪽으로 세운 판에 그린다.
///   · 그림: Resources/Enemies/{종}/{Idle|Walk|Attack}_{south|north|east|west}.png — 한 칸 92×92 가로 띠(칸 수는 폭 ÷ 92)
///   · 유형 → 종: 잡귀 = 도깨비(방망이), 절굿공이귀 = 불가사리, 무주귀 = 넋불(도깨비불), 허깨비 = 구미호, 순라귀 = 조선 창병.
///     그림이 없는 유형(번개귀 · 수귀 · 왕지네 · 침귀)은 예전 몸 그대로
///   · 걸으면 걷기, 멈추면 서 있기, 공격을 시작하면(EnemyAttack.IsWindingUp) 공격 띠를 한 번
///   · 몸(MeshRenderer)이 꺼지면(숨기 · 사망) 그림도 숨는다 — 몸은 그리지만 않고 켜짐 표시는 둔다
/// </summary>
public class EnemySprite : MonoBehaviour
{
    public const int Frame = 92;
    public const int FeetPad = 4;
    /// <summary>1m 에 몇 칸 — 도깨비(키 82칸)가 2.2m 남짓. 주인공(1.8m)보다 조금 크다.</summary>
    public const float PixelsPerMeter = 38f;
    private const float Fps = 9f;

    private static readonly string[] Dirs = { "north", "east", "south", "west" };

    public static string SpeciesOf(EnemyArchetype a) => a switch
    {
        EnemyArchetype.Scav => "Dokkaebi",
        EnemyArchetype.Crusher => "Bulgasari",
        EnemyArchetype.Wraith => "DokkaebiFire",   // 무주귀 — 제사를 못 받은 넋 → 떠도는 넋불
        EnemyArchetype.Settled => "Gumiho",
        EnemyArchetype.Sentry => "JoseonSoldier",
        _ => null,
    };

    private sealed class Set
    {
        public Texture2D[] Idle = new Texture2D[4], Walk = new Texture2D[4], Attack = new Texture2D[4];
        public bool Complete;
    }

    private static readonly Dictionary<string, Set> Sets = new();
    private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

    private Set set;
    private string species;
    private Transform card;
    private MeshRenderer cardRenderer;
    private MeshRenderer bodyRenderer;
    private Collider bodyCollider;
    private Rigidbody body;
    private EnemyAttack attack;
    private MaterialPropertyBlock block;
    private int dir = 2;
    private float clock;
    private float attackClock = -1f;
    private bool wasWinding;

    /// <summary>EnemyIdentity.Apply 가 부른다 — 풀에서 다시 나올 때 유형이 바뀌어도 맞춘다.</summary>
    public static void Dress(GameObject enemy, EnemyArchetype archetype)
    {
        string sp = SpeciesOf(archetype);
        enemy.TryGetComponent(out EnemySprite existing);
        if (sp == null || !Load(sp).Complete)
        {
            if (existing != null)
                existing.Undress();
            return;
        }
        if (existing == null)
            existing = enemy.AddComponent<EnemySprite>();
        existing.Use(sp);
    }

    private static Set Load(string sp)
    {
        if (Sets.TryGetValue(sp, out Set s))
            return s;
        s = new Set();
        bool ok = true;
        for (int d = 0; d < 4; d++)
        {
            s.Idle[d] = Resources.Load<Texture2D>($"Enemies/{sp}/Idle_{Dirs[d]}");
            s.Walk[d] = Resources.Load<Texture2D>($"Enemies/{sp}/Walk_{Dirs[d]}");
            s.Attack[d] = Resources.Load<Texture2D>($"Enemies/{sp}/Attack_{Dirs[d]}");
            ok &= s.Idle[d] != null && s.Walk[d] != null;
        }
        s.Complete = ok;
        Sets[sp] = s;   // 정적으로 붙잡아 장면을 바꿔도 내려가지 않게
        return s;
    }

    private void Use(string sp)
    {
        set = Load(sp);
        species = sp;
        enabled = true;
        body = GetComponent<Rigidbody>();
        attack = GetComponent<EnemyAttack>();
        bodyCollider = GetComponent<Collider>();
        if (bodyRenderer == null && TryGetComponent(out bodyRenderer))
            bodyRenderer.forceRenderingOff = true;
        else if (bodyRenderer != null)
            bodyRenderer.forceRenderingOff = true;
        Transform figure = transform.Find(PlaceholderFigure.FigureName);
        if (figure != null)
            figure.gameObject.SetActive(false);

        if (card == null)
        {
            var go = new GameObject("~EnemySprite");
            card = go.transform;
            card.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = BuildingSprite.Quad();
            cardRenderer = go.AddComponent<MeshRenderer>();
            cardRenderer.sharedMaterial = BuildingSprite.Material();
            cardRenderer.shadowCastingMode = ShadowCastingMode.On;
            cardRenderer.receiveShadows = true;
            block = new MaterialPropertyBlock();
        }
        card.gameObject.SetActive(true);
        attackClock = -1f;
        clock = Random.value * 10f;
    }

    private void Undress()
    {
        if (bodyRenderer != null)
            bodyRenderer.forceRenderingOff = false;
        if (card != null)
            card.gameObject.SetActive(false);
        enabled = false;
    }

    private void OnDestroy()
    {
        if (bodyRenderer != null)
            bodyRenderer.forceRenderingOff = false;
    }

    private void LateUpdate()
    {
        if (card == null || set == null)
            return;
        cardRenderer.enabled = bodyRenderer == null || bodyRenderer.enabled;

        Vector3 v = body != null ? body.linearVelocity : Vector3.zero;
        bool walking = v.x * v.x + v.z * v.z > 0.09f;

        bool winding = attack != null && attack.IsWindingUp;
        if (winding && !wasWinding && set.Attack[dir] != null)
            attackClock = 0f;
        wasWinding = winding;

        // 공격 중에는 몸이 바라보는 쪽(조준), 아니면 걷는 쪽. 북 = 화면 위(+z). 4방향.
        Vector3 f = attackClock >= 0f || winding ? transform.forward : walking ? v : Vector3.zero;
        if (f.x * f.x + f.z * f.z > 0.0001f)
            dir = Mathf.Abs(f.x) > Mathf.Abs(f.z) ? (f.x > 0f ? 1 : 3) : (f.z > 0f ? 0 : 2);

        Texture2D tex;
        int frame;
        if (attackClock >= 0f && set.Attack[dir] != null)
        {
            tex = set.Attack[dir];
            frame = (int)attackClock;
            attackClock += Time.deltaTime * Fps * 1.4f;
            if (frame >= Frames(tex))
            {
                attackClock = -1f;
                frame = Frames(tex) - 1;
            }
        }
        else
        {
            clock += Time.deltaTime * Fps;
            tex = walking ? set.Walk[dir] : set.Idle[dir];
            frame = (int)clock % Frames(tex);
        }
        Show(tex, frame);
        Place();
    }

    private static int Frames(Texture2D t) => Mathf.Max(1, t.width / Frame);

    private void Show(Texture2D tex, int frame)
    {
        int n = Frames(tex);
        cardRenderer.GetPropertyBlock(block);
        block.SetTexture(BaseMap, tex);
        block.SetVector(BaseMapST, new Vector4(1f / n, 1f, (float)frame / n, 0f));
        cardRenderer.SetPropertyBlock(block);
    }

    private void Place()
    {
        // 장 맵 바닥은 y = 0 — 몸이 조금 묻혀 나와도 그림은 땅 위에 선다.
        float ground = Mathf.Max(0f, bodyCollider != null ? bodyCollider.bounds.min.y : transform.position.y);
        Quaternion tilt = Quaternion.Euler(ArtCatalog.Pitch, 0f, 0f);
        Vector3 up = tilt * Vector3.up;
        float size = Frame / PixelsPerMeter;
        card.SetPositionAndRotation(new Vector3(transform.position.x, ground, transform.position.z) - up * (FeetPad / PixelsPerMeter), tilt);
        Vector3 s = transform.lossyScale;
        card.localScale = new Vector3(size / Mathf.Max(0.001f, s.x), size / Mathf.Max(0.001f, s.y), 1f / Mathf.Max(0.001f, s.z));
    }
}
