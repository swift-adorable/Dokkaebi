using System.Diagnostics;
using Debug = UnityEngine.Debug;

/// <summary>
/// 조건부 컴파일 로거.
///
/// [Conditional] 특성은 호출 지점 자체를 컴파일에서 제거한다.
/// 따라서 릴리즈 빌드에서는 GameLogger.Log($"XP : {value}") 같은 코드의
/// 문자열 보간으로 인한 힙 할당(GC Alloc)조차 발생하지 않는다.
///
/// if (Debug.isDebugBuild) 방식과의 결정적 차이가 바로 이 점이다.
/// (그 방식은 인자 평가가 먼저 일어나 문자열이 실제로 생성된다.)
/// </summary>
public static class GameLogger
{
    private const string SymbolEditor = "UNITY_EDITOR";
    private const string SymbolDevelopmentBuild = "DEVELOPMENT_BUILD";

    [Conditional(SymbolEditor), Conditional(SymbolDevelopmentBuild)]
    public static void Log(string message)
    {
        Debug.Log(message);
    }

    [Conditional(SymbolEditor), Conditional(SymbolDevelopmentBuild)]
    public static void Log(string message, UnityEngine.Object context)
    {
        Debug.Log(message, context);
    }

    [Conditional(SymbolEditor), Conditional(SymbolDevelopmentBuild)]
    public static void Warning(string message)
    {
        Debug.LogWarning(message);
    }

    /// <summary>에러는 릴리즈 빌드에서도 유지한다. (실기기 크래시 원인 추적용)</summary>
    public static void Error(string message)
    {
        Debug.LogError(message);
    }

    public static void Error(string message, UnityEngine.Object context)
    {
        Debug.LogError(message, context);
    }
}
