using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 【장 꾸밈 굽기】 (v8 · 사용자 「소굴 → 0장 넘어갈 때 느려」) — ChapterArt 가 씬을 열 때마다 만들던 꾸밈(나무 · 소품 카드 수천 장 ·
/// 낮은 다각 덩어리 · 소품 아틀라스)을 애셋으로 저장해 씬에 붙인다. 씬을 열면 저장된 메시만 읽는다.
///   Assets/Art/Generated/Chapter{n}_Art.asset (메시 묶음) · WildAtlas.png / WildAtlas.mat (아틀라스)
/// </summary>
public static class ChapterArtBake
{
    public static void Bake(Scene scene, int chapter)
    {
        ChapterArt art = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            if ((art = root.GetComponentInChildren<ChapterArt>(true)) != null) break;
        if (art == null) { Debug.LogWarning($"[ChapterArtBake] {scene.name} 에 ChapterArt 가 없다"); return; }

        // 옛 구운 것 치우고 새로 만든다
        for (int i = art.transform.childCount - 1; i >= 0; i--)
            if (art.transform.GetChild(i).name.StartsWith("Baked_")) Object.DestroyImmediate(art.transform.GetChild(i).gameObject);
        art.Baked = false;
        art.Rebuild();

        // 아틀라스 · 재질 — 모든 장이 같이 쓴다
        Material atlasMat = null;
        var (atlas, runtimeMat) = PixelDress.AtlasForBake();
        if (atlas != null)
        {
            const string png = "Assets/Art/Generated/WildAtlas.png", matPath = "Assets/Art/Generated/WildAtlas.mat";
            System.IO.File.WriteAllBytes(png, atlas.EncodeToPNG());
            AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(png);
            imp.textureType = TextureImporterType.Default;
            imp.filterMode = FilterMode.Point;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.maxTextureSize = 4096;
            imp.textureCompression = TextureImporterCompression.Uncompressed;   // 도트 · 투명 가장자리가 압축에 뭉개지지 않게
            imp.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
            atlasMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (atlasMat == null) { atlasMat = new Material(runtimeMat); AssetDatabase.CreateAsset(atlasMat, matPath); }
            else { atlasMat.shader = runtimeMat.shader; atlasMat.CopyPropertiesFromMaterial(runtimeMat); atlasMat.renderQueue = runtimeMat.renderQueue; }
            atlasMat.SetTexture("_BaseMap", tex);
            atlasMat.SetTexture("_MainTex", tex);
            EditorUtility.SetDirty(atlasMat);
        }

        // 메시 → 한 애셋 파일
        string meshPath = $"Assets/Art/Generated/Chapter{chapter}_Art.asset";
        AssetDatabase.DeleteAsset(meshPath);
        Mesh container = null;
        var made = new List<GameObject>();
        int n = 0;
        for (int i = art.transform.childCount - 1; i >= 0; i--)
        {
            Transform c = art.transform.GetChild(i);
            if (!c.name.StartsWith("~") || !c.TryGetComponent(out MeshFilter mf) || mf.sharedMesh == null) continue;
            var r = c.GetComponent<MeshRenderer>();
            Mesh m = Object.Instantiate(mf.sharedMesh);
            m.name = c.name.TrimStart('~');
            m.hideFlags = HideFlags.None;
            if (container == null) { AssetDatabase.CreateAsset(m, meshPath); container = m; }
            else AssetDatabase.AddObjectToAsset(m, meshPath);
            var go = new GameObject("Baked_" + m.name);
            go.transform.SetParent(art.transform, false);
            go.transform.localPosition = c.localPosition; go.transform.localRotation = c.localRotation; go.transform.localScale = c.localScale;
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var nr = go.AddComponent<MeshRenderer>();
            Material mat = r.sharedMaterial;
            if (mat != null && !AssetDatabase.Contains(mat)) mat = atlasMat != null ? atlasMat : mat;
            nr.sharedMaterial = mat;
            nr.shadowCastingMode = r.shadowCastingMode;
            nr.receiveShadows = r.receiveShadows;
            made.Add(go); n++;
        }
        // 만든 「~」 조각은 치운다 (구운 것만 남김)
        for (int i = art.transform.childCount - 1; i >= 0; i--)
            if (art.transform.GetChild(i).name.StartsWith("~")) Object.DestroyImmediate(art.transform.GetChild(i).gameObject);
        art.Baked = true;
        EditorUtility.SetDirty(art);

        // 나무 밑동 막이 (v8 · 사용자 「갈 수 있는 곳과 갈 수 없는 곳의 차이를 잘 모르겠다 · 특히 나무 사이」) — 덕코프처럼 나무 하나하나가 막는다.
        // 막힌 덩어리 안의 것은 덩어리가 이미 막으니 건너뛴다. 맵 뿌리(Map_Ch) 아래에 두어 길찾기 굽기가 함께 피한다.
        ChapterMap map = ZoneMapTable.Of(chapter);
        GameObject mapRoot = null;
        foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == $"Map_Ch{chapter}") mapRoot = root;
        if (mapRoot != null && map != null)
        {
            Transform old = mapRoot.transform.Find("TreeTrunks");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var trunks = new GameObject("TreeTrunks").transform;
            trunks.SetParent(mapRoot.transform, false);
            // 밑동은 팔각 기둥 — 32m 칸마다 메시 하나로 합쳐 메시 충돌체 하나 (낱개 3천 개는 씬을 5MB 로 불려 다시 느려졌다)
            var cells = new Dictionary<(int, int), (List<Vector3> v, List<int> tri)>();
            int t = 0;
            foreach (var (sprite, pos, ppm) in PixelDress.LastCards)
            {
                if (!PixelDress.Blocks(sprite) || map.IsBlocked(pos, 0f)) continue;
                float r = Mathf.Clamp(PixelDress.WidthOf(sprite, ppm) * 0.28f, 0.45f, 1.4f);
                var key = (Mathf.FloorToInt(pos.x / 32f), Mathf.FloorToInt(pos.z / 32f));
                if (!cells.TryGetValue(key, out var g)) cells[key] = g = (new List<Vector3>(), new List<int>());
                int b0 = g.v.Count;
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI / 4f;
                    Vector3 d = new(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    g.v.Add(new Vector3(pos.x, 0f, pos.z) + d);
                    g.v.Add(new Vector3(pos.x, 3f, pos.z) + d);
                }
                for (int k = 0; k < 8; k++)
                {
                    int i0 = b0 + k * 2, i1 = b0 + ((k + 1) % 8) * 2;
                    g.tri.AddRange(new[] { i0, i0 + 1, i1 + 1, i0, i1 + 1, i1, i0, i1 + 1, i0 + 1, i0, i1, i1 + 1 });   // 양면
                    g.tri.AddRange(new[] { b0 + 1, i1 + 1, i0 + 1 });   // 뚜껑
                }
                t++;
            }
            foreach (var kv in cells)
            {
                var m = new Mesh { name = $"Trunks_{kv.Key.Item1}_{kv.Key.Item2}", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                m.SetVertices(kv.Value.v); m.SetTriangles(kv.Value.tri, 0); m.RecalculateBounds(); m.RecalculateNormals();
                AssetDatabase.AddObjectToAsset(m, meshPath);
                var go = new GameObject(m.name) { layer = GameLayers.Obstacle, isStatic = true };
                go.transform.SetParent(trunks, false);
                go.AddComponent<MeshCollider>().sharedMesh = m;
            }
            ZoneMapBaker.RebakeNavMesh(scene, map);
            Debug.Log($"[ChapterArtBake] 나무 밑동 막이 {t}개 · 길찾기 다시 구움");
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[ChapterArtBake] {scene.name} 꾸밈 {n}덩어리를 {meshPath} 에 구움");
    }
}
