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
        ("start", -66f, -28f, 9.5f),
        ("bamboo", -52f, 8f, 9.5f),
        ("stream", -24f, 0f, 9.5f),
        ("mill", -6f, 18f, 9.5f),
        ("oldtree", 18f, 0f, 9.5f),
        ("bush", 48f, 0f, 9.5f),
    };

    [MenuItem("Dokkaebi/Debug/장 맵 찍기 (열린 씬)")]
    public static void Shoot()
    {
        ChapterMap map = ZoneMapTable.Of(ZoneMapTable.ChapterOfScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name));
        System.IO.Directory.CreateDirectory("Temp/screens");
        foreach (var (name, x, z, half) in Ch0)
        {
            float h = half;
            int w = 1600, hp = 900;
            if (h <= 0f)
            {
                h = (map != null ? map.HalfSize.y : 45f) * Mathf.Cos(ArtCatalog.Pitch * Mathf.Deg2Rad) + 4f;
                w = 2400; hp = 1300;
            }
            Shot($"Temp/screens/map_{name}.png", new Vector3(x, 0f, z), h, w, hp);
        }
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
