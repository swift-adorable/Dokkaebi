/// <summary>
/// 지도에 이어진 패시브 셋 (결정 2-93 · Audit A9) — 길목 표시 · 전리품 표시 · 쓰러진 자리 되찾기.
/// 패시브 관리자가 없으면(테스트 · 씬 없음) 모두 꺼져 있다.
/// </summary>
public static class MapPassives
{
    /// <summary>「돌아갈 길목」 — 찾지 않아도 길목이 지도 · 미니맵 · 화살표에 보인다.</summary>
    public static bool ExtractMark => Has(PassiveEffectType.ExtractMark);

    /// <summary>「옛 지도」 — 봇짐 · 열매 나무 · 내가 쓰러뜨린 적의 시체가 지도 · 미니맵에 보인다.</summary>
    public static bool MapLoot => Has(PassiveEffectType.MapLoot);

    /// <summary>「회수 계약」 — 쓰러진 자리에 잃은 것이 남는다.</summary>
    public static bool CorpseRecovery => Has(PassiveEffectType.CorpseRecovery);

    private static bool Has(PassiveEffectType effect)
        => PassiveManager.HasInstance && PassiveManager.Instance.HasUnlock(effect);
}
