using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 출시 빌드를 만들 때 씬에서 개발 전용 오브젝트를 **제거한다**.
///
/// 빌드에 들어가는 씬 사본을 대상으로 동작하므로 **원본 씬은 건드리지 않는다.**
/// 에디터에서는 DEBUG 버튼이 그대로 보이고, 출시 빌드에만 없다.
///
/// 런타임 자폭(DebugOnly.Awake)만으로는 부족한 이유 —
/// 그 방식은 오브젝트가 빌드 데이터에 **들어간 다음** 지우는 것이라
/// 한 프레임 깜빡일 수 있고, 에셋 번들을 뜯으면 흔적이 남는다.
/// 애초에 넣지 않는 편이 낫다.
/// </summary>
public class DebugOnlyStripper : IProcessSceneWithReport
{
    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        // report가 null이면 에디터에서 플레이 모드로 들어가는 중이다. 건드리지 않는다.
        if (report == null)
            return;

        if (report.summary.options.HasFlag(BuildOptions.Development))
            return;

        var doomed = new List<GameObject>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (DebugOnly marker in root.GetComponentsInChildren<DebugOnly>(true))
                doomed.Add(marker.gameObject);
        }

        foreach (GameObject target in doomed)
        {
            if (target != null)
                Object.DestroyImmediate(target);
        }

        if (doomed.Count > 0)
            Debug.Log($"[DebugOnlyStripper] 출시 빌드 — 「{scene.name}」에서 개발 전용 {doomed.Count}개를 제거했습니다.");
    }
}
