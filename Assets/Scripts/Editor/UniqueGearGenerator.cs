using System.Collections.Generic;
using UnityEditor;
using static EquipmentAssetWriter;

/// <summary>
/// 【중간 보스 고유 장비 11】 (결정 2-109) — 덕코프의 보스 장비처럼, 그 보스의 본문 모습에서 딴 장비.
/// 같은 티어(= 그 장) 일반 장비보다 조금 좋고, 대가가 하나 붙는다. 값은 비싸서 팔아도 된다.
/// 방어구는 피해를 올리지 않는다(「무기 = 화력 / 방어구 = 생존」 경계). 이름 · 설명 · 수치는 [임시값].
/// 만든 뒤 「아이템 카탈로그 생성」을 함께 돌린다.
/// </summary>
public static class UniqueGearGenerator
{
    private const string Folder = Root + "/Unique";

    [MenuItem("Dokkaebi/Equipment/중간 보스 고유 장비 생성")]
    public static void Generate()
    {
        foreach (Row row in Rows())
            Write(row, Folder, isWeapon: false);

        AssetDatabase.SaveAssets();
        ItemCatalogBuilder.Build();
    }

    private static Row Gear(string boss, string name, string desc, ItemKind kind, EquipmentSlot slot, int tier,
        float weight, int durability, int value, params EquipmentStat[] stats)
        => new()
        {
            id = $"uniq_{boss}",
            name = name,
            desc = desc,
            kind = kind,
            slot = slot,
            tier = tier,
            weight = weight,
            durability = durability,
            value = value,
            stats = new List<EquipmentStat>(stats)
        };

    public static IEnumerable<Row> Rows()
    {
        // 1장 — 야광귀 (본문 211 「봇짐을 훔쳐」) · 달걀귀신 (부록 1184 「빗물에 번진 먹 글씨처럼 흐려져」)
        yield return Gear("yagwanggwi", "야광귀의 봇짐", "남의 것을 훔쳐 담던 봇짐. 넉넉하지만 덜그럭거린다.",
            ItemKind.Backpack, EquipmentSlot.Backpack, 1, 0.5f, 0, 600,
            S(EquipmentStatType.MaxCarryWeight, 7f), S(EquipmentStatType.SlotCapacity, 11),
            S(EquipmentStatType.MoveSoundRange, 0.15f));

        yield return Gear("dalgyal", "먹 번진 탈", "빗물에 번진 먹 글씨처럼 흐릿한 탈. 발소리가 묻히지만 귀도 먹먹해진다.",
            ItemKind.Armour, EquipmentSlot.Face, 1, 0.3f, 30, 500,
            S(EquipmentStatType.ResistCold, 0.08f), S(EquipmentStatType.ResistChaos, 0.08f),
            S(EquipmentStatType.MoveSoundRange, -0.15f), S(EquipmentStatType.Hearing, -0.1f));

        // 2장 — 어둑시니 (부록 1185 「그림자가 되어 댓잎 사이로」) · 강철이 (부록 1186 「강물을 막아」)
        yield return Gear("eodukssini", "어둑시니 삿갓", "그림자처럼 짙은 삿갓. 쓰면 기척이 줄지만 귀가 덮인다.",
            ItemKind.Armour, EquipmentSlot.Head, 2, 0.8f, 50, 1300,
            S(EquipmentStatType.HeadArmour, 2f), S(EquipmentStatType.MoveSoundRange, -0.2f),
            S(EquipmentStatType.Hearing, -0.1f));

        yield return Gear("gangcheori", "강철이 비늘 갑", "강물을 막고 똬리를 틀던 것의 비늘. 불에 강하지만 무겁다.",
            ItemKind.Armour, EquipmentSlot.Body, 2, 3.0f, 70, 2100,
            S(EquipmentStatType.BodyArmour, 3f), S(EquipmentStatType.ResistFire, 0.15f),
            S(EquipmentStatType.SlotCapacity, 2), S(EquipmentStatType.MoveAbility, -0.05f));

        // 3장 — 처녀귀신 · 몽달귀신 (부록 1187 「서로를 돌아본다」) · 두억시니 (부록 1188 「약탕기 조각 · 역병」)
        yield return Gear("wongwi", "원귀의 방울", "서로를 부르던 소리가 배어 있는 방울. 소리를 잘 듣지만 몸이 시리다.",
            ItemKind.Armour, EquipmentSlot.Ears, 3, 0.2f, 0, 2700,
            S(EquipmentStatType.Hearing, 0.45f), S(EquipmentStatType.SoundLocate, 0.4f),
            S(EquipmentStatType.MaxHealth, -5f));

        var tonic = Gear("dueoksini", "깨진 약탕기 탈", "깨진 약탕기 조각을 엮은 탈. 역병을 막지만 불에 약하다.",
            ItemKind.Armour, EquipmentSlot.Face, 3, 0.6f, 60, 3600,
            S(EquipmentStatType.ResistChaos, 0.25f), S(EquipmentStatType.ResistFire, -0.1f));
        tonic.immunity = StatusEffectType.Poison;
        yield return tonic;

        // 4장 — 두두리 (부록 1189 「고목 둥치처럼 다시 선다」) · 꺼먹살이 (부록 1178 「긁어낸다」)
        yield return Gear("duduri", "두두리 껍질 갑", "고목 둥치 같은 나무껍질 갑. 단단하지만 굼뜨다.",
            ItemKind.Armour, EquipmentSlot.Body, 4, 6.5f, 180, 9000,
            S(EquipmentStatType.BodyArmour, 5f), S(EquipmentStatType.ResistPhysical, 0.1f),
            S(EquipmentStatType.SlotCapacity, 1), S(EquipmentStatType.MoveAbility, -0.1f));

        yield return Gear("kkeomeoksari", "꺼먹살이 자루", "긁어 모은 것을 담던 검은 자루. 많이 들어가지만 걸음이 처진다.",
            ItemKind.Backpack, EquipmentSlot.Backpack, 4, 2.4f, 0, 9500,
            S(EquipmentStatType.MaxCarryWeight, 22f), S(EquipmentStatType.SlotCapacity, 20),
            S(EquipmentStatType.MoveAbility, -0.06f));

        // 5장 — 창귀 (부록 1190 「지름길이라며 꾀어」) · 산군 (부록 1191 「바랜 호랑이 그림에서 나온다」)
        yield return Gear("changgwi", "창귀의 부름", "산군에게 꾀어 가던 목소리가 들리는 것. 멀리서도 들리지만 내 발소리도 커진다.",
            ItemKind.Armour, EquipmentSlot.Ears, 5, 0.25f, 0, 12000,
            S(EquipmentStatType.Hearing, 0.7f), S(EquipmentStatType.SoundLocate, 0.6f),
            S(EquipmentStatType.MoveSoundRange, 0.2f));

        yield return Gear("sangun", "산군 범가죽 모자", "바랜 그림에서 나온 범의 가죽. 몸이 든든해지지만 기척을 숨기지 못한다.",
            ItemKind.Armour, EquipmentSlot.Head, 5, 3.0f, 130, 18000,
            S(EquipmentStatType.HeadArmour, 5.5f), S(EquipmentStatType.MaxHealth, 15f),
            S(EquipmentStatType.MoveSoundRange, 0.15f));

        // 6장 — 삼족오 (부록 1192 「동쪽 하늘로 날아오르고, 그쪽 하늘이 조금 희어진다」)
        yield return Gear("samjogo", "삼족오 깃 갑", "새벽빛이 도는 세 발 까마귀의 깃. 가볍고 불과 번개에 강하지만 추위에 약하다.",
            ItemKind.Armour, EquipmentSlot.Body, 6, 4.0f, 150, 30000,
            S(EquipmentStatType.BodyArmour, 6f), S(EquipmentStatType.ResistFire, 0.25f),
            S(EquipmentStatType.ResistLightning, 0.15f), S(EquipmentStatType.SlotCapacity, 3),
            S(EquipmentStatType.ResistCold, -0.15f));
    }
}
