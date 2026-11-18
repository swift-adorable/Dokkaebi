using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【퀘스트 자리】 (결정 2-108 · QuestSpotTable) — 본문 행동을 하는 곳. 가까이 가서 누르면 줍거나 · 말을 걸거나 · 건넨다.
/// 지키는 자리(장승)는 누르는 것이 아니라 가까이 가면 달라붙은 잡귀가 드러나고, 다 떼어 내면 끝난다.
/// 모양은 임시(빛나는 기둥 · 작은 물건). 아트가 들어오면 프리팹으로 바꾼다.
/// </summary>
public class QuestSpot : MonoBehaviour
{
    /// <summary>지키는 자리 — 이 거리 안에 들어오면 잡귀가 드러난다 (m) [임시값].</summary>
    public const float DefendWakeRange = 22f;

    private QuestSpotDefinition definition;
    private readonly List<Health> guards = new();
    private bool guardsSpawned;

    public QuestSpotDefinition Definition => definition;

    /// <summary>지금 서 있는 퀘스트 자리 — 지도 · 미니맵이 「!」로 찍는다 (결정 2-142).</summary>
    public static readonly List<QuestSpot> Active = new();

    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);

    /// <summary>버튼 글.</summary>
    public string Prompt => definition != null ? definition.Label : string.Empty;

    public static QuestSpot Create(QuestSpotDefinition spot, Vector3 at)
    {
        var root = new GameObject($"QuestSpot ({spot.Id})");
        root.transform.position = at;

        var quest = root.AddComponent<QuestSpot>();
        quest.definition = spot;
        quest.BuildVisual();

        if (spot.Kind != QuestSpotKind.Defend)
            root.AddComponent<BunkerStation>().Setup(BunkerStation.Kind.QuestSpot, 2f);

        return quest;
    }

    private void BuildVisual()
    {
        Color color = definition.Kind switch
        {
            QuestSpotKind.Pickup => new Color(1f, 0.85f, 0.35f),
            QuestSpotKind.Defend => new Color(0.95f, 0.35f, 0.3f),
            _ => new Color(0.45f, 0.75f, 1f),
        };

        // 빛기둥 — 멀리서도 보이게.
        Make(PrimitiveType.Cylinder, new Vector3(0f, 1.6f, 0f), new Vector3(0.12f, 1.6f, 0.12f), color, 0.55f);

        // 바닥 고리 — 지키는 자리는 넓게.
        float ring = definition.Kind == QuestSpotKind.Defend ? 3f : 1.2f;
        Make(PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(ring, 0.01f, ring), color, 0.3f);

        // 줍는 것은 작은 물건으로.
        if (definition.Kind == QuestSpotKind.Pickup)
            Make(PrimitiveType.Cube, new Vector3(0f, 0.35f, 0f), new Vector3(0.45f, 0.3f, 0.35f), color, 1f);
    }

    private void Make(PrimitiveType type, Vector3 local, Vector3 scale, Color color, float alpha)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = local;
        go.transform.localScale = scale;
        Destroy(go.GetComponent<Collider>());

        var renderer = go.GetComponent<Renderer>();
        if (renderer == null)
            return;

        Material m = renderer.material;
        color.a = alpha;
        m.color = color;
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", color);
    }

    /// <summary>누르면 — 줍기 · 말 걸기 · 건네기.</summary>
    public void Use()
    {
        if (definition == null || definition.Kind == QuestSpotKind.Defend)
            return;

        // 넋 보내기 — 본문 1079 ~ 1103을 보여 준 뒤 끝낸다.
        if (definition.Id == QuestSpotTable.SendSoul)
        {
            string id = definition.DoneEvent;
            StoryManager.PlayOnce(StoryTable.SendSoulEvent, () => Complete(id));
            Destroy(gameObject);
            return;
        }

        if (definition.Kind == QuestSpotKind.Pickup)
            StoryDialogueUI.ShowBanner($"{Josa.EulReul(definition.Label)} 주웠다.", 1.5f);

        Complete(definition.DoneEvent);
        Destroy(gameObject);
    }

    private static void Complete(string doneEvent)
    {
        StoryManager.Progress.See(doneEvent);
        StoryManager.NotifyChanged();

        if (SceneFlow.InBunker)
            SaveManager.Commit("퀘스트");
    }

    // ── 지키는 자리 ─────────────────────────────────────────────────

    private void Update()
    {
        if (definition == null || definition.Kind != QuestSpotKind.Defend)
            return;

        if (!guardsSpawned)
        {
            Transform player = QuestSpotDirector.Player;
            if (player == null)
                return;

            Vector3 d = player.position - transform.position;
            d.y = 0f;
            if (d.magnitude > DefendWakeRange)
                return;

            guardsSpawned = true;
            SpawnGuards();
            return;
        }

        guards.RemoveAll(h => h == null || h.IsDead || !h.isActiveAndEnabled);

        if (guards.Count == 0)
        {
            Complete(definition.DoneEvent);
            StoryDialogueUI.ShowBanner($"{definition.Label} — 잡귀를 떼어 냈다.", 1.8f);
            Destroy(gameObject);
        }
    }

    private void SpawnGuards()
    {
        EnemyPrefabCatalog catalog = EnemyPrefabCatalog.Load();
        GameObject prefab = catalog != null ? catalog.Get(EnemyArchetype.Scav) : null;
        if (prefab == null)
        {
            guards.Clear();
            return;
        }

        PoolManager pool = PoolManager.EnsureInstance();
        int count = Mathf.Max(1, definition.Guards);

        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            Vector3 at = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.6f;

            GameObject body = pool.Spawn(prefab, at, Quaternion.identity);
            if (body == null)
                continue;

            if (body.TryGetComponent(out EnemyController keep))
                keep.KeepWhenFar = true;   // 지키기 잡귀 — 떼어 내야 끝나므로 사라지면 안 된다 (결정 2-128)

            if (body.TryGetComponent(out EnemyIdentity identity))
                identity.Apply(RaidManager.Current.Apply(EnemyProfile.Build(
                    EnemyArchetype.Scav, EnemyRarity.Normal, System.Array.Empty<EnemyAffix>())));

            if (body.TryGetComponent(out Health health))
                guards.Add(health);
        }

        if (guards.Count == 0)
            guardsSpawned = false;   // 내지 못했으면 다음에 다시
    }
}
