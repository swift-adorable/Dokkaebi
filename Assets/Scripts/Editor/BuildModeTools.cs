using UnityEditor;
using UnityEngine;

/// <summary>
/// 빌드 모드 확인·전환. (로드맵 6-P)
///
/// 【왜 메뉴로 두는가】
/// Unity 6에서 창 이름이 「Build Settings」에서 「Build Profiles」로 바뀌었고,
/// Development Build 체크박스의 위치도 프로필 구조 안으로 들어갔다.
/// 출시 전 마지막에 확인해야 하는 값이 찾기 어려운 곳에 있으면,
/// 「껐다고 생각했는데 켜져 있었다」가 실제로 일어난다.
///
/// 이 값이 정하는 것 —
///   DEVELOPMENT_BUILD 심볼 → PlaytestPanelUI · DebugOnly 자폭 · DebugManager 치트
///   BuildOptions.Development → DebugOnlyStripper가 씬을 훑을지 말지
/// 즉 【DEBUG 버튼이 실기에 보이는지】가 여기서 갈린다.
/// </summary>
public static class BuildModeTools
{
    private const string Menu = "Blob/Build/";

    /// <summary>현재 설정을 문장으로. 대화상자를 띄우지 않는다.</summary>
    public static string Describe()
    {
        bool dev = EditorUserBuildSettings.development;

        return $"대상 플랫폼 : {EditorUserBuildSettings.activeBuildTarget}\n"
               + $"Development Build : {(dev ? "켜짐 (ON)" : "꺼짐 (OFF)")}\n"
               + $"스크립트 디버깅 : {(EditorUserBuildSettings.allowDebugging ? "켜짐" : "꺼짐")}\n\n"
               + (dev
                   ? "→ 개발 빌드입니다. DEBUG 버튼과 검증 패널이 실기에 보입니다."
                   : "→ 출시 빌드입니다. DebugOnlyStripper가 DEBUG 버튼과 디버그 패널을 제거하고,\n"
                     + "   DebugUIManager · DebugManager 컴포넌트도 떼어 냅니다.");
    }

    /// <summary>
    /// 사람이 부르는 확인. 대화상자를 띄운다 —
    /// 출시 직전에 확인하는 값이라 콘솔 한 줄로 흘려보내면 안 된다.
    /// </summary>
    [MenuItem(Menu + "현재 빌드 설정 확인")]
    public static void Report()
    {
        string message = Describe();

        Debug.Log("[Build] " + message.Replace("\n", " | "));

        EditorUtility.DisplayDialog("현재 빌드 설정", message, "확인");
    }

    [MenuItem(Menu + "개발 빌드 켜기 (검증 도구 포함)")]
    public static void EnableDevelopment() => SetDevelopment(true);

    [MenuItem(Menu + "출시 빌드로 전환 (개발 요소 제거)")]
    public static void DisableDevelopment() => SetDevelopment(false);

    private static void SetDevelopment(bool value)
    {
        EditorUserBuildSettings.development = value;

        // 개발 빌드가 아니면 스크립트 디버깅과 프로파일러 자동 연결도 의미가 없다.
        // 켜진 채로 두면 출시 빌드에 디버거 대기 코드가 남는다.
        if (!value)
        {
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
        }

        // 여기서는 대화상자를 띄우지 않는다. 자동화(MCP)가 부를 수 있고,
        // 모달이 뜨면 에디터가 응답을 멈춘다.
        Debug.Log($"[Build] Development Build → {(value ? "켜짐" : "꺼짐")}\n" + Describe());
    }
}
