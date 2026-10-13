using UnityEngine;

/// <summary>
/// 【적 속성 넷의 실제 효과】 (결정 2-117 · Hunting_System 3절) — 지금까지 이름 · 프로필 값만 있고 몸에 닿지 않던 것.
///   · 재생 — 초당 최대 체력의 2% 회복 (판 특성 「되살아나는 잡귀」도 같은 값). 이야기 보스는 받지 않는다
///   · 껍질 — 첫 직접 피격 한 번 무효 (Health.ShellCharges)
///   · 분열성 — 쓰러지면 같은 유형 일반 몸 둘(체력 절반, 다시 분열하지 않음)
///   · 육중(통제 면역) — 동결 · 마비에 걸리지 않는다 (Health.ImmuneToIncapacitation)
/// EnemyIdentity.Apply가 프로필을 꽂을 때마다 붙이고 갱신한다(풀 재사용 포함).
/// </summary>
public class EnemyAffixEffects : MonoBehaviour
{
    private Health health;
    private EnemyProfile profile;
    private bool hasProfile;
    private float regenCarry;

    public static void ApplyTo(GameObject enemy, EnemyProfile profile)
    {
        if (enemy == null)
            return;

        if (!enemy.TryGetComponent(out EnemyAffixEffects effects))
            effects = enemy.AddComponent<EnemyAffixEffects>();

        effects.Bind(profile);
    }

    private void Bind(EnemyProfile p)
    {
        if (health == null)
        {
            health = GetComponent<Health>();
            if (health != null)
                health.OnDied += OnDied;
        }

        profile = p;
        hasProfile = true;
        regenCarry = 0f;

        if (health != null)
        {
            health.ShellCharges = p.hasShell ? 1 : 0;
            health.ImmuneToIncapacitation = p.immuneToControl;
        }
    }

    /// <summary>이번 판에 실제로 쓰는 재생 비율 — 이야기 보스는 0.</summary>
    public static float RegenFor(EnemyProfile p) => p.isStoryBoss ? 0f : p.regenPerSecond;

    private void Update()
    {
        if (health == null || !hasProfile)
            return;

        float amount = EnemyAffixTable.RegenAmount(health.Max, health.Current, RegenFor(profile), Time.deltaTime);
        if (amount <= 0f)
        {
            regenCarry = 0f;
            return;
        }

        regenCarry += amount;
        int whole = Mathf.FloorToInt(regenCarry);
        if (whole > 0)
        {
            regenCarry -= whole;
            health.Heal(whole);
        }
    }

    private void OnDied()
    {
        if (!hasProfile || !profile.splitsOnDeath)
            return;

        EnemyPrefabCatalog catalog = EnemyPrefabCatalog.Load();
        GameObject prefab = catalog != null ? catalog.Get(profile.archetype) : null;
        if (prefab == null)
            return;

        Vector3 origin = transform.position;
        for (int i = 0; i < EnemyAffixTable.SplitCount; i++)
        {
            float angle = (i * 360f / EnemyAffixTable.SplitCount + Random.Range(-30f, 30f)) * Mathf.Deg2Rad;
            Vector3 at = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.9f;
            if (!ZoneMap.CanSpawnAt(at))
                at = origin;

            GameObject child = PoolManager.EnsureInstance().Spawn(prefab, at, transform.rotation);
            if (child == null || !child.TryGetComponent(out EnemyIdentity identity))
                continue;

            // 다시 분열하지 않는 일반 몸 — 속성 없이, 판 특성만 얹는다.
            EnemyProfile split = EnemyProfile.Build(profile.archetype, EnemyRarity.Normal, System.Array.Empty<EnemyAffix>());
            split.health = Mathf.Max(1, Mathf.RoundToInt(split.health * EnemyAffixTable.SplitHealthScale));
            identity.Apply(RaidManager.HasInstance ? RaidManager.Current.Apply(split) : split);
        }
    }

    private void OnDestroy()
    {
        if (health != null)
            health.OnDied -= OnDied;
    }
}
