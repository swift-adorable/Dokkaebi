using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 【아트 미리보기】 (결정 2-127) — 플레이 없이 지금 장 씬을 게임 카메라(2.5D 정사영)로 몇 자리에서 찍는다.
/// 자리: 구역마다 가운데 + 맵 가장자리 한 곳. Temp/screens/art_Ch{n}_{k}.png (1920×1080)
/// </summary>
public static class ArtPreview
{
    [MenuItem("Dokkaebi/Art/지금 장 미리보기 찍기")]
    public static void Capture()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!ZoneMapTable.IsMapScene(scene.name))
            return;
        ChapterMap map = ZoneMapTable.Of(ZoneMapTable.ChapterOfScene(scene.name));

        Directory.CreateDirectory("Temp/screens");
        var go = new GameObject("ArtPreviewCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = ArtCatalog.OrthoSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.14f, 0.1f);
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 120f;
        int ui = LayerMask.NameToLayer("UI");
        cam.cullingMask = ~(ui >= 0 ? 1 << ui : 0) & ~(1 << MapDiorama.Layer);
        go.transform.rotation = Quaternion.Euler(ArtCatalog.Pitch, 0f, 0f);

        var rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
        cam.targetTexture = rt;
        var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);

        int k = 0;
        void Shot(Vector3 focus)
        {
            go.transform.position = focus - go.transform.forward * 30f;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes($"Temp/screens/art_Ch{map.Chapter}_{k++}.png", tex.EncodeToPNG());
        }

        foreach (ZoneArea a in map.Areas)
            Shot(new Vector3(a.Bounds.center.x, 0f, a.Bounds.center.y));
        Shot(new Vector3(0f, 0f, -map.HalfSize.y + 4f));

        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(go);
        Debug.Log($"[ArtPreview] {k}장 찍음");
    }
}
