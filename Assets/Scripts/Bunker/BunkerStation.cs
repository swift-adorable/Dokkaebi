using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 벙커의 한 자리 — 가까이 가면 버튼이 뜨고, 누르면 그 일을 한다. (로드맵 8-J)
///
/// 【덕코프처럼 걸어 다니는 벙커다.】 덕코프의 벙커는 건물을 지어 두고 그 앞으로
/// 걸어가서 쓰는 공간이다 [확인됨]. 메뉴 화면 하나로 두면 빠르지만,
/// 「건물을 지으면 NPC가 입주한다」(Bunker 0절)가 설 자리가 없다.
///
/// 【버튼은 늘 같은 자리에 뜬다】 — 흡수 버튼(AbsorbPrompt)과 같은 이유다.
/// 자리 위에 버튼이 떠오르면 엄지를 매번 옮겨야 한다.
/// </summary>
public class BunkerStation : MonoBehaviour
{
    public enum Kind
    {
        /// <summary>창고 (보관고).</summary>
        Stash = 0,

        /// <summary>잡화 상점.</summary>
        GeneralStore = 1,

        /// <summary>파밍 출발 지점.</summary>
        Departure = 2
    }

    [SerializeField] private Kind kind = Kind.Stash;

    [Tooltip("이 거리 안에 들어오면 버튼이 뜬다 (m).")]
    [Min(0.5f)]
    [SerializeField] private float radius = 2.5f;

    private static readonly List<BunkerStation> all = new();

    public Kind StationKind => kind;

    /// <summary>버튼에 적을 말.</summary>
    public string Label => LabelOf(kind);

    public static string LabelOf(Kind kind)
    {
        switch (kind)
        {
            case Kind.Stash:        return "창고";
            case Kind.GeneralStore: return ShopTable.GeneralStoreName;
            case Kind.Departure:    return "파밍 출발";
            default:                return string.Empty;
        }
    }

    /// <summary>에디터 생성기가 쓴다.</summary>
    public void EditorSetup(Kind stationKind, float stationRadius)
    {
        kind = stationKind;
        radius = stationRadius;
    }

    private void OnEnable()
    {
        all.Add(this);
        StationPromptUI.EnsureInstance();
    }

    private void OnDisable() => all.Remove(this);

    /// <summary>이 위치에서 쓸 수 있는 가장 가까운 자리. 없으면 null.</summary>
    public static BunkerStation NearestTo(Vector3 position)
    {
        BunkerStation best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < all.Count; i++)
        {
            BunkerStation station = all[i];

            if (station == null)
                continue;

            Vector3 offset = station.transform.position - position;
            offset.y = 0f;

            float distance = offset.magnitude;

            if (distance > station.radius || distance >= bestDistance)
                continue;

            best = station;
            bestDistance = distance;
        }

        return best;
    }

    /// <summary>그 일을 한다.</summary>
    public void Use()
    {
        switch (kind)
        {
            case Kind.Stash:
                ExchangeWindowUI.EnsureInstance().OpenStash();
                break;

            case Kind.GeneralStore:
                ExchangeWindowUI.EnsureInstance().OpenShop();
                break;

            case Kind.Departure:
                SceneFlow.Depart();
                break;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
