using System;
using System.Collections.Generic;

/// <summary>
/// 디스크에 남는 것. 【죽어도 잃지 않는 것만 담는다.】 (로드맵 8-F)
///
/// 【덕코프의 저장 규칙을 따른다】 — 자동 저장만 있고 수동 저장은 없다.
/// 파밍 **전과 후에만** 저장하므로, 파밍 중에 끄면 그 파밍에서 얻은 것도
/// 잃은 것도 없다(롤백). [커뮤니티 확인 — 개발사 공식 설명은 찾지 못했다]
///
/// 그래서 여기에는 파밍 안의 것(가방 · 착용 장비 · 소켓에 꽂힌 젬)이 없다.
/// 레벨은 영구라 담는다 (결정 2-33).
/// 그것들은 파밍이 끝나야 의미가 정해진다 — 철수하면 남고 죽으면 잃는다.
/// 철수가 생기면(9단계) 그때 가방·장비를 담는다.
///
/// JsonUtility로 직렬화하므로 필드는 public이고 Dictionary를 쓰지 않는다.
/// </summary>
[Serializable]
public class SaveData
{
    /// <summary>
    /// 이 형식의 판. 필드를 바꾸면 올린다.
    ///
    /// 【더 새 판은 읽지 않는다.】 옛 빌드가 새 세이브를 열어 모르는 필드를
    /// 버린 채 저장하면 진행이 소리 없이 사라진다.
    /// </summary>
    ///   1 — 처음 판. 옛 「계정 레벨」을 JSON 키 accountLevel로 담았다
    ///   2 — 레벨이 하나가 됐다(결정 2-33). 키는 accountLevel 그대로, 경험치를 더했다
    ///   3 — 키 이름을 awakeningLevel로 바꿨다
    ///   4 — 키 이름을 level로 바꿨다(용어 통일). 옛 키 둘은 SaveStore.FromJson이
    ///       읽기 전에 바꿔 준다(Migrate). 1판에는 경험치가 없어 0으로 읽히고,
    ///       1판의 값은 오르는 길이 없던 값이라 그대로 레벨로 쓴다.
    ///   5 — 창고(stash)와 잡화 상점 재고(shop)를 더했다. 4판에는 없어 비어 있는
    ///       창고 · 가득 찬 재고로 읽힌다.
    ///   6 — 재화 키 credits를 gold로 바꿨다(덕코프 이름). 옛 키는 Migrate가 바꾼다.
    ///   7 — 벙커가 생겼다. 파밍 전 저장에 【들고 가는 것】(가방 · 착용 장비)을 담는다.
    ///       6판에는 없어 빈 가방 · 맨몸으로 읽힌다.
    ///   8 — 벙커 건물(buildings)과 상점 종류(shop 줄의 shop)를 더했다. 7판에는 없어
    ///       빈 벙커 · 잡화 상점 줄로 읽힌다.
    ///   9 — 이야기 진행(story)을 더했다 (3단계). 8판에는 없어 처음부터로 읽힌다.
    ///  10 — 상인 넷 = 가게 넷 (결정 2-52 · 2-57). 무기 상점 · 방어구 상점(weapon_shop · armour_shop)은
    ///       대장간(smithy)으로, 상점 줄의 Weapon · Armour는 Smithy로 읽는다(BuildingManager · ShopManager).
    ///       잡화 상점의 약 줄은 잡화 가게 표에 없어 버려지고 약탕간은 가득 찬 재고로 시작한다.
    ///  11 — 난이도(difficulty)를 더했다 (Normal · Nightmare · Hell — 결정 2-69). 10판에는 없어
    ///       「아직 고르지 않음」으로 읽힌다.
    ///  12 — 고목 뿌리 샘(spring — 남은 물병)를 더했다 (결정 2-73). 11판에는 없어 가득 찬 샘으로 읽힌다.
    ///       들판의 「맑은 물」(water_bottle)은 물병(con_water)로 읽는다(ItemCatalog.Find).
    ///  13 — 구슬 도감(codex)을 없앴다 (결정 2-75). 고르지 않은 구슬이 떨어지고 가방에서 고른다.
    ///       12판의 codex 줄은 읽지 않고 버린다.
    public const int CurrentVersion = 13;

    public int version = CurrentVersion;

    /// <summary>저장한 시각(UTC, ISO 8601). 사람이 파일을 열었을 때 읽으려고 둔다.</summary>
    public string savedAtUtc = string.Empty;

    // ── 계정 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 【레벨】 — 이 게임의 유일한 레벨. (결정 2-33)
    /// JSON 이름을 바꾸지 않는다. 1판 세이브가 그대로 읽혀야 한다.
    /// </summary>
    public int level = 1;

    /// <summary>다음 레벨을 향해 모은 경험치. 1판에는 없어 0으로 읽힌다.</summary>
    public int experience;

    public int gold;

    /// <summary>역행 계열을 발견했는가. 만나는 곳은 미정이다 (Story 9절 TBD 4번).</summary>
    public bool discoveredRegression;

    // ── 성장 ──────────────────────────────────────────────────────────

    /// <summary>배운 패시브의 id.</summary>
    public List<string> learnedPassives = new();

    /// <summary>
    /// 끼고 있는 각인 둘. 【각인은 죽어도 남는 유일한 장착품이다.】
    /// 비어 있는 칸은 id를 비워 둔다 — 자리 순서(A · B)가 의미를 갖는다.
    /// </summary>
    public List<SavedItem> imprints = new();

    // ── 들고 가는 것 ─────────────────────────────────────────────────
    //
    // 【파밍 전에 저장되고, 파밍 중에는 저장되지 않는다.】 그래서 파밍 도중에
    // 끄면 들고 들어간 그대로 돌아온다(롤백). 죽으면 떨어뜨린 뒤 저장되므로
    // 빈 가방이 남는다. 덕코프와 같다.

    /// <summary>가방.</summary>
    public List<SavedItem> bag = new();

    /// <summary>착용 장비 — 각인을 뺀 여섯 자리. 각인은 imprints가 따로 맡는다.</summary>
    public List<SavedEquip> equipment = new();

    /// <summary>든 무기 — 0 = 무기 1, 1 = 무기 2 (결정 2-81). 옛 세이브는 0.</summary>
    public int activeWeapon;

    // ── 벙커 ──────────────────────────────────────────────────────────

    /// <summary>창고. 【죽어도 잃지 않는다】 — 파밍에 들고 가지 않은 것이다.</summary>
    public List<SavedItem> stash = new();

    /// <summary>잡화 상점의 남은 재고. 비어 있으면 가득 찬 것으로 읽는다.</summary>
    public List<SavedStock> shop = new();

    /// <summary>지은 건물과 놓은 자리. 놓지 않은 건물은 placed가 false다.</summary>
    public List<SavedBuilding> buildings = new();

    /// <summary>고목 뿌리 샘에 남은 물병 (결정 2-73). 파밍이 끝나면 다시 찬다.</summary>
    public int spring = SpringTable.PerNight;

    // ── 이야기 ────────────────────────────────────────────────────────

    /// <summary>이야기 진행. 【죽어도 잃지 않는다】 — 쓰러뜨린 보스 · 주운 조각은 남는다.</summary>
    public SavedStory story = new();

    /// <summary>
    /// 난이도 (DifficultyLevel 값 · 결정 2-69). 새로 시작할 때 한 번 고르고 바꿀 수 없다.
    /// -1이면 아직 고르지 않았다 — 판 10 이전 세이브도 -1로 읽혀 다음 소굴에서 고른다.
    /// </summary>
    public int difficulty = DifficultyManager.NotChosen;
}

/// <summary>
/// 이야기 진행 (StoryProgress). 열린 장 · 구역 · 사신패 · 상인은 담지 않는다 —
/// 아래 다섯에서 계산한다.
/// </summary>
[Serializable]
public class SavedStory
{
    public List<string> bosses = new();
    public List<string> pieces = new();
    public List<string> notices = new();
    public List<string> seen = new();
    public List<int> nights = new();
}

/// <summary>벙커 건물 하나.</summary>
[Serializable]
public class SavedBuilding
{
    public string id = string.Empty;
    public bool placed;
    public float x;
    public float z;

    /// <summary>90도 단위 회전 (0~3).</summary>
    public int turns;
}

/// <summary>착용 장비 한 자리. 자리 번호는 EquipmentSlot의 값이다.</summary>
[Serializable]
public class SavedEquip
{
    public int slot;
    public SavedItem item = new();
}

/// <summary>상점 한 줄의 남은 재고.</summary>
[Serializable]
public class SavedStock
{
    /// <summary>어느 상점인가 (ShopKind 이름). 비어 있으면 잡화 상점 — 판 5~7.</summary>
    public string shop = string.Empty;

    public string id = string.Empty;
    public int remaining;
}

/// <summary>저장된 아이템 한 줄. 정의는 id로만 가리킨다.</summary>
[Serializable]
public class SavedItem
{
    public string id = string.Empty;
    public int count = 1;

    /// <summary>-1이면 「최대치로」다. 내구도가 없는 아이템도 -1로 둔다.</summary>
    public int durability = -1;

    public bool IsEmpty => string.IsNullOrEmpty(id) || count <= 0;
}
