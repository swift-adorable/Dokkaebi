using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static EquipmentAssetWriter;

/// <summary>
/// 무기 에셋 생성 — 활 6 + 편전 · 쇠뇌 · 총통 · 신기전 17 = 23종 (로드맵 6-C · 결정 2-80).
///
/// 수치는 전부 docs/Dokkaebi_Combat_Baseline.md 3절 표에서 그대로 가져왔다.
/// 여기서 임의로 바꾸면 문서와 코드가 갈라진다 —
/// WeaponAssetTests가 표와 에셋이 일치하는지 검사한다.
///
/// 【티어는 DPS 단조 증가가 아니다】 4·5티어는 발사 간격이 오히려 늘어난다.
/// "세게 한 방" ↔ "빠르게 여러 발"의 선택이 유지되어야
/// 다탄 빌드가 후반에 죽지 않는다.
/// </summary>
public static class WeaponAssetGenerator
{
    private const string Folder = Root + "/Weapons";

    /// <summary>
    /// 문서 표의 6줄. 테스트가 이 표를 직접 읽어 에셋과 비교한다.
    /// </summary>
    public struct Spec
    {
        public int tier;
        public string id;
        public string name;
        public string family;
        public WeaponKind kind;
        public string desc;
        public float damage;
        public float interval;
        public float range;
        public int penetration;
        public float weight;
        public int durability;
        public int value;
        public int attachments;
        public float speed;
        public List<EquipmentStat> extra;
    }

    /// <summary>
    /// 활 6종 — 티어 기준선 (Combat_Baseline 3절 표). 다른 종류는 이 줄에 종류 배율을 얹는다.
    /// </summary>
    public static List<Spec> BowTable()
    {
        return new List<Spec>
        {
            new()
            {
                tier = 1, id = "wpn_t1_pipe", name = "환목궁", family = "활",
                desc = "통나무 한 토막을 깎아 만든 활. 맞으면 아프다는 것 말고는 장점이 없다.",
                damage = 10f, interval = 0.40f, range = 10f, penetration = 0,
                weight = 2.0f, durability = 40, value = 180, attachments = 0, speed = 18f,
                extra = new List<EquipmentStat>()
            },
            new()
            {
                tier = 2, id = "wpn_t2_coil", name = "죽궁", family = "활",
                desc = "대나무로 만든 활. 통나무 활보다 조용하고 빠르다.",
                damage = 13f, interval = 0.35f, range = 12f, penetration = 1,
                weight = 2.6f, durability = 60, value = 600, attachments = 1, speed = 24f,
                extra = new List<EquipmentStat>()
            },
            new()
            {
                tier = 3, id = "wpn_t3_acid", name = "향각궁", family = "활",
                desc = "우리 소뿔을 댄 각궁. 갑옷 틈을 비집고 들어간다.",
                damage = 16f, interval = 0.32f, range = 13f, penetration = 2,
                weight = 3.0f, durability = 90, value = 1600, attachments = 2, speed = 22f,
                extra = new List<EquipmentStat>()
            },
            new()
            {
                // 여기서부터 성능 상단이다 — 대가가 붙는다.
                tier = 4, id = "wpn_t4_breaker", name = "흑각궁", family = "활",
                desc = "물소 뿔을 댄 각궁. 한 발이 무겁다. 들고 뛰는 것은 포기해야 한다.",
                damage = 22f, interval = 0.45f, range = 15f, penetration = 3,
                weight = 5.5f, durability = 130, value = 4200, attachments = 3, speed = 20f,
                extra = new List<EquipmentStat> { S(EquipmentStatType.MoveAbility, -0.08f) }
            },
            new()
            {
                tier = 5, id = "wpn_t5_rail", name = "녹각궁", family = "활",
                desc = "사슴뿔을 댄 활. 멀리서 뚫는다. 다만 시위 소리가 구역 절반에 퍼진다.",
                damage = 28f, interval = 0.50f, range = 20f, penetration = 5,
                weight = 4.8f, durability = 150, value = 9000, attachments = 4, speed = 34f,
                extra = new List<EquipmentStat> { S(EquipmentStatType.DetectedDistance, 2f) }
            },
            new()
            {
                tier = 6, id = "wpn_t6_eraser", name = "철태궁", family = "활",
                desc = "몸을 쇠로 만든 활. 쓰는 쪽도 무사하지 않다.",
                damage = 34f, interval = 0.45f, range = 20f, penetration = 6,
                weight = 7.0f, durability = 150, value = 20000, attachments = 6, speed = 30f,
                extra = new List<EquipmentStat>
                {
                    S(EquipmentStatType.MoveAbility, -0.12f),
                    S(EquipmentStatType.DetectedDistance, 3f)
                }
            }
        };
    }

    /// <summary>
    /// 활이 아닌 무기 17종 (Naming 1절 · 결정 2-80). (티어, id, 이름, 종류, 설명)
    /// 천자총통은 산탄 철환을 쓴다 — 탄 표(3-2절)에 이름이 없어 큰 총통의 조란환(흩어지는 알)을 따랐다.
    /// </summary>
    public static readonly (int tier, string id, string name, WeaponKind kind, string desc)[] Lineup =
    {
        (1, "wpn_t1_sechongtong",   "세총통",       WeaponKind.Gun,               "손바닥만 한 작은 총통. 철환을 재어 쏜다. 소리가 크다."),
        (2, "wpn_t2_gwoljangno",    "궐장노",       WeaponKind.Crossbow,          "발로 밟아 당기는 쇠뇌. 시위 소리가 거의 없다."),
        (2, "wpn_t2_seungja",       "승자총통",     WeaponKind.ScatterGun,        "철환 여러 알을 한 번에 흩뿌린다. 가까이서 무섭다."),
        (3, "wpn_t3_pyeonjeon",     "편전",         WeaponKind.Pyeonjeon,         "통아에 넣어 쏘는 짧은 화살. 갑옷 틈을 뚫는다."),
        (3, "wpn_t3_sunogi",        "수노기",       WeaponKind.RepeatingCrossbow, "손으로 당겨 잇달아 쏘는 쇠뇌. 한 발은 가볍다."),
        (3, "wpn_t3_soseungja",     "소승자총통",   WeaponKind.Gun,               "승자총통을 줄인 것. 들고 다니기 좋다."),
        (4, "wpn_t4_jangpyeonjeon", "장편전",       WeaponKind.Pyeonjeon,         "긴 통아를 쓴 편전. 멀리서 깊이 박힌다."),
        (4, "wpn_t4_yongdu",        "용두삼시수노", WeaponKind.RepeatingCrossbow, "용머리를 새긴 쇠뇌. 살을 잇달아 쏟아낸다."),
        (4, "wpn_t4_byeolseungja",  "별승자총통",   WeaponKind.Gun,               "몸이 무거운 승자총통. 한 발 한 발이 단단하다."),
        (4, "wpn_t4_sosingijeon",   "소신기전",     WeaponKind.Rocket,            "작은 화약 통을 단 신기전. 맞은 자리에서 터진다."),
        (5, "wpn_t5_cheonbo",       "천보편전",     WeaponKind.Pyeonjeon,         "천 걸음을 간다는 편전."),
        (5, "wpn_t5_gangno",        "강노",         WeaponKind.Crossbow,          "센 쇠뇌. 당기기 어렵지만 한 발이 묵직하다."),
        (5, "wpn_t5_paljeon",       "팔전총통",     WeaponKind.ScatterGun,        "철환을 한 움큼 흩뿌린다. 발사음이 멀리 간다."),
        (5, "wpn_t5_jungsingijeon", "중신기전",     WeaponKind.Rocket,            "중간 크기의 신기전. 폭발이 둘레를 휩쓴다."),
        (6, "wpn_t6_sujil",         "수질구궁노",   WeaponKind.RepeatingCrossbow, "아홉 활을 묶은 큰 쇠뇌를 한 사람이 들 만큼 줄였다."),
        (6, "wpn_t6_cheonja",       "천자총통",     WeaponKind.ScatterGun,        "가장 큰 총통을 줄였다. 쏘는 쪽도 무사하지 않다."),
        (6, "wpn_t6_sanhwa",        "산화신기전",   WeaponKind.Rocket,            "불꽃을 흩뿌리며 터지는 신기전."),
    };

    /// <summary>
    /// 무기 전부 — 활 6 + 17 = 23종. 활이 아닌 것은 같은 티어 활에 종류 배율을 얹는다 (WeaponKindTable):
    /// 간격 = 활 × 간격 비율 · 한 번 피해 = 활 초당 피해 × 배율 × 간격 · 관통 + 보너스 · 무게 × 배율.
    /// 티어 4 이상의 대가(활의 extra)는 그대로 따라간다.
    /// </summary>
    public static List<Spec> Table()
    {
        List<Spec> bows = BowTable();
        var all = new List<Spec>(bows);

        foreach ((int tier, string id, string name, WeaponKind kind, string desc) in Lineup)
        {
            Spec bow = bows.Find(b => b.tier == tier);
            WeaponKindInfo k = WeaponKindTable.Of(kind);

            all.Add(new Spec
            {
                tier = tier, id = id, name = name, family = k.Name, kind = kind, desc = desc,
                damage = Mathf.Round(WeaponKindTable.ShotDamage(kind, bow.damage, bow.interval) * 10f) / 10f,
                interval = Mathf.Round(WeaponKindTable.Interval(kind, bow.interval) * 1000f) / 1000f,
                range = bow.range,
                penetration = Mathf.Min(CombatConstants.MaxArmour, bow.penetration + k.PenetrationBonus),
                weight = Mathf.Round(bow.weight * k.WeightScale * 10f) / 10f,
                durability = bow.durability, value = bow.value, attachments = bow.attachments, speed = bow.speed,
                extra = new List<EquipmentStat>(bow.extra)
            });
        }

        return all;
    }

    [MenuItem("Dokkaebi/Equipment/무기 에셋 생성")]
    public static void Generate()
    {
        List<Spec> table = Table();

        foreach (Spec spec in table)
            WriteOne(spec);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[WeaponAssetGenerator] 무기 {table.Count}종 생성 완료");
    }

    public static void WriteOne(Spec spec)
    {
        var stats = new List<EquipmentStat>
        {
            // 방어 관통은 stats로 둔다 — 부착물과 각인도 같은 축을 건드리므로
            // 합산 경로가 하나여야 "왜 이 수치가 나왔는가"를 추적할 수 있다.
            S(EquipmentStatType.ArmourPenetration, spec.penetration)
        };

        if (spec.extra != null)
            stats.AddRange(spec.extra);

        Write(new Row
        {
            id = spec.id,
            name = spec.name,
            desc = spec.desc,
            kind = ItemKind.Weapon,
            slot = EquipmentSlot.Weapon,
            tier = spec.tier,
            weight = spec.weight,
            durability = spec.durability,
            value = spec.value,
            stats = stats,

            baseDamage = spec.damage,
            fireInterval = spec.interval,
            effectiveRange = spec.range,
            projectileSpeed = spec.speed,
            attachmentSlots = spec.attachments,
            weaponFamily = spec.family,
            weaponKind = spec.kind
        }, Folder, isWeapon: true);
    }
}
