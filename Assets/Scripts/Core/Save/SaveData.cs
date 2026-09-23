using System;
using System.Collections.Generic;

/// <summary>
/// 디스크에 남는 것. 【죽어도 잃지 않는 것만 담는다.】 (로드맵 8-F)
///
/// 【덕코프의 저장 규칙을 따른다】 — 자동 저장만 있고 수동 저장은 없다.
/// 출격 **전과 후에만** 저장하므로, 출격 중에 끄면 그 출격에서 얻은 것도
/// 잃은 것도 없다(롤백). [커뮤니티 확인 — 개발사 공식 설명은 찾지 못했다]
///
/// 그래서 여기에는 출격 안의 것(가방 · 착용 장비 · 각성 레벨 · 소켓)이 없다.
/// 그것들은 출격이 끝나야 의미가 정해진다 — 추출하면 남고 죽으면 잃는다.
/// 추출이 생기면(9단계) 그때 가방·장비를 담는다.
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
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;

    /// <summary>저장한 시각(UTC, ISO 8601). 사람이 파일을 열었을 때 읽으려고 둔다.</summary>
    public string savedAtUtc = string.Empty;

    // ── 계정 ──────────────────────────────────────────────────────────

    public int accountLevel = 1;
    public int credits;

    /// <summary>역행 계열을 발견했는가. 4장 관측실에서 켜진다.</summary>
    public bool discoveredRegression;

    // ── 성장 ──────────────────────────────────────────────────────────

    /// <summary>배운 패시브의 id.</summary>
    public List<string> learnedPassives = new();

    /// <summary>도감에 오른 스킬의 id. 드롭 풀이 여기서 정해진다.</summary>
    public List<string> codex = new();

    /// <summary>
    /// 끼고 있는 각인 둘. 【각인은 죽어도 남는 유일한 장착품이다.】
    /// 비어 있는 칸은 id를 비워 둔다 — 자리 순서(A · B)가 의미를 갖는다.
    /// </summary>
    public List<SavedItem> imprints = new();
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
