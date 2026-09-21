using UnityEngine;

/// <summary>
/// 개발용 디버그 버튼. 씬의 「DEBUG」 버튼 OnClick에 TogglePanel()이 걸려 있다.
///
/// 검증 도구(PlaytestPanelUI)도 여기서 연다 —
/// 화면에 떠 있는 버튼이 둘이면 게임 화면을 그만큼 더 가린다.
/// 개발용 진입점은 하나로 모은다.
/// </summary>
public class DebugUIManager : MonoBehaviour
{
    [Tooltip("옛 디버그 스크롤 패널(선택). 없어도 검증 패널은 열린다.")]
    [SerializeField] private GameObject debugPanel;

    public void TogglePanel()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        PlaytestPanelUI.EnsureInstance().Toggle();
#endif

        if (debugPanel != null)
            debugPanel.SetActive(!debugPanel.activeSelf);
    }
}
