/// <summary>
/// 착용 슬롯. 무기 1 + 방어 5 + 각인 2 + 화살통 · 탄창 1 = 9칸.
/// (docs/Dokkaebi_Equipment_System.md 1절)
/// </summary>
public enum EquipmentSlot
{
    /// <summary>무기 — 기본 피해 · 발사 간격 · 유효 사거리 · 방어 관통을 정한다.</summary>
    Weapon = 0,

    /// <summary>머리 — 머리 방어도(원거리 피격) + 감각 페널티.</summary>
    Head = 1,

    /// <summary>몸통 — 몸통 방어도(근접 피격) + 가방 칸.</summary>
    Body = 2,

    /// <summary>얼굴 — 속성 저항 · 상태이상 면역 + 시야·감지.</summary>
    Face = 3,

    /// <summary>청각 — 소리 · 위치 파악.</summary>
    Ears = 4,

    /// <summary>가방 — 최대 소지 중량 + 가방 칸.</summary>
    Backpack = 5,

    /// <summary>각인 1 — 철수 실패에도 유실되지 않는다.</summary>
    ImprintA = 6,

    /// <summary>각인 2.</summary>
    ImprintB = 7,

    /// <summary>
    /// 화살통 · 탄창 (결정 2-80) — 무기의 탄이 든다. 무기가 정한 탄 한 종류만, 탄마다 정해진 수까지.
    /// 비면 가방에서 저절로 채운다(채우는 동안 못 쏜다). 무기를 벗으면 가방으로, 쓰러지면 무기와 함께 잃는다.
    /// </summary>
    Ammo = 8
}
