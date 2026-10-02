/// <summary>
/// 적 유형 9종. (docs/Dokkaebi_Hunting_System.md 1절)
///
/// 【유형은 고정 수치를 갖는다.】
/// 장(Stage)이 올라간다고 같은 유형의 체력에 배율을 곱하지 않는다.
/// 장이 바꾸는 것은 【구성비】다 — 5장에서 잡귀가 사라지고 순라귀가 늘어난다.
///
/// 근거 — 덕코프는 상위 구역에서 "넝마꾼의 체력"이 아니라
/// "용병이 철갑탄을 들기 시작한다"가 바뀐다. [확인됨]
///
/// 수치는 EnemyArchetypeTable에 있다. 이 enum은 이름만 정한다.
/// </summary>
public enum EnemyArchetype
{
    /// <summary>잡귀 — 제 이야기를 잃은 것. 근접 돌진. (9종 모두 잡귀의 갈래 — 결정 2-45)</summary>
    Scav = 0,

    /// <summary>절굿공이귀 — 버려진 헌 물건에서 난 잡귀. 느리고 단단하다.</summary>
    Crusher = 1,

    /// <summary>번개귀 — 번개를 머금은 잡귀. 기본 원거리.</summary>
    Dynamo = 2,

    /// <summary>수귀 — 물귀신. 물속에서 기다린다. 발소리가 없다.</summary>
    Lurker = 3,

    /// <summary>왕지네 — 독을 뿌리는 지네. 8방향 장판.</summary>
    Chemic = 4,

    /// <summary>침귀 — 약방골의 침을 쏘는 잡귀. 원거리 연사.</summary>
    Specimen = 5,

    /// <summary>허깨비 — 없는데 있는 것처럼 보이는 것. 끌어당기고 숨는다.</summary>
    Settled = 6,

    /// <summary>순라귀 — 밤을 도는 순라의 잡귀. 한 번 물면 끝까지 쫓는다.</summary>
    Sentry = 7,

    /// <summary>무주귀 — 제사를 받지 못한 넋. 비물질.</summary>
    Wraith = 8
}
