using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【장 맵의 본문 연출 — 임시】 (결정 2-111). 장 맵이 뜨면 이야기 진행에 맞춰 소품 · 빛을 놓는다. StoryDirector가 붙인다.
///   1장 1-2  붉은 팥 골목 — 밟으면 발바닥이 따끔(본문 227)
///   2장      우레가 가끔 번쩍(본문 370) · 멈춘 물레방아 → 강철이를 쫓으면 강물이 돌아오고 물레방아가 돈다(본문 432)
///   3장 3-2  불씨를 다 모으면 돌아가는 길에 불씨가 하나둘 사라진다(본문 579) → 약방 처마 밑 새색시가 나타났다
///            사라지며 삼켰던 불씨가 흩어진다(본문 583 ~ 593) → 다시 주워 담는다(퀘스트 자리 「흩어진 불씨」 · 결정 2-112)
///   6장      새벽 냄새(본문 1079) — 빛이 밝다 · 삼족오를 쫓으면 더 희어진다(부록 1192) · 6-1 꽃밭의 꽃 · 빈 꽃대 → 엔딩 뒤 봉오리(본문 1101)
///   6장 6-2  구미호와 싸울 때 각시탈을 쓴다(본문 1063)
/// 모양 · 수치는 [임시값] — 아트 작업 때 프리팹으로 바꾼다.
/// </summary>
public class StoryScenery : MonoBehaviour
{
    public const string BrideSeenEvent = QuestSpotTable.BrideSeenEvent;

    /// <summary>불씨가 하나 사라지기까지(초) [임시값].</summary>
    public const float EmberFadeSeconds = 6f;

    private int chapter = -1;
    private Transform player;
    private Health playerHealth;

    private Transform wheel;
    private GameObject water;
    private float thunderTimer;
    private Light sun;
    private Color sunBase;
    private float sunIntensity;

    private float beanTimer;
    private bool brideStarted;
    private float emberTimer = EmberFadeSeconds;
    private bool coolingAnnounced;
    private GameObject mask;
    private GameObject bud;

    private static readonly Vector3 BeanAlley = new(-30f, 0f, -1f);
    private static readonly Vector3 BrideSpot = new(10f, 0f, 26f);
    private static readonly Vector3 MillWheel = new(-10f, 0f, 8f);
    private static readonly Vector3 EmptyStalk = new(0f, 0f, 0f);

    private void Start()
    {
        if (ZoneMap.Current == null || ZoneMap.Current.Map == null)
        {
            enabled = false;
            return;
        }

        chapter = ZoneMap.Current.Map.Chapter;
        sun = FindSun();
        if (sun != null)
        {
            sunBase = sun.color;
            sunIntensity = sun.intensity;
        }

        thunderTimer = Random.Range(15f, 30f);

        switch (chapter)
        {
            case 1: BuildBeans(); break;
            case 2: BuildMill(); break;
            case 6: BuildGarden(); break;
        }
    }

    private void Update()
    {
        if (player == null)
        {
            player = QuestSpotDirector.Player;
            if (player != null)
                player.TryGetComponent(out playerHealth);
        }

        StoryProgress p = StoryManager.Progress;

        switch (chapter)
        {
            case 1: TickBeans(); break;
            case 2: TickMill(p); break;
            case 3: TickBride(p); break;
            case 6: TickDawn(p); TickMask(p); TickBud(p); break;
        }
    }

    // ── 1장 — 팥 ─────────────────────────────────────────────────────

    private void BuildBeans()
    {
        var root = new GameObject("Scenery_RedBeans").transform;
        for (int i = 0; i < 40; i++)
        {
            Vector2 r = Random.insideUnitCircle * 3.2f;
            SceneryKit.Shape(PrimitiveType.Sphere, root, BeanAlley + new Vector3(r.x, 0.05f, r.y),
                Vector3.one * 0.12f, new Color(0.65f, 0.08f, 0.1f), 1f, "Bean");
        }
    }

    private void TickBeans()
    {
        if (player == null || playerHealth == null || playerHealth.IsDead)
            return;

        Vector3 d = player.position - BeanAlley;
        d.y = 0f;
        if (d.magnitude > 3.2f)
            return;

        beanTimer -= Time.deltaTime;
        if (beanTimer > 0f)
            return;

        beanTimer = 1.5f;
        playerHealth.TakeTickDamage(1);   // 따끔
    }

    // ── 2장 — 우레 · 물레방아 · 강물 ──────────────────────────────────

    private void BuildMill()
    {
        wheel = SceneryKit.Shape(PrimitiveType.Cylinder, null, MillWheel + new Vector3(1.8f, 2.2f, 0f),
            new Vector3(3.6f, 0.25f, 3.6f), new Color(0.45f, 0.33f, 0.2f), 1f, "Scenery_MillWheel").transform;
        wheel.rotation = Quaternion.Euler(0f, 0f, 90f);
    }

    private void TickMill(StoryProgress p)
    {
        bool flowing = p.HasDefeated("gangcheori");

        if (flowing && water == null)
            water = SceneryKit.Shape(PrimitiveType.Cube, null, new Vector3(-8f, 0.05f, 0f),
                new Vector3(7.6f, 0.04f, 140f), new Color(0.25f, 0.45f, 0.75f), 0.6f, "Scenery_River");

        if (flowing && wheel != null)
            wheel.Rotate(Vector3.up, 40f * Time.deltaTime, Space.Self);

        // 우레 — 강철이를 쫓기 전까지 가끔 번쩍인다.
        if (!flowing && sun != null)
        {
            thunderTimer -= Time.deltaTime;
            if (thunderTimer <= 0f)
            {
                thunderTimer = Random.Range(18f, 40f);
                StartCoroutine(Flash());
            }
        }
    }

    private System.Collections.IEnumerator Flash()
    {
        sun.intensity = sunIntensity * 3f;
        yield return new WaitForSeconds(0.08f);
        sun.intensity = sunIntensity;
        yield return new WaitForSeconds(0.1f);
        sun.intensity = sunIntensity * 2f;
        yield return new WaitForSeconds(0.06f);
        sun.intensity = sunIntensity;
    }

    // ── 3장 — 새색시 ────────────────────────────────────────────────

    private void TickBride(StoryProgress p)
    {
        if (brideStarted || player == null || p.HasSeen(BrideSeenEvent) || !AllEmbers(p))
            return;

        Vector3 d = player.position - BrideSpot;
        d.y = 0f;
        if (d.magnitude > 9f)
        {
            // 부엌으로 돌아가는 길 — 화로가 식으며 불씨가 하나둘 사라진다(본문 579).
            if (!coolingAnnounced)
            {
                coolingAnnounced = true;
                StoryDialogueUI.ShowBanner("화로가 조금씩 식었다. 불씨가 하나둘 사라지고 있었다.", 3f);
            }

            emberTimer -= Time.deltaTime;
            if (emberTimer <= 0f)
            {
                emberTimer = EmberFadeSeconds;
                foreach (string lost in QuestSpotTable.LostEmbers)
                {
                    if (p.HasSeen(lost))
                        continue;
                    p.See(lost);
                    StoryManager.NotifyChanged();
                    break;
                }
            }
            return;
        }

        brideStarted = true;
        StartCoroutine(Bride());
    }

    private System.Collections.IEnumerator Bride()
    {
        var root = new GameObject("Scenery_Bride").transform;
        root.position = BrideSpot;

        SceneryKit.Shape(PrimitiveType.Capsule, root, new Vector3(0f, 1f, 0f), new Vector3(0.7f, 1f, 0.7f),
            new Color(0.8f, 0.15f, 0.2f), 1f, "Bride");
        SceneryKit.Shape(PrimitiveType.Sphere, root, new Vector3(0f, 1.85f, 0.25f), new Vector3(0.45f, 0.55f, 0.15f),
            new Color(0.98f, 0.93f, 0.86f), 1f, "Kaksital");

        yield return new WaitForSeconds(2.2f);

        // 도깨비불이 하얗게 타오른다 — 새색시가 물러서며 사라진다(여우 꼬리).
        GameObject flare = SceneryKit.Shape(PrimitiveType.Sphere, null, player != null ? player.position + Vector3.up * 2.3f : BrideSpot,
            Vector3.one * 1.6f, Color.white, 0.8f, "WhiteFlare");
        SceneryKit.Shape(PrimitiveType.Capsule, root, new Vector3(0f, 0.3f, -0.5f), new Vector3(0.25f, 0.6f, 0.25f),
            new Color(0.9f, 0.55f, 0.2f), 1f, "FoxTail");

        yield return new WaitForSeconds(0.6f);
        Destroy(flare);
        Destroy(root.gameObject);

        StoryManager.Progress.See(BrideSeenEvent);
    }

    private static bool AllEmbers(StoryProgress p)
    {
        foreach (string e in QuestSpotTable.Embers)
            if (!p.HasSeen(QuestSpotTable.EventOf(e)))
                return false;
        return true;
    }

    // ── 6장 — 새벽 · 꽃밭 · 각시탈 ───────────────────────────────────

    private void BuildGarden()
    {
        var root = new GameObject("Scenery_Garden").transform;
        Color[] petals = { new(0.95f, 0.55f, 0.7f), new(0.95f, 0.85f, 0.4f), new(0.75f, 0.6f, 0.95f), new(0.95f, 0.95f, 0.95f) };

        for (int i = 0; i < 60; i++)
        {
            Vector2 r = Random.insideUnitCircle * 28f;
            Vector3 at = new(r.x, 0f, r.y);
            if ((at - EmptyStalk).magnitude < 3f)
                continue;
            Flower(root, at, petals[i % petals.Length], wilted: i % 7 == 0);
        }

        // 참봉의 꽃 · 빚쟁이의 꽃 (본문 1017 · 1019) — 빚쟁이의 꽃은 시들어 간다.
        Flower(root, new Vector3(-10f, 0f, 8f), new Color(0.6f, 0.85f, 0.95f), wilted: false);
        Flower(root, new Vector3(10f, 0f, 8f), new Color(0.85f, 0.75f, 0.5f), wilted: true);

        // 이름표가 빈 꽃대 (본문 1011)
        SceneryKit.Shape(PrimitiveType.Cylinder, root, EmptyStalk + new Vector3(0f, 0.6f, 0f), new Vector3(0.06f, 0.6f, 0.06f),
            new Color(0.35f, 0.55f, 0.25f), 1f, "EmptyStalk");
    }

    private static void Flower(Transform root, Vector3 at, Color petal, bool wilted)
    {
        float h = wilted ? 0.35f : 0.5f;
        SceneryKit.Shape(PrimitiveType.Cylinder, root, at + new Vector3(0f, h, 0f), new Vector3(0.05f, h, 0.05f),
            new Color(0.3f, 0.5f, 0.22f), 1f, "Stem");
        SceneryKit.Shape(PrimitiveType.Sphere, root, at + new Vector3(wilted ? 0.12f : 0f, h * 2f, 0f), Vector3.one * (wilted ? 0.2f : 0.32f),
            wilted ? petal * 0.55f : petal, 1f, "Bloom");
    }

    private void TickDawn(StoryProgress p)
    {
        if (sun == null)
            return;

        float dawn = p.HasDefeated("samjogo") ? 1f : 0.5f;
        sun.color = Color.Lerp(sunBase, new Color(1f, 0.85f, 0.7f), dawn);
        sun.intensity = sunIntensity * (1f + 0.5f * dawn);
    }

    private void TickMask(StoryProgress p)
    {
        Health gumiho = KaksitalRule.Gumiho;
        bool fighting = gumiho != null && gumiho.isActiveAndEnabled && !gumiho.IsDead && KaksitalRule.Has(p) && player != null
                        && (gumiho.transform.position - player.position).sqrMagnitude < 30f * 30f;

        if (fighting && mask == null)
        {
            mask = SceneryKit.Shape(PrimitiveType.Sphere, player, new Vector3(0f, 1.75f, 0.35f), new Vector3(0.45f, 0.55f, 0.12f),
                new Color(0.98f, 0.93f, 0.86f), 1f, "Kaksital_Worn");
        }
        else if (!fighting && mask != null)
        {
            Destroy(mask);
        }
    }

    private void TickBud(StoryProgress p)
    {
        if (bud != null || !p.IsEnded)
            return;

        // 이름표가 비어 있던 꽃대 끝에 작은 봉오리 하나(본문 1101).
        bud = SceneryKit.Shape(PrimitiveType.Sphere, null, EmptyStalk + new Vector3(0f, 1.25f, 0f), new Vector3(0.18f, 0.26f, 0.18f),
            new Color(0.95f, 0.6f, 0.7f), 1f, "Bud");
    }

    private static Light FindSun()
    {
        foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            if (l.type == LightType.Directional)
                return l;
        return null;
    }
}
