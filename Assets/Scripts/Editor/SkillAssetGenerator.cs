using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Skill 정의 에셋 53종을 코드 표에서 생성하는 에디터 도구. (로드맵 5-E)
///
/// 왜 손으로 만들지 않는가:
///  · Inspector로 53개를 만들면 오타와 누락을 잡을 방법이 없다.
///  · MCP Unity는 컴포넌트의 List 필드를 채울 수 없다. 에디터 스크립트로 우회한다.
///  · 표가 코드에 있으면 문서와의 대조가 diff 한 번으로 끝난다.
///
/// 이미 있는 에셋은 덮어쓴다. GUID는 보존되므로 참조가 끊기지 않는다.
/// 출처: docs/Blob_Skill_System.md 5~8절
/// </summary>
public static class SkillAssetGenerator
{
    private const string Root = "Assets/Data/ScriptableObjects/Skills";

    private struct Row
    {
        public string id;
        public string name;
        public string desc;
        public SkillCategory category;
        public CoreFamily family;
        public int level;
        public SkillTag tags;
        public SkillTag requiredTags;
        public StatusEffectType creates;
        public StatusEffectType consumes;
        public GroundEffectType ground;
        public bool blocks;
        public StatusEffectType blocked;
        public string[] exclusive;
        public CostType cost;
        public string costDesc;
        public ProjectileBehaviourType behaviour;
        public int charges;
        public int ricochet;
        public int extraProjectiles;
        public float fireInterval;
        public float lifetime;
        public float speed;
    }

    private static Row New(string id, string name, string desc, SkillCategory category, int level)
    {
        return new Row
        {
            id = id, name = name, desc = desc, category = category, level = level,
            family = CoreFamily.None,
            tags = SkillTag.None, requiredTags = SkillTag.None,
            creates = StatusEffectType.None, consumes = StatusEffectType.None,
            ground = GroundEffectType.None,
            blocks = false, blocked = StatusEffectType.None,
            exclusive = null,
            cost = CostType.None, costDesc = string.Empty,
            behaviour = ProjectileBehaviourType.None, charges = 0, ricochet = 0,
            extraProjectiles = 0, fireInterval = 1f, lifetime = 1f, speed = 1f
        };
    }

    [MenuItem("Blob/Skill/정의 에셋 53종 생성")]
    public static void Generate()
    {
        List<Row> rows = BuildTable();

        if (rows.Count != 53)
        {
            Debug.LogError($"[SkillAssetGenerator] 53종이어야 하는데 {rows.Count}종입니다. 표를 확인하세요.");
            return;
        }

        EnsureFolders();

        int created = 0;
        int updated = 0;

        foreach (Row row in rows)
        {
            string folder = $"{Root}/{row.category}";
            string path = $"{folder}/{row.id}.asset";

            var definition = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
            bool isNew = definition == null;

            if (isNew)
            {
                definition = ScriptableObject.CreateInstance<SkillDefinition>();
                AssetDatabase.CreateAsset(definition, path);
                created++;
            }
            else
            {
                updated++;
            }

            Write(definition, row);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        SkillCatalogBuilder.Rebuild();

        Debug.Log($"[SkillAssetGenerator] 완료 — 신규 {created}종 / 갱신 {updated}종 (총 {rows.Count}종)");
    }

    private static void Write(SkillDefinition definition, Row row)
    {
        definition.name = row.id;

        var so = new SerializedObject(definition);

        so.FindProperty("id").stringValue = row.id;
        so.FindProperty("displayName").stringValue = row.name;
        so.FindProperty("description").stringValue = row.desc;

        so.FindProperty("category").intValue = (int)row.category;
        so.FindProperty("coreFamily").intValue = (int)row.family;
        so.FindProperty("requiredLevel").intValue = row.level;

        so.FindProperty("tags").intValue = (int)row.tags;
        so.FindProperty("requiredTags").intValue = (int)row.requiredTags;

        so.FindProperty("createsStatus").intValue = (int)row.creates;
        so.FindProperty("consumesStatus").intValue = (int)row.consumes;
        so.FindProperty("createsGroundEffect").intValue = (int)row.ground;

        so.FindProperty("blocksStatusCreation").boolValue = row.blocks;
        so.FindProperty("blockedStatus").intValue = (int)row.blocked;

        SerializedProperty ex = so.FindProperty("mutuallyExclusiveIds");
        int exCount = row.exclusive?.Length ?? 0;
        ex.arraySize = exCount;

        for (int i = 0; i < exCount; i++)
            ex.GetArrayElementAtIndex(i).stringValue = row.exclusive[i];

        so.FindProperty("costType").intValue = (int)row.cost;
        so.FindProperty("costDescription").stringValue = row.costDesc;

        so.FindProperty("grantedBehaviour").intValue = (int)row.behaviour;
        so.FindProperty("behaviourCharges").intValue = row.charges;
        so.FindProperty("ricochetBounces").intValue = row.ricochet;
        so.FindProperty("extraProjectiles").intValue = row.extraProjectiles;
        so.FindProperty("fireIntervalMultiplier").floatValue = row.fireInterval;
        so.FindProperty("lifetimeMultiplier").floatValue = row.lifetime;
        so.FindProperty("speedMultiplier").floatValue = row.speed;

        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(definition);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  표 — docs/Blob_Skill_System.md 5~8절과 1:1로 대응한다.
    //  문서를 고치면 이 표도 고친다. SkillCatalogTests가 개수·구조를 고정한다.
    // ══════════════════════════════════════════════════════════════════════
    private static List<Row> BuildTable()
    {
        var t = new List<Row>();
        Row r;

        // ── Core 부여 계열 5 ──────────────────────────────────────────────
        // 전부 투사체 태그를 가진다. Blob의 기본 동사가 사격이므로
        // 투사체 Support가 붙을 곳이 항상 존재해야 한다.

        r = New("core_fire", "화염", "적중한 적을 점화시킨다.", SkillCategory.Core, 1);
        r.family = CoreFamily.Ailment;
        r.tags = SkillTag.Projectile | SkillTag.Fire | SkillTag.Duration;
        r.creates = StatusEffectType.Ignite;
        t.Add(r);

        r = New("core_plague", "역병", "적중마다 중독이 중첩된다. 10중첩에서 위독해진다.", SkillCategory.Core, 1);
        r.family = CoreFamily.Ailment;
        r.tags = SkillTag.Projectile | SkillTag.Chaos | SkillTag.Duration;
        r.creates = StatusEffectType.Poison;
        t.Add(r);

        r = New("core_frost", "서리", "냉기를 누적시키고 임계치를 넘으면 동결시킨다.", SkillCategory.Core, 3);
        r.family = CoreFamily.Ailment;
        r.tags = SkillTag.Projectile | SkillTag.Cold;
        r.creates = StatusEffectType.Freeze;
        t.Add(r);

        r = New("core_thunder", "뇌전", "감전시켜 대상이 받는 모든 피해를 20% 증폭시킨다.", SkillCategory.Core, 5);
        r.family = CoreFamily.Ailment;
        r.tags = SkillTag.Projectile | SkillTag.Lightning;
        r.creates = StatusEffectType.Shock;
        t.Add(r);

        r = New("core_laceration", "열상", "출혈을 유발한다. 이동 중인 적에게 피해가 배증한다.", SkillCategory.Core, 7);
        r.family = CoreFamily.Ailment;
        r.tags = SkillTag.Projectile | SkillTag.Physical | SkillTag.Duration;
        r.creates = StatusEffectType.Bleed;
        t.Add(r);

        // ── Core 기폭 계열 3 ──────────────────────────────────────────────
        // 투사체 태그가 없다. 파동·잔류물·플레이어 중심 효과다.

        r = New("core_elemental_burst", "원소 작렬",
            "상태이상에 걸린 적이 사망하거나 최대 중첩에 도달하면 그 상태를 소모해 폭발시킨다.",
            SkillCategory.Core, 1);
        r.family = CoreFamily.Detonation;
        r.tags = SkillTag.AreaOfEffect | SkillTag.Detonator | SkillTag.Consuming;
        t.Add(r);

        r = New("core_gravity_collapse", "중력 붕괴",
            "중력 우물을 만들어 적을 끌어모으고 응집을 부여한다.", SkillCategory.Core, 7);
        r.family = CoreFamily.Detonation;
        r.tags = SkillTag.AreaOfEffect | SkillTag.Zone | SkillTag.Duration;
        r.creates = StatusEffectType.Congeal;
        r.ground = GroundEffectType.GravityWell;
        t.Add(r);

        r = New("core_shockwave", "충격파",
            "주기적으로 충격파를 내보내 적을 밀어내고, 잔류물과 지연 기폭을 즉시 터뜨린다.",
            SkillCategory.Core, 9);
        r.family = CoreFamily.Detonation;
        r.tags = SkillTag.AreaOfEffect | SkillTag.Detonator;
        t.Add(r);

        // ── Support 투사체 계열 12 ────────────────────────────────────────

        r = New("sup_pierce", "관통", "일직선상의 대상을 연속으로 타격한다. 기본 3체.", SkillCategory.Support, 1);
        r.requiredTags = SkillTag.Projectile;
        r.behaviour = ProjectileBehaviourType.Pierce; r.charges = 3;
        r.cost = CostType.ProjectileSpeed; r.costDesc = "탄속 -20%"; r.speed = 0.8f;
        t.Add(r);

        r = New("sup_projectile_accel", "투사체 가속", "탄속이 40% 빨라진다.", SkillCategory.Support, 1);
        r.requiredTags = SkillTag.Projectile;
        r.cost = CostType.EffectiveRange; r.costDesc = "유효 사거리 -15%"; r.speed = 1.4f;
        t.Add(r);

        r = New("sup_projectile_decel", "투사체 감속",
            "탄속이 40% 느려진다. 체류가 길어져 적중 판정이 늘어난다.", SkillCategory.Support, 1);
        r.requiredTags = SkillTag.Projectile;
        r.cost = CostType.Immediacy; r.costDesc = "즉시성 상실"; r.speed = 0.6f;
        t.Add(r);

        r = New("sup_ricochet", "튕겨 쏘기",
            "지형에 부딪히면 튕긴다. 3회. 매 튕김마다 부여 판정이 다시 일어난다.",
            SkillCategory.Support, 1);
        r.requiredTags = SkillTag.Projectile;
        r.ricochet = 3;
        r.cost = CostType.BarrageDensity; r.costDesc = "발사 간격 +15%"; r.fireInterval = 1.15f;
        t.Add(r);

        r = New("sup_chain", "연쇄",
            "적중 후 다른 적에게 재유도된다. 3회. 같은 적은 다시 때리지 않는다.",
            SkillCategory.Support, 3);
        r.requiredTags = SkillTag.Projectile;
        r.behaviour = ProjectileBehaviourType.Chain; r.charges = 3;
        r.cost = CostType.EffectiveRange; r.costDesc = "유효 사거리 -25%";
        t.Add(r);

        r = New("sup_fork", "갈래", "적중 시 2갈래로 갈라진다. 원 궤도 기준 ±60도.", SkillCategory.Support, 3);
        r.requiredTags = SkillTag.Projectile;
        r.behaviour = ProjectileBehaviourType.Fork; r.charges = 1;
        r.cost = CostType.BarrageDensity; r.costDesc = "발사 간격 +20%"; r.fireInterval = 1.20f;
        t.Add(r);

        r = New("sup_shrapnel", "파편 탄환",
            "날아가는 중에 3갈래로 쪼개진다. 파편은 다시 쪼개지지 않는다.", SkillCategory.Support, 5);
        r.requiredTags = SkillTag.Projectile;
        r.behaviour = ProjectileBehaviourType.Split; r.charges = 1;
        r.cost = CostType.EffectiveRange; r.costDesc = "유효 사거리 -25%";
        t.Add(r);

        r = New("sup_multishot", "다중 사격", "부채꼴로 탄환 2발을 추가로 발사한다.", SkillCategory.Support, 5);
        r.requiredTags = SkillTag.Projectile;
        r.extraProjectiles = 2;
        r.cost = CostType.BarrageDensity; r.costDesc = "발사 간격 +30%"; r.fireInterval = 1.30f;
        t.Add(r);

        r = New("sup_pursuit", "추격",
            "탄환이 적을 추적한다. 상태이상에 걸린 적을 우선한다.", SkillCategory.Support, 5);
        r.requiredTags = SkillTag.Projectile;
        r.cost = CostType.ProjectileSpeed; r.costDesc = "탄속 -25%"; r.speed = 0.75f;
        t.Add(r);

        r = New("sup_boomerang", "부메랑",
            "최대 사거리에서 되돌아오며 다시 타격한다. 이미 맞은 적을 다시 때릴 수 있는 유일한 수단이다.",
            SkillCategory.Support, 7);
        r.requiredTags = SkillTag.Projectile;
        r.behaviour = ProjectileBehaviourType.Return; r.charges = 1;
        r.cost = CostType.ProjectileSpeed; r.costDesc = "탄속 -25%"; r.speed = 0.75f;
        t.Add(r);

        r = New("sup_nova_projectiles", "폭발 투사체",
            "적중 지점에서 투사체가 사방으로 퍼져 나간다.", SkillCategory.Support, 7);
        r.requiredTags = SkillTag.Projectile;
        r.cost = CostType.BarrageDensity; r.costDesc = "발사 간격 +25%"; r.fireInterval = 1.25f;
        t.Add(r);

        r = New("sup_far_shot", "원거리 사격", "멀리 있는 적일수록 효과가 커진다.", SkillCategory.Support, 7);
        r.requiredTags = SkillTag.Projectile;
        r.cost = CostType.ControlConstraint; r.costDesc = "가까울수록 효과가 소멸한다";
        // 「근접 전투」와 정반대 조건이라 서로를 상쇄한다.
        r.exclusive = new[] { "sup_melee_combat" };
        t.Add(r);

        // ── Support 속성 계열 10 ──────────────────────────────────────────
        // 확산형 1 + 배타형 1 대칭. 배타형은 단독으로 전혀 작동하지 않으므로
        // 반드시 상태를 만드는 다른 수단이 필요하다.

        r = New("sup_fan_the_flames", "불난 집 부채질",
            "점화가 잠시 후 주변으로 퍼진다.", SkillCategory.Support, 5);
        r.requiredTags = SkillTag.Fire;
        r.cost = CostType.Duration; r.costDesc = "점화 지속시간 -40%"; r.lifetime = 0.6f;
        t.Add(r);

        r = New("sup_burnt_offering", "번제",
            "점화된 적에게 큰 추가 화염 피해를 준다.", SkillCategory.Support, 7);
        r.requiredTags = SkillTag.Fire;
        r.blocks = true; r.blocked = StatusEffectType.Ignite;
        r.cost = CostType.FunctionalExclusion; r.costDesc = "점화를 유발할 수 없다";
        t.Add(r);

        r = New("sup_frozen_malice", "얼어붙은 악의", "동결 임계치가 30% 낮아진다.", SkillCategory.Support, 5);
        r.requiredTags = SkillTag.Cold;
        r.cost = CostType.Duration; r.costDesc = "냉기 직접 피해가 지속 피해로 전환된다";
        t.Add(r);

        r = New("sup_bitter_frost", "살을 에는 서리",
            "동결된 적에게 주는 피해가 대폭 증가한다.", SkillCategory.Support, 7);
        r.requiredTags = SkillTag.Cold;
        r.consumes = StatusEffectType.Freeze;
        r.cost = CostType.FunctionalExclusion; r.costDesc = "적의 동결을 소모해 버린다";
        t.Add(r);

        r = New("sup_overflowing_charge", "넘치는 충전",
            "감전 증폭률이 15%p 오른다. (총 35%)", SkillCategory.Support, 5);
        r.requiredTags = SkillTag.Lightning;
        r.cost = CostType.Duration; r.costDesc = "감전 지속시간 -40%"; r.lifetime = 0.6f;
        t.Add(r);

        r = New("sup_shock_leap", "감전 도약", "감전이 인접한 적에게 전도된다.", SkillCategory.Support, 7);
        r.requiredTags = SkillTag.Lightning;
        r.blocks = true; r.blocked = StatusEffectType.Shock;
        r.cost = CostType.FunctionalExclusion; r.costDesc = "직접 감전을 유발하지 않는다";
        t.Add(r);

        r = New("sup_deadly_poison", "치명적인 중독", "중독 지속시간이 80% 늘어난다.", SkillCategory.Support, 5);
        r.requiredTags = SkillTag.Chaos;
        r.cost = CostType.Duration; r.costDesc = "초기 피해가 지연 피해로 전환된다"; r.lifetime = 1.8f;
        t.Add(r);

        r = New("sup_escalating_poison", "격화되는 중독",
            "중독 중첩 수에 비례해 피해가 증가한다.", SkillCategory.Support, 7);
        r.requiredTags = SkillTag.Chaos;
        r.blocks = true; r.blocked = StatusEffectType.Poison;
        r.cost = CostType.FunctionalExclusion; r.costDesc = "중독을 유발할 수 없다";
        t.Add(r);

        r = New("sup_deep_cuts", "깊은 상처", "출혈 피해가 60% 증가한다.", SkillCategory.Support, 5);
        r.requiredTags = SkillTag.Physical;
        r.cost = CostType.Duration; r.costDesc = "출혈 지속시간 -50%"; r.lifetime = 0.5f;
        t.Add(r);

        r = New("sup_bloodlust", "유혈 충동",
            "출혈 중인 적에게 주는 피해가 대폭 증가한다.", SkillCategory.Support, 7);
        r.requiredTags = SkillTag.Physical;
        r.blocks = true; r.blocked = StatusEffectType.Bleed;
        r.cost = CostType.FunctionalExclusion; r.costDesc = "출혈을 유발할 수 없다";
        t.Add(r);

        // ── Support 기폭 장치 · 잔류물 계열 6 ─────────────────────────────

        r = New("sup_short_fuse", "짧은 퓨즈", "기폭 지연이 사라진다.", SkillCategory.Support, 5);
        r.requiredTags = SkillTag.Detonator;
        r.cost = CostType.EffectiveRange; r.costDesc = "기폭 범위 -30%";
        r.exclusive = new[] { "sup_long_fuse" };
        t.Add(r);

        r = New("sup_caltrops", "마름쇠", "적중 지점에 잔류물을 남긴다.", SkillCategory.Support, 5);
        r.requiredTags = SkillTag.Zone;
        r.cost = CostType.BarrageDensity; r.costDesc = "발사 간격 +25%"; r.fireInterval = 1.25f;
        t.Add(r);

        r = New("sup_chain_detonation", "연쇄 기폭", "폭발이 1회 연쇄된다.", SkillCategory.Support, 7);
        r.requiredTags = SkillTag.Detonator;
        r.cost = CostType.Immediacy; r.costDesc = "기폭 쿨다운 +0.4초";
        t.Add(r);

        r = New("sup_lasting_ground", "유지되는 대지", "잔류물 지속시간이 100% 늘어난다.", SkillCategory.Support, 7);
        r.requiredTags = SkillTag.Zone;
        r.cost = CostType.EffectiveRange; r.costDesc = "잔류물 범위 -25%"; r.lifetime = 2f;
        t.Add(r);

        r = New("sup_long_fuse", "긴 퓨즈",
            "기폭이 0.8초 지연되는 대신 쌓인 중첩을 전부 소모한다.", SkillCategory.Support, 9);
        r.requiredTags = SkillTag.Detonator;
        r.cost = CostType.Immediacy; r.costDesc = "즉시성 상실";
        r.exclusive = new[] { "sup_short_fuse" };
        t.Add(r);

        r = New("sup_zone_potency", "잔류물 효력", "잔류물 범위가 한도까지 커진다.", SkillCategory.Support, 9);
        r.requiredTags = SkillTag.Zone;
        r.cost = CostType.Duration; r.costDesc = "잔류물 지속시간 -30%"; r.lifetime = 0.7f;
        t.Add(r);

        // ── Support 조건부 계열 3 ─────────────────────────────────────────
        // 요구 태그가 없다. 어떤 Core에도 붙는다.

        r = New("sup_momentum", "기세", "일정 거리 이상 이동하면 피해가 증가한다.", SkillCategory.Support, 5);
        r.cost = CostType.ControlConstraint; r.costDesc = "정지하면 보너스가 즉시 소멸한다";
        t.Add(r);

        r = New("sup_melee_combat", "근접 전투", "적과 가까울수록 피해가 증가한다.", SkillCategory.Support, 7);
        r.cost = CostType.EffectiveRange; r.costDesc = "유효 사거리 -50%";
        r.exclusive = new[] { "sup_far_shot" };
        t.Add(r);

        r = New("sup_cadence", "운율", "연사할수록 발사 속도가 증폭된다.", SkillCategory.Support, 9);
        r.cost = CostType.ControlConstraint; r.costDesc = "상한에 도달하면 1초간 강제로 과열 정지한다";
        t.Add(r);

        // ── Support 지속시간 계열 2 ───────────────────────────────────────

        r = New("sup_duration_extend", "지속시간 연장",
            "상태이상과 잔류물의 지속시간이 50% 늘어난다.", SkillCategory.Support, 3);
        r.requiredTags = SkillTag.Duration;
        r.cost = CostType.Immediacy; r.costDesc = "초기 피해가 지연 피해로 전환된다"; r.lifetime = 1.5f;
        r.exclusive = new[] { "sup_duration_compress" };
        t.Add(r);

        r = New("sup_duration_compress", "지속시간 압축",
            "지속시간이 절반이 되는 대신 초당 피해가 증가한다.", SkillCategory.Support, 3);
        r.requiredTags = SkillTag.Duration;
        r.cost = CostType.Duration; r.costDesc = "잔류물 지속시간도 함께 감소한다"; r.lifetime = 0.5f;
        r.exclusive = new[] { "sup_duration_extend" };
        t.Add(r);

        // ── Support 속성 전환 2 ───────────────────────────────────────────

        r = New("sup_fire_attunement", "화염 조율",
            "부여 계열의 속성을 화염으로 완전히 전환한다.", SkillCategory.Support, 9);
        r.requiredTags = SkillTag.Projectile;
        r.cost = CostType.FunctionalExclusion; r.costDesc = "전환 전 속성 전용 Support가 무효화된다";
        r.exclusive = new[] { "sup_elemental_fusion" };
        t.Add(r);

        r = New("sup_elemental_fusion", "원소 융합",
            "원래 속성을 유지한 채 2차 속성 상태를 추가로 부여한다.", SkillCategory.Support, 11);
        r.requiredTags = SkillTag.Projectile;
        r.cost = CostType.BarrageDensity; r.costDesc = "양쪽 속성의 적용 빈도가 절반이 된다";
        r.exclusive = new[] { "sup_fire_attunement" };
        t.Add(r);

        // ── Meta 5 ────────────────────────────────────────────────────────
        // 에너지 축적형이다. 확률 발동을 쓰지 않는다. 동시 장착 상한 2개.

        r = New("meta_cast_on_shock", "감전 시 시전",
            "감전을 유발할 때마다 8% 충전된다. 발동 시 화면 내 감전된 모든 적에게 낙뢰가 떨어진다.",
            SkillCategory.Meta, 11);
        r.tags = SkillTag.Trigger | SkillTag.Lightning;
        t.Add(r);

        r = New("meta_cast_on_ignite", "점화 시 시전",
            "점화된 적을 처치할 때마다 10% 충전된다. 발동 시 장착한 Core를 주변 적 전체에게 발동한다.",
            SkillCategory.Meta, 11);
        r.tags = SkillTag.Trigger | SkillTag.Fire;
        t.Add(r);

        r = New("meta_cast_on_freeze", "동결 시 시전",
            "동결을 유발할 때마다 7% 충전된다. 발동 시 범위 내 전체를 동결시키고 5초간 동결이 전이된다.",
            SkillCategory.Meta, 11);
        r.tags = SkillTag.Trigger | SkillTag.Cold;
        t.Add(r);

        r = New("meta_cast_on_crit", "치명타 시 시전",
            "치명타가 발생할 때마다 6% 충전된다. 발동 시 장착한 Core를 즉시 재발동한다.",
            SkillCategory.Meta, 13);
        r.tags = SkillTag.Trigger;
        t.Add(r);

        r = New("meta_cast_on_elemental_ailment", "원소 상태 이상 시 시전",
            "점화·동결·감전을 유발할 때마다 5% 충전된다. 발동 시 걸려 있는 모든 원소 상태를 한 번에 기폭한다.",
            SkillCategory.Meta, 13);
        r.tags = SkillTag.Trigger;
        t.Add(r);

        // ── Persistent(전령) 5 ────────────────────────────────────────────
        // 전부 "조건부 처치 시 연쇄" 구조다. 동시 장착 1개.

        r = New("her_ash", "재의 전령",
            "점화된 적을 처치하면 점화가 폭발하며 주변을 점화시킨다.", SkillCategory.Persistent, 11);
        r.tags = SkillTag.Persistent | SkillTag.Fire;
        r.consumes = StatusEffectType.Ignite;
        t.Add(r);

        r = New("her_ice", "얼음의 전령",
            "동결된 적을 처치하면 얼음이 폭발하며 주변을 동결시킨다.", SkillCategory.Persistent, 11);
        r.tags = SkillTag.Persistent | SkillTag.Cold;
        r.consumes = StatusEffectType.Freeze;
        t.Add(r);

        r = New("her_thunder", "천둥의 전령",
            "감전된 적을 처치하면 주변 전체에 번개를 방출한다.", SkillCategory.Persistent, 11);
        r.tags = SkillTag.Persistent | SkillTag.Lightning;
        r.consumes = StatusEffectType.Shock;
        t.Add(r);

        r = New("her_plague", "역병의 전령",
            "중독된 적을 처치하면 중독이 주변으로 확산된다.", SkillCategory.Persistent, 11);
        r.tags = SkillTag.Persistent | SkillTag.Chaos;
        r.consumes = StatusEffectType.Poison;
        t.Add(r);

        r = New("her_blood", "피의 전령",
            "출혈 중인 적을 처치하면 축적된 출혈량에 비례해 혈액이 폭발한다.", SkillCategory.Persistent, 11);
        r.tags = SkillTag.Persistent | SkillTag.Physical;
        r.consumes = StatusEffectType.Bleed;
        t.Add(r);

        return t;
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");

        if (!AssetDatabase.IsValidFolder("Assets/Data/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets/Data", "ScriptableObjects");

        if (!AssetDatabase.IsValidFolder(Root))
            AssetDatabase.CreateFolder("Assets/Data/ScriptableObjects", "Skills");

        foreach (string sub in new[] { "Core", "Support", "Meta", "Persistent" })
        {
            if (!AssetDatabase.IsValidFolder($"{Root}/{sub}"))
                AssetDatabase.CreateFolder(Root, sub);
        }
    }
}
