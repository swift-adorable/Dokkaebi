/// <summary>
/// 합성 발사(Composite Fire) 상태 — Core 2개 동시 보유 시의 발사 방식. (확정 기획)
///
/// 【확정 사항】
/// Core 2개는 각각 따로 발사하지 않는다. 한 발에 합쳐진다.
///   전달 계열 Core → 투사체 행동(관통·분열·추적·튕김)을 제공
///   적재 계열 Core → 그 탄이 부여할 상태(점화·중독·동결·감전·출혈)를 제공
///   기폭 계열 Core → 투사체가 아니므로 합성 대상이 아니다. 독립 발동한다.
/// 예: 관통 + 화염 = "관통하면서 점화시키는 탄 1발"
///
/// 【이 구조체의 책임】
/// 적재 계열 Core를 2개 보유했을 때 어느 상태를 부여할지 고른다.
/// 두 상태를 매 발사마다 동시에 걸면 Support 「이차 주입」(대가: 적용 빈도 절반)이
/// 무가치해지므로, 탄마다 번갈아 부여한다. 결과적으로 각 상태는 50% 빈도가 된다.
///
/// 【발사 수가 늘지 않는 이유】
/// 동시 발사(탄 2배)나 교대 발사(상태 빈도 절반)와 달리, 합성은 탄 수를 늘리지 않는다.
/// 모바일에서 투사체 수는 발열·드로우콜과 직결되므로 이 점이 결정적이다.
/// 탄이 늘어나는 것은 「다중 사격」·Fork 계열뿐이다.
///
/// MonoBehaviour에 의존하지 않는 순수 구조체다. EditMode 테스트 대상이다.
/// </summary>
[System.Serializable]
public struct CompositeFireState
{
    private int ailmentCursor;

    /// <summary>현재 커서 위치. 디버그와 테스트용.</summary>
    public int Cursor => ailmentCursor;

    /// <summary>
    /// 이번 발사에 부여할 적재 속성의 인덱스를 돌려주고 커서를 넘긴다.
    /// 적재 계열 Core가 없으면 -1.
    ///
    /// ailmentCount가 런 도중 바뀌어도(2번째 적재 Core 획득) 안전하도록
    /// 매 호출마다 나머지 연산으로 보정한다.
    /// </summary>
    public int NextAilmentIndex(int ailmentCount)
    {
        if (ailmentCount <= 0)
        {
            ailmentCursor = 0;
            return -1;
        }

        int index = ailmentCursor % ailmentCount;

        ailmentCursor = (index + 1) % ailmentCount;

        return index;
    }

    /// <summary>다음 발사가 첫 번째 상태부터 시작하도록 되돌린다. 런 시작 시 호출한다.</summary>
    public void Reset()
    {
        ailmentCursor = 0;
    }
}
