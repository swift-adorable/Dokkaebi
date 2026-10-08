using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몸을 녹이는 자리 — 화로 · 모닥불 (결정 2-61 · 추천안 v3 「화로 · 모닥불 곁에서 녹는다」).
/// 붙은 물체 둘레 Radius 안이면 추위가 초당 5씩 녹는다. 5장 담장 화로 등에 붙인다 [자리는 아트 때].
/// </summary>
public class WarmthSource : MonoBehaviour
{
    public const float Radius = 3f;

    private static readonly List<WarmthSource> all = new();

    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);

    public static bool IsNear(Vector3 position)
    {
        for (int i = 0; i < all.Count; i++)
        {
            Vector3 d = all[i].transform.position - position;
            d.y = 0f;
            if (d.sqrMagnitude <= Radius * Radius)
                return true;
        }

        return false;
    }
}
