using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 【픽셀 몸】 (결정 2-115 · PixelArtView) — 캐릭터의 메시 렌더러마다 「PixelChar」 층 복제본을 붙여 픽셀 카메라가 그리게 하고,
/// 원본은 그림자만 남긴다. 원본의 켜짐 · 재료 · 메시는 매 프레임 복제본으로 옮긴다(흐려짐 · 은신 · 피격 색이 그대로 보이게).
/// 글자(TextMesh)는 건드리지 않는다. 떼면 원래대로 돌아간다.
/// </summary>
public class PixelCharacter : MonoBehaviour
{
    private sealed class Pair
    {
        public MeshRenderer Source;
        public MeshFilter SourceFilter;
        public MeshRenderer Clone;
        public MeshFilter CloneFilter;
        public ShadowCastingMode OriginalShadows;
    }

    private readonly List<Pair> pairs = new();
    private readonly List<Material> materials = new();
    private int sourceCount = -1;
    private float feetDrop;   // 뿌리에서 발까지 내려가는 높이
    private Transform body;   // 복제본을 모아 두는 곳 — 뿌리 크기를 되돌려 늘 1배(비뚤어짐 없이 눕힐 수 있게)
    public const string BodyName = "PixelBody";

    public int PairCount => pairs.Count;

    public static PixelCharacter Ensure(GameObject go)
    {
        if (go == null || PixelArtView.Layer < 0)
            return null;

        if (!go.TryGetComponent(out PixelCharacter c))
            c = go.AddComponent<PixelCharacter>();

        c.Refresh();
        return c;
    }

    /// <summary>몸이 바뀌었으면(렌더러 수가 달라졌으면) 다시 붙인다.</summary>
    public void Refresh()
    {
        int count = CountSources();
        if (count == sourceCount)
            return;

        Detach();
        Attach();
        sourceCount = count;
    }

    private int CountSources()
    {
        int n = 0;
        foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>(true))
            if (IsSource(r))
                n++;
        return n;
    }

    private static bool IsSource(MeshRenderer r)
    {
        if (r == null || r.gameObject.layer == PixelArtView.Layer)
            return false;
        if (r.TryGetComponent(out TextMesh _))
            return false;
        return r.TryGetComponent(out MeshFilter f) && f.sharedMesh != null;
    }

    private void Attach()
    {
        int layer = PixelArtView.Layer;
        float minY = float.MaxValue;

        if (body == null)
        {
            body = new GameObject(BodyName) { layer = layer }.transform;
            body.SetParent(transform, false);
        }
        FitBody();
        foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!IsSource(r))
                continue;

            var go = new GameObject("Pixel_" + r.name) { layer = layer };
            go.transform.SetParent(body, false);
            Place(go.transform, r.transform, Quaternion.identity, Vector3.zero);
            var filter = go.AddComponent<MeshFilter>();
            var clone = go.AddComponent<MeshRenderer>();
            var source = r.GetComponent<MeshFilter>();
            filter.sharedMesh = source.sharedMesh;
            clone.sharedMaterials = r.sharedMaterials;
            clone.shadowCastingMode = ShadowCastingMode.Off;
            clone.receiveShadows = true;

            pairs.Add(new Pair
            {
                Source = r,
                SourceFilter = source,
                Clone = clone,
                CloneFilter = filter,
                OriginalShadows = r.shadowCastingMode
            });

            r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            if (r.enabled)
                minY = Mathf.Min(minY, r.bounds.min.y);
        }

        feetDrop = minY < float.MaxValue ? Mathf.Max(0f, transform.position.y - minY) : 0f;
    }

    private void Detach()
    {
        foreach (Pair p in pairs)
        {
            if (p.Source != null)
                p.Source.shadowCastingMode = p.OriginalShadows;
            if (p.Clone != null)
                Kill(p.Clone.gameObject);
        }
        pairs.Clear();
        sourceCount = -1;
        if (body != null)
        {
            Kill(body.gameObject);
            body = null;
        }
    }

    private void LateUpdate()
    {
        FitBody();
        Quaternion lean = PixelArtView.Lean;
        Vector3 feet = transform.position + Vector3.down * feetDrop;

        for (int i = pairs.Count - 1; i >= 0; i--)
        {
            Pair p = pairs[i];
            if (p.Source == null || p.Clone == null)
            {
                if (p.Clone != null) Kill(p.Clone.gameObject);
                pairs.RemoveAt(i);
                continue;
            }

            if (p.CloneFilter.sharedMesh != p.SourceFilter.sharedMesh)
                p.CloneFilter.sharedMesh = p.SourceFilter.sharedMesh;

            p.Source.GetSharedMaterials(materials);
            p.Clone.SetSharedMaterials(materials);

            // 보이는 몸만 발을 축으로 카메라 반대쪽으로 젖힌다(그림자를 지는 원본은 그대로).
            p.Clone.enabled = p.Source.enabled && p.Source.gameObject.activeInHierarchy;
            Place(p.Clone.transform, p.Source.transform, lean, feet);
        }
    }

    /// <summary>복제본 자리 — 원본과 같은 크기 · 자리에서, 발을 축으로 lean만큼 돌린다.</summary>
    private static void Place(Transform clone, Transform source, Quaternion lean, Vector3 feet)
    {
        Vector3 offset = source.position - feet;
        clone.SetPositionAndRotation(feet + lean * offset, lean * source.rotation);
        clone.localScale = source.lossyScale;
    }

    private void FitBody()
    {
        if (body == null)
            return;
        Vector3 s = transform.lossyScale;
        body.localPosition = Vector3.zero;
        body.localRotation = Quaternion.identity;
        body.localScale = new Vector3(1f / Mathf.Max(0.0001f, s.x), 1f / Mathf.Max(0.0001f, s.y), 1f / Mathf.Max(0.0001f, s.z));
    }

    /// <summary>떼어 원래대로 돌린다(복제본 지우고 그림자 되돌림). 지울 때도 부른다.</summary>
    public void Release() => Detach();

    private void OnDestroy() => Detach();

    private static void Kill(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }
}
