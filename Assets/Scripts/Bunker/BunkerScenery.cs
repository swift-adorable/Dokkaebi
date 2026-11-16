using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【소굴의 본문 연출 — 임시】 (결정 2-111). 소굴(고목 아래)에 이야기 진행에 맞춰 소품을 놓는다. StoryDirector가 붙인다.
///   · 빗자루 — 고목 뿌리 사이에 둔 몽당 빗자루와 오색 매듭(본문 116 ~ 124 · 부록 1205). 매듭 두 곳은 새김패를 묶는 자리
///   · 돌문 — 고목 뿌리 아래 네 홈(본문 164) · 받은 사신패가 홈에 서 있다(본문 713 · 855) · 5장에 들어가면 열린 채
///   · 상인 넷 — 영감 · 참봉 · 빚쟁이 · 길달이 온 밤부터 제 가게 앞(없으면 고목 둘레)에 선다
/// 모양은 단색 도형 [임시] — 아트 작업 때 바꾼다.
/// </summary>
public class BunkerScenery : MonoBehaviour
{
    private static readonly Vector3 BroomAt = new(-2.6f, 0f, 5.6f);
    private static readonly Vector3 GateAt = new(0f, 0f, BunkerLayout.RoomDepth * 0.5f - 0.5f);   // 북쪽 벽 바로 앞

    private static readonly (string tablet, Color color)[] Tablets =
    {
        ("현무패", new Color(0.15f, 0.15f, 0.2f)), ("청룡패", new Color(0.25f, 0.5f, 0.85f)),
        ("주작패", new Color(0.85f, 0.25f, 0.2f)), ("백호패", new Color(0.92f, 0.92f, 0.9f))
    };

    private static readonly (string id, Color color, float height, float girth, Vector3 fallback)[] Merchants =
    {
        (StoryTable.Elder,    new Color(0.55f, 0.5f, 0.4f),  1.2f, 0.7f, new Vector3(-1.6f, 0f, 3.2f)),
        (StoryTable.Chambong, new Color(0.4f, 0.55f, 0.6f),  1.1f, 0.65f, new Vector3(5f, 0f, 3.6f)),
        (StoryTable.Debtor,   new Color(0.45f, 0.35f, 0.3f), 1.6f, 1.1f, new Vector3(1.8f, 0f, 3.2f)),
        (StoryTable.Gildal,   new Color(0.75f, 0.5f, 0.25f), 1.3f, 0.6f, new Vector3(-4.5f, 0f, 3.6f))
    };

    private readonly List<GameObject> tabletObjects = new();
    private readonly Dictionary<string, GameObject> merchantObjects = new();
    private GameObject gateSlab;
    private float timer;

    private void Start()
    {
        BuildBroom();
        BuildGate();
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f)
            return;
        timer = 1f;

        StoryProgress p = StoryManager.Progress;
        RefreshTablets(p);
        RefreshMerchants(p);
    }

    private void BuildBroom()
    {
        var root = new GameObject("Scenery_Broom").transform;
        root.position = BroomAt;
        root.rotation = Quaternion.Euler(0f, 0f, 12f);

        SceneryKit.Shape(PrimitiveType.Cylinder, root, new Vector3(0f, 0.7f, 0f), new Vector3(0.07f, 0.7f, 0.07f),
            new Color(0.55f, 0.42f, 0.25f), 1f, "Handle");
        SceneryKit.Shape(PrimitiveType.Cube, root, new Vector3(0f, 0.12f, 0f), new Vector3(0.45f, 0.25f, 0.16f),
            new Color(0.75f, 0.65f, 0.35f), 1f, "Bristles");

        // 오색 매듭 — 가장 굵은 두 곳(새김패 칸 둘)은 크다.
        Color[] five = { Color.red, new(0.2f, 0.4f, 0.9f), Color.yellow, Color.white, new(0.1f, 0.1f, 0.1f) };
        for (int i = 0; i < five.Length; i++)
        {
            float size = i < 2 ? 0.16f : 0.1f;
            SceneryKit.Shape(PrimitiveType.Sphere, root, new Vector3(0f, 0.55f + i * 0.17f, 0f), Vector3.one * size,
                five[i], 1f, $"Knot{i}");
        }
    }

    private void BuildGate()
    {
        var root = new GameObject("Scenery_StoneGate").transform;
        root.position = GateAt;

        gateSlab = SceneryKit.Shape(PrimitiveType.Cube, root, new Vector3(0f, 1.2f, 0f), new Vector3(2.4f, 2.4f, 0.3f),
            new Color(0.42f, 0.42f, 0.4f), 1f, "Slab");

        for (int i = 0; i < 4; i++)
        {
            float x = -0.75f + i * 0.5f;
            SceneryKit.Shape(PrimitiveType.Cube, root, new Vector3(x, 1.2f, -0.17f), new Vector3(0.32f, 0.45f, 0.05f),
                new Color(0.12f, 0.12f, 0.12f), 1f, $"Groove{i}");
        }
    }

    private void RefreshTablets(StoryProgress p)
    {
        List<string> owned = p.Tablets;

        for (int i = 0; i < Tablets.Length; i++)
        {
            bool has = owned.Contains(Tablets[i].tablet);
            bool shown = i < tabletObjects.Count && tabletObjects[i] != null;

            if (has && !shown)
            {
                while (tabletObjects.Count <= i) tabletObjects.Add(null);
                tabletObjects[i] = SceneryKit.Shape(PrimitiveType.Cube, null,
                    GateAt + new Vector3(-0.75f + i * 0.5f, 1.2f, -0.2f), new Vector3(0.28f, 0.4f, 0.06f),
                    Tablets[i].color, 1f, $"Tablet_{Tablets[i].tablet}");
            }
        }

        // 돌문이 열렸다(본문 864) — 5장에 들어간 뒤로는 판이 내려앉아 길이 보인다.
        if (gateSlab != null && p.HasSeen(StoryTable.EnterEvent("5-1")))
        {
            gateSlab.transform.localPosition = new Vector3(0f, -0.9f, 0f);
            SceneryKit.Tint(gateSlab, new Color(0.2f, 0.2f, 0.2f));
        }
    }

    private void RefreshMerchants(StoryProgress p)
    {
        foreach (var m in Merchants)
        {
            bool here = p.HasMerchant(m.id);
            merchantObjects.TryGetValue(m.id, out GameObject existing);

            if (!here)
            {
                if (existing != null) Destroy(existing);
                merchantObjects.Remove(m.id);
                continue;
            }

            Vector3 at = m.fallback;
            MerchantDefinition def = StoryTable.Merchant(m.id);
            if (def != null && !string.IsNullOrEmpty(def.BuildingId)
                && BuildingManager.State.TryGetPose(def.BuildingId, out BuildingPose pose))
            {
                // 가게 앞 — 건물 자리 앞쪽 가장자리에서 0.6m 앞에 선다.
                BuildingDefinition building = BuildingTable.Find(def.BuildingId);
                float front = building != null ? building.Depth * 0.5f + 0.6f : 1.4f;
                at = new Vector3(pose.X, 0f, pose.Z - front);
            }

            if (existing == null)
            {
                existing = SceneryKit.Shape(PrimitiveType.Capsule, null, at + new Vector3(0f, m.height, 0f),
                    new Vector3(m.girth, m.height, m.girth), m.color, 1f, $"Merchant_{m.id}");
                merchantObjects[m.id] = existing;
            }
            else
            {
                existing.transform.position = at + new Vector3(0f, m.height, 0f);
            }
        }
    }
}
