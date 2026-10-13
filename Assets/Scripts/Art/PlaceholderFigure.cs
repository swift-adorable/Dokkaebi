using UnityEngine;

/// <summary>
/// 【임시 인형】 (결정 2-115 · 시험용) — 캡슐 · 상자 대신 머리가 큰 작은 인형을 도형으로 세운다. 픽셀 캐릭터가 어떻게 보이는지 보려는 것이다.
/// 생김새는 본문과 상관없는 임시 모양이다(뿔 · 옷 · 색은 아트 방향이 정해지면 3D 모델로 바꾼다). 원래 몸(캡슐 · 상자)은 끄고 그 자리에 세운다.
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
            Color skin = new(0.96f, 0.82f, 0.68f);
            Color cloth = new(0.88f, 0.86f, 0.8f);
            Color pants = new(0.22f, 0.26f, 0.38f);
            Color hair = new(0.12f, 0.1f, 0.12f);

            anim.LegL = Part(t, PrimitiveType.Cube, new Vector3(-0.13f, 0.25f, 0f), new Vector3(0.17f, 0.5f, 0.2f), pants, pivotTop: true);
            anim.LegR = Part(t, PrimitiveType.Cube, new Vector3(0.13f, 0.25f, 0f), new Vector3(0.17f, 0.5f, 0.2f), pants, pivotTop: true);
            anim.Body = Part(t, PrimitiveType.Cube, new Vector3(0f, 0.8f, 0f), new Vector3(0.56f, 0.62f, 0.4f), cloth);
            anim.ArmL = Part(t, PrimitiveType.Cube, new Vector3(-0.37f, 0.82f, 0f), new Vector3(0.14f, 0.46f, 0.15f), cloth, pivotTop: true);
            anim.ArmR = Part(t, PrimitiveType.Cube, new Vector3(0.37f, 0.82f, 0f), new Vector3(0.14f, 0.46f, 0.15f), cloth, pivotTop: true);
            Transform head = Part(t, PrimitiveType.Sphere, new Vector3(0f, 1.42f, 0f), Vector3.one * 0.78f, skin);
            Part(head, PrimitiveType.Sphere, new Vector3(0f, 0.12f, -0.06f), new Vector3(1.04f, 0.78f, 1.0f), hair, local: true);
            Part(head, PrimitiveType.Sphere, new Vector3(-0.17f, -0.02f, 0.44f), Vector3.one * 0.13f, Color.black, local: true);
            Part(head, PrimitiveType.Sphere, new Vector3(0.17f, -0.02f, 0.44f), Vector3.one * 0.13f, Color.black, local: true);
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
        SceneryKit.Tint(shape, color);
        if (shape.TryGetComponent(out Renderer r))
        {
            Material m = r.material;
            if (!glow && m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
            if (m.HasProperty("_SpecularHighlights")) { m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); }
            if (m.HasProperty("_EnvironmentReflections")) { m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); }
        }

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
