using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static EquipmentAssetWriter;

/// <summary>
/// 무기 티어 1~6 에셋 생성. (로드맵 6-C)
///
/// 수치는 전부 docs/Blob_Combat_Baseline.md 3절 표에서 그대로 가져왔다.
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

    public static List<Spec> Table()
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

    [MenuItem("Blob/Equipment/무기 에셋 생성")]
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
            weaponFamily = spec.family
        }, Folder, isWeapon: true);
    }
}
