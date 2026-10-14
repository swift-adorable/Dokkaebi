using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 【아트 방향 시험 찍기】 (결정 2-127) — 플레이 중 같은 자리를 몇 가지 「보는 법」으로 찍어 비교한다. 찍고 나면 원래대로 되돌린다.
///   A 지금 그대로 · B 화면 전체 도트(1/4 해상도 · 점 확대) · C B + 정사영 카메라(바람의나라 · 거상처럼 원근 없음) · D C에서 더 눕힌 각도
/// Temp/screens/look_{글자}.png
/// </summary>
public static class ArtLookTrial
{
    private struct Look
    {
        public string Name;
        public float Scale;
        public bool Point;
        public bool Ortho;
        public float Pitch;
        public float Size;
    }

    private static readonly Look[] Looks =
    {
        new() { Name = "A_now", Scale = -1f },
        new() { Name = "B_pixel", Scale = 0.25f, Point = true },
        new() { Name = "C_pixel_ortho", Scale = 0.25f, Point = true, Ortho = true, Pitch = 55f, Size = 9f },
        new() { Name = "D_pixel_ortho_low", Scale = 0.25f, Point = true, Ortho = true, Pitch = 42f, Size = 9f },
    };

    private static int step;
    private static double next;
    private static bool shot;
    private static float baseScale;
    private static UpscalingFilterSelection baseFilter;
    private static Quaternion baseRot;
    private static bool baseOrtho;
    private static float baseSize;
    private static Camera cam;
    private static Behaviour follow;

    [MenuItem("Dokkaebi/Art/보는 법 시험 찍기 (A·B·C·D)")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || !(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp))
            return;

        cam = Camera.main;
        if (cam == null)
            return;

        baseScale = urp.renderScale;
        baseFilter = urp.upscalingFilter;
        baseRot = cam.transform.rotation;
        baseOrtho = cam.orthographic;
        baseSize = cam.orthographicSize;
        follow = cam.GetComponent<CameraFollow>();

        System.IO.Directory.CreateDirectory("Temp/screens");
        step = 0;
        shot = false;
        next = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || cam == null)
        {
            EditorApplication.update -= Tick;
            return;
        }
        if (EditorApplication.timeSinceStartup < next)
            return;

        var urp = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;

        if (step >= Looks.Length)
        {
            Restore(urp);
            EditorApplication.update -= Tick;
            Debug.Log("[ArtLook] 끝 — 원래대로 되돌렸다");
            return;
        }

        Look look = Looks[step];
        if (!shot)
        {
            Restore(urp);
            if (look.Scale > 0f)
            {
                urp.renderScale = look.Scale;
                urp.upscalingFilter = look.Point ? UpscalingFilterSelection.Point : UpscalingFilterSelection.Auto;
            }
            if (look.Ortho)
            {
                if (follow != null) follow.enabled = false;
                Vector3 focus = FocusPoint();
                cam.orthographic = true;
                cam.orthographicSize = look.Size;
                cam.transform.rotation = Quaternion.Euler(look.Pitch, 0f, 0f);
                cam.transform.position = focus - cam.transform.forward * 40f;
            }
            shot = true;
            next = EditorApplication.timeSinceStartup + 0.6;
            return;
        }

        ScreenCapture.CaptureScreenshot($"Temp/screens/look_{look.Name}.png");
        shot = false;
        step++;
        next = EditorApplication.timeSinceStartup + 0.5;
    }

    private static Vector3 FocusPoint()
    {
        GameObject player = GameObject.FindWithTag("Player");
        return player != null ? player.transform.position : Vector3.zero;
    }

    private static void Restore(UniversalRenderPipelineAsset urp)
    {
        urp.renderScale = baseScale;
        urp.upscalingFilter = baseFilter;
        if (cam != null)
        {
            cam.orthographic = baseOrtho;
            cam.orthographicSize = baseSize;
            cam.transform.rotation = baseRot;
        }
        if (follow != null) follow.enabled = true;
    }
}
