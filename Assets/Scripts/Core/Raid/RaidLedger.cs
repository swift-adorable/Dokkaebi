using System.Collections.Generic;

/// <summary>
/// 【이번 판 결산】 (결정 2-142 · 덕코프 모드 「이번 라운드 수익 · 시간 · 창고 가치」 133만)
/// 출발할 때 가방 + 장비 가치를 적어 두고, 철수할 때 다시 재서 차이를 낸다. 걸린 시간 · 쓰러뜨린 수도 함께.
/// 가치는 BaseValue(상세 창의 엽전 값)로 센다 — 판매가(절반)가 아니라 「가져온 물건의 값」.
/// 쓴 탄 · 약 · 닳은 내구도는 빠지므로 덕코프 모드처럼 「실제로 늘어난 몫」이 나온다. 마이너스일 수도 있다.
/// 죽으면 결산 없이 지운다(사망 화면이 따로 있다).
/// MonoBehaviour 의존이 없는 순수 클래스 — EditMode 테스트 대상.
/// </summary>
public static class RaidLedger
{
    public readonly struct Summary
    {
        public readonly int StartValue;
        public readonly int EndValue;
        public readonly float Seconds;
        public readonly int Kills;
        public readonly string ZoneId;

        public Summary(int startValue, int endValue, float seconds, int kills, string zoneId)
        {
            StartValue = startValue;
            EndValue = endValue;
            Seconds = seconds;
            Kills = kills;
            ZoneId = zoneId;
        }

        public int Gain => EndValue - StartValue;
    }

    private static bool running;
    private static int startValue;
    private static float startTime;
    private static int kills;
    private static string zone;

    /// <summary>철수해서 생긴 결산 — 소굴에 돌아가 한 번 보여 주고 지운다.</summary>
    public static Summary? Pending { get; private set; }

    public static bool Running => running;

    public static void Begin(int value, float now, string zoneId)
    {
        running = true;
        startValue = value;
        startTime = now;
        kills = 0;
        zone = zoneId;
        Pending = null;
    }

    public static void CountKill()
    {
        if (running)
            kills++;
    }

    /// <summary>철수 — 결산을 남긴다. 출발 기록이 없으면(에디터에서 구역 씬부터 연 경우) 시간만 0으로 남긴다.</summary>
    public static Summary End(int value, float now)
    {
        var summary = new Summary(running ? startValue : value, value, running ? now - startTime : 0f, kills, zone);
        running = false;
        Pending = summary;
        return summary;
    }

    /// <summary>죽었다 — 결산 없이 지운다.</summary>
    public static void Abort()
    {
        running = false;
        Pending = null;
    }

    public static void ClearPending() => Pending = null;

    /// <summary>묶음들의 가치 합 (BaseValue × 개수).</summary>
    public static int ValueOf(IEnumerable<ItemStack> stacks)
    {
        int total = 0;
        if (stacks == null)
            return total;
        foreach (ItemStack s in stacks)
            if (s?.Definition != null && !s.IsEmpty)
                total += s.Definition.BaseValue * s.Count;
        return total;
    }

    /// <summary>「12분 34초」.</summary>
    public static string FormatTime(float seconds)
    {
        int s = UnityEngine.Mathf.Max(0, UnityEngine.Mathf.RoundToInt(seconds));
        return s >= 60 ? $"{s / 60}분 {s % 60:00}초" : $"{s}초";
    }
}
