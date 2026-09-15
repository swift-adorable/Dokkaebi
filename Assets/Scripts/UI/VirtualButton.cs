using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 터치 전용 가상 버튼.
///
/// UnityEngine.UI.Button은 '클릭 완료(Down+Up)' 시점에 이벤트를 주므로
/// 대시처럼 즉각 반응이 필요한 액션에는 지연이 생긴다.
/// 이 컴포넌트는 PointerDown 즉시 눌림을 기록한다.
/// </summary>
public class VirtualButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    /// <summary>버튼을 누르고 있는 상태인지.</summary>
    public bool IsHeld { get; private set; }

    private bool pressedThisFrame;

    public void OnPointerDown(PointerEventData eventData)
    {
        IsHeld = true;
        pressedThisFrame = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        IsHeld = false;
    }

    private void OnDisable()
    {
        IsHeld = false;
        pressedThisFrame = false;
    }

    /// <summary>
    /// 눌림 이벤트를 1회 소비한다. 프레임당 한 번만 호출해야 한다.
    /// (PlayerInputHandler가 단독으로 호출하도록 설계되어 있다.)
    /// </summary>
    public bool ConsumePressed()
    {
        bool wasPressed = pressedThisFrame;
        pressedThisFrame = false;
        return wasPressed;
    }
}
