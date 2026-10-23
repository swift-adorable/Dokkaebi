using UnityEngine;

/// <summary>
/// 【도깨비불】 (결정 2-111 · 본문 32 「머리 위를 맴도는 도깨비불」) — 어린 도깨비 머리 위를 따라다니는 작은 불.
///   · 늘 머리 위에서 천천히 맴돈다 · 아궁이(3-2 불씨) 곁에서는 조금 더 밝다(본문 565)
///   · 5-3 뒤로는 할머니의 넋불 — 6장에서는 손바닥 높이로 내려와 품는다(본문 947 · 1035)
///   · 넋을 보낸 뒤(엔딩)에는 없다 — 「손바닥을 내려다보았다. 아무것도 없었다」(본문 1089)
/// 모양은 임시(빛나는 구 + 작은 빛). 플레이어에 붙는다.
/// </summary>
public class PlayerWisp : MonoBehaviour
{
    private Transform flame;
    private Light glow;
    private float phase;

    private void Start()
    {
        flame = SceneryKit.Shape(PrimitiveType.Sphere, null, Vector3.zero, Vector3.one * 0.32f,
            new Color(0.75f, 0.9f, 1f), 1f, "DokkaebiFire").transform;

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
        bool sent = p.IsEnded;
        flame.gameObject.SetActive(!sent);
        if (sent)
            return;

        bool soul = p.HasSeen(StoryTable.EnterEvent("5-3"));
        bool cradled = soul && ZoneMap.Current != null && ZoneMap.Current.Map != null && ZoneMap.Current.Map.Chapter == 6;

        phase += Time.deltaTime;
        Vector3 offset = cradled
            ? new Vector3(0.35f, 1.0f + Mathf.Sin(phase * 2f) * 0.04f, 0.35f)
            : new Vector3(Mathf.Cos(phase * 1.3f) * 0.45f, 2.3f + Mathf.Sin(phase * 2.1f) * 0.12f, Mathf.Sin(phase * 1.3f) * 0.45f);

        flame.position = transform.position + offset;

        // 넋불은 둥글고 작아 접시를 닮았다(본문 941) — 조금 납작하고 따뜻한 빛.
        flame.localScale = soul ? new Vector3(0.34f, 0.2f, 0.34f) : Vector3.one * 0.32f;
        Color c = soul ? new Color(1f, 0.85f, 0.6f) : new Color(0.75f, 0.9f, 1f);
        glow.color = c;
        glow.intensity = (NearHearth() ? 2.2f : 1.2f) + Mathf.Sin(phase * 6f) * 0.15f;
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
