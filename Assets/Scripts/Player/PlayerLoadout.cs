using UnityEngine;

/// <summary>
/// 착용 장비를 실제 플레이어 성능에 반영하는 배선. (로드맵 6-H)
///
/// 이 컴포넌트가 하는 일은 하나다 —
/// 【LoadoutSnapshot을 만들어 각 전담 컴포넌트에 나눠주는 것.】
/// 계산은 전부 LoadoutSnapshot(순수)에 있고 여기에는 규칙이 없다.
///
/// 왜 필요했는가 —
/// 6-C에서 장비 82종을 만들었지만 PlayerWeapon은 프리팹 하드코딩 값으로 쏘고,
/// 과중량은 UI 문구만 바꾸고, 방어도는 아무 데도 전달되지 않았다.
/// 전부 「코드는 있는데 부르는 곳이 없는」 상태였다. (docs/Blob_Audit.md A절)
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerLoadout : MonoBehaviour
{
    [Tooltip("장비 변경을 확인하는 주기(초). 0이면 매 프레임 확인한다.")]
    [Min(0f)]
    [SerializeField] private float refreshInterval = 0.25f;

    private Health health;
    private PlayerWeapon weapon;
    private PlayerMovement movement;
    private PlayerDash dash;

    private float nextRefresh;

    /// <summary>마지막으로 적용한 값. UI와 테스트가 읽는다.</summary>
    public LoadoutSnapshot Current { get; private set; } = LoadoutSnapshot.Empty;

    private void Awake()
    {
        health = GetComponent<Health>();
        weapon = GetComponent<PlayerWeapon>();
        movement = GetComponent<PlayerMovement>();
        dash = GetComponent<PlayerDash>();
    }

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        // 장비는 자주 바뀌지 않는다. 매 프레임 합산할 이유가 없다.
        if (Time.unscaledTime < nextRefresh)
            return;

        nextRefresh = Time.unscaledTime + refreshInterval;

        Refresh();
    }

    /// <summary>
    /// 지금 착용 상태와 가방 무게를 읽어 전부 다시 적용한다.
    /// 장비를 바꾸는 UI가 즉시 반영을 원하면 직접 호출한다.
    /// </summary>
    public void Refresh()
    {
        PlayerInventory inventory = PlayerInventory.EnsureInstance();

        LoadoutSnapshot snapshot = LoadoutSnapshot.Create(
            inventory.Loadout, inventory.Encumbrance);

        // 면역은 수치가 아니라 목록이라 스냅샷에 담지 않는다.
        // 참조를 넘겨 두면 장비가 바뀌는 즉시 반영된다.
        if (health != null)
            health.SetImmunitySource(inventory.Loadout.Modifiers);

        Apply(snapshot);
    }

    /// <summary>스냅샷을 각 컴포넌트에 나눠준다. 테스트가 직접 부를 수 있다.</summary>
    public void Apply(in LoadoutSnapshot snapshot)
    {
        Current = snapshot;

        if (weapon != null)
            weapon.ApplyProfile(snapshot.Weapon);

        if (movement != null)
            movement.SpeedScale = snapshot.MoveScale;

        if (dash != null)
        {
            dash.DistanceScale = snapshot.DashDistanceScale;
            dash.CooldownScale = snapshot.DashCooldownScale;
        }

        if (health == null)
            return;

        health.SetDefence(
            snapshot.Defence.headArmour,
            snapshot.Defence.bodyArmour,
            snapshot.Defence.resistances);

        // 최대 체력이 줄어드는 각인을 뺐을 때 회복시키지 않는다.
        // 상한만 되돌리고 현재 체력은 그대로 둔다 — 각인 교체가 회복 수단이 되면 안 된다.
        if (health.Max != snapshot.MaxHealth)
            health.SetMaxHealth(snapshot.MaxHealth);
    }
}
