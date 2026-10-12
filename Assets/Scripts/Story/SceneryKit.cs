using UnityEngine;

/// <summary>【임시】 연출 소품을 단색 도형으로 만든다 (결정 2-111). 아트가 들어오면 프리팹으로 바꾼다.</summary>
public static class SceneryKit
{
    public static GameObject Shape(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale,
        Color color, float alpha = 1f, string name = "Prop")
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.Destroy(go.GetComponent<Collider>());
        if (parent != null)
            go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        Tint(go, color, alpha);
        return go;
    }

    public static void Tint(GameObject go, Color color, float alpha = 1f)
    {
        var renderer = go != null ? go.GetComponent<Renderer>() : null;
        if (renderer == null)
            return;

        Material m = renderer.material;
        color.a = alpha;
        m.color = color;
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", color);
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 0.6f);
        }
    }
}
