using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전리품 표 에셋. 적 원형·상자 종류마다 하나씩 만든다.
/// (docs/Blob_Hunting_System.md 드랍 / docs/Blob_Equipment_System.md)
/// </summary>
[CreateAssetMenu(fileName = "LootTable", menuName = "Blob/Loot Table")]
public class LootTable : ScriptableObject
{
    [Header("Rolls")]
    [Tooltip("최소 추첨 횟수.")]
    [Min(0)]
    [SerializeField] private int minRolls = 1;

    [Tooltip("최대 추첨 횟수.")]
    [Min(0)]
    [SerializeField] private int maxRolls = 3;

    [Header("Entries")]
    [Tooltip("item을 비워 두면 「아무것도 안 나옴」 줄이 된다. 빈손 확률을 여기서 조절한다.")]
    [SerializeField] private List<LootEntry> entries = new();

    public IReadOnlyList<LootEntry> Entries => entries;

    public int MinRolls => Mathf.Max(0, minRolls);
    public int MaxRolls => Mathf.Max(MinRolls, maxRolls);

    /// <summary>
    /// 이 표로 컨테이너를 채운다. luckMultiplier는 적 등급 배수다.
    /// 추첨 횟수에만 곱한다 — 등급이 높으면 더 많이 나오되 표 자체는 같다.
    /// </summary>
    public int Fill(LootContainer into, System.Random random, int luckMultiplier = 1)
    {
        random ??= new System.Random();

        int rolls = MinRolls == MaxRolls ? MinRolls : random.Next(MinRolls, MaxRolls + 1);

        rolls *= Mathf.Max(1, luckMultiplier);

        return LootRoller.Roll(entries, rolls, random, into);
    }

#if UNITY_EDITOR
    public void EditorSetEntries(List<LootEntry> source)
    {
        entries.Clear();

        if (source != null)
            entries.AddRange(source);
    }
#endif
}
