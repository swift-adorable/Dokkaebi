using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 【흐린 뒤 화면】 (결정 2-121) — 덮는 창(전체 지도 · 가게) 뒤에 지금 게임 화면을 흐리게 깐다. 참고 그림의 「뒤 배경 흐림」.
/// 프레임 끝에서 화면을 한 번 찍어 반씩 여러 번 줄인다 — 작게 줄였다가 크게 펴면 흐려진다(셰이더 없이).
/// 창이 떠 있는 동안 게임은 멈춰 있으므로(결정 2-92) 한 번 찍으면 된다. 다 쓰면 Release.
/// </summary>
public static class ScreenBlur
{
    /// <summary>가장 작게 줄이는 한 변(화소) — 작을수록 더 흐리다.</summary>
    public const int TargetHeight = 90;

    /// <summary>
    /// 이번 프레임 끝에서 찍는다. 찍기 전에 창을 켜면 창까지 찍히므로, 부르는 쪽은 done에서 창을 켠다.
    /// </summary>
    public static IEnumerator Capture(Action<RenderTexture> done)
    {
        yield return new WaitForEndOfFrame();

        Texture2D shot = null;
        RenderTexture current = null;

        try
        {
            shot = ScreenCapture.CaptureScreenshotAsTexture();
            int w = Mathf.Max(2, shot.width / 2), h = Mathf.Max(2, shot.height / 2);
            current = Temp(w, h);
            Graphics.Blit(shot, current);

            while (current.height / 2 >= TargetHeight)
            {
                RenderTexture next = Temp(current.width / 2, current.height / 2);
                Graphics.Blit(current, next);
                RenderTexture.ReleaseTemporary(current);
                current = next;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ScreenBlur] 화면을 찍지 못했다 — 흐린 뒤 화면 없이 연다 ({e.Message})");
            if (current != null)
                RenderTexture.ReleaseTemporary(current);
            current = null;
        }
        finally
        {
            if (shot != null)
                UnityEngine.Object.Destroy(shot);
        }

        done?.Invoke(current);
    }

    public static void Release(ref RenderTexture texture)
    {
        if (texture != null)
            RenderTexture.ReleaseTemporary(texture);
        texture = null;
    }

    private static RenderTexture Temp(int w, int h)
    {
        RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Bilinear;
        rt.wrapMode = TextureWrapMode.Clamp;
        return rt;
    }

    /// <summary>가장자리가 번지는 UI 재료 — 없으면(셰이더를 못 찾으면) null, 그러면 그냥 그린다.</summary>
    public static Material SoftEdge(float feather)
    {
        Shader shader = Shader.Find("Dokkaebi/UI/SoftEdge");
        if (shader == null)
            return null;

        var m = new Material(shader) { name = "UISoftEdge (Runtime)" };
        m.SetFloat("_Feather", feather);
        return m;
    }
}
