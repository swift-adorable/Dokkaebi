using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 【보스 패턴의 위험 표시】 (결정 2-114) — 범위와 맞기까지 남은 시간을 함께 보인다.
///   · 범위 = 바닥의 옅은 면 + 또렷한 테두리 (맞는 자리가 정확히 어디까지인지). 면은 언제나 빨강, 테두리는 속성 색
///   · 시간 = 안에서 차오르는 진한 면. 테두리까지 다 차는 순간 맞는다
/// 모양은 셋 — 원(대부분) · 고리(주작: 안쪽 원은 안전) · 줄(해태: 들이받는 길).
/// 스스로 시간을 재고 다 차면 지워진다. 보스가 먼저 죽어도 남은 표시는 제 시간에 사라진다. 색 · 굵기는 [임시값].
/// </summary>
public class BossTelegraph : MonoBehaviour
{
    private enum Shape { Circle, Ring, Line }

    private const float Height = 0.05f;
    private const int Segments = 48;
    private const float EdgeWidth = 0.09f;
    private const float AreaAlpha = 0.12f;
    private const float FillAlpha = 0.38f;
    private const float EdgeAlpha = 0.95f;
    private const float FlashSeconds = 0.2f;

    /// <summary>범위를 채우는 색 — 패턴 · 속성과 상관없이 빨강 (사용자 2026-10-13). 속성 색은 테두리에만 남긴다.</summary>
    public static readonly Color FillColor = new(0.9f, 0.1f, 0.08f);

    private Shape shape;
    private float duration;
    private float elapsed;
    private float inner;
    private float outer;
    private float length;
    private float width;
    private Vector3 origin;
    private Vector3 direction;
    private Transform fill;
    private readonly System.Collections.Generic.List<Material> materials = new();

    /// <summary>차오른 정도(0~1) — 테스트가 읽는다.</summary>
    public float Progress => BossPatternTable.TelegraphProgress(elapsed, duration);

    /// <summary>차오르는 면.</summary>
    public Transform Fill => fill;

    /// <summary>at을 중심으로 반지름 radius — seconds 뒤에 맞는다.</summary>
    public static BossTelegraph Circle(Vector3 at, float radius, Color color, float seconds)
    {
        BossTelegraph t = Create("BossTelegraph_Circle", at, Shape.Circle, seconds);
        t.outer = radius;
        t.Disc(at, radius, FillColor, AreaAlpha, 0);
        t.fill = t.Disc(at, 0f, FillColor, FillAlpha, 1).transform;
        t.Edge(at, radius, color);
        t.Apply();
        return t;
    }

    /// <summary>inner ~ outer 사이가 맞는 자리, inner 안은 안전(어둡게).</summary>
    public static BossTelegraph Ring(Vector3 center, float inner, float outer, Color color, float seconds)
    {
        BossTelegraph t = Create("BossTelegraph_Ring", center, Shape.Ring, seconds);
        t.inner = inner;
        t.outer = outer;
        t.Disc(center, outer, FillColor, AreaAlpha, 0);
        t.fill = t.Disc(center, inner, FillColor, FillAlpha, 1).transform;
        t.Disc(center + Vector3.up * 0.01f, inner, new Color(0.08f, 0.08f, 0.1f), 0.55f, 2);
        t.Edge(center, outer, color);
        t.Edge(center, inner, new Color(0.85f, 0.85f, 0.85f));
        t.Apply();
        return t;
    }

    /// <summary>start에서 dir로 length만큼, 폭 width — 들이받는 길.</summary>
    public static BossTelegraph Line(Vector3 start, Vector3 dir, float length, float width, Color color, float seconds)
    {
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        BossTelegraph t = Create("BossTelegraph_Line", start, Shape.Line, seconds);
        t.origin = start;
        t.direction = dir;
        t.length = length;
        t.width = width;
        t.Strip(length, FillColor, AreaAlpha, 0);
        t.fill = t.Strip(0f, FillColor, FillAlpha, 1).transform;
        t.Box(color);
        t.Apply();
        return t;
    }

    /// <summary>맞는 순간 잠깐 번쩍인다.</summary>
    public static void Flash(Vector3 at, float radius, Color color)
    {
        BossTelegraph t = Create("BossTelegraph_Flash", at, Shape.Circle, FlashSeconds);
        t.outer = radius;
        t.Disc(at, radius, FillColor, 0.6f, 1);
        t.fill = null;
    }

    private static BossTelegraph Create(string name, Vector3 at, Shape shape, float seconds)
    {
        var go = new GameObject(name);
        go.transform.position = new Vector3(at.x, 0f, at.z);
        BossTelegraph t = go.AddComponent<BossTelegraph>();
        t.shape = shape;
        t.duration = Mathf.Max(0.01f, seconds);
        return t;
    }

    /// <summary>【개발용】 차오른 정도를 바로 정한다 — 멈춘 화면에서 확인하려고.</summary>
    public void PreviewAt(float progress)
    {
        elapsed = Mathf.Clamp01(progress) * duration;
        Apply();
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        Apply();
        if (elapsed >= duration)
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        foreach (Material m in materials)
            if (m != null)
            {
                if (Application.isPlaying) Destroy(m);
                else DestroyImmediate(m);
            }
        materials.Clear();
    }

    /// <summary>차오른 정도대로 안쪽 면을 키운다.</summary>
    private void Apply()
    {
        if (fill == null)
            return;

        float p = Progress;
        switch (shape)
        {
            case Shape.Circle:
                SetDiscRadius(fill, outer * p);
                break;
            case Shape.Ring:
                SetDiscRadius(fill, Mathf.Lerp(inner, outer, p));
                break;
            case Shape.Line:
                float l = length * p;
                fill.position = Flat(origin + direction * (l * 0.5f), Height + 0.005f);
                fill.localScale = new Vector3(width, 0.01f, Mathf.Max(0.0001f, l));
                break;
        }
    }

    // ────────────────────────────────── 만들기

    private GameObject Disc(Vector3 at, float radius, Color color, float alpha, int layer)
    {
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "Disc";
        Object.DestroyImmediate(disc.GetComponent<Collider>());
        disc.transform.SetParent(transform, true);
        disc.transform.position = Flat(at, Height + layer * 0.005f);
        SetDiscRadius(disc.transform, radius);
        Paint(disc.GetComponent<Renderer>(), color, alpha, layer);
        return disc;
    }

    private GameObject Strip(float len, Color color, float alpha, int layer)
    {
        GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strip.name = "Strip";
        Object.DestroyImmediate(strip.GetComponent<Collider>());
        strip.transform.SetParent(transform, true);
        strip.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        strip.transform.position = Flat(origin + direction * (len * 0.5f), Height + layer * 0.005f);
        strip.transform.localScale = new Vector3(width, 0.01f, Mathf.Max(0.0001f, len));
        Paint(strip.GetComponent<Renderer>(), color, alpha, layer);
        return strip;
    }

    private void Edge(Vector3 center, float radius, Color color)
    {
        LineRenderer line = NewLine("Edge", color);
        line.loop = true;
        line.positionCount = Segments;
        for (int i = 0; i < Segments; i++)
        {
            float a = i * Mathf.PI * 2f / Segments;
            line.SetPosition(i, Flat(center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius, Height + 0.02f));
        }
    }

    private void Box(Color color)
    {
        LineRenderer line = NewLine("Edge", color);
        line.loop = true;
        Vector3 side = Vector3.Cross(Vector3.up, direction) * (width * 0.5f);
        Vector3 end = origin + direction * length;
        line.positionCount = 4;
        line.SetPosition(0, Flat(origin - side, Height + 0.02f));
        line.SetPosition(1, Flat(end - side, Height + 0.02f));
        line.SetPosition(2, Flat(end + side, Height + 0.02f));
        line.SetPosition(3, Flat(origin + side, Height + 0.02f));
    }

    private LineRenderer NewLine(string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.widthMultiplier = EdgeWidth;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        Material m = NewMaterial(color, EdgeAlpha, 3);
        line.sharedMaterial = m;
        color.a = EdgeAlpha;
        line.startColor = line.endColor = color;
        return line;
    }

    private void Paint(Renderer renderer, Color color, float alpha, int layer)
    {
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = NewMaterial(color, alpha, layer);
    }

    /// <summary>반투명 · 빛을 받지 않는 재료. 층이 높을수록 나중에 그린다.</summary>
    private Material NewMaterial(Color color, float alpha, int layer)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                        ?? Shader.Find("Sprites/Default");
        var m = new Material(shader) { name = "BossTelegraph (Runtime)" };
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent + layer;
        color.a = alpha;
        m.color = color;
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", color);
        materials.Add(m);
        return m;
    }

    private static void SetDiscRadius(Transform disc, float radius)
    {
        float d = Mathf.Max(0.0001f, radius * 2f);
        disc.localScale = new Vector3(d, 0.01f, d);
    }

    private static Vector3 Flat(Vector3 v, float y) => new(v.x, y, v.z);
}
