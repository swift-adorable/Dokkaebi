using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 벙커의 한 자리 — 가까이 가면 버튼이 뜨고, 누르면 그 일을 한다. (로드맵 8-J · 8-K)
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
    /// <summary>
    /// 자리의 종류. 【값을 바꾸지 않는다】 — 벙커 씬에 숫자로 저장돼 있다.
    /// </summary>
    public enum Kind
    {
        /// <summary>창고 (보관고).</summary>
        Stash = 0,

        /// <summary>잡화 상점.</summary>
        GeneralStore = 1,

        /// <summary>파밍 출발 지점.</summary>
        Departure = 2,

        /// <summary>건물 설계도 테이블 — 건설.</summary>
        Blueprint = 3,

        // 4 · 5는 옛 무기 상점 · 방어구 상점이다 — 대장간으로 합쳤다 (결정 2-52). 다시 쓰지 않는다.

        /// <summary>대장간 — 빚쟁이.</summary>
        Smithy = 6,

        /// <summary>약탕간 — 참봉.</summary>
        Apothecary = 7,

        /// <summary>부뚜막 — 잡화 가게 옆에 저절로 생기는 요리 자리 (결정 2-71).</summary>
        Cooking = 8,

        /// <summary>고목 뿌리 샘 — 물병을 떠 간다 (결정 2-73). 처음부터 있다.</summary>
        Spring = 9,

        /// <summary>【임시】 열매 나무 — 구역에서 산열매를 딴다 (결정 2-76). 소굴이 아니라 구역에 선다.</summary>
        BerryTree = 10,

        /// <summary>0-1 길가의 젖은 봇짐 — 한 번 연다 (결정 2-79). 구역에 선다.</summary>
        StoryBundle = 11,

        /// <summary>역행 비밀 통로 — 이전 장 맵으로 (결정 2-88). 구역에 선다.</summary>
        SecretPassage = 12,

        /// <summary>쓰러진 자리 — 「회수 계약」으로 남은 잃은 것 (결정 2-93). 구역에 선다.</summary>
        FallenStash = 13,

        /// <summary>작업대 — 단계 · 바치기 · 만들기 (결정 2-95).</summary>
        Workbench = 14,

        /// <summary>여는 것이 없다 (작업대 — 제작은 다음 단계).</summary>
        None = 99
    }

    [SerializeField] private Kind kind = Kind.Stash;

    [Tooltip("이 거리 안에 들어오면 버튼이 뜬다 (m).")]
    [Min(0.5f)]
    [SerializeField] private float radius = 2.5f;

    private static readonly List<BunkerStation> all = new();

    public Kind StationKind => kind;

    /// <summary>버튼에 적을 말.</summary>
    public string Label => kind == Kind.SecretPassage && TryGetComponent(out SecretPassage passage)
        ? passage.Prompt
        : LabelOf(kind);

    public static string LabelOf(Kind kind)
    {
        switch (kind)
        {
            case Kind.Stash:        return "창고";
            case Kind.GeneralStore: return ShopTable.GeneralStoreName;
            case Kind.Smithy:       return ShopTable.SmithyName;
            case Kind.Apothecary:   return ShopTable.ApothecaryName;
            case Kind.Cooking:      return CookingTable.HearthName;
            case Kind.Spring:       return $"{SpringTable.Name} ({SpringManager.Remaining})";
            case Kind.BerryTree:    return BerryTree.Name;
            case Kind.StoryBundle:  return ChapterZeroTable.BundleName;
            case Kind.FallenStash:  return FallenStash.Name;
            case Kind.Workbench:    return "작업대";
            case Kind.Departure:    return "파밍 출발";
            case Kind.Blueprint:    return "건설";
            default:                return string.Empty;
        }
    }

    /// <summary>에디터 생성기와 건물 세우기(BunkerBuildings)가 쓴다.</summary>
    public void Setup(Kind stationKind, float stationRadius)
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

            if (station == null || station.kind == Kind.None)
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
                ExchangeWindowUI.EnsureInstance().OpenShop(ShopKind.General);
                break;

            case Kind.Smithy:
                ExchangeWindowUI.EnsureInstance().OpenShop(ShopKind.Smithy);
                break;

            case Kind.Apothecary:
                ExchangeWindowUI.EnsureInstance().OpenShop(ShopKind.Apothecary);
                break;

            case Kind.Cooking:
                CookingUI.Open();
                break;

            case Kind.BerryTree:
                if (TryGetComponent(out BerryTree tree))
                    tree.Harvest();
                break;

            case Kind.StoryBundle:
                if (TryGetComponent(out StoryBundle bundle))
                    bundle.Open();
                break;

            case Kind.Workbench:
                WorkbenchUI.Open();
                break;

            case Kind.FallenStash:
                if (TryGetComponent(out FallenStash fallen))
                    fallen.Open();
                break;

            case Kind.SecretPassage:
                if (TryGetComponent(out SecretPassage passage))
                    passage.Enter();
                break;

            case Kind.Spring:
            {
                SpringError error = SpringManager.Draw();
                StoryDialogueUI.ShowBanner(error == SpringError.None
                    ? $"물병에 물을 채웠다. (샘에 남은 물 {SpringManager.Remaining})"
                    : SpringState.Explain(error), 2f);
                break;
            }

            case Kind.Blueprint:
                BuildingScreenUI.Open();
                break;

            case Kind.Departure:
                DepartureUI.Open();
                break;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
