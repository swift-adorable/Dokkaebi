using System.Collections.Generic;

/// <summary>
/// 【열쇠고리 — 등록한 열쇠】 (결정 2-143 · 덕코프 「열쇠 등록」)
/// 소굴에서 열쇠를 열쇠고리에 걸면 열쇠는 사라지고 그 문은 영구히 열린다(들고 다니지 않아도 된다 · 쓰러져도 남는다).
/// 덕코프는 작업대를 올려 등록기를 만든다 — 우리는 작업대가 지어져 있으면 가방의 열쇠 메뉴에서 「열쇠고리에 걸기」.
/// 등록 안 한 열쇠는 쓸 때마다 닳는다(UsesPerKey번 쓰면 부서진다).
/// 세이브 판 24 `registeredKeys`. MonoBehaviour 의존이 없는 순수 클래스.
/// </summary>
public static class KeyRing
{
    /// <summary>등록 안 한 열쇠를 쓸 수 있는 횟수 [임시값] — 열쇠의 최대 내구도와 같다.</summary>
    public const int UsesPerKey = 3;

    private static readonly HashSet<string> registered = new();

    public static bool IsRegistered(string keyId) => !string.IsNullOrEmpty(keyId) && registered.Contains(keyId);

    /// <summary>건다. 처음 건 것이면 true.</summary>
    public static bool Register(string keyId) => !string.IsNullOrEmpty(keyId) && registered.Add(keyId);

    public static int Count => registered.Count;

    public static IEnumerable<string> All => registered;

    public static void Restore(IEnumerable<string> ids)
    {
        registered.Clear();
        if (ids == null)
            return;
        foreach (string id in ids)
            if (!string.IsNullOrEmpty(id))
                registered.Add(id);
    }

    public static List<string> Capture() => new(registered);

    public static void Reset() => registered.Clear();

    /// <summary>이 아이템이 문 열쇠인가 (잠긴 곳 표에 있는 id).</summary>
    public static bool IsDoorKey(ItemDefinition definition)
        => definition != null && definition.Kind == ItemKind.Key && definition.Id != null && definition.Id.StartsWith("key_");
}
