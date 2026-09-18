using UnityEngine;

/// <summary>
/// 전리품 표의 한 줄. 「무엇이 · 얼마나 · 얼마나 자주」.
///
/// 확률(%)이 아니라 가중치(weight)를 쓰는 이유 —
/// 항목을 하나 추가할 때마다 모든 확률을 다시 계산해 합을 100으로 맞추는 작업이
/// 사라진다. 기획자가 한 줄만 고치면 된다.
/// </summary>
[System.Serializable]
public struct LootEntry
{
    [Tooltip("나올 아이템. 비어 있으면 「아무것도 안 나옴」 줄이 된다.")]
    public ItemDefinition item;

    [Tooltip("뽑힐 가중치. 클수록 자주 나온다. 0 이하면 나오지 않는다.")]
    [Min(0)]
    public int weight;

    [Tooltip("최소 개수.")]
    [Min(1)]
    public int minCount;

    [Tooltip("최대 개수.")]
    [Min(1)]
    public int maxCount;

    public LootEntry(ItemDefinition item, int weight, int minCount = 1, int maxCount = 1)
    {
        this.item = item;
        this.weight = weight;
        this.minCount = minCount;
        this.maxCount = maxCount;
    }

    /// <summary>실제로 뽑힐 수 있는 줄인지.</summary>
    public bool IsValid => weight > 0;

    /// <summary>아무것도 나오지 않는 줄인지. 「빈손」 확률을 표로 표현할 때 쓴다.</summary>
    public bool IsEmptyRoll => item == null;

    public int ClampedMin => Mathf.Max(1, minCount);
    public int ClampedMax => Mathf.Max(ClampedMin, maxCount);
}
