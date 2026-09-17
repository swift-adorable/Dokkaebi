using UnityEngine;

/// <summary>
/// 지금 열려 있는 슬롯의 수. 각성 레벨 하나로 전부 결정된다.
/// </summary>
public struct SocketCapacity
{
    /// <summary>쓸 수 있는 Core 슬롯 수 (0~2).</summary>
    public int CoreSlots;

    /// <summary>1번 Core에 열린 소켓 수 (0~3).</summary>
    public int SocketsInCore0;

    /// <summary>2번 Core에 열린 소켓 수 (0~3).</summary>
    public int SocketsInCore1;

    /// <summary>발동(Meta) 슬롯 수 (0~2).</summary>
    public int MetaSlots;

    /// <summary>전령 슬롯 수 (0~1).</summary>
    public int HeraldSlots;

    /// <summary>지정한 Core에 열린 소켓 수. 범위를 벗어나면 0.</summary>
    public int SocketsIn(int coreIndex)
    {
        return coreIndex == 0 ? SocketsInCore0
             : coreIndex == 1 ? SocketsInCore1
             : 0;
    }

    public int TotalSockets => SocketsInCore0 + SocketsInCore1;

    /// <summary>인자를 끼울 수 있는 자리의 총 개수. UI 상단 표시에 쓴다.</summary>
    public int TotalSlots => CoreSlots + TotalSockets + MetaSlots + HeraldSlots;
}

/// <summary>
/// 각성 레벨 → 개방된 슬롯. (docs/Blob_Skill_System.md 12-1절)
///
/// 레벨업의 보상이 「스킬 선택」에서 「소켓 개방」으로 바뀌었다.
/// 선택창은 뜨지 않는다. 자리가 하나 열릴 뿐이고, 무엇을 끼울지는
/// 그때까지 무엇을 주웠는지가 정한다.
///
/// MonoBehaviour 의존이 없는 순수 정적 클래스다. EditMode 테스트 대상.
/// </summary>
public static class SocketUnlockTable
{
    /// <summary>Core 동시 보유 상한.</summary>
    public const int MaxCores = 2;

    /// <summary>Core 1개당 소켓 수.</summary>
    public const int SocketsPerCore = 3;

    /// <summary>발동(Meta) 동시 장착 상한.</summary>
    public const int MaxMetas = 2;

    /// <summary>전령 동시 장착 상한.</summary>
    public const int MaxHeralds = 1;

    /// <summary>2번째 Core 슬롯이 열리는 레벨. (기존 확정 사항 유지)</summary>
    public const int SecondCoreUnlockLevel = 7;

    /// <summary>모든 슬롯이 열리는 레벨. 이 이상은 더 열 것이 없다.</summary>
    public const int FullyOpenLevel = 15;

    /// <summary>
    /// 12-1절 표를 그대로 옮긴 것. 행 = { 레벨, Core, 소켓0, 소켓1, 발동, 전령 }.
    ///
    /// 표를 코드에 두는 이유 — 문서와의 대조가 diff 한 번으로 끝난다.
    /// 값을 바꾸려면 문서와 이 표를 같이 고쳐야 하고, 계약 테스트가 둘의 어긋남을 잡는다.
    /// </summary>
    private static readonly int[,] Rows =
    {
        //  Lv   Core  소켓0  소켓1  발동  전령
        {    1,     1,     1,     0,    0,    0 },
        {    3,     1,     2,     0,    0,    0 },
        {    5,     1,     3,     0,    0,    0 },
        {    7,     2,     3,     1,    0,    0 },
        {    9,     2,     3,     2,    0,    0 },
        {   11,     2,     3,     3,    1,    0 },
        {   13,     2,     3,     3,    2,    0 },
        {   15,     2,     3,     3,    2,    1 }
    };

    private const int ColLevel = 0;
    private const int ColCore = 1;
    private const int ColSocket0 = 2;
    private const int ColSocket1 = 3;
    private const int ColMeta = 4;
    private const int ColHerald = 5;

    /// <summary>표의 행 수. 테스트가 전 행을 훑는 데 쓴다.</summary>
    public static int RowCount => Rows.GetLength(0);

    /// <summary>index번째 행의 레벨.</summary>
    public static int LevelAt(int index) => Rows[index, ColLevel];

    /// <summary>해당 각성 레벨에서 열려 있는 슬롯 구성.</summary>
    public static SocketCapacity Evaluate(int awakeningLevel)
    {
        // 각성 레벨이 0이 되는 경로는 없어야 하지만, 생겨도 아무것도 끼우지 못하는
        // 상태로 런이 시작되면 안 된다. 여기서 한 번 잘라 둔다.
        int level = ClampLevel(awakeningLevel);

        var capacity = new SocketCapacity();

        for (int i = 0; i < Rows.GetLength(0); i++)
        {
            if (Rows[i, ColLevel] > level)
                break;

            capacity.CoreSlots = Rows[i, ColCore];
            capacity.SocketsInCore0 = Rows[i, ColSocket0];
            capacity.SocketsInCore1 = Rows[i, ColSocket1];
            capacity.MetaSlots = Rows[i, ColMeta];
            capacity.HeraldSlots = Rows[i, ColHerald];
        }

        return capacity;
    }

    /// <summary>
    /// 그 레벨에 새로 열리는 자리가 몇 개인지. 레벨업 연출이 이 값으로 판단한다.
    /// 개방이 없는 레벨(2·4·6…)은 0이다.
    /// </summary>
    public static int SlotsOpenedAt(int awakeningLevel)
    {
        if (awakeningLevel <= 1)
            return awakeningLevel == 1 ? Evaluate(1).TotalSlots : 0;

        return Evaluate(awakeningLevel).TotalSlots - Evaluate(awakeningLevel - 1).TotalSlots;
    }

    /// <summary>
    /// 그 레벨에 열린 것을 한국어로 설명한다. 열린 것이 없으면 빈 문자열.
    /// UI 토스트가 그대로 출력한다.
    /// </summary>
    public static string DescribeUnlock(int awakeningLevel)
    {
        if (awakeningLevel <= 0)
            return string.Empty;

        SocketCapacity now = Evaluate(awakeningLevel);
        SocketCapacity before = awakeningLevel <= 1
            ? new SocketCapacity()
            : Evaluate(awakeningLevel - 1);

        var text = new System.Text.StringBuilder();

        Append(text, "핵심 슬롯", now.CoreSlots - before.CoreSlots);
        Append(text, "소켓", now.TotalSockets - before.TotalSockets);
        Append(text, "발동 슬롯", now.MetaSlots - before.MetaSlots);
        Append(text, "전령 슬롯", now.HeraldSlots - before.HeraldSlots);

        return text.ToString();
    }

    private static void Append(System.Text.StringBuilder text, string label, int delta)
    {
        if (delta <= 0)
            return;

        if (text.Length > 0)
            text.Append(" + ");

        text.Append(label).Append(' ').Append(delta);
    }

    /// <summary>다음으로 무언가 열리는 레벨. 더 없으면 0.</summary>
    public static int NextUnlockLevel(int awakeningLevel)
    {
        for (int i = 0; i < Rows.GetLength(0); i++)
        {
            if (Rows[i, ColLevel] > awakeningLevel)
                return Rows[i, ColLevel];
        }

        return 0;
    }

    /// <summary>레벨을 유효 범위로 자른다.</summary>
    public static int ClampLevel(int awakeningLevel) => Mathf.Max(1, awakeningLevel);
}
