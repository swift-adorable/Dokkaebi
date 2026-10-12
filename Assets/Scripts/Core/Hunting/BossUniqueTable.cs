using System.Collections.Generic;

/// <summary>
/// 【중간 보스의 고유 장비】 (결정 2-109 · 부록 1136 「다시 나와 고유 물건을 떨어뜨린다」).
/// 덕코프의 보스처럼 그 보스가 걸치던 장비를 떨어뜨린다 — 쓰거나 비싸게 팔 수 있다.
/// 이야기에서 처음 만나 쓰러뜨릴 때가 아니라 【다시 나왔을 때】 쓰러뜨리면 반드시 하나.
/// 장비 수치는 Editor/UniqueGearGenerator가 정한다(같은 티어 일반 장비보다 조금 좋고 대가가 하나).
/// </summary>
public static class BossUniqueTable
{
    private static readonly Dictionary<string, string> items = new()
    {
        { "yagwanggwi",   "uniq_yagwanggwi" },
        { "dalgyal",      "uniq_dalgyal" },
        { "eodukssini",   "uniq_eodukssini" },
        { "gangcheori",   "uniq_gangcheori" },
        { "wongwi",       "uniq_wongwi" },
        { "dueoksini",    "uniq_dueoksini" },
        { "duduri",       "uniq_duduri" },
        { "kkeomeoksari", "uniq_kkeomeoksari" },
        { "changgwi",     "uniq_changgwi" },
        { "sangun",       "uniq_sangun" },
        { "samjogo",      "uniq_samjogo" },
    };

    public static IReadOnlyDictionary<string, string> Items => items;

    /// <summary>그 보스의 고유 장비 id. 없으면 null(장 보스).</summary>
    public static string ItemOf(string bossId)
        => bossId != null && items.TryGetValue(bossId, out string id) ? id : null;

    /// <summary>이번에 쓰러뜨리면 고유 장비를 떨어뜨리는가 — 중간 보스가 다시 나왔다.</summary>
    public static bool DropsOn(BossDefinition boss, bool alreadyDefeated)
        => boss != null && !boss.IsChapterBoss && alreadyDefeated && ItemOf(boss.Id) != null;
}
