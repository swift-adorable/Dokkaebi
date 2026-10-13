using UnityEngine;

/// <summary>
/// 【임시 인형】 (결정 2-115 · 시험용) — 캡슐 · 상자 대신 머리가 큰 작은 인형을 도형으로 세운다. 픽셀 캐릭터가 어떻게 보이는지 보려는 것이다.
/// 주인공은 이마 가운데 뿔 하나 · 소년 (사용자 2026-10-13). 그 밖의 옷 · 색은 임시 모양이다 — 아트 방향이 정해지면 3D 모델로 바꾼다. 원래 몸(캡슐 · 상자)은 끄고 그 자리에 세운다.
/// 걸으면 팔다리를 흔든다(FigureAnimator).
/// </summary>
public static class PlaceholderFigure
{
    public enum Kind { Player, Imp }

    public const string FigureName = "PlaceholderFigure";

    public static GameObject Build(GameObject root, Kind kind)
    {
        if (root == null)
            return null;

        Transform existing = root.transform.Find(FigureName);
        if (existing != null)
            return existing.gameObject;

        float feet = root.transform.position.y;
        if (root.TryGetComponent(out MeshRenderer body))
        {
            feet = body.bounds.min.y;
            body.enabled = false;
        }

        var figure = new GameObject(FigureName);
        figure.transform.SetParent(root.transform, false);
        Vector3 s = root.transform.lossyScale;
        figure.transform.localScale = new Vector3(1f / Mathf.Max(0.001f, s.x), 1f / Mathf.Max(0.001f, s.y), 1f / Mathf.Max(0.001f, s.z));
        figure.transform.position = new Vector3(root.transform.position.x, feet, root.transform.position.z);

        var anim = figure.AddComponent<FigureAnimator>();
        Transform t = figure.transform;

        if (kind == Kind.Player)
        {
            // 소년 — 이마 가운데 뿔 하나 (사용자 2026-10-13). 머리를 줄여 소년 비율로, 짧은 저고리 · 허리띠 · 무릎 바지 · 맨 정강이.
            Color skin = new(0.97f, 0.8f, 0.64f);
            Color jacket = new(0.93f, 0.91f, 0.85f);
            Color sash = new(0.62f, 0.2f, 0.16f);
            Color pants = new(0.24f, 0.3f, 0.46f);
            Color hair = new(0.13f, 0.1f, 0.1f);
            Color horn = new(0.96f, 0.9f, 0.72f);
            Color shoe = new(0.36f, 0.26f, 0.18f);

            anim.LegL = Leg(t, -0.11f, pants, skin, shoe);
            anim.LegR = Leg(t, 0.11f, pants, skin, shoe);
            anim.Body = Part(t, PrimitiveType.Cube, new Vector3(0f, 0.74f, 0f), new Vector3(0.46f, 0.42f, 0.3f), jacket);
            Part(anim.Body, PrimitiveType.Cube, new Vector3(0f, -0.4f, 0f), new Vector3(1.04f, 0.2f, 1.06f), sash, local: true);
            Part(anim.Body, PrimitiveType.Cube, new Vector3(0.05f, 0.1f, 0.51f), new Vector3(0.12f, 0.75f, 0.02f), jacket * 0.82f, local: true);
            anim.ArmL = Arm(t, -0.3f, jacket, skin);
            anim.ArmR = Arm(t, 0.3f, jacket, skin);

            Transform head = Part(t, PrimitiveType.Sphere, new Vector3(0f, 1.24f, 0f), Vector3.one * 0.62f, skin);
            // 짧은 머리 — 뒤통수를 덮는 덩어리 + 앞머리 몇 가닥
            Part(head, PrimitiveType.Sphere, new Vector3(0f, 0.16f, -0.1f), new Vector3(1.06f, 0.8f, 0.96f), hair, local: true);
            for (int i = -2; i <= 2; i++)
            {
                Transform tuft = Part(head, PrimitiveType.Cube, new Vector3(i * 0.17f, 0.3f, 0.33f), new Vector3(0.16f, 0.2f, 0.16f), hair, local: true);
                tuft.localRotation = Quaternion.Euler(-35f, 0f, i * 14f);
            }
            for (int i = -1; i <= 1; i++)
            {
                Transform spike = Part(head, PrimitiveType.Cube, new Vector3(i * 0.22f, 0.5f, -0.05f), new Vector3(0.18f, 0.22f, 0.18f), hair, local: true);
                spike.localRotation = Quaternion.Euler(-20f, 45f, i * 20f);
            }
            // 눈 · 입
            Part(head, PrimitiveType.Sphere, new Vector3(-0.16f, -0.04f, 0.47f), new Vector3(0.15f, 0.22f, 0.12f), new Color(0.1f, 0.08f, 0.1f), local: true);
            Part(head, PrimitiveType.Sphere, new Vector3(0.16f, -0.04f, 0.47f), new Vector3(0.15f, 0.22f, 0.12f), new Color(0.1f, 0.08f, 0.1f), local: true);
            Part(head, PrimitiveType.Cube, new Vector3(0f, -0.26f, 0.44f), new Vector3(0.12f, 0.04f, 0.06f), new Color(0.55f, 0.25f, 0.22f), local: true);
            // 뿔 하나 — 이마 가운데에서 위로, 조금 앞으로
            Transform hornT = Cone(head, new Vector3(0f, 0.42f, 0.22f), 0.1f, 0.36f, horn);
            hornT.localRotation = Quaternion.Euler(18f, 0f, 0f);
            anim.Head = head;
        }
        else
        {
            Color skin = new(0.32f, 0.36f, 0.3f);
            Color eye = new(1f, 0.85f, 0.25f);

            anim.LegL = Part(t, PrimitiveType.Cube, new Vector3(-0.18f, 0.18f, 0f), new Vector3(0.16f, 0.36f, 0.18f), skin * 0.8f, pivotTop: true);
            anim.LegR = Part(t, PrimitiveType.Cube, new Vector3(0.18f, 0.18f, 0f), new Vector3(0.16f, 0.36f, 0.18f), skin * 0.8f, pivotTop: true);
            Transform blob = Part(t, PrimitiveType.Sphere, new Vector3(0f, 0.75f, 0f), new Vector3(0.95f, 0.85f, 0.85f), skin);
            anim.Body = blob;
            anim.ArmL = Part(t, PrimitiveType.Cube, new Vector3(-0.5f, 0.8f, 0.1f), new Vector3(0.13f, 0.5f, 0.13f), skin * 0.85f, pivotTop: true);
            anim.ArmR = Part(t, PrimitiveType.Cube, new Vector3(0.5f, 0.8f, 0.1f), new Vector3(0.13f, 0.5f, 0.13f), skin * 0.85f, pivotTop: true);
            Part(blob, PrimitiveType.Sphere, new Vector3(-0.18f, 0.12f, 0.45f), Vector3.one * 0.16f, eye, local: true, glow: true);
            Part(blob, PrimitiveType.Sphere, new Vector3(0.18f, 0.12f, 0.45f), Vector3.one * 0.16f, eye, local: true, glow: true);
        }

        return figure;
    }

    /// <summary>다리 — 무릎 바지 · 맨 정강이 · 신. 엉덩이를 축으로 흔든다.</summary>
    private static Transform Leg(Transform parent, float x, Color pants, Color skin, Color shoe)
    {
        Transform pivot = Part(parent, PrimitiveType.Cube, new Vector3(x, 0.38f, 0f), new Vector3(0.17f, 0.22f, 0.2f), pants, pivotTop: true);
        Add(pivot, PrimitiveType.Cube, new Vector3(0f, -0.32f, 0f), new Vector3(0.12f, 0.2f, 0.13f), skin);
        Add(pivot, PrimitiveType.Cube, new Vector3(0f, -0.45f, 0.03f), new Vector3(0.14f, 0.08f, 0.2f), shoe);
        return pivot;
    }

    /// <summary>팔 — 소매 · 손. 어깨를 축으로 흔든다.</summary>
    private static Transform Arm(Transform parent, float x, Color sleeve, Color skin)
    {
        Transform pivot = Part(parent, PrimitiveType.Cube, new Vector3(x, 0.74f, 0f), new Vector3(0.14f, 0.34f, 0.15f), sleeve, pivotTop: true);
        Add(pivot, PrimitiveType.Sphere, new Vector3(0f, -0.4f, 0f), Vector3.one * 0.12f, skin);
        return pivot;
    }

    /// <summary>축(빈 부모) 아래에 도형을 하나 더 단다.</summary>
    private static Transform Add(Transform parent, PrimitiveType type, Vector3 position, Vector3 size, Color color)
    {
        GameObject shape = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(shape.GetComponent<Collider>());
        shape.name = type.ToString();
        Paint(shape, color, false);
        shape.transform.SetParent(parent, false);
        shape.transform.localPosition = position;
        shape.transform.localScale = size;
        return shape.transform;
    }

    /// <summary>원뿔(뿔) — 밑면 반지름 · 높이(m, 부모 크기 무시하고 세계 기준).</summary>
    private static Transform Cone(Transform parent, Vector3 localPosition, float radius, float height, Color color)
    {
        const int sides = 12;
        var vertices = new Vector3[sides + 2];
        var triangles = new int[sides * 6];
        vertices[sides] = new Vector3(0f, height, 0f);
        vertices[sides + 1] = Vector3.zero;
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            vertices[i] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            int n = (i + 1) % sides;
            triangles[i * 6 + 0] = i; triangles[i * 6 + 1] = sides; triangles[i * 6 + 2] = n;
            triangles[i * 6 + 3] = i; triangles[i * 6 + 4] = n; triangles[i * 6 + 5] = sides + 1;
        }
        var mesh = new Mesh { name = "Horn", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject("Horn");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>();
        Paint(go, color, false);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        Vector3 s = parent.lossyScale;
        go.transform.localScale = new Vector3(1f / Mathf.Max(0.001f, s.x), 1f / Mathf.Max(0.001f, s.y), 1f / Mathf.Max(0.001f, s.z));
        return go.transform;
    }

    /// <summary>색을 입히고 반들거림을 없앤다(도트처럼 면이 끊기게).</summary>
    private static void Paint(GameObject shape, Color color, bool glow)
    {
        SceneryKit.Tint(shape, color);
        if (!shape.TryGetComponent(out Renderer r))
            return;
        Material m = r.material;
        if (!glow && m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
        if (m.HasProperty("_SpecularHighlights")) { m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); }
        if (m.HasProperty("_EnvironmentReflections")) { m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); }
    }

    /// <summary>
    /// 도형 하나. pivotTop이면 위쪽 끝을 축으로 하는 빈 부모를 만들어 그것을 돌리게 한다(팔다리 흔들기).
    /// local이면 부모 크기에 상대적으로(머리 위 머리카락 · 눈).
    /// </summary>
    private static Transform Part(Transform parent, PrimitiveType type, Vector3 position, Vector3 size, Color color,
        bool pivotTop = false, bool local = false, bool glow = false)
    {
        GameObject shape = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(shape.GetComponent<Collider>());
        shape.name = type.ToString();
        Paint(shape, color, glow);

        if (!pivotTop)
        {
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = size;
            return shape.transform;
        }

        var pivot = new GameObject(type + "Pivot").transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = position + new Vector3(0f, size.y * 0.5f, 0f);
        shape.transform.SetParent(pivot, false);
        shape.transform.localPosition = new Vector3(0f, -size.y * 0.5f, 0f);
        shape.transform.localScale = size;
        _ = local;
        return pivot;
    }
}

/// <summary>임시 인형의 걸음 — 움직인 만큼 팔다리를 흔들고 몸을 조금 들썩인다.</summary>
public class FigureAnimator : MonoBehaviour
{
    public Transform LegL, LegR, ArmL, ArmR, Body, Head;

    private Vector3 last;
    private float phase;
    private float speed;
    private Vector3 bodyBase;

    private void Start()
    {
        last = transform.position;
        if (Body != null) bodyBase = Body.localPosition;
    }

    private void LateUpdate()
    {
        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        Vector3 delta = transform.position - last;
        delta.y = 0f;
        last = transform.position;
        speed = Mathf.Lerp(speed, delta.magnitude / dt, 0.2f);

        float amount = Mathf.Clamp01(speed / 4f);
        phase += dt * (6f + speed * 1.5f);
        float swing = Mathf.Sin(phase) * 35f * amount;

        if (LegL != null) LegL.localRotation = Quaternion.Euler(swing, 0f, 0f);
        if (LegR != null) LegR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        if (ArmL != null) ArmL.localRotation = Quaternion.Euler(-swing * 0.8f, 0f, 0f);
        if (ArmR != null) ArmR.localRotation = Quaternion.Euler(swing * 0.8f, 0f, 0f);
        if (Body != null) Body.localPosition = bodyBase + Vector3.up * Mathf.Abs(Mathf.Sin(phase)) * 0.05f * amount;
    }
}
