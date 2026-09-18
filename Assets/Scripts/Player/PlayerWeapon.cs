using UnityEngine;

/// <summary>
/// 플레이어 사격 전담. 투사체는 오브젝트 풀에서 꺼낸다.
/// </summary>
public class PlayerWeapon : MonoBehaviour
{
    [Header("Projectile")]
    [SerializeField] private GameObject bulletPrefab;

    [Tooltip("총구 위치. 비워두면 자식 중 이름이 'FirePoint'인 오브젝트를 자동으로 사용한다.")]
    [SerializeField] private Transform firePoint;

    [Tooltip("firePoint가 비어 있을 때 찾을 자식 오브젝트 이름")]
    [SerializeField] private string firePointChildName = "FirePoint";

    [Header("Fire Rate")]
    [Tooltip("발사 간격(초). 작을수록 연사가 빠르다.")]
    [SerializeField] private float fireRate = 0.15f;

    [Header("Pooling")]
    [Tooltip("시작 시 미리 생성할 총알 수. (연사속도 x 총알수명) 이상이면 충분하다.")]
    [SerializeField] private int prewarmCount = 32;

    private readonly WeaponModifiers defaultModifiers = new();

    /// <summary>합성 발사의 적재 속성 순번. 적재 Core 2개일 때 번갈아 부여한다.</summary>
    private CompositeFireState compositeFire;

    private PoolManager poolManager;
    private CooldownTimer cooldown;

    /// <summary>Skill 보정이 적용된 실제 발사 간격.</summary>
    public float EffectiveFireInterval => fireRate * GetModifiers().FireIntervalMultiplier;

    /// <summary>발사 준비가 되었는지.</summary>
    public bool CanFire => cooldown.IsReady(Time.time);

    /// <summary>필수 참조가 모두 연결되었는지.</summary>
    public bool IsConfigured => bulletPrefab != null && firePoint != null;

    private void Start()
    {
        poolManager = PoolManager.EnsureInstance();

        if (bulletPrefab == null)
            GameLogger.Error("[PlayerWeapon] bulletPrefab이 할당되지 않았습니다.", this);

        if (firePoint == null)
        {
            // 명시적으로 할당하지 않으면 약속된 이름의 자식을 사용한다.
            // Inspector에서 직접 지정하면 이 탐색은 일어나지 않는다.
            firePoint = transform.Find(firePointChildName);

            if (firePoint == null)
                GameLogger.Error(
                    $"[PlayerWeapon] firePoint가 없습니다. Inspector에 지정하거나 " +
                    $"'{firePointChildName}' 이름의 자식 오브젝트를 두세요.", this);
            else
                GameLogger.Log($"[PlayerWeapon] firePoint 자동 연결: {firePointChildName}");
        }

        if (bulletPrefab != null)
            poolManager.Prewarm(bulletPrefab, prewarmCount);
    }

    /// <summary>발사를 시도한다. 연사 간격에 걸리거나 참조가 없으면 false.</summary>
    public bool TryFire()
    {
        if (!IsConfigured)
            return false;

        if (!cooldown.TryConsume(Time.time, EffectiveFireInterval))
            return false;

        Fire();

        return true;
    }

    private void Fire()
    {
        if (poolManager == null)
            poolManager = PoolManager.EnsureInstance();

        WeaponModifiers modifiers = GetModifiers();

        // 합성 발사: 적재 계열 Core가 2개면 발사마다 번갈아 부여한다. (확정 기획)
        // 투사체마다가 아니라 '발사마다'이므로 루프 밖에서 한 번만 고른다.
        StatusEffectType ailment = SelectAilment(modifiers);

        int count = modifiers.TotalProjectiles;

        // 여러 발이면 정면을 중심으로 좌우 대칭이 되도록 각도를 배분한다.
        float spread = modifiers.SpreadAngle;
        float startAngle = count > 1 ? -spread * (count - 1) * 0.5f : 0f;

        // 총구와 같은 높이의 발사자 중심. 캡슐 판정이 수평이 되도록 y를 맞춘다.
        var shooterCenter = new Vector3(
            transform.position.x, firePoint.position.y, transform.position.z);

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + spread * i;

            Quaternion rotation = firePoint.rotation * Quaternion.Euler(0f, angle, 0f);

            GameObject bullet = poolManager.Spawn(bulletPrefab, firePoint.position, rotation);

            if (bullet == null)
                continue;

            if (bullet.TryGetComponent(out BulletController controller))
            {
                controller.Configure(
                    modifiers.Behaviours,
                    modifiers.RicochetBounces,
                    modifiers.SpeedMultiplier,
                    modifiers.LifetimeMultiplier,
                    firePoint.position,
                    ailment);

                // 적이 몸에 붙으면 총구가 이미 적 안이라 트리거 진입이 발생하지 않는다.
                // 발사자 중심 → 총구 구간을 여기서 직접 판정한다.
                controller.ResolveMuzzleOverlap(shooterCenter);
            }
        }
    }

    /// <summary>이번 발사에 실을 적재 속성을 고른다. 적재 계열 Core가 없으면 None.</summary>
    private StatusEffectType SelectAilment(WeaponModifiers modifiers)
    {
        int index = compositeFire.NextAilmentIndex(modifiers.Ailments.Count);

        return index < 0 ? StatusEffectType.None : modifiers.Ailments[index];
    }

    /// <summary>현재 보유 Skill의 합산 결과. 없으면 기본값을 돌려준다.</summary>
    private WeaponModifiers GetModifiers()
    {
        if (SkillManager.HasInstance)
            return SkillManager.Instance.Build.GetModifiers();

        return defaultModifiers;
    }
}
