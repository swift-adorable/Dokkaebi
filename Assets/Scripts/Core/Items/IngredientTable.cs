using System.Collections.Generic;

/// <summary>요리 재료 한 가지 — 어느 장에서 나오는가 · 값 · 무게 (결정 2-73).</summary>
public sealed class Ingredient
{
    public readonly string Id;
    public readonly string Name;
    public readonly string Description;

    /// <summary>나오는 장. 0 = 공용(1~6장 어디서나 · 0장 포함).</summary>
    public readonly int Chapter;

    public readonly int Value;
    public readonly float Weight;
    public readonly int StackMax;

    /// <summary>전리품 표의 가중치. 장이 맞지 않으면 그 판에서는 빠진다.</summary>
    public readonly int DropWeight;

    public readonly int MinCount;
    public readonly int MaxCount;

    /// <summary>산적의 「고기 아무거나」에 들어가는가.</summary>
    public readonly bool IsMeat;

    /// <summary>이미 있는 아이템을 재료로도 쓴다(생강 = 동결 해제약). 생성기가 새로 만들지 않는다.</summary>
    public readonly bool Existing;

    public Ingredient(string id, string name, string description, int chapter, int value, float weight,
                      int stackMax, int dropWeight, int minCount = 1, int maxCount = 1,
                      bool isMeat = false, bool existing = false)
    {
        Id = id;
        Name = name;
        Description = description;
        Chapter = chapter;
        Value = value;
        Weight = weight;
        StackMax = stackMax;
        DropWeight = dropWeight;
        MinCount = minCount;
        MaxCount = maxCount < minCount ? minCount : maxCount;
        IsMeat = isMeat;
        Existing = existing;
    }

    public bool IsCommon => Chapter == IngredientTable.Common;
}

/// <summary>
/// 【요리 재료 표.】 공용 재료는 어느 장에서나, 장 재료는 그 장(과 같은 계절의 뒤 장)에서만 떨어진다 (결정 2-73).
///
///   · 몬스터와 묶지 않는다 — 그 장 안이면 어느 적에게서나 나온다.
///   · 같은 계절끼리 함께 — 5장(겨울)에서 1장 재료도, 6장(봄)에서 2장 재료도 나온다. 거꾸로는 아니다.
///   · 효과가 작은 음식은 공용 재료만으로 만든다 — 어느 장에서든 배를 채울 수 있다.
///   · 물은 고목 뿌리 우물에서 떠 오는 물병(con_water)이다 — 재료 표가 아니라 SpringTable이 맡는다.
///
/// 이름 · 값 · 무게 · 가중치는 [임시값]. 원본 문서는 docs/Dokkaebi_Cooking_System.md.
/// </summary>
public static class IngredientTable
{
    public const int Common = 0;

    /// <summary>산적에 쓰는 「고기 아무거나」. 카탈로그에 없는 묶음 id다.</summary>
    public const string MeatTag = "tag_meat";

    public const string Rice = "food_rice";
    public const string Malt = "food_malt";
    public const string Persimmon = "food_persimmon";
    public const string Chestnut = "food_chestnut";
    public const string Potato = "food_potato";
    public const string SweetPotato = "food_sweet_potato";
    public const string Berry = "food_berry";

    public const string RiceCake = "food_rice_cake";
    public const string Radish = "food_radish";
    public const string Ginger = "con_defroster";
    public const string Barley = "food_barley";
    public const string GlutinousRice = "food_glutinous_rice";
    public const string Chicken = "food_chicken";
    public const string Ginseng = "food_ginseng";
    public const string Bean = "food_bean";
    public const string Jujube = "food_jujube";
    public const string Acorn = "food_acorn";
    public const string Pumpkin = "food_pumpkin";
    public const string Pheasant = "food_pheasant";
    public const string Wheat = "food_wheat";
    public const string Sesame = "food_sesame";
    public const string Venison = "food_venison";
    public const string Boar = "food_boar";
    public const string Petal = "food_petal";
    public const string Honey = "food_honey";

    private static Ingredient I(string id, string name, string desc, int chapter, int value, float weight,
                                int dropWeight, int max = 2, bool meat = false)
        => new(id, name, desc, chapter, value, weight, meat ? 5 : 10, dropWeight, 1, max, meat);

    private static readonly Ingredient[] all =
    {
        // ── 공용 — 1~6장 어디서나. 말리거나 갈무리해 둔 것이라 계절과 부딪히지 않는다.
        I(Rice,        "쌀",     "한 줌의 쌀. 누룽지도 식혜도 여기서 시작한다.",   Common, 15, 0.3f,  6),
        I(Malt,        "엿기름", "싹 틔운 보리를 말린 것. 엿과 식혜를 삭힌다.",     Common, 12, 0.2f,  4, 1),
        I(Persimmon,   "감",     "떫은 감. 말리면 곶감이 된다.",                    Common, 15, 0.25f, 4),
        I(Chestnut,    "밤",     "가시를 벗긴 밤. 구우면 군밤이다.",                Common, 15, 0.2f,  4),
        I(Potato,      "감자",   "흙이 묻은 감자. 불에 묻어 두면 익는다.",          Common, 12, 0.3f,  4),
        I(SweetPotato, "고구마", "달다. 구우면 더 달다.",                           Common, 15, 0.3f,  4),
        // 산열매는 몬스터가 떨어뜨리지 않는다 — 구역의 열매 나무에서 딴다 (결정 2-76 · BerryTree). 비중 0 = 전리품 표에 없음.
        I(Berry,       "산열매", "오디 · 오미자 · 산수유 · 머루 — 나무에서 딴 작은 열매.", Common, 15, 0.1f, 0),

        // ── 1장 · 겨울 · 비 오는 폐장터
        I(RiceCake,    "가래떡", "좌판에 남아 있던 떡. 굳었지만 끓이면 풀어진다.",  1, 20, 0.4f, 3),
        I(Radish,      "무",     "겨울 무. 국물을 시원하게 한다.",                  1, 15, 0.5f, 3, 1),
        new(Ginger,    "생강",   "동결 해제약. 부뚜막에서는 수정과에 들어간다.",    1, 90, 0.1f, 3, 2, 1, 1, existing: true),

        // ── 2장 · 봄 · 대숲과 물레방아
        I(Barley,      "보리",   "볶으면 고소하다. 미숫가루가 된다.",               2, 15, 0.3f, 4),
        I(GlutinousRice, "찹쌀", "찰기가 있는 쌀. 떡과 과줄에 쓴다.",              2, 15, 0.3f, 4),

        // ── 3장 · 여름 · 약방골
        I(Chicken,     "닭고기", "삼계탕에 넣는다.",                                3, 25, 0.6f, 3, 1, meat: false),
        I(Ginseng,     "인삼",   "약방골에서 말리던 뿌리. 삼계탕에 넣는다.",        3, 40, 0.1f, 2, 1),

        // ── 4장 · 가을 · 장승 벌판
        I(Bean,        "콩",     "송편의 소가 된다.",                               4, 15, 0.3f, 3),
        I(Jujube,      "대추",   "말린 대추. 달여 마시고, 약식에도 넣는다.",        4, 20, 0.1f, 3),
        I(Acorn,       "도토리", "가루를 내어 쑤면 묵이 된다.",                     4, 15, 0.3f, 3),
        I(Pumpkin,     "늙은호박", "누렇게 익은 호박. 죽을 쑨다.",                  4, 20, 1.0f, 2, 1),
        I(Pheasant,    "꿩고기", "꼬챙이에 꿰어 굽는다.",                           4, 25, 0.6f, 2, 1, meat: true),

        // ── 5장 · 겨울 · 궁궐 문 돌길 · 산허리 (제사)
        I(Wheat,       "밀",     "가루를 내어 약과를 빚는다.",                      5, 15, 0.3f, 3),
        I(Sesame,      "깨",     "기름을 짜면 고소하다.",                           5, 15, 0.1f, 3),
        I(Venison,     "사슴고기", "꼬챙이에 꿰어 굽는다.",                         5, 25, 0.6f, 2, 1, meat: true),
        I(Boar,        "산돼지고기", "꼬챙이에 꿰어 굽는다.",                       5, 25, 0.6f, 2, 1, meat: true),

        // ── 6장 · 봄 · 꽃밭
        I(Petal,       "꽃잎",   "진달래 꽃잎. 찹쌀 반죽에 얹어 부친다.",           6, 15, 0.05f, 3),
        I(Honey,       "꿀",     "꽃밭의 꿀. 물에 타도, 약식에 넣어도 좋다.",       6, 25, 0.3f, 2, 1),
    };

    public static IReadOnlyList<Ingredient> All => all;

    public static Ingredient Find(string id)
    {
        foreach (Ingredient i in all)
            if (i.Id == id)
                return i;

        return null;
    }

    /// <summary>묶음 id(산적의 고기)라면 그 안의 재료들, 아니면 그 하나.</summary>
    public static IReadOnlyList<string> Members(string id)
    {
        if (id != MeatTag)
            return new[] { id };

        var list = new List<string>();
        foreach (Ingredient i in all)
            if (i.IsMeat)
                list.Add(i.Id);

        return list;
    }

    public static bool IsTag(string id) => id == MeatTag;

    /// <summary>화면에 적을 이름. 묶음은 「고기(꿩 · 사슴 · 산돼지)」.</summary>
    public static string NameOf(string id)
    {
        if (id == MeatTag)
            return "고기(꿩 · 사슴 · 산돼지)";

        return Find(id)?.Name;
    }

    /// <summary>
    /// 이 아이템이 이 장의 전리품에서 나올 수 있는가. 재료가 아닌 것은 늘 나온다.
    /// 공용은 어디서나 · 장 재료는 그 장과, 같은 계절인 뒤 장에서(결정 2-73 — 1 → 5 겨울 · 2 → 6 봄).
    /// </summary>
    public static bool DropsIn(string itemId, int chapter)
    {
        Ingredient i = Find(itemId);

        if (i == null || i.IsCommon || i.Chapter == chapter)
            return true;

        if (chapter < 1 || i.Chapter > chapter)
            return false;

        return SeasonOf(i.Chapter) == SeasonOf(chapter);
    }

    /// <summary>이 장에서 나오는 장 재료(공용 제외).</summary>
    public static List<Ingredient> ChapterOnly(int chapter)
    {
        var list = new List<Ingredient>();
        foreach (Ingredient i in all)
            if (!i.IsCommon && DropsIn(i.Id, chapter))
                list.Add(i);

        return list;
    }

    private static Season SeasonOf(int chapter)
    {
        ChapterData c = ZoneDataTable.Chapter(chapter);
        return c != null ? c.Season : Season.Spring;
    }
}
