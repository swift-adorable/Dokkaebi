using System;
using UnityEngine;

/// <summary>
/// 계정 축의 영구 성장 — 패시브. 【죽어도 잃지 않는다.】
/// (docs/Blob_Progression_System.md — 각성 / 계정 2축)
///
///   각성 레벨 : 런마다 초기화. 소켓을 연다.        → SocketUnlockTable
///   계정 레벨 : 영구. 패시브를 연다.               → 여기
///
/// 【패시브는 전투 수치를 주지 않는다.】 휴대 · 수집 · 벙커 해금만 건드린다.
/// 방어도·피해·체력·이동·감지는 전부 장비의 몫이다. 이유는 PassiveEffectType 주석 참조.
/// </summary>
public class PassiveManager : Singleton<PassiveManager>
{
    [Header("Tree")]
    [Tooltip("비워두면 Resources/PassiveTree 에셋을 자동으로 불러온다.")]
    [SerializeField] private PassiveTree tree;

    [Header("계정 (세이브 연결 전 임시)")]
    [Tooltip("계정 레벨. 패시브 해금 조건이 된다. ※ 세이브가 붙기 전까지 인스펙터 값이다.")]
    [Min(1)]
    [SerializeField] private int accountLevel = 1;

    [Tooltip("보유 크레딧. 패시브를 배우는 데 쓴다.")]
    [Min(0)]
    [SerializeField] private int credits = 5000;

    private readonly PassiveState state = new();

    /// <summary>배운 패시브.</summary>
    public PassiveState State => state;

    /// <summary>패시브 트리. 로드 실패 시 null일 수 있다.</summary>
    public PassiveTree Tree => tree;

    /// <summary>계정 레벨.</summary>
    public int AccountLevel
    {
        get => accountLevel;
        set
        {
            accountLevel = Mathf.Max(1, value);
            OnChanged?.Invoke();
        }
    }

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
        PassiveError error = state.CanLearn(node, accountLevel, credits);

        if (error != PassiveError.None)
        {
            OnLearnRejected?.Invoke(node, error);
            return false;
        }

        int spent = state.TryLearn(node, accountLevel, credits);

        credits -= spent;

        GameLogger.Log($"[PassiveManager] 배움: {node.DisplayName} (-{spent})");

        Apply();

        OnChanged?.Invoke();

        return true;
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
