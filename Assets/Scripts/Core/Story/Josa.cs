/// <summary>
/// 한국어 조사 — 받침에 따라 이/가 · 을/를 · 은/는 · 과/와 · 으로/로를 고른다.
/// 이름을 끼워 넣는 글(「현무패를 얻었다」)이 어색하지 않게 한다.
/// </summary>
public static class Josa
{
    private static bool HasFinal(string word, out bool rieul)
    {
        rieul = false;

        if (string.IsNullOrEmpty(word))
            return false;

        char last = word[word.Length - 1];

        if (last < '가' || last > '힣')
            return false;

        int final = (last - '가') % 28;
        rieul = final == 8;
        return final != 0;
    }

    public static string IGa(string word) => word + (HasFinal(word, out _) ? "이" : "가");
    public static string EulReul(string word) => word + (HasFinal(word, out _) ? "을" : "를");
    public static string EunNeun(string word) => word + (HasFinal(word, out _) ? "은" : "는");
    public static string GwaWa(string word) => word + (HasFinal(word, out _) ? "과" : "와");

    public static string EuroRo(string word)
        => word + (HasFinal(word, out bool rieul) && !rieul ? "으로" : "로");
}
