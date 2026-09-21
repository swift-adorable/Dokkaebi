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

    /// <summary>
    /// 오브젝트째로 지울 수 없는 개발용 **컴포넌트**. 타입으로 떼어 낸다.
    ///
    /// 왜 따로 두는가 — DebugUIManager는 Canvas 본체에 붙어 있다.
    /// 그 오브젝트에 DebugOnly를 붙이면 UI 캔버스가 통째로 사라진다.
    /// 오브젝트는 남기고 컴포넌트만 떼는 경우가 실제로 있다.
    /// </summary>
    public static readonly System.Type[] StrippedComponents =
    {
        typeof(DebugUIManager),
        typeof(DebugManager)
    };

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        // report가 null이면 에디터에서 플레이 모드로 들어가는 중이다. 건드리지 않는다.
        if (report == null)
            return;

        if (report.summary.options.HasFlag(BuildOptions.Development))
            return;

        GameObject[] roots = scene.GetRootGameObjects();

        // 1) 표시된 오브젝트를 통째로 지운다.
        var doomedObjects = new List<GameObject>();

        foreach (GameObject root in roots)
        {
            foreach (DebugOnly marker in root.GetComponentsInChildren<DebugOnly>(true))
                doomedObjects.Add(marker.gameObject);
        }

        foreach (GameObject target in doomedObjects)
        {
            if (target != null)
                Object.DestroyImmediate(target);
        }

        // 2) 남은 개발용 컴포넌트를 타입으로 떼어 낸다.
        //    1)에서 이미 사라진 것도 있으므로 다시 훑는다.
        var doomedComponents = new List<Component>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (System.Type type in StrippedComponents)
            {
                foreach (Component found in root.GetComponentsInChildren(type, true))
                    doomedComponents.Add(found);
            }
        }

        foreach (Component target in doomedComponents)
        {
            if (target != null)
                Object.DestroyImmediate(target);
        }

        if (doomedObjects.Count > 0 || doomedComponents.Count > 0)
        {
            Debug.Log($"[DebugOnlyStripper] 출시 빌드 — 「{scene.name}」에서 "
                      + $"오브젝트 {doomedObjects.Count}개 · 컴포넌트 {doomedComponents.Count}개를 제거했습니다.");
        }
    }
}
