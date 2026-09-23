using System;
using UnityEngine;

/// <summary>
/// 계정 축의 영구 성장 — 패시브. 【죽어도 잃지 않는다.】
/// (docs/Blob_Progression_System.md — 각성 / 계정 2축)
///
///   각성 레벨 : 영구. 소켓과 패시브를 연다.   → PlayerStats (결정 2-33)
///
/// 예전에는 「계정 레벨」을 여기서 따로 들고 있었다. 오르는 길이 없어 디버그
/// 버튼으로만 바뀌었다. 이제 레벨은 PlayerStats 하나뿐이고 여기는 읽기만 한다.
///
/// 【패시브는 전투 수치를 주지 않는다.】 휴대 · 수집 · 벙커 해금만 건드린다.
/// 방어도·피해·체력·이동·감지는 전부 장비의 몫이다. 이유는 PassiveEffectType 주석 참조.
/// </summary>
public class PassiveManager : Singleton<PassiveManager>
{
    [Header("Tree")]
    [Tooltip("비워두면 Resources/PassiveTree 에셋을 자동으로 불러온다.")]
    [SerializeField] private PassiveTree tree;

    [Header("계정 — 세이브가 없을 때의 처음 값")]
    [Tooltip("보유 크레딧. 패시브를 배우는 데 쓴다.")]
    [Min(0)]
    [SerializeField] private int credits = 5000;

    [Tooltip("역행 계열을 발견했는지. 세이브가 있으면 SaveManager가 덮는다. " +
             "본래는 4장 관측실에서 「역행자」를 만나야 켜진다.")]
    [SerializeField] private bool discoveredRegression = false;

    private readonly PassiveState state = new();

    /// <summary>배운 패시브.</summary>
    public PassiveState State => state;

    /// <summary>
    /// 패시브 트리. 인스펙터가 비어 있으면 처음 읽을 때 Resources에서 불러온다.
    /// 【Start를 기다리지 않는다.】 EnsureInstance()로 갓 만들어진 매니저를
    /// 같은 프레임에 UI가 읽어도 트리가 나와야 하기 때문이다.
    /// </summary>
    public PassiveTree Tree => tree != null ? tree : (tree = PassiveTree.Load());

    /// <summary>
    /// 패시브 요구 레벨과 비교하는 값 = 각성 레벨. (결정 2-33)
    /// 식별자 이름은 옛 것을 둔다 — 패시브 에셋의 직렬화 필드가 이 이름을 쓴다.
    /// </summary>
    public int AwakeningLevel => PlayerStats.HasInstance ? PlayerStats.Instance.AwakeningLevel : 1;

    private void OnEnable()
    {
        // 레벨이 오르면 패시브 화면의 「요구 Lv」 회색 처리가 바로 풀려야 한다.
        PlayerStats.EnsureInstance().OnChanged += RaiseChanged;
    }

    private void OnDisable()
    {
        if (PlayerStats.HasInstance)
            PlayerStats.Instance.OnChanged -= RaiseChanged;
    }

    private void RaiseChanged() => OnChanged?.Invoke();

    /// <summary>보유 크레딧.</summary>
    public int Credits
    {
        get => credits;
        set
        {
            credits = Mathf.Max(0, value);
            OnChanged?.Invoke();
        }
    }

    /// <summary>
    /// 크레딧을 더한다. 처치 보상이 이 경로로 들어온다. (Hunting 6-1절)
    /// 대입(Credits = x)과 나눠 둔 이유 — 더하기는 경합이 없어야 한다.
    /// </summary>
    public void AddCredits(int amount)
    {
        if (amount <= 0)
            return;

        Credits = credits + amount;
    }

    /// <summary>
    /// 역행 계열을 발견했는지. 켜지기 전에는 계열이 화면에 보이지도 않는다.
    ///
    /// 레벨도 돈도 아닌 【거기까지 갔는가】가 조건인 갈래를 하나 둔 것은
    /// 덕코프 「이상한 개조」의 구조를 가져온 것이다. [확인됨]
    /// </summary>
    public bool DiscoveredRegression
    {
        get => discoveredRegression;
        set
        {
            if (discoveredRegression == value)
                return;

            discoveredRegression = value;

            if (value)
                GameLogger.Log("[PassiveManager] 역행 계열 발견");

            OnChanged?.Invoke();
        }
    }

    /// <summary>지금 상태로 만든 판단 재료. 재료는 가방에서 꺼낸다.</summary>
    public PassiveContext Context => new(
        AwakeningLevel,
        credits,
        PlayerInventory.HasInstance ? PlayerInventory.Instance.Bag : null,
        discoveredRegression);

    /// <summary>패시브나 계정 상태가 바뀌었을 때 발행된다.</summary>
    public event Action OnChanged;

    /// <summary>배우지 못했을 때 이유가 실린다.</summary>
    public event Action<PassiveNode, PassiveError> OnLearnRejected;

    public static PassiveManager EnsureInstance()
    {
        if (HasInstance)
            return Instance;

        var existing = FindAnyObjectByType<PassiveManager>(FindObjectsInactive.Include);

        if (existing != null)
            return existing;

        return new GameObject("PassiveManager (Runtime)").AddComponent<PassiveManager>();
    }

    private void Start()
    {
        if (tree == null)
        {
            tree = PassiveTree.Load();

            if (tree == null)
            {
                GameLogger.Error(
                    $"[PassiveManager] Resources/{PassiveTree.ResourcePath} 에셋이 없습니다. " +
                    "메뉴 Blob > Passive > 패시브 에셋 생성 을 실행하십시오.", this);
            }
        }

        Apply();
    }

    /// <summary>배운다. 크레딧이 차감된다.</summary>
    public bool TryLearn(PassiveNode node)
    {
        PassiveContext context = Context;

        PassiveError error = state.CanLearn(node, in context);

        if (error != PassiveError.None)
        {
            OnLearnRejected?.Invoke(node, error);
            return false;
        }

        // 재료 소모가 TryLearn 안에서 함께 일어난다. 검사와 소모를 갈라 두면
        // "검사는 통과했는데 재료가 안 빠지는" 상태가 조용히 생긴다.
        int spent = state.TryLearn(node, in context);

        credits -= spent;

        GameLogger.Log($"[PassiveManager] 배움: {node.DisplayName} (-{spent})");

        Apply();

        OnChanged?.Invoke();

        return true;
    }

    /// <summary>배울 수 있는지. 이유까지 돌려준다. UI가 그대로 쓴다.</summary>
    public PassiveError CanLearn(PassiveNode node)
    {
        PassiveContext context = Context;

        return state.CanLearn(node, in context);
    }

    /// <summary>배운 패시브의 효과 합.</summary>
    public float Total(PassiveEffectType effect) => state.Total(tree, effect);

    /// <summary>해금형 효과가 켜졌는지.</summary>
    public bool HasUnlock(PassiveEffectType effect) => state.HasUnlock(tree, effect);

    /// <summary>
    /// 지금 배운 것을 실제 시스템에 반영한다.
    ///
    /// 효과를 「읽는 쪽이 매번 물어본다」가 아니라 「바뀔 때 한 번 밀어 넣는다」로 둔 이유 —
    /// 가방 용량처럼 매 프레임 읽히는 값을 그때그때 트리 전체에서 합산하면
    /// 칸이 늘어날수록 비용이 커진다.
    /// </summary>
    public void Apply()
    {
        if (PlayerInventory.HasInstance)
            PlayerInventory.Instance.RefreshCapacity();
    }
}
