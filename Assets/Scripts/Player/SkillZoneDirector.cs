using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【기폭 · 잔류물을 실제로 일으킨다】 (Audit A5 · A6 · 결정 2-74). 플레이어에 붙는다.
///
///   · 원소 작렬 — 상태가 걸린 적이 죽거나 최대 중첩이 되면 그 상태를 소모해 터진다 (DetonationResolver)
///   · 충격파 — 주기마다 둘레의 적을 밀어내고, 잔류물과 걸린 상태를 바로 터뜨린다
///   · 중력 붕괴 — 주기마다 가장 가까운 적 자리에 우물을 만들어 끌어당기고 응집을 건다
///   · 잔류물 — 상태가 걸린 채 죽은 적의 자리에 남는다(Skill_System 4절) · 기폭이 남긴다 · 우물
///
/// 플레이어가 만든 것은 【적에게만】 효과가 있다. 수치는 SkillZoneTable [임시값].
/// 기폭 계열 핵심 젬이 꽂혀 있지 않으면 잔류물(사망 자리)만 돈다.
/// </summary>
public class SkillZoneDirector : MonoBehaviour
{
    private readonly GroundZoneSet grounds = new();
    private readonly Dictionary<GroundZone, GameObject> visuals = new();
    private readonly List<GroundZone> removed = new();
    private readonly List<Health> targets = new();
    private readonly List<Health> shockTargets = new();
    private readonly List<GroundZone> zoneSnapshot = new();
    private readonly List<PendingDetonation> pending = new();
    private readonly List<Push> pushes = new();
    private readonly HashSet<Health> detonatedThisFrame = new();

    private PlayerWeapon weapon;
    private float shockwaveTimer;
    private float gravityTimer;
    private float detonationReadyAt;
    private float pollTimer;

    private struct PendingDetonation
    {
        public Vector3 Position;
        public StatusEffectType Status;
        public int Stacks;
        public float At;
        public DetonationLoadout Loadout;
        public int ChainsLeft;
    }

    private struct Push
    {
        public Rigidbody Body;
        public Vector3 Velocity;
        public float Until;
    }

    public GroundZoneSet Grounds => grounds;

    private void Awake() => weapon = GetComponent<PlayerWeapon>();

    private void OnEnable() => EnemyController.Killed += HandleKilled;

    private void OnDisable()
    {
        EnemyController.Killed -= HandleKilled;
        ClearAll();
    }

    // ────────────────────────────────── 매 프레임

    private void Update()
    {
        if (!EnemyManager.HasInstance)
        {
            if (grounds.Count > 0)
                ClearAll();
            return;
        }

        float dt = Time.deltaTime;
        detonatedThisFrame.Clear();

        TickGrounds(dt);
        TickPending();
        TickPushes();

        SocketedBuild build = SkillManager.HasInstance ? SkillManager.Instance.Build : null;
        if (build == null)
            return;

        for (int c = 0; c < SocketedBuild.MaxCores; c++)
        {
            DetonationLoadout loadout = build.GetDetonationLoadout(c);
            if (!loadout.IsValid)
                continue;

            switch (loadout.CoreId)
            {
                case SkillZoneTable.ElementalBurstId: PollMaxStacks(loadout, dt); break;
                case SkillZoneTable.ShockwaveId:      TickShockwave(loadout, dt); break;
                case SkillZoneTable.GravityCollapseId: TickGravity(loadout, dt); break;
            }
        }
    }

    private float ShotDamage => weapon != null ? Mathf.Max(1f, weapon.Profile.Damage) : 10f;

    // ────────────────────────────────── 원소 작렬

    private DetonationLoadout BurstLoadout(out bool found)
    {
        found = false;
        SocketedBuild build = SkillManager.HasInstance ? SkillManager.Instance.Build : null;
        if (build == null)
            return default;

        for (int c = 0; c < SocketedBuild.MaxCores; c++)
        {
            DetonationLoadout l = build.GetDetonationLoadout(c);
            if (l.IsValid && l.CoreId == SkillZoneTable.ElementalBurstId)
            {
                found = true;
                return l;
            }
        }

        return default;
    }

    /// <summary>적이 죽었다 — 원소 작렬이 있으면 그 상태를 터뜨리고, 없으면 잔류물만 남긴다.</summary>
    private void HandleKilled(EnemyController enemy)
    {
        if (enemy == null || enemy.Health == null)
            return;

        StatusEffectState status = enemy.Health.Status;
        StatusEffectType picked = SkillZoneTable.PickDetonation(status);
        Vector3 at = enemy.transform.position;

        DetonationLoadout burst = BurstLoadout(out bool hasBurst);

        if (hasBurst && picked != StatusEffectType.None)
        {
            Queue(at, picked, burst.ConsumesAllStacks ? SkillZoneTable.StacksFor(status, picked) : 1, burst);
            return;
        }

        // 상태가 걸린 채 죽은 적의 자리에 잔류물이 남는다 (Skill_System 4절).
        GroundEffectType ground = GroundEffectTable.FromStatus(StrongestAilment(status));
        if (ground != GroundEffectType.None)
            SpawnGround(ground, at, SkillZoneTable.GroundRadius, SkillZoneTable.GroundDuration, ShotDamage);
    }

    private static StatusEffectType StrongestAilment(StatusEffectState status)
    {
        StatusEffectType picked = SkillZoneTable.PickDetonation(status);
        if (picked != StatusEffectType.None)
            return picked;

        return status != null && status.Has(StatusEffectType.Chill) ? StatusEffectType.Chill : StatusEffectType.None;
    }

    /// <summary>살아 있는 적이 최대 중첩에 닿으면 터진다. 0.1초마다 본다.</summary>
    private void PollMaxStacks(DetonationLoadout loadout, float dt)
    {
        pollTimer -= dt;
        if (pollTimer > 0f)
            return;
        pollTimer = 0.1f;

        IReadOnlyList<EnemyController> enemies = EnemyManager.Instance.ActiveEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyController e = enemies[i];
            if (e == null || e.Health == null || e.Health.IsDead)
                continue;

            if (!SkillZoneTable.AtMaxStacks(e.Health.Status, out StatusEffectType detonateAs,
                                             out StatusEffectType consume, out int fullStacks))
                continue;

            // 위험 상태를 소모한다 — 한 번 터지면 다시 쌓아야 한다.
            e.Health.Status.Clear(consume);
            Queue(e.transform.position, detonateAs, loadout.ConsumesAllStacks ? fullStacks : 1, loadout);
        }
    }

    private void Queue(Vector3 at, StatusEffectType status, int stacks, DetonationLoadout loadout)
    {
        pending.Add(new PendingDetonation
        {
            Position = at, Status = status, Stacks = Mathf.Max(1, stacks),
            At = Mathf.Max(Time.time + loadout.Fuse, detonationReadyAt),
            Loadout = loadout, ChainsLeft = loadout.Chains
        });
        detonationReadyAt = Mathf.Max(detonationReadyAt, Time.time) + loadout.Cooldown;
    }

    private void TickPending()
    {
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            if (pending[i].At > Time.time)
                continue;

            PendingDetonation p = pending[i];
            pending.RemoveAt(i);
            Explode(p.Position, p.Status, p.Stacks, p.Loadout, p.ChainsLeft);
        }
    }

    /// <summary>지연 중인 기폭을 바로 터뜨린다 (충격파).</summary>
    private void FlushPending()
    {
        for (int i = 0; i < pending.Count; i++)
        {
            PendingDetonation p = pending[i];
            p.At = Time.time;
            pending[i] = p;
        }

        TickPending();
    }

    private void Explode(Vector3 at, StatusEffectType status, int stacks, DetonationLoadout loadout, int chainsLeft)
    {
        DetonationResult result = DetonationResolver.Resolve(
            status, stacks, Mathf.RoundToInt(ShotDamage), loadout.IsValid ? loadout.RadiusMultiplier : 1f);

        if (!result.Detonated)
            return;

        int damage = Mathf.Max(1, Mathf.RoundToInt(result.Damage * (1f + (loadout.IsValid ? loadout.DamageIncrease : 0f))));
        Health farthest = null;
        float farthestSq = -1f;

        CollectEnemies(at, result.Radius, targets);
        foreach (Health target in targets)
        {
            // 응집된 적은 전이 범위가 넓다 — 확산은 대상의 SpreadMultiplier를 따른다.
            if (result.SpreadStatus != StatusEffectType.None)
                target.ApplyStatus(result.SpreadStatus, ShotDamage);

            Hit(target, damage, result.Element);

            float sq = (target.transform.position - at).sqrMagnitude;
            if (sq > farthestSq) { farthestSq = sq; farthest = target; }
        }

        Flash(at, result.Radius, ColorOf(result.Element));

        if (result.Ground != GroundEffectType.None)
            SpawnGround(result.Ground, at,
                SkillZoneTable.GroundRadius * (loadout.IsValid ? loadout.ZoneRadiusMultiplier : 1f),
                SkillZoneTable.GroundDuration * (loadout.IsValid ? loadout.DurationMultiplier : 1f),
                ShotDamage);

        // 연쇄 기폭 — 가장 멀리 맞은 적 자리에서 한 번 더 터진다.
        if (chainsLeft > 0 && farthest != null)
            Explode(farthest.transform.position, status, stacks, loadout, chainsLeft - 1);
    }

    /// <summary>터뜨린 상태를 지운다 — 위험 상태로 넘어가 있었으면 위험 상태를.</summary>
    private static void ConsumeFor(StatusEffectState status, StatusEffectType picked, int stacks)
    {
        if (picked == StatusEffectType.Poison && status.Has(StatusEffectType.Corrode)) status.Clear(StatusEffectType.Corrode);
        else if (picked == StatusEffectType.Shock && status.Has(StatusEffectType.Paralyze)) status.Clear(StatusEffectType.Paralyze);
        else status.RemoveStacks(picked, stacks);
    }

    // ────────────────────────────────── 충격파

    private void TickShockwave(DetonationLoadout loadout, float dt)
    {
        shockwaveTimer -= dt;
        if (shockwaveTimer > 0f)
            return;
        shockwaveTimer = SkillZoneTable.ShockwaveInterval + loadout.Cooldown - SkillZoneTable.DetonationCooldown;

        Vector3 center = transform.position;
        float radius = loadout.Radius(SkillZoneTable.ShockwaveRadius);

        // Explode가 targets를 다시 채우므로 따로 담는다.
        CollectEnemies(center, radius, shockTargets);
        foreach (Health target in shockTargets)
        {
            if (target == null || target.IsDead)
                continue;

            Vector3 away = target.transform.position - center;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = transform.forward;

            if (target.TryGetComponent(out Rigidbody body))
                pushes.Add(new Push
                {
                    Body = body,
                    Velocity = away.normalized * (SkillZoneTable.ShockwavePush / SkillZoneTable.ShockwavePushSeconds),
                    Until = Time.time + SkillZoneTable.ShockwavePushSeconds
                });

            // 걸린 상태를 바로 터뜨린다.
            StatusEffectType picked = SkillZoneTable.PickDetonation(target.Status);
            if (picked == StatusEffectType.None || detonatedThisFrame.Contains(target))
                continue;

            int stacks = loadout.ConsumesAllStacks ? SkillZoneTable.StacksFor(target.Status, picked) : 1;
            ConsumeFor(target.Status, picked, stacks);
            detonatedThisFrame.Add(target);
            Explode(target.transform.position, picked, stacks, loadout, loadout.Chains);
        }

        // 둘레의 잔류물도 터뜨린다 — 잔류물의 상태로 한 번 터지고 사라진다.
        zoneSnapshot.Clear();
        zoneSnapshot.AddRange(grounds.Zones);
        foreach (GroundZone zone in zoneSnapshot)
        {
            if (zone.IsExpired || (zone.Position - center).sqrMagnitude > radius * radius)
                continue;

            StatusEffectType st = SkillZoneTable.ZoneStatus(zone.Type);
            zone.Expire();

            if (st != StatusEffectType.None && st != StatusEffectType.Congeal && st != StatusEffectType.Chill)
                Explode(zone.Position, st, 1, loadout, 0);
        }

        FlushPending();
        Flash(center, radius, new Color(0.85f, 0.85f, 1f));
    }

    private void TickPushes()
    {
        for (int i = pushes.Count - 1; i >= 0; i--)
        {
            Push p = pushes[i];
            if (p.Body == null || Time.time > p.Until)
            {
                pushes.RemoveAt(i);
                continue;
            }

            p.Body.MovePosition(p.Body.position + p.Velocity * Time.deltaTime);
        }
    }

    // ────────────────────────────────── 중력 붕괴

    private void TickGravity(DetonationLoadout loadout, float dt)
    {
        gravityTimer -= dt;
        if (gravityTimer > 0f)
            return;

        Health nearest = NearestEnemy(transform.position, SkillZoneTable.GravitySearchRange);
        if (nearest == null)
            return; // 적이 없으면 기다린다 — 빈 땅에 우물을 만들지 않는다

        gravityTimer = SkillZoneTable.GravityInterval;
        SpawnGround(GroundEffectType.GravityWell, nearest.transform.position,
            SkillZoneTable.GravityRadius * loadout.ZoneRadiusMultiplier,
            SkillZoneTable.GravityDuration * loadout.DurationMultiplier,
            ShotDamage);
    }

    // ────────────────────────────────── 잔류물

    private void SpawnGround(GroundEffectType type, Vector3 at, float radius, float duration, float sourceDamage)
    {
        GroundZone zone = grounds.Add(new GroundZone(type, at, radius, duration, sourceDamage), out GroundZone evicted);
        if (zone == null)
            return;

        if (evicted != null)
            DestroyVisual(evicted);

        visuals[zone] = MakeDisc($"Ground_{type}", at, zone.Radius, ColorOf(type), 0.35f);
    }

    private void TickGrounds(float dt)
    {
        foreach (GroundZone zone in grounds.Zones)
        {
            // 우물은 매 프레임 끌어당긴다.
            if (zone.Type == GroundEffectType.GravityWell)
                PullInto(zone, dt);

            if (!zone.Advance(dt))
                continue;

            StatusEffectType status = SkillZoneTable.ZoneStatus(zone.Type);
            if (status == StatusEffectType.None)
                continue;

            CollectEnemies(zone.Position, zone.Radius, targets);
            foreach (Health target in targets)
                target.ApplyStatus(status, zone.SourceDamage);
        }

        removed.Clear();
        grounds.RemoveExpired(removed);
        foreach (GroundZone zone in removed)
            DestroyVisual(zone);
    }

    private void PullInto(GroundZone zone, float dt)
    {
        CollectEnemies(zone.Position, zone.Radius, targets);
        foreach (Health target in targets)
        {
            Vector3 to = zone.Position - target.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.04f)
                continue;

            Vector3 delta = to.normalized * Mathf.Min(to.magnitude, SkillZoneTable.GravityPullSpeed * dt);
            if (target.TryGetComponent(out Rigidbody body))
                body.MovePosition(body.position + delta);
            else
                target.transform.position += delta;
        }
    }

    // ────────────────────────────────── 도움

    /// <summary>둘레의 살아 있는 적. 【적에게만】 — 플레이어가 만든 잔류물 · 폭발은 플레이어를 다치게 하지 않는다.</summary>
    private static void CollectEnemies(Vector3 center, float radius, List<Health> into)
    {
        into.Clear();
        if (!EnemyManager.HasInstance)
            return;

        float sq = radius * radius;
        IReadOnlyList<EnemyController> enemies = EnemyManager.Instance.ActiveEnemies;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyController e = enemies[i];
            if (e == null || e.Health == null || e.Health.IsDead)
                continue;

            Vector3 d = e.transform.position - center;
            d.y = 0f;
            if (d.sqrMagnitude <= sq)
                into.Add(e.Health);
        }
    }

    private static Health NearestEnemy(Vector3 from, float range)
    {
        Health best = null;
        float bestSq = range * range;
        IReadOnlyList<EnemyController> enemies = EnemyManager.Instance.ActiveEnemies;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyController e = enemies[i];
            if (e == null || e.Health == null || e.Health.IsDead)
                continue;

            float sq = (e.transform.position - from).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = e.Health; }
        }

        return best;
    }

    private static void Hit(Health target, int damage, DamageElement element)
    {
        var request = new DamageRequest
        {
            baseDamage = damage,
            element = element,
            hitKind = HitKind.Melee, // 폭발 → 몸통 방어도
            effectiveRange = 999f,
            distance = 0f,
            bypassArmour = false
        };

        target.TakeDamage(request);
    }

    private void ClearAll()
    {
        foreach (GameObject go in visuals.Values)
            if (go != null) Destroy(go);

        visuals.Clear();
        grounds.Clear();
        pending.Clear();
        pushes.Clear();
    }

    private void DestroyVisual(GroundZone zone)
    {
        if (visuals.TryGetValue(zone, out GameObject go) && go != null)
            Destroy(go);
        visuals.Remove(zone);
    }

    private static GameObject MakeDisc(string name, Vector3 at, float radius, Color color, float alpha)
    {
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = name;
        Destroy(disc.GetComponent<Collider>());
        disc.transform.position = new Vector3(at.x, 0.03f, at.z);
        disc.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);

        var renderer = disc.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material m = renderer.material;
            color.a = alpha;
            m.color = color;
            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", color);
        }

        return disc;
    }

    private static void Flash(Vector3 at, float radius, Color color)
    {
        GameObject disc = MakeDisc("Detonation", at, radius, color, 0.6f);
        Destroy(disc, 0.2f);
    }

    private static Color ColorOf(GroundEffectType type)
    {
        switch (type)
        {
            case GroundEffectType.FireZone:    return new Color(1f, 0.45f, 0.15f);
            case GroundEffectType.ToxicSwamp:  return new Color(0.45f, 0.8f, 0.2f);
            case GroundEffectType.FrostField:  return new Color(0.6f, 0.85f, 1f);
            case GroundEffectType.BloodZone:   return new Color(0.7f, 0.1f, 0.15f);
            case GroundEffectType.GravityWell: return new Color(0.45f, 0.3f, 0.75f);
            default:                           return Color.white;
        }
    }

    private static Color ColorOf(DamageElement element)
    {
        switch (element)
        {
            case DamageElement.Fire:      return new Color(1f, 0.45f, 0.15f);
            case DamageElement.Cold:      return new Color(0.6f, 0.85f, 1f);
            case DamageElement.Lightning: return new Color(1f, 0.95f, 0.4f);
            case DamageElement.Chaos:     return new Color(0.45f, 0.8f, 0.2f);
            default:                      return new Color(0.8f, 0.2f, 0.2f);
        }
    }
}
