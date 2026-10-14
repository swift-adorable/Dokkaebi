using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 【iOS 개발 빌드】 (결정 2-126 · 11단계) — 아이폰 시험용 Xcode 프로젝트를 뽑는다.
///   · IL2CPP · 실기(Device SDK) · 가로 고정 · iOS 16 이상 · Development Build
///   · 서명은 「자동」으로 두고 Team은 비워 둔다 — Xcode에서 사용자가 자기 Apple 계정 Team을 고른다
///   · 결과: Builds/iOS/Dokkaebi_dev/ (Unity-iPhone.xcodeproj) + Builds/iOS/last_build.txt(요약)
/// Xcode 빌드 · 서명 · 아이폰 설치는 Mac의 Xcode에서 한다(여기서는 프로젝트까지).
/// iOS Build Support 모듈이 없으면 만들지 않고 이유만 남긴다.
/// </summary>
public static class IOSBuildMenu
{
    private const string Menu = "Dokkaebi/Build/";
    public const string OutputFolder = "Builds/iOS";
    public const string ProjectFolder = OutputFolder + "/Dokkaebi_dev";
    public const string LastReportPath = OutputFolder + "/last_build.txt";

    public static bool ModuleInstalled => BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS);

    [MenuItem(Menu + "iOS 빌드 준비 확인")]
    public static void Check()
    {
        string text = Describe();
        Debug.Log("[iOSBuild] " + text.Replace("\n", " | "));
        Write(LastReportPath, text);
    }

    public static string Describe()
    {
        var named = NamedBuildTarget.iOS;
        var sb = new StringBuilder();
        sb.AppendLine($"iOS 모듈: {(ModuleInstalled ? "있음" : "없음 — Unity Hub › Installs › 6000.4.11f1 › Add modules › iOS Build Support")}");
        sb.AppendLine($"지금 대상: {EditorUserBuildSettings.activeBuildTarget}");
        sb.AppendLine($"스크립트: {PlayerSettings.GetScriptingBackend(named)} · SDK: {PlayerSettings.iOS.sdkVersion} · 최소 iOS {PlayerSettings.iOS.targetOSVersionString}");
        sb.AppendLine($"번들 {PlayerSettings.GetApplicationIdentifier(named)} · 버전 {PlayerSettings.bundleVersion} ({PlayerSettings.iOS.buildNumber})");
        sb.AppendLine($"서명: {(PlayerSettings.iOS.appleEnableAutomaticSigning ? "자동" : "수동")} · Team {(string.IsNullOrEmpty(PlayerSettings.iOS.appleDeveloperTeamID) ? "(비움 — Xcode에서 고름)" : PlayerSettings.iOS.appleDeveloperTeamID)}");
        sb.AppendLine($"화면: {PlayerSettings.defaultInterfaceOrientation} · 전체 화면 {PlayerSettings.iOS.requiresFullScreen} · 상태 막대 숨김 {PlayerSettings.statusBarHidden}");
        sb.AppendLine("씬: " + string.Join(", ", EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => Path.GetFileNameWithoutExtension(s.path))));
        return sb.ToString();
    }

    [MenuItem(Menu + "iOS 개발 빌드 (Xcode 프로젝트)")]
    public static void BuildDevelopmentXcode()
    {
        if (!ModuleInstalled)
        {
            string why = "[iOSBuild] 만들지 못함 — iOS Build Support 모듈이 없습니다.\n" + Describe();
            Debug.LogError(why);
            Write(LastReportPath, why);
            return;
        }

        var named = NamedBuildTarget.iOS;
        PlayerSettings.SetScriptingBackend(named, ScriptingImplementation.IL2CPP);
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        PlayerSettings.iOS.requiresFullScreen = true;
        PlayerSettings.statusBarHidden = true;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        EditorUserBuildSettings.development = true;

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Directory.CreateDirectory(OutputFolder);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = ProjectFolder,
            target = BuildTarget.iOS,
            targetGroup = BuildTargetGroup.iOS,
            options = BuildOptions.Development,   // 같은 폴더에 다시 뽑으면 Unity가 바꿀지 덮을지 알아서 고른다
        };

        Write(LastReportPath, $"빌드 중… {DateTime.Now:HH:mm:ss} 시작\n" + Describe());

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary s = report.summary;

        var sb = new StringBuilder();
        sb.AppendLine($"결과: {s.result} · {s.totalTime.TotalMinutes:F1}분 · 오류 {s.totalErrors} · 경고 {s.totalWarnings}");
        if (s.result == BuildResult.Succeeded)
            sb.AppendLine($"Xcode 프로젝트: {ProjectFolder}/Unity-iPhone.xcodeproj");

        foreach (BuildStep step in report.steps)
            foreach (BuildStepMessage m in step.messages)
                if ((m.type == LogType.Error || m.type == LogType.Exception) && !m.content.Contains("com.gamelovers.mcp-unity"))   // MCP 도구 패키지의 meta 경고는 빌드와 상관없다
                    sb.AppendLine($"  [{step.name}] {m.content.Split('\n')[0]}");

        sb.AppendLine();
        sb.Append(Describe());

        string text = sb.ToString();
        Write(LastReportPath, text);
        Debug.Log("[iOSBuild] " + text);
    }

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
