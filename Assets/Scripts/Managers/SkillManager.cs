using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 젬(Gem) 관리자 — 파밍 · 도감 · 소켓 장착.
/// (docs/Dokkaebi_Skill_System.md 0·11·12절)
///
/// 흐름:
///   도감(해금 기록) → 드롭 풀 → 적을 흡수하면 젬이 가방에 들어온다
///   → 플레이어가 직접 소켓에 끼운다 → 즉시 작동
///   → 레벨이 오르면 【선택창이 아니라 소켓이 열린다】
///
/// 【이전 구조와의 차이】
///   레벨업 시 3장을 제시하고 하나를 고르게 하던 경로를 전부 걷어냈다.
///   SkillLoadout · SkillSelectionPool · SkillDraft · SkillSelectionUI · RunSkillState는
///   더 이상 존재하지 않는다.
/// </summary>
public class SkillManager : Singleton<SkillManager>
{
    [Header("Catalog")]
    [Tooltip("비워두면 Resources/SkillCatalog 에셋을 자동으로 불러온다.")]
    [SerializeField] private SkillCatalog catalog;

    [Tooltip("비워두면 Resources/SkillGemCatalog 에셋을 자동으로 불러온다.")]
    [SerializeField] private SkillGemCatalog gemCatalog;

    [Header("도감 (세이브 연결 전 임시)")]
    [Tooltip("※ 임시 — 변이 샘플 해금과 세이브가 붙기 전까지 전 젬을 드롭 풀에 넣는다.")]
    [SerializeField] private bool unlockAllOnStart = true;

    [Header("드롭")]
    [Tooltip("시체 1구를 흡수했을 때 젬이 나올 확률.")]
    [Range(0f, 1f)]
    [SerializeField] private float gemDropChance = 0.18f;

    [Tooltip("레이드 시작 직후 부여 계열 핵심 젬 1개를 확정 지급한다. (11-3절) " +
             "끄면 아무것도 쏘지 못하는 상태로 시작한다.")]
    [SerializeField] private bool grantFirstCore = true;

    [Tooltip("첫 핵심 젬을 자동으로 1번 슬롯에 끼운다. 끄면 플레이어가 직접 끼운다.")]
    [SerializeField] private bool autoEquipFirstCore = true;

    private readonly SocketedBuild build = new();
    private readonly SkillCodex codex = new();
    private readonly List<SkillDefinition> dropPool = new();
    private readonly List<SkillDefinition> returned = new();
    private readonly List<ItemStack> gemStackBuffer = new();

    private System.Random random;

    /// <summary>지금 소켓에 끼워져 있는 구성.</summary>
    public SocketedBuild Build => build;

    /// <summary>도감 — 무엇이 드롭 풀에 들어오는가.</summary>
    public SkillCodex Codex => codex;

    /// <summary>현재 드롭 풀. 도감에 해금된 것만 들어 있다.</summary>
    public IReadOnlyList<SkillDefinition> DropPool => dropPool;

    /// <summary>전체 정의 카탈로그. 로드 실패 시 null일 수 있다.</summary>
    public SkillCatalog Catalog => catalog;

    /// <summary>젬을 주웠을 때 발행된다. UI 토스트가 구독한다.</summary>
    public event Action<SkillDefinition> OnGemGained;

    /// <summary>
    /// 레벨이 올라 자리가 열렸을 때 발행된다. (레벨, "소켓 1" 같은 설명)
    /// 선택창을 여는 이벤트가 아니다. 안내만 한다.
    /// </summary>
    public event Action<int, string> OnSocketsOpened;

    /// <summary>소켓 구성이 바뀌었을 때 발행된다.</summary>
    public event Action OnBuildChanged;

    /// <summary>젬을 끼우지 못했을 때 그 이유가 실린다. UI가 문구로 바꾼다.</summary>
    public event Action<SkillDefinition, SocketError> OnEquipRejected;

    /// <summary>인스턴스를 보장한다. 씬 배치를 강제하지 않는다.</summary>
    public static SkillManager EnsureInstance()
    {
        if (HasInstance)
            return Instance;

        var existing = FindAnyObjectByType<SkillManager>(FindObjectsInactive.Include);

        if (existing != null)
            return existing;

        return new GameObject("SkillManager (Runtime)").AddComponent<SkillManager>();
    }

    protected override void OnSingletonAwake()
    {
        random = new System.Random(Environment.TickCount);

        build.OnChanged += HandleBuildChanged;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        build.OnChanged -= HandleBuildChanged;
    }

    private void Start()
    {
        LoadCatalogsIfNeeded();

        if (unlockAllOnStart && catalog != null)
            codex.UnlockAll(catalog.Definitions);

        RebuildDropPool();

        SyncLevel();

        // 가방 화면(장비 · 가방 · 젬 소켓 · 패시브)을 보장한다.
        InventoryScreenUI.EnsureInstance();

        // 체력 · 수분 · 에너지 막대. 씬 배치를 강제하지 않는다.
        PlayerSurvival.EnsureInstance();
        SurvivalHudUI.EnsureInstance();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 검증 패널. 실제 기기 빌드에서 장비를 얻을 유일한 경로다.
        // 출시 빌드에는 컴파일되지 않는다.
        PlaytestPanelUI.EnsureInstance();
#endif

        // 【벙커에서는 주지 않는다】 — 소켓은 파밍 안의 것이다.
        if (grantFirstCore && !SceneFlow.InBunker)
            GrantFirstCore();
    }

    private void HandleBuildChanged() => OnBuildChanged?.Invoke();

    /// <summary>가방이나 창고에 핵심 젬이 하나라도 있는가.</summary>
    private static bool OwnsCoreGem()
    {
        if (!PlayerInventory.HasInstance)
            return false;

        return HasCore(PlayerInventory.Instance.Bag) || HasCore(PlayerInventory.Instance.Stash);

        static bool HasCore(Inventory inventory)
        {
            foreach (ItemStack stack in inventory.Stacks)
            {
                ItemDefinition definition = stack?.Definition;

                if (definition != null && definition.IsSkillGem
                    && definition.Skill.Category == SkillCategory.Core)
                    return true;
            }

            return false;
        }
    }

    // ────────────────────────────────── 카탈로그 · 도감

    private void LoadCatalogsIfNeeded()
    {
        if (catalog == null)
        {
            catalog = SkillCatalog.Load();

            if (catalog == null)
            {
                GameLogger.Error(
                    $"[SkillManager] Resources/{SkillCatalog.ResourcePath} 에셋이 없습니다. " +
                    "메뉴 Dokkaebi > Skill > 카탈로그 다시 만들기 를 실행하십시오.", this);
            }
        }

        if (gemCatalog == null)
        {
            gemCatalog = SkillGemCatalog.Load();

            if (gemCatalog == null)
            {
                GameLogger.Error(
                    $"[SkillManager] Resources/{SkillGemCatalog.ResourcePath} 에셋이 없습니다. " +
                    "메뉴 Dokkaebi > Skill > 젬 아이템 에셋 생성 을 실행하십시오.", this);
            }
        }
    }

    /// <summary>도감이 바뀌었을 때 드롭 풀을 다시 만든다.</summary>
    public void RebuildDropPool()
    {
        if (catalog == null)
        {
            dropPool.Clear();
            return;
        }

        codex.BuildDropPool(catalog.Definitions, dropPool);

        GameLogger.Log($"[SkillManager] 드롭 풀 {dropPool.Count}종 (도감 {codex.Count}종 해금)");
    }

    // ────────────────────────────────── 레벨 = 소켓 개방

    /// <summary>
    /// 레벨업을 반영한다. PlayerStats가 호출한다.
    ///
    /// 【선택창을 열지 않는다.】 자리가 열릴 뿐이다.
    /// 무엇을 끼울지는 그때까지 무엇을 주웠는지가 정한다.
    /// </summary>
    public void EnqueueLevelUp(int count = 1)
    {
        if (count <= 0)
        {
            GameLogger.Warning($"[SkillManager] 유효하지 않은 레벨업 횟수: {count}");
            return;
        }

        int before = build.Level;

        SyncLevel();

        // 한 번에 여러 레벨이 올라도 각 레벨의 개방을 빠짐없이 알린다.
        for (int level = before + 1; level <= build.Level; level++)
        {
            string opened = SocketUnlockTable.DescribeUnlock(level);

            if (string.IsNullOrEmpty(opened))
                continue;

            GameLogger.Log($"[SkillManager] Lv.{level} — {opened} 개방");

            OnSocketsOpened?.Invoke(level, opened);
        }
    }

    /// <summary>세이브를 불러온 뒤 소켓 수를 레벨에 다시 맞춘다. 알림은 내지 않는다.</summary>
    public void ResyncLevel() => SyncLevel();

    private void SyncLevel()
    {
        int level = PlayerStats.HasInstance ? PlayerStats.Instance.Level : 1;

        build.SetLevel(level);
    }

    // ────────────────────────────────── 드롭 · 획득

    /// <summary>
    /// 레이드 시작 직후의 확정 드롭. 부여 계열 핵심 젬 1개.
    /// 이것이 없으면 젬이 하나도 없어 아무것도 쏘지 못한다. (11-3절)
    /// </summary>
    public bool GrantFirstCore()
    {
        if (build.HasCore)
            return false;

        // 【이미 핵심 젬을 가지고 있으면 주지 않는다.】 벙커와 철수가 생기기 전에는
        // 파밍마다 맨몸으로 시작했으므로 늘 줬다. 이제는 가방이 이어지므로 매번 주면
        // 철수할 때마다 공짜 젬이 창고에 쌓인다. 없을 때만 준다 — 젬 없이
        // 들어가 싸울 수 없는 상태를 막는 안전판이다.
        if (OwnsCoreGem())
            return false;

        SkillDefinition core = SkillGemDropTable.DrawFirstCore(dropPool, random);

        if (core == null)
        {
            GameLogger.Error("[SkillManager] 드롭 풀에 부여 계열 핵심 젬이 없습니다. 도감을 확인하십시오.", this);
            return false;
        }

        if (!GrantGem(core))
            return false;

        if (autoEquipFirstCore)
            TryEquipCore(core, 0);

        return true;
    }

    /// <summary>
    /// 젬 드롭을 굴려 【아이템 정의만】 돌려준다. 시체가 자기 전리품 칸에 담는다.
    /// luckMultiplier는 적 등급 배율다. 희귀한 적일수록 잘 나온다.
    ///
    /// 가방에 바로 넣지 않는 이유 — 「무엇을 들고 갈지 고른다」가 철수 루팅의 결정이다.
    /// 자동으로 들어가면 그 결정이 사라진다. (전리품 창 도입, 확정 기획)
    /// </summary>
    public ItemDefinition RollGemDropItem(int luckMultiplier = 1)
    {
        if (dropPool.Count == 0)
            return null;

        random ??= new System.Random(Environment.TickCount);

        float chance = Mathf.Clamp01(gemDropChance * Mathf.Max(1, luckMultiplier));

        if (random.NextDouble() >= chance)
            return null;

        SkillDefinition drawn = SkillGemDropTable.Draw(dropPool, random);

        return FindGemItem(drawn);
    }

    /// <summary>
    /// 젬을 가방에 넣는다. 자리가 없으면 false.
    ///
    /// 요구 레벨 미달이어도 넣는다. 【주울 수는 있으나 끼울 수 없다】가 규칙이다. (11-3절)
    /// </summary>
    public bool GrantGem(SkillDefinition skill)
    {
        ItemDefinition gem = FindGemItem(skill);

        if (gem == null)
            return false;

        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        if (bag.TryAdd(gem) <= 0)
        {
            GameLogger.Log($"[SkillManager] 가방이 가득 차 젬을 줍지 못했습니다: {skill.DisplayName}");
            return false;
        }

        GameLogger.Log($"[SkillManager] 젬 획득: {skill.DisplayName}");

        OnGemGained?.Invoke(skill);

        return true;
    }

    /// <summary>스킬 정의에 대응하는 젬 아이템. 없으면 null.</summary>
    public ItemDefinition FindGemItem(SkillDefinition skill)
    {
        if (skill == null)
            return null;

        if (gemCatalog == null)
        {
            GameLogger.Error("[SkillManager] 젬 아이템 카탈로그가 없습니다.", this);
            return null;
        }

        ItemDefinition gem = gemCatalog.Find(skill);

        if (gem == null)
            GameLogger.Error($"[SkillManager] '{skill.DisplayName}'의 젬 아이템이 없습니다.", this);

        return gem;
    }

    /// <summary>가방에 든 젬 목록. 소켓 UI가 이 목록을 그린다.</summary>
    public List<ItemStack> GetGemsInBag(List<ItemStack> result = null)
    {
        result ??= new List<ItemStack>();
        result.Clear();

        if (!PlayerInventory.HasInstance)
            return result;

        IReadOnlyList<ItemStack> stacks = PlayerInventory.Instance.Bag.Stacks;

        for (int i = 0; i < stacks.Count; i++)
        {
            if (stacks[i].Definition != null && stacks[i].Definition.IsSkillGem)
                result.Add(stacks[i]);
        }

        return result;
    }

    // ────────────────────────────────── 장착 · 탈착

    public bool TryEquipCore(SkillDefinition skill, int coreIndex)
    {
        // 【자리 검사를 하지 않는다.】 젬은 칸에 잡히지 않는다.
        // (ItemDefinition.IsCargo) 핵심 젬을 갈아 끼우면 소켓의 보조 젬까지
        // 최대 4개가 한꺼번에 돌아오는데, 예전에는 그만큼의 빈 칸이 없으면
        // 교체 자체가 막혔다. 이제 돌아올 곳은 늘 있다.
        return Equip(skill, () => build.TryEquipCore(skill, coreIndex, returned),
                     build.CanEquipCore(skill, coreIndex));
    }

    public bool TryEquipSupport(SkillDefinition skill, int coreIndex, int socketIndex)
    {
        return Equip(skill, () => build.TryEquipSupport(skill, coreIndex, socketIndex, returned),
                     build.CanEquipSupport(skill, coreIndex, socketIndex));
    }

    public bool TryEquipMeta(SkillDefinition skill, int slot)
    {
        return Equip(skill, () => build.TryEquipMeta(skill, slot, returned),
                     build.CanEquipMeta(skill, slot));
    }

    public bool TryEquipHerald(SkillDefinition skill)
    {
        return Equip(skill, () => build.TryEquipHerald(skill, returned),
                     build.CanEquipHerald(skill));
    }

    /// <summary>분류를 보고 비어 있는 첫 자리에 끼운다. UI의 「빠른 장착」.</summary>
    public bool TryEquipAuto(SkillDefinition skill)
    {
        return Equip(skill, () => build.TryEquipAuto(skill, returned),
                     build.CanEquipAnywhere(skill));
    }

    /// <summary>
    /// 가방에서 젬을 꺼내 소켓에 끼운다.
    ///
    /// 빠져나온 젬(교체된 것, 태그를 잃은 보조 젬)는 가방으로 돌아간다.
    /// 조작 도중에 아이템이 사라지지 않게 하는 것이 이 함수의 핵심 책임이다.
    /// </summary>
    private bool Equip(SkillDefinition skill, Func<bool> equipAction, SocketError precheck)
    {
        if (precheck != SocketError.None)
        {
            OnEquipRejected?.Invoke(skill, precheck);
            return false;
        }

        ItemDefinition gem = FindGemItem(skill);

        if (gem == null)
            return false;

        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        if (!bag.Contains(gem))
        {
            GameLogger.Warning($"[SkillManager] 가방에 없는 젬입니다: {skill.DisplayName}");
            return false;
        }

        returned.Clear();

        if (!equipAction())
            return false;

        // 끼울 젬을 먼저 빼서 자리를 만든 뒤 되돌린다. 순서를 바꾸면
        // 1:1 교체조차 가방이 꽉 찼을 때 실패한다.
        bag.Remove(gem);

        ReturnToBag(bag);

        GameLogger.Log($"[SkillManager] 장착: {skill.DisplayName}");

        return true;
    }

    public bool TryUnequipCore(int coreIndex)
    {
        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        // 뺄 것이 있는지만 본다. 자리는 보지 않는다 — 젬은 칸을 쓰지 않는다.
        int needed = build.GetCore(coreIndex) == null ? 0 : 1;

        for (int s = 0; s < SocketedBuild.SocketsPerCore; s++)
        {
            if (build.GetSocket(coreIndex, s) != null)
                needed++;
        }

        if (needed == 0)
            return false;

        returned.Clear();
        build.UnequipCore(coreIndex, returned);
        ReturnToBag(bag);

        return true;
    }

    public bool TryUnequipSupport(int coreIndex, int socketIndex)
    {
        return Unequip(() => build.UnequipSupport(coreIndex, socketIndex));
    }

    public bool TryUnequipMeta(int slot)
    {
        return Unequip(() => build.UnequipMeta(slot));
    }

    public bool TryUnequipHerald()
    {
        return Unequip(() => build.UnequipHerald());
    }

    private bool Unequip(Func<SkillDefinition> unequipAction)
    {
        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        SkillDefinition removed = unequipAction();

        if (removed == null)
            return false;

        returned.Clear();
        returned.Add(removed);

        ReturnToBag(bag);

        return true;
    }

    private void ReturnToBag(Inventory bag)
    {
        for (int i = 0; i < returned.Count; i++)
        {
            ItemDefinition gem = FindGemItem(returned[i]);

            if (gem == null)
                continue;

            if (bag.TryAdd(gem) <= 0)
            {
                GameLogger.Error(
                    $"[SkillManager] 가방이 가득 차 '{returned[i].DisplayName}'을 되돌리지 못했습니다. " +
                    "탈착 전 자리 검사가 빠진 경로가 있습니다.", this);
            }
        }

        returned.Clear();
    }

    // ────────────────────────────────── 파밍 종료

    /// <summary>
    /// 파밍 종료 시 소켓을 비운다.
    ///
    /// 【젬을 여기서 없애지 않는다.】 철수 성공이면 그대로 창고로 가고,
    /// 사망이면 PlayerInventory.DropOnDeath가 규칙 하나로 처리한다.
    /// </summary>
    public void ResetRun()
    {
        if (PlayerInventory.HasInstance)
        {
            returned.Clear();
            build.UnequipAll(returned);
            ReturnToBag(PlayerInventory.Instance.Bag);
        }

        build.Clear();
    }
}
