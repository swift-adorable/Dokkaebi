using UnityEditor;
using UnityEngine;

/// <summary>
/// 프로젝트 설정 중 【잊으면 며칠을 헤매는 것들】을 한 번에 맞춘다.
///
/// 설정은 코드가 아니라서 커밋 diff에 잘 드러나지 않고, 누가 언제 왜 바꿨는지도
/// 남지 않는다. 그래서 「이 값이어야 하는 이유」를 여기 적어 두고 메뉴로 실행한다.
/// </summary>
public static class ProjectSettingsCheck
{
    [MenuItem("Dokkaebi/설정/프로젝트 설정 점검")]
    public static void Apply()
    {
        int fixedCount = 0;

        // 【창이 포커스를 잃어도 계속 돈다.】
        //
        // 이것이 꺼져 있으면 에디터 밖을 클릭하는 순간 게임이 멈춘다.
        // 「가끔 게임이 멈추는데 패널을 열었다 닫으면 다시 이어진다」의 정체다 —
        // 패널을 누르느라 Game 뷰에 포커스가 돌아온 것뿐이었다.
        //
        // 감사 문서의 「에디터 포커스 나가면 게임이 멈춤」도 같은 원인이다.
        // 적끼리 싸우는지 확인할 수 없던 이유가 여기 있었다.
        //
        // iOS 빌드에서는 무시된다 — 모바일은 앱이 뒤로 가면 OS가 재운다.
        if (!PlayerSettings.runInBackground)
        {
            PlayerSettings.runInBackground = true;
            fixedCount++;

            Debug.Log("[설정] Run In Background를 켰습니다. "
                      + "에디터 밖을 클릭해도 게임이 멈추지 않습니다.");
        }

        if (fixedCount == 0)
        {
            Debug.Log("[설정] 점검 완료 — 고칠 것이 없습니다.");
            return;
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"[설정] {fixedCount}개 항목을 고쳤습니다.");
    }
}
