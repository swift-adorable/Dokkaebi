/// <summary>
/// 투사체가 충돌했을 때 취할 수 있는 행동.
///
/// 열거형의 '선언 순서가 곧 우선순위'다. 값이 작을수록 먼저 판정된다.
/// 한 번의 충돌에는 단 하나의 행동만 발동한다. 이 배타성이
/// "여러 변이를 겹칠수록 무조건 강해지는" 수치 인플레를 구조적으로 막는다.
/// </summary>
public enum ProjectileBehaviourType
{
    /// <summary>아무 행동도 하지 않는다. 투사체는 소멸한다.</summary>
    None = 0,

    /// <summary>유도 투사체로 쪼개진다. 가장 우선한다.</summary>
    Split = 1,

    /// <summary>관통한다. 궤도를 유지하며 계속 나아간다.</summary>
    Pierce = 2,

    /// <summary>좌우로 분열한다.</summary>
    Fork = 3,

    /// <summary>근처의 다른 적으로 재유도된다.</summary>
    Chain = 4,

    /// <summary>발사 지점으로 되돌아간다. 가장 나중에 판정된다.</summary>
    Return = 5
}
