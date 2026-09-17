/// <summary>
/// 아이템의 종류. 장비·인자·전리품이 같은 인벤토리를 공유하므로
/// "어디에 낄 수 있는가"를 이 축으로 구분한다.
/// (docs/Blob_Equipment_System.md / Blob_Skill_System.md 11-1절)
/// </summary>
public enum ItemKind
{
    /// <summary>재료 · 전리품. 팔거나 제작에 쓴다.</summary>
    Material = 0,

    /// <summary>무기. 기본 피해 · 발사 간격 · 유효 사거리 · 방어 관통을 정한다.</summary>
    Weapon = 1,

    /// <summary>방어구. 머리 / 몸통 / 얼굴 / 청각 슬롯에 들어간다.</summary>
    Armour = 2,

    /// <summary>가방. 최대 소지 중량과 적재 공간을 준다.</summary>
    Backpack = 3,

    /// <summary>각인. 2슬롯. 추출에 실패해도 잃지 않는다.</summary>
    Imprint = 4,

    /// <summary>인자 — 실물이 된 스킬. 소켓에 끼운다.</summary>
    SkillGem = 5,

    /// <summary>소모품. 회복 · 주사 · 음식.</summary>
    Consumable = 6,

    /// <summary>열쇠 · 인증. 구역 게이트를 연다.</summary>
    Key = 7
}
