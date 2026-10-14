using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【아트 꾸러미】 (결정 2-127) — 장 꾸미기가 쓰는 부품 메시 · 재질을 이름으로 찾게 모은 것. Resources/ArtKit.asset.
/// 에디터의 「장 꾸미기」가 채운다. 열쇠: 로우폴리 빛깔 이름(Wood · Tile …) 또는 「S:그림 경로」.
/// </summary>
public class ArtKit : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public string Key;
        public Material Material;
        public float Aspect;
    }

    public List<Entry> Entries = new();
    public List<Mesh> Meshes = new();

    private Dictionary<string, Entry> byKey;
    private Dictionary<string, Mesh> byMesh;

    public static ArtKit Load() => Resources.Load<ArtKit>("ArtKit");

    private void Index()
    {
        if (byKey != null)
            return;
        byKey = new Dictionary<string, Entry>();
        foreach (Entry e in Entries)
            if (!string.IsNullOrEmpty(e.Key) && e.Material != null)
                byKey[e.Key] = e;
        byMesh = new Dictionary<string, Mesh>();
        foreach (Mesh m in Meshes)
            if (m != null)
                byMesh[m.name] = m;
    }

    public void Reindex() => byKey = null;

    public bool Has(string key) { Index(); return byKey.ContainsKey(key); }
    public Material Mat(string key) { Index(); return byKey.TryGetValue(key, out Entry e) ? e.Material : null; }
    public float Aspect(string key) { Index(); return byKey.TryGetValue(key, out Entry e) && e.Aspect > 0f ? e.Aspect : 1f; }
    public Mesh Mesh(string name) { Index(); return byMesh.TryGetValue(name, out Mesh m) ? m : null; }
    public Mesh Crown(int v) => Mesh($"Crown{Mathf.Abs(v) % 4}");
    public Mesh RockMesh(int v) => Mesh($"Rock{Mathf.Abs(v) % 3}");
}
