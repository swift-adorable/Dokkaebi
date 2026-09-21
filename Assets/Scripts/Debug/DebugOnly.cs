using UnityEngine;

/// <summary>
/// 【이 오브젝트는 출시 빌드에 존재해서는 안 된다】는 표시.
///
/// 왜 컴포넌트 하나를 따로 두는가 —
/// `#if UNITY_EDITOR || DEVELOPMENT_BUILD`는 **코드만** 감싼다.
/// 씬에 배치된 GameObject는 그 전처리기와 아무 상관이 없어서
/// 그대로 빌드에 실려 화면에 보이고 눌린다.
/// 실제로 DEBUG 버튼이 출시 빌드에 노출될 뻔했다.
///
/// 막는 층을 셋 둔다. 하나가 뚫려도 나머지가 잡는다 —
///   1) 빌드 시 씬에서 제거    DebugOnlyStripper (Editor, IProcessSceneWithReport)
///      데이터 자체가 빌드에 들어가지 않는다. 가장 확실하다.
///   2) 실행 시 자폭           아래 Awake
///      프리팹에서 생성되는 등 1)을 우회한 경로를 잡는다.
///   3) 계약 테스트            DebugOnlyTests
///      개발용 오브젝트에 이 표시가 빠지면 테스트가 먼저 막는다.
///
/// 붙이는 대상 — DEBUG 버튼, 디버그 패널, 치트 입력을 받는 오브젝트.
/// </summary>
[DisallowMultipleComponent]
public class DebugOnly : MonoBehaviour
{
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
    private void Awake()
    {
        // 숨기는 것이 아니라 지운다. SetActive(false)는 누군가 다시 켤 수 있다.
        Destroy(gameObject);
    }
#endif
}
