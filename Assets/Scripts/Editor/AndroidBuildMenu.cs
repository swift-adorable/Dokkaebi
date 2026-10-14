using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 【Android 개발 빌드】 (결정 2-125 · 11단계) — 휴대폰에 바로 까는 APK를 만든다.
///   · IL2CPP · ARM64 · 가로 고정 · Development Build(검증 패널 · 위 가운데 fps 표시 · 프로파일러 연결 가능)
///   · 서명은 Unity 기본 디버그 키 — 출시용(AAB · 우리 서명 키)은 나중에 따로
///   · 결과: Builds/Android/Dokkaebi_dev_{날짜시각}.apk + 같은 이름 .txt(요약)
/// Android Build Support 모듈이 없으면 만들지 않고 이유만 남긴다.
/// </summary>
public static class AndroidBuildMenu
{
    private const string Menu = "Dokkaebi/Build/";
    public const string OutputFolder = "Builds/Android";
    public const string LastReportPath = OutputFolder + "/last_build.txt";

    public static bool ModuleInstalled => BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);

    [MenuItem(Menu + "Android 빌드 준비 확인")]
    public static void Check()
    {
        string text = Describe();
        Debug.Log("[AndroidBuild] " + text.Replace("\n", " | "));
        Write(LastReportPath, text);
    }

    public static string Describe()
    {
        var named = NamedBuildTarget.Android;
        var sb = new StringBuilder();
        sb.AppendLine($"Android 모듈: {(ModuleInstalled ? "있음" : "없음 — Unity Hub › Installs › 6000.4.11f1 › Add modules › Android Build Support (OpenJDK · SDK & NDK 포함)")}");
        sb.AppendLine($"지금 대상: {EditorUserBuildSettings.activeBuildTarget}");
        sb.AppendLine($"스크립트: {PlayerSettings.GetScriptingBackend(named)} · 구조: {PlayerSettings.Android.targetArchitectures}");
        sb.AppendLine($"SDK 최소 {PlayerSettings.Android.minSdkVersion} · 목표 {PlayerSettings.Android.targetSdkVersion} · 패키지 {PlayerSettings.GetApplicationIdentifier(named)} · 버전 {PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode})");
        sb.AppendLine($"화면: {PlayerSettings.defaultInterfaceOrientation}");
        sb.AppendLine("씬: " + string.Join(", ", EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => Path.GetFileNameWithoutExtension(s.path))));
        return sb.ToString();
    }

    [MenuItem(Menu + "Android 개발 빌드 (APK)")]
    public static void BuildDevelopmentApk()
    {
        if (!ModuleInstalled)
        {
            string why = "[AndroidBuild] 만들지 못함 — Android Build Support 모듈이 없습니다.\n" + Describe();
            Debug.LogError(why);
            Write(LastReportPath, why);
            return;
        }

        var named = NamedBuildTarget.Android;
        PlayerSettings.SetScriptingBackend(named, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.development = true;

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Directory.CreateDirectory(OutputFolder);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        string apk = $"{OutputFolder}/Dokkaebi_dev_{stamp}.apk";

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = apk,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.Development,
        };

        Write(LastReportPath, $"빌드 중… {DateTime.Now:HH:mm:ss} 시작\n" + Describe());

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary s = report.summary;

        var sb = new StringBuilder();
        sb.AppendLine($"결과: {s.result} · {s.totalTime.TotalMinutes:F1}분 · 오류 {s.totalErrors} · 경고 {s.totalWarnings}");
        if (s.result == BuildResult.Succeeded)
            sb.AppendLine($"파일: {apk} · {new FileInfo(apk).Length / (1024f * 1024f):F1}MB");   // summary.totalSize는 압축 전 합이라 쓰지 않는다

        foreach (BuildStep step in report.steps)
            foreach (BuildStepMessage m in step.messages)
                if ((m.type == LogType.Error || m.type == LogType.Exception) && !m.content.Contains("com.gamelovers.mcp-unity"))   // MCP 도구 패키지의 meta 경고는 빌드와 상관없다
                    sb.AppendLine($"  [{step.name}] {m.content.Split('\n')[0]}");

        sb.AppendLine();
        sb.Append(Describe());

        string text = sb.ToString();
        Write(LastReportPath, text);
        if (s.result == BuildResult.Succeeded)
            Write(Path.ChangeExtension(apk, ".txt"), text);
        Debug.Log("[AndroidBuild] " + text);
    }

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
