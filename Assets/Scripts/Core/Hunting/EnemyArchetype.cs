/// <summary>
/// 적 유형 9종. (docs/Blob_Hunting_System.md 1절)
///
/// 【유형은 고정 수치를 갖는다.】
/// 장(Stage)이 올라간다고 같은 유형의 체력에 배율을 곱하지 않는다.
/// 장이 바꾸는 것은 【구성비】다 — 5장에서 스캐브가 사라지고 보안기가 늘어난다.
///
/// 근거 — 덕코프는 상위 구역에서 "넝마꾼의 체력"이 아니라
/// "용병이 철갑탄을 들기 시작한다"가 바뀐다. [확인됨]
///
/// 수치는 EnemyArchetypeTable에 있다. 이 enum은 이름만 정한다.
/// </summary>
public enum EnemyArchetype
{
    /// <summary>스캐브 — 자아 없는 야생 슬라임. 근접 돌진.</summary>
    Scav = 0,

    /// <summary>압착기 — 폐기물 처리 로봇. 느리고 단단하다.</summary>
    Crusher = 1,

    /// <summary>자전체 — 전기를 머금은 슬라임. 기본 원거리.</summary>
    Dynamo = 2,

    /// <summary>잠복체 — 수중 매복형. 발소리가 없다.</summary>
    Lurker = 3,

    /// <summary>화공체 — 산성 용액 개체. 8방향 장판.</summary>
    Chemic = 4,

    /// <summary>검체 — 주사기 실험체. 원거리 연사.</summary>
    Specimen = 5,

    /// <summary>정착체 — 형질 고정에 성공한 이전 개체. 스토리의 핵심.</summary>
    Settled = 6,

    /// <summary>보안기 — 무장 드론. 한 번 물면 끝까지 쫓는다.</summary>
    Sentry = 7,

    /// <summary>데이터체 — 서버에 남은 의식 잔해. 비물질.</summary>
    Wraith = 8
}
