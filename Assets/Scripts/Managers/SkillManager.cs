using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인자(因子) 관리자 — 파밍 · 도감 · 소켓 장착.
/// (docs/Blob_Skill_System.md 0·11·12절)
///
/// 흐름:
///   도감(해금 기록) → 드랍 풀 → 적을 흡수하면 인자가 가방에 들어온다
///   → 유저가 직접 소켓에 끼운다 → 즉시 작동
///   → 각성 레벨이 오르면 【선택창이 아니라 소켓이 열린다】
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
    [Tooltip("※ 임시 — 변이 샘플 해금과 세이브가 붙기 전까지 전 인자를 드랍 풀에 넣는다.")]
    [SerializeField] private bool unlockAllOnStart = true;

    [Header("드랍")]
    [Tooltip("시체 1구를 흡수했을 때 인자가 나올 확률.")]
    [Range(0f, 1f)]
    [SerializeField] private float gemDropChance = 0.18f;

    [Tooltip("레이드 시작 직후 부여 계열 Core 1개를 확정 지급한다. (11-3절) " +
             "끄면 아무것도 쏘지 못하는 상태로 시작한다.")]
    [SerializeField] private bool grantFirstCore = true;

    [Tooltip("첫 Core를 자동으로 1번 슬롯에 끼운다. 끄면 유저가 직접 끼운다.")]
    [SerializeField] private bool autoEquipFirstCore = true;

    private readonly SocketedBuild build = new();
    private readonly SkillCodex codex = new();
    private readonly List<SkillDefinition> dropPool = new();
    private readonly List<SkillDefinition> returned = new();
    private readonly List<ItemStack> gemStackBuffer = new();

    private System.Random random;

    /// <summary>지금 소켓에 끼워져 있는 구성.</summary>
    public SocketedBuild Build => build;

    /// <summary>도감 — 무엇이 드랍 풀에 들어오는가.</summary>
    public SkillCodex Codex => codex;

    /// <summary>현재 드랍 풀. 도감에 해금된 것만 들어 있다.</summary>
    public IReadOnlyList<SkillDefinition> DropPool => dropPool;

    /// <summary>전체 정의 카탈로그. 로드 실패 시 null일 수 있다.</summary>
    public SkillCatalog Catalog => catalog;

    /// <summary>인자를 주웠을 때 발행된다. UI 토스트가 구독한다.</summary>
    public event Action<SkillDefinition> OnGemGained;

    /// <summary>
    /// 각성 레벨이 올라 자리가 열렸을 때 발행된다. (레벨, "소켓 1" 같은 설명)
    /// 선택창을 여는 이벤트가 아니다. 안내만 한다.
    /// </summary>
    public event Action<int, string> OnSocketsOpened;

    /// <summary>소켓 구성이 바뀌었을 때 발행된다.</summary>
    public event Action OnBuildChanged;

    /// <summary>인자를 끼우지 못했을 때 그 이유가 실린다. UI가 문구로 바꾼다.</summary>
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

        SyncAwakeningLevel();

        if (FindAnyObjectByType<SkillSocketUI>(FindObjectsInactive.Include) == null)
            SkillSocketUI.Create();

        if (grantFirstCore)
            GrantFirstCore();
    }

    private void HandleBuildChanged() => OnBuildChanged?.Invoke();

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
                    "메뉴 Blob > Skill > 카탈로그 다시 만들기 를 실행하십시오.", this);
            }
        }

        if (gemCatalog == null)
        {
            gemCatalog = SkillGemCatalog.Load();

            if (gemCatalog == null)
            {
                GameLogger.Error(
                    $"[SkillManager] Resources/{SkillGemCatalog.ResourcePath} 에셋이 없습니다. " +
                    "메뉴 Blob > Skill > 인자 아이템 에셋 생성 을 실행하십시오.", this);
            }
        }
    }

    /// <summary>도감이 바뀌었을 때 드랍 풀을 다시 만든다.</summary>
    public void RebuildDropPool()
    {
        if (catalog == null)
        {
            dropPool.Clear();
            return;
        }

        codex.BuildDropPool(catalog.Definitions, dropPool);

        GameLogger.Log($"[SkillManager] 드랍 풀 {dropPool.Count}종 (도감 {codex.Count}종 해금)");
    }

    // ────────────────────────────────── 각성 레벨 = 소켓 개방

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

        int before = build.AwakeningLevel;

        SyncAwakeningLevel();

        // 한 번에 여러 레벨이 올라도 각 레벨의 개방을 빠짐없이 알린다.
        for (int level = before + 1; level <= build.AwakeningLevel; level++)
        {
            string opened = SocketUnlockTable.DescribeUnlock(level);

            if (string.IsNullOrEmpty(opened))
                continue;

            GameLogger.Log($"[SkillManager] 각성 Lv.{level} — {opened} 개방");

            OnSocketsOpened?.Invoke(level, opened);
        }
    }

    private void SyncAwakeningLevel()
    {
        int level = PlayerStats.HasInstance ? PlayerStats.Instance.Level : 1;

        build.SetAwakeningLevel(level);
    }

    // ────────────────────────────────── 드랍 · 획득

    /// <summary>
    /// 레이드 시작 직후의 확정 드랍. 부여 계열 Core 1개.
    /// 이것이 없으면 인자가 하나도 없어 아무것도 쏘지 못한다. (11-3절)
    /// </summary>
    public bool GrantFirstCore()
    {
        if (build.HasCore)
            return false;

        SkillDefinition core = SkillGemDropTable.DrawFirstCore(dropPool, random);

        if (core == null)
        {
            GameLogger.Error("[SkillManager] 드랍 풀에 부여 계열 Core가 없습니다. 도감을 확인하십시오.", this);
            return false;
        }

        if (!GrantGem(core))
            return false;

        if (autoEquipFirstCore)
            TryEquipCore(core, 0);

        return true;
    }

    /// <summary>
    /// 인자 드랍을 굴려 【아이템 정의만】 돌려준다. 시체가 자기 전리품 칸에 담는다.
    /// luckMultiplier는 적 등급 배수다. 희귀한 적일수록 잘 나온다.
    ///
    /// 가방에 바로 넣지 않는 이유 — 「무엇을 들고 갈지 고른다」가 추출 루팅의 결정이다.
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
    /// 인자를 가방에 넣는다. 자리가 없으면 false.
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
            GameLogger.Log($"[SkillManager] 가방이 가득 차 인자를 줍지 못했습니다: {skill.DisplayName}");
            return false;
        }

        GameLogger.Log($"[SkillManager] 인자 획득: {skill.DisplayName}");

        OnGemGained?.Invoke(skill);

        return true;
    }

    /// <summary>스킬 정의에 대응하는 인자 아이템. 없으면 null.</summary>
    public ItemDefinition FindGemItem(SkillDefinition skill)
    {
        if (skill == null)
            return null;

        if (gemCatalog == null)
        {
            GameLogger.Error("[SkillManager] 인자 아이템 카탈로그가 없습니다.", this);
            return null;
        }

        ItemDefinition gem = gemCatalog.Find(skill);

        if (gem == null)
            GameLogger.Error($"[SkillManager] '{skill.DisplayName}'의 인자 아이템이 없습니다.", this);

        return gem;
    }

    /// <summary>가방에 든 인자 목록. 소켓 UI가 이 목록을 그린다.</summary>
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
        // Core 교체는 빠져나오는 인자가 최대 4개(기존 Core + 소켓 3)다.
        // 끼울 인자 1개가 가방에서 빠지므로 실제로 필요한 여유는 그보다 1 적다.
        int returning = CountReturnsForCore(skill, coreIndex);

        if (returning > 1 &&
            PlayerInventory.EnsureInstance().Bag.FreeSlots + 1 < returning)
        {
            GameLogger.Log("[SkillManager] 가방에 자리가 없어 핵심 스킬을 교체할 수 없습니다.");
            OnEquipRejected?.Invoke(skill, SocketError.None);
            return false;
        }

        return Equip(skill, () => build.TryEquipCore(skill, coreIndex, returned),
                     build.CanEquipCore(skill, coreIndex));
    }

    /// <summary>이 Core를 저 자리에 끼우면 가방으로 돌아올 인자가 몇 개인지.</summary>
    private int CountReturnsForCore(SkillDefinition skill, int coreIndex)
    {
        if (skill == null)
            return 0;

        int count = build.GetCore(coreIndex) == null ? 0 : 1;

        for (int s = 0; s < SocketedBuild.SocketsPerCore; s++)
        {
            SkillDefinition support = build.GetSocket(coreIndex, s);

            if (support != null && !skill.Tags.ContainsAll(support.RequiredTags))
                count++;
        }

        return count;
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
    /// 가방에서 인자를 꺼내 소켓에 끼운다.
    ///
    /// 빠져나온 인자(교체된 것, 태그를 잃은 Support)는 가방으로 돌아간다.
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
            GameLogger.Warning($"[SkillManager] 가방에 없는 인자입니다: {skill.DisplayName}");
            return false;
        }

        returned.Clear();

        if (!equipAction())
            return false;

        // 끼울 인자를 먼저 빼서 자리를 만든 뒤 되돌린다. 순서를 바꾸면
        // 1:1 교체조차 가방이 꽉 찼을 때 실패한다.
        bag.Remove(gem);

        ReturnToBag(bag);

        GameLogger.Log($"[SkillManager] 장착: {skill.DisplayName}");

        return true;
    }

    public bool TryUnequipCore(int coreIndex)
    {
        Inventory bag = PlayerInventory.EnsureInstance().Bag;

        // 뺄 것이 가방에 다 들어가는지 먼저 본다. 들어가지 못하면 빼지 않는다.
        // 인자를 바닥에 버리는 처리를 만들지 않는 한, 이것이 아이템을 지키는 유일한 방법이다.
        int needed = build.GetCore(coreIndex) == null ? 0 : 1;

        for (int s = 0; s < SocketedBuild.SocketsPerCore; s++)
        {
            if (build.GetSocket(coreIndex, s) != null)
                needed++;
        }

        if (needed == 0)
            return false;

        if (bag.FreeSlots < needed)
        {
            GameLogger.Log("[SkillManager] 가방에 자리가 없어 인자를 뺄 수 없습니다.");
            return false;
        }

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

        if (bag.FreeSlots < 1)
        {
            GameLogger.Log("[SkillManager] 가방에 자리가 없어 인자를 뺄 수 없습니다.");
            return false;
        }

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

    // ────────────────────────────────── 런 종료

    /// <summary>
    /// 런 종료 시 소켓을 비운다.
    ///
    /// 【인자를 여기서 없애지 않는다.】 추출 성공이면 그대로 창고로 가고,
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
