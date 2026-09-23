using UnityEngine;

/// <summary>
/// 착용 장비와 가방 무게를 플레이어 컴포넌트가 읽을 숫자로 바꾼 결과. (로드맵 6-H)
///
/// MonoBehaviour 의존이 없는 순수 구조체다. 그래서
/// "티어 5 무기 + 진격 Ⅱ + 과중량이면 실제로 어떤 숫자가 나오는가"를
/// 게임을 켜지 않고 검증할 수 있다.
///
/// 이 클래스가 생긴 이유 —
/// 6-C에서 장비 82종을 만들었지만 게임은 프리팹의 하드코딩 값으로 쏘고 있었다.
/// 계산은 맞는데 아무도 부르지 않는 코드가 되기 쉬운 지점이라
/// 변환을 한 곳에 모으고 테스트로 고정한다.
/// </summary>
public readonly struct LoadoutSnapshot
{
    /// <summary>무기 + 보정이 합쳐진 최종 사격 성능.</summary>
    public readonly WeaponProfile Weapon;

    /// <summary>전투 공식에 넘길 방어 정보.</summary>
    public readonly DefenceProfile Defence;

    /// <summary>최대 체력. 각인이 깎으면 줄어든다.</summary>
    public readonly int MaxHealth;

    /// <summary>이동 속도 배율. 과중량과 장비 기동 옵션이 곱해진 값이다.</summary>
    public readonly float MoveScale;

    /// <summary>대시 거리 배율.</summary>
    public readonly float DashDistanceScale;

    /// <summary>대시 쿨다운 배율. 1보다 크면 길어진다.</summary>
    public readonly float DashCooldownScale;

    /// <summary>현재 과중량 단계. UI 표시에 쓴다.</summary>
    public readonly EncumbranceLevel Encumbrance;

    /// <summary>
    /// 움직일 때 내는 소리의 반경 배율. 【작을수록 좋다.】
    ///
    /// 덕코프에서 「안 뛰는 것만으로 기습을 피한다」가 성립하는 축이다.
    /// [확인됨 — research/duckov/08_전투_실측과_교전.md 3절]
    /// 이 값이 없던 동안 소음 관련 장비는 아무 일도 하지 않았다. (감사 A11)
    /// </summary>
    public readonly float MoveSoundScale;

    /// <summary>
    /// 소리를 듣는 거리 배율. 【클수록 좋다.】 이어폰 6종이 올린다.
    /// 적이 아니라 【플레이어】가 듣는 거리다 — 소리의 크기는 내는 쪽이 정한다.
    /// </summary>
    public readonly float HearingScale;

    /// <summary>소리의 방향을 표시해 주는가. 이어폰 상위 티어가 준다.</summary>
    public readonly bool LocatesSound;

    private LoadoutSnapshot(
        WeaponProfile weapon, DefenceProfile defence, int maxHealth,
        float moveScale, float dashDistanceScale, float dashCooldownScale,
        EncumbranceLevel encumbrance,
        float moveSoundScale, float hearingScale, bool locatesSound)
    {
        MoveSoundScale = moveSoundScale;
        HearingScale = hearingScale;
        LocatesSound = locatesSound;

        Weapon = weapon;
        Defence = defence;
        MaxHealth = maxHealth;
        MoveScale = moveScale;
        DashDistanceScale = dashDistanceScale;
        DashCooldownScale = dashCooldownScale;
        Encumbrance = encumbrance;
    }

    /// <summary>아무것도 착용하지 않고 가방도 비었을 때.</summary>
    public static LoadoutSnapshot Empty => new(
        WeaponProfile.Unarmed,
        DefenceProfile.None,
        CombatConstants.PlayerBaseHealth,
        1f, 1f, 1f,
        EncumbranceLevel.Normal,
        1f, 1f, false);

    /// <summary>
    /// 착용 상태와 무게 단계로부터 최종 수치를 만든다.
    ///
    /// 과중량을 별도 인자로 받는 이유 — 무게는 가방(Inventory)이 알고
    /// 착용은 Loadout이 안다. 둘을 여기서 합치면 양쪽 다 서로를 몰라도 된다.
    /// </summary>
    public static LoadoutSnapshot Create(EquipmentLoadout loadout, EncumbranceLevel encumbrance)
    {
        if (loadout == null)
            return Empty;

        EquipmentModifiers modifiers = loadout.Modifiers;

        var weapon = WeaponProfile.Create(
            loadout.GetDefinition(EquipmentSlot.Weapon) as WeaponDefinition, modifiers);

        // 최대 체력은 장비가 올리지 않는다. 각인만 깎는다.
        // (docs/Blob_Combat_Baseline.md 1절 — 체력은 100 고정)
        int maxHealth = Mathf.Clamp(
            CombatConstants.PlayerBaseHealth
                + Mathf.RoundToInt(modifiers.Get(EquipmentStatType.MaxHealth)),
            CombatConstants.PlayerMinHealth,
            CombatConstants.PlayerHealthCap);

        // 이동은 두 축이 곱해진다 — 장비가 준 기동 옵션과 지금 든 무게.
        // 가벼운 장비를 골라도 전리품을 가득 채우면 느려진다. 그 교환이 핵심이다.
        float moveScale =
            Mathf.Max(0.1f, 1f + modifiers.Get(EquipmentStatType.MoveAbility))
            * WeightCalculator.MoveMultiplier(encumbrance);

        float dashDistance = WeightCalculator.DashMultiplier(encumbrance);

        // 각인 「중장 Ⅲ」은 여기에 +99를 넣어 사실상 대시를 막는다.
        float dashCooldown = Mathf.Max(
            0.1f, 1f + modifiers.Get(EquipmentStatType.DashCooldown));

        // ── 소리 (감사 A11) ───────────────────────────────────────────
        // MoveSoundRange는 「작아야 좋은」 축이다. 음수를 주는 장비가
        // 소리를 줄인다. 0 아래로 내려가면 완전 무음이 되어 잠입이
        // 선택이 아니라 정답이 되므로 0.2를 바닥으로 둔다. 【불확실 — 문서에 수치 없음】
        float moveSound = Mathf.Clamp(
            1f + modifiers.Get(EquipmentStatType.MoveSoundRange), 0.2f, 3f);

        // 과중량은 소리도 키운다. 무겁게 들고 다니면 조용할 수 없다.
        // 이동 배율의 역수를 그대로 쓰지 않는다 — 그러면 과중량 하나로
        // 느려지고 시끄러워지고 대시까지 짧아져 벌이 세 겹이 된다.
        if (encumbrance == EncumbranceLevel.Heavy)
            moveSound *= 1.15f;
        else if (encumbrance == EncumbranceLevel.Overloaded)
            moveSound *= 1.3f;
        else if (encumbrance == EncumbranceLevel.Immobile)
            moveSound *= 1.5f;

        float hearing = Mathf.Max(0.1f, 1f + modifiers.Get(EquipmentStatType.Hearing));

        bool locates = modifiers.Get(EquipmentStatType.SoundLocate) > 0f;

        return new LoadoutSnapshot(
            weapon,
            modifiers.ToDefenceProfile(),
            maxHealth,
            moveScale,
            dashDistance,
            dashCooldown,
            encumbrance,
            moveSound,
            hearing,
            locates);
    }

    /// <summary>움직일 수 없는 상태인지. UI가 경고를 띄운다.</summary>
    public bool IsImmobile => Encumbrance == EncumbranceLevel.Immobile;
}
