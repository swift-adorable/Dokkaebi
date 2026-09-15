/// <summary>
/// 프로젝트 전역 상수 모음.
///
/// 주의: 게임 밸런스/감각에 관한 튜닝 수치(이동속도, 연사속도, 대시거리 등)는
/// 여기에 두지 않는다. 그런 값은 각 컴포넌트의 [SerializeField]로 노출하여
/// 기획자가 Inspector에서 직접 조정할 수 있게 한다.
///
/// 이 클래스에는 코드 전반에서 공유되는 '식별자'만 둔다.
/// </summary>
public static class GameConstants
{
    /// <summary>Tag 문자열. 오타로 인한 런타임 버그를 막기 위해 상수화한다.</summary>
    public static class Tags
    {
        public const string Player = "Player";
        public const string Enemy = "Enemy";
    }

    /// <summary>Scene 이름.</summary>
    public static class Scenes
    {
        public const string SampleScene = "SampleScene";
    }

    // TBD — 추후 결정
    // Layers: Player / Enemy / Bullet / Corpse 레이어 분리 시 여기에 추가
    // (레이어 분리는 로드맵 2단계 Object Pooling 작업과 함께 진행 예정)
}
