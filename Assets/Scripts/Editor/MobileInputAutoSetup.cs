using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 모바일 입력 UI를 최초 1회 자동으로 구성하고 씬까지 저장한다.
///
/// 수동으로 메뉴를 실행하고 Mode를 바꾸고 씬을 저장하는 3단계를 모두 대신한다.
/// EditorPrefs 키로 1회만 실행되며, 이후에는 메뉴(Tools > Blob)로 직접 조작한다.
/// </summary>
[InitializeOnLoad]
public static class MobileInputAutoSetup
{
    private const string DoneKey = "Blob.MobileInputAutoSetup.v1";
    private const int MaxRetry = 30;

    private static int retryCount;

    static MobileInputAutoSetup()
    {
        EditorApplication.delayCall += TryRun;
    }

    [MenuItem("Tools/Blob/3. Reset Auto Setup (자동 설정 다시 실행)", false, 12)]
    private static void ResetFlag()
    {
        EditorPrefs.DeleteKey(DoneKey);
        retryCount = 0;

        EditorUtility.DisplayDialog(
            "Blob 자동 설정",
            "자동 설정 플래그를 초기화했습니다.\n다음 스크립트 재컴파일 시 다시 실행됩니다.",
            "확인");
    }

    private static void TryRun()
    {
        if (EditorPrefs.GetBool(DoneKey, false))
            return;

        // 컴파일/임포트 중이거나 플레이 모드면 잠시 후 재시도한다.
        if (EditorApplication.isCompiling
            || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (retryCount++ < MaxRetry)
                EditorApplication.delayCall += TryRun;

            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (retryCount++ < MaxRetry)
                EditorApplication.delayCall += TryRun;

            return;
        }

        RunSetup(scene);
    }

    private static void RunSetup(Scene scene)
    {
        bool changed = false;

        // 1) 모바일 입력 UI 생성
        if (Object.FindAnyObjectByType<TouchInputSource>(FindObjectsInactive.Include) == null)
        {
            MobileInputUIBuilder.BuildInternal(true);
            changed = true;

            Debug.Log("[Blob 자동 설정] 모바일 입력 UI(MobileInputCanvas)를 생성했습니다.");
        }

        // 2) 에디터에서 조이스틱이 보이도록 Mode를 ForceTouch로 설정
        if (ApplyForceTouchMode())
        {
            changed = true;

            Debug.Log("[Blob 자동 설정] PlayerInputHandler.Mode를 ForceTouch로 변경했습니다. " +
                      "(키보드/마우스로 테스트하려면 Auto 또는 ForceDesktop으로 되돌리세요)");
        }

        // 3) 씬 저장
        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Blob 자동 설정] 씬을 저장했습니다: {scene.path}");
        }
        else
        {
            Debug.Log("[Blob 자동 설정] 이미 구성되어 있어 변경 사항이 없습니다.");
        }

        EditorPrefs.SetBool(DoneKey, true);

        Debug.Log("[Blob 자동 설정] 완료. 이제 Play를 누르면 화면을 드래그해 조이스틱을 사용할 수 있습니다.");
    }

    private static bool ApplyForceTouchMode()
    {
        var handler = Object.FindAnyObjectByType<PlayerInputHandler>(FindObjectsInactive.Include);

        if (handler == null)
        {
            Debug.LogWarning("[Blob 자동 설정] 씬에서 PlayerInputHandler를 찾지 못했습니다.");
            return false;
        }

        var serialized = new SerializedObject(handler);
        var modeProperty = serialized.FindProperty("mode");

        if (modeProperty == null)
            return false;

        if (modeProperty.enumValueIndex == (int)InputSourceMode.ForceTouch)
            return false;

        modeProperty.enumValueIndex = (int)InputSourceMode.ForceTouch;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(handler);

        return true;
    }
}
