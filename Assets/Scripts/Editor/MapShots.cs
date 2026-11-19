using UnityEditor;
using UnityEngine;

/// <summary>
/// 【개발용】 열린 장 씬을 게임 카메라 각도(정사영 · Pitch)로 찍는다 — 플레이 없이 꾸밈을 훑어보려고 (2026-11-17).
///   · 맵 전체 한 장 + 게임 화면 크기로 몇 군데 (Temp/screens/map_&lt;이름&gt;.png)
/// </summary>
public static class MapShots
{
    private static readonly (string name, float x, float z, float half)[] Ch0 =
    {
        ("all", 0f, 0f, 0f),
        ("start", -128f, -66f, 9.5f),        // 헛간 · 헛간 마을 (결정 2-150 새 0장)
        ("bamboo", -112f, 30f, 12f),         // 첫 대숲 (가장자리 번짐이 보이게 조금 넓게)
        ("barn", -138f, -80f, 9.5f),         // 헛간 (결정 2-150 도트 그림)
        ("stream", -44f, 0f, 9.5f),          // 냇물 · 큰길
        ("mill", -2f, 54f, 9.5f),            // 방앗간 마을 · 못
        ("oldtree", 40f, 0f, 9.5f),          // 고목 · 금줄 두 갈래
        ("bush", 112f, -38f, 9.5f),          // 동쪽 야영지
    };

    [MenuItem("Dokkaebi/Debug/장 맵 찍기 (열린 씬)")]
    public static void Shoot()
    {
        ChapterMap map = ZoneMapTable.Of(ZoneMapTable.ChapterOfScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name));
        System.IO.Directory.CreateDirectory("Temp/screens");
        // 열린 집 미리보기 (결정 2-149 — 판이 열릴 때 런타임이 세우는 것을 사진에도)
        var preview = new System.Collections.Generic.List<GameObject>();
        LockedSite.PreviewOnly = true;
        LockedSite.PreviewNoRoof = true;   // 게임에서는 가까이 가면 지붕이 걷힌다 — 사진은 걷힌 모습으로
        if (map != null)
            foreach (ZoneArea a in map.Areas)
                foreach (LockSite s in LockSiteTable.OpenHouses(map, a.ZoneId))
                {
                    LockedSite site = LockedSite.Create(s);
                    preview.Add(site.gameObject);
                }
        LockedSite.PreviewOnly = false;
        LockedSite.PreviewNoRoof = false;
        foreach (var (name, x, z, half) in Ch0)
        {
            float h = half;
            int w = 1600, hp = 900;
            if (h <= 0f)
            {
                w = 2400; hp = 1300;
                // 맵 전체가 들어오게 — 세로(기울어 짧아 보임)와 가로 중 큰 쪽에 맞춘다 (결정 2-150: 0장이 340×230m)
                float hy = (map != null ? map.HalfSize.y : 45f) * Mathf.Cos(ArtCatalog.Pitch * Mathf.Deg2Rad);
                float hx = (map != null ? map.HalfSize.x : 80f) / (w / (float)hp);
                h = Mathf.Max(hy, hx) + 4f;
            }
            Shot($"Temp/screens/map_{name}.png", new Vector3(x, 0f, z), h, w, hp);
        }
        // 집마다 가까이 한 장 (결정 2-151 — 3D 한옥 짜임 확인)
        for (int i = 0; i < preview.Count; i++)
        {
            LockedSite site = preview[i].GetComponent<LockedSite>();
            string variant = "";
            foreach (Transform ch in preview[i].transform) if (ch.name.StartsWith("House3D_")) variant = ch.name.Substring(8);
            // 집터가 커져(v7) 틀을 집터 크기에 맞춘다 — 파일 이름 끝 _o 는 카메라 반높이(같은 축척 비교용)
            float ortho = Mathf.Max(9f, Mathf.Max(site.Site.Size.x / (1600f / 900f), site.Site.Size.y * Mathf.Sin(ArtCatalog.Pitch * Mathf.Deg2Rad)) * 0.5f + 2f);
            Shot($"Temp/screens/house_{i}_{site.Site.Art}_{variant}_o{ortho:0.0}.png", preview[i].transform.position + new Vector3(0f, 0f, -1.5f), ortho, 1600, 900);
        }
        foreach (GameObject g in preview)
            Object.DestroyImmediate(g);
        Debug.Log("[MapShots] Temp/screens/map_*.png");
    }

    public static void Shot(string path, Vector3 center, float orthoHalf, int w, int h)
    {
        var go = new GameObject("MapShotCamera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        Camera main = Camera.main;
        if (main != null)
            cam.CopyFrom(main);
        cam.orthographic = true;
        cam.orthographicSize = orthoHalf;
        cam.transform.rotation = Quaternion.Euler(ArtCatalog.Pitch, 0f, 0f);
        cam.transform.position = center - cam.transform.forward * 80f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 300f;
        var rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(go);
    }
}
