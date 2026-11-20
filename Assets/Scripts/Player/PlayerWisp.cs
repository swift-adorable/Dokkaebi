using UnityEngine;

/// <summary>
/// 【도깨비불】 (결정 2-111 · 본문 32 「머리 위를 맴도는 도깨비불」) — 어린 도깨비 머리 위를 따라다니는 작은 불.
///   · 늘 머리 위에서 천천히 맴돈다 · 아궁이(3-2 불씨) 곁에서는 조금 더 밝다(본문 565)
///   · 5-3 뒤로는 할머니의 넋불 — 6장에서는 손바닥 높이로 내려와 품는다(본문 947 · 1035)
///   · 넋을 보낸 뒤(엔딩)에는 없다 — 「손바닥을 내려다보았다. 아무것도 없었다」(본문 1089)
/// 모양: 도트 불꽃 애니메이션(Resources/Wisp/&lt;색&gt;_&lt;모양&gt;.png, 가로 띠 32px 프레임 · devkidd 불꽃 팩)을 카메라 쪽으로 세운다.
///   도깨비불은 파랑, 넋불은 같은 모양의 노랑. 그림이 없으면 예전처럼 빛나는 구. 플레이어에 붙는다.
/// </summary>
public class PlayerWisp : MonoBehaviour
{
    /// <summary>불꽃 모양 — g4-5 (작고 둥근 불) · g4-4 (길쭉한 불). 시험 중 (2026-11-17).</summary>
    public static string Shape = "g4-4";

    /// <summary>불꽃 그림 1m 에 몇 칸 — 두 모양이 같은 도트 굵기로 (캐릭터는 23.5칸/m). 작을수록 불이 커진다.
    /// 길쭉한 불(48칸)이 약 1m — 사용자 「길쭉한 불로 하되 사이즈를 조금만 줄여서」 (2026-11-17).</summary>
    public static float FlamePixelsPerMeter = 48f;

    private Vector3 followVelocity;
    private bool placed;
    private float orbitAngle;   // 궤도 각 — 빠르기를 쌓아 가며 돌린다(빠르기가 바뀌어도 튀지 않게)

    /// <summary>불을 끌어당기는 힘 · 흔들림을 잡는 힘 — 스프링처럼 쫓아가 곡선이 부드럽게 휜다.</summary>
    private const float Pull = 14f, Damp = 5.5f;

    private const float FramesPerSecond = 10f;

    /// <summary>시험용 — 엔딩을 본 세이브에서도 불을 띄운다 (Playtest 메뉴가 켠다).</summary>
    public static bool DebugShow;

    private Transform flame;
    private Light glow;
    private float phase;

    private SpriteRenderer sprite;
    private Sprite[] blueFrames, soulFrames;
    private string builtShape;

    private void Start()
    {
        flame = new GameObject("DokkaebiFire").transform;
        sprite = flame.gameObject.AddComponent<SpriteRenderer>();
        LoadFrames();

        glow = flame.gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.range = 4f;
        glow.intensity = 1.2f;
        glow.color = new Color(0.7f, 0.85f, 1f);
        glow.shadows = LightShadows.None;
    }

    /// <summary>
    /// 【씬이 닫히며 지워질 때는 불을 따로 지우지 않는다】 (결정 2-135) — 철수로 구역이 닫힐 때 여기서 건
    /// Destroy가 소굴 씬을 읽는 동안 처리되며 에디터가 멈췄다(2-128의 ChapterArt와 같은 교착 — 프로세스 샘플:
    /// MeshRenderer 정리 ↔ 씬 읽기 스레드). 불은 같은 씬에 있으므로 씬과 함께 사라진다.
    /// </summary>
    private void OnDestroy()
    {
        if (flame != null && gameObject.scene.isLoaded)
            Destroy(flame.gameObject);
    }

    private void LateUpdate()
    {
        if (flame == null)
            return;

        StoryProgress p = StoryManager.Progress;
        bool sent = p.IsEnded && !DebugShow;
        flame.gameObject.SetActive(!sent);
        if (sent)
            return;

        bool soul = p.HasSeen(StoryTable.EnterEvent("5-3")) && !DebugShow;   // 시험 띄우기는 파란 도깨비불로
        bool cradled = soul && ZoneMap.Current != null && ZoneMap.Current.Map != null && ZoneMap.Current.Map.Chapter == 6;

        float dt = Time.deltaTime;
        phase += dt;
        Vector3 offset = cradled
            ? new Vector3(0.35f, 1.0f + Mathf.Sin(phase * 2f) * 0.04f, 0.35f)
            : Wander(phase, ref orbitAngle, dt);

        // 떠도는 점을 스프링처럼 쫓아간다 — 바로 붙지 않고 관성으로 휘어 들어가며,
        // 걸으면 뒤로 처졌다가 다시 감긴다.
        Vector3 target = transform.position + offset;
        dt = Mathf.Min(dt, 0.05f);   // 장면을 읽느라 한 프레임이 길어도 튀지 않게
        if (!placed || cradled || (target - flame.position).sqrMagnitude > 25f)   // 순간이동 · 장면 이동은 바로 붙인다
        {
            flame.position = target;
            followVelocity = Vector3.zero;
            placed = true;
        }
        else if (dt > 0f)
        {
            followVelocity += ((target - flame.position) * Pull - followVelocity * Damp) * dt;
            flame.position += followVelocity * dt;
        }

        // 움직이는 쪽으로 살짝 기울고, 빠를수록 꼬리가 조금 늘어난다(불꽃이 바람을 받는 듯).
        float lean = Mathf.Clamp(-followVelocity.x * 9f, -22f, 22f);
        float stretch = 1f + Mathf.Clamp(followVelocity.magnitude * 0.06f, 0f, 0.18f);
        flame.rotation = ArtCatalog.CardTilt * Quaternion.Euler(0f, 0f, lean);   // 캐릭터 판처럼 카메라 쪽으로 세운다

        if (builtShape != Shape)
            LoadFrames();
        Sprite[] frames = soul ? soulFrames : blueFrames;
        if (frames != null && frames.Length > 0)
        {
            sprite.sprite = frames[(int)(phase * FramesPerSecond) % frames.Length];
            flame.localScale = new Vector3(1f / Mathf.Sqrt(stretch), stretch, 1f);
        }
        else
            // 넋불은 둥글고 작아 접시를 닮았다(본문 941) — 조금 납작하고 따뜻한 빛.
            flame.localScale = soul ? new Vector3(0.34f, 0.2f, 0.34f) : Vector3.one * 0.32f;
        Color c = soul ? new Color(1f, 0.85f, 0.6f) : new Color(0.75f, 0.9f, 1f);
        glow.color = c;
        glow.intensity = (NearHearth() ? 2.2f : 1.2f) + Mathf.Sin(phase * 6f) * 0.15f;
    }

    /// <summary>
    /// 몸 둘레를 떠도는 점 — 같은 원을 되풀이하지 않게 (사용자 요청 2026-11-17 「좀 더 자연스럽게」).
    ///   · 도는 빠르기 · 거리 · 높이를 각각 다른 잡음(Perlin)으로 정해, 같은 길이 다시 나오지 않는다
    ///   · 빠르기는 거의 멈춰 머무는 때 ~ 휙 감아 도는 때를 오간다 (각을 쌓아 가므로 튀지 않는다)
    ///   · 기울어진 궤도: 앞(-Z, 카메라 쪽)으로 올 때 내려오고 뒤로 갈 때 올라가 대각선으로 감긴다
    ///   · 높이는 허리 ~ 머리 위, 거리는 몸에 바짝 ~ 한 걸음 밖
    /// 불은 이 점을 스프링처럼 쫓아가므로(LateUpdate) 꺾이는 곳이 둥글게 휜다.
    /// </summary>
    private static Vector3 Wander(float t, ref float angle, float dt)
    {
        float speed = Mathf.Lerp(0.15f, 2.4f, Mathf.SmoothStep(0f, 1f, Mathf.PerlinNoise(t * 0.18f, 3.7f)));
        float turn = Mathf.PerlinNoise(t * 0.05f, 8.1f) < 0.3f ? -1f : 1f;   // 드물게 반대로 돈다
        angle += speed * turn * dt;

        float radius = Mathf.Lerp(0.45f, 1.25f, Mathf.PerlinNoise(t * 0.22f, 5.3f));
        float tilt = Mathf.Lerp(0.15f, 0.6f, Mathf.PerlinNoise(t * 0.09f, 1.9f));
        float height = Mathf.Lerp(1.1f, 2.3f, Mathf.PerlinNoise(t * 0.16f, 9.4f));
        float x = Mathf.Cos(angle) * radius;
        float z = Mathf.Sin(angle) * radius * 0.75f;
        float y = height + Mathf.Sin(angle) * tilt + Mathf.Sin(t * 2.7f) * 0.04f;
        return new Vector3(x, y, z);
    }

    /// <summary>가로 띠 그림을 32px 프레임으로 자른다. 바닥 가운데가 원점 — 불꽃이 머리 위에 앉아 위로 탄다.</summary>
    private void LoadFrames()
    {
        builtShape = Shape;
        blueFrames = Slice($"Wisp/blue_{Shape}");
        soulFrames = Slice($"Wisp/yellow_{Shape}");
        if (blueFrames == null && sprite != null)
            sprite.enabled = false;
    }

    private static readonly System.Collections.Generic.Dictionary<string, Sprite[]> cache = new();

    private static Sprite[] Slice(string path)
    {
        if (cache.TryGetValue(path, out Sprite[] cached) && cached != null && cached.Length > 0 && cached[0] != null)
            return cached;
        var sheet = Resources.Load<Texture2D>(path);
        if (sheet == null)
            return null;
        const int frameW = 32;
        int n = Mathf.Max(1, sheet.width / frameW);
        var frames = new Sprite[n];
        float ppu = FlamePixelsPerMeter;
        for (int i = 0; i < n; i++)
            frames[i] = Sprite.Create(sheet, new Rect(i * frameW, 0, frameW, sheet.height), new Vector2(0.5f, 0.15f), ppu, 0, SpriteMeshType.FullRect);
        cache[path] = frames;   // 정적으로 붙잡아 장면을 바꿔도 내려가지 않게 (BuildingSprite 와 같은 이유)
        return frames;
    }

    private bool NearHearth()
    {
        if (ZoneMap.Current == null || ZoneMap.Current.Map == null || ZoneMap.Current.Map.Chapter != 3)
            return false;

        foreach (string id in QuestSpotTable.Embers)
        {
            QuestSpotDefinition s = QuestSpotTable.Spot(id);
            if (s != null && (new Vector3(s.X, 0f, s.Z) - transform.position).sqrMagnitude < 36f)
                return true;
        }
        return false;
    }
}
