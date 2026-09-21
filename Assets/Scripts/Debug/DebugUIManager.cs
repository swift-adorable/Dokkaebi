using UnityEngine;

/// <summary>
/// 옛 디버그 패널 토글. 씬의 「DEBUG」 버튼이 쓰던 것이다.
///
/// 【지금은 쓰지 않는다】
/// 개발용 진입점을 화면 좌상단의 「검증」 버튼(PlaytestPanelUI)으로 일원화했다.
/// 그쪽은 파일 전체가 `UNITY_EDITOR || DEVELOPMENT_BUILD`로 감싸여 있어
/// **출시 빌드에 컴파일조차 되지 않는다.** 씬에 놓인 버튼은 그 보장을 받지 못해
/// 실제로 출시 빌드에 노출될 뻔했다.
///
/// 이 컴포넌트는 옛 씬 참조가 끊기지 않도록 남겨 둔다.
/// DebugOnlyStripper가 출시 빌드에서 타입째 떼어 낸다.
/// </summary>
public class DebugUIManager : MonoBehaviour
{
    [Tooltip("옛 디버그 스크롤 패널. 지금은 쓰지 않는다.")]
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
