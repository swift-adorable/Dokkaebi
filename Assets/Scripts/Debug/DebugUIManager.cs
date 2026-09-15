using UnityEngine;

/// <summary>
/// 개발용 디버그 패널 토글. UI 버튼 OnClick에 TogglePanel()을 연결해 사용한다.
/// </summary>
public class DebugUIManager : MonoBehaviour
{
    [SerializeField] private GameObject debugPanel;

    public void TogglePanel()
    {
        if (debugPanel == null)
        {
            GameLogger.Warning("[DebugUIManager] debugPanel이 할당되지 않았습니다.");
            return;
        }

        debugPanel.SetActive(!debugPanel.activeSelf);
    }
}
