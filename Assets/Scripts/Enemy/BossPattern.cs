using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【보스 17의 고유 패턴】 (결정 2-110 · BossPatternTable) — 이야기 보스 몸에 붙는다(StoryRaidDirector).
/// 보통 공격(EnemyAttack)은 그대로 두고, 그 위에 보스마다의 행동을 얹는다.
///   · 위험 자리는 붉은 원으로 먼저 보이고(TelegraphSeconds) 그다음 맞는다 — 피할 수 있게
///   · 받는 피해는 Health.IncomingDamageScale로 바꾼다 — 흐려짐 · 뿌리내림 · 함께 섬
/// 죽거나 풀로 돌아가면 스스로 지워진다. 모양 · 수치는 [임시값].
/// </summary>
public class BossPattern : MonoBehaviour
{
    private static readonly List<BossPattern> active = new();

    private string bossId;
    private BossPatternKind kind;
    private Health health;
    private EnemyMovement movement;
    private Rigidbody body;
    private Vector3 baseScale;
    private float growScale = 1f;
    private int formsPassed;
    private bool retreated;
    private readonly List<GameObject> decoys = new();
    private GameObject soulMark;

    public string BossId => bossId;

    public static void Attach(GameObject target, string bossId)
    {
        BossPatternKind kind = BossPatternTable.KindOf(bossId);
        if (target == null || kind == BossPatternKind.None)
            return;

        if (!target.TryGetComponent(out BossPattern pattern))
            pattern = target.AddComponent<BossPattern>();

        pattern.bossId = bossId;
        pattern.kind = kind;
        pattern.Begin();
    }

    private void Begin()
    {
        health = GetComponent<Health>();
        movement = GetComponent<EnemyMovement>();
        body = GetComponent<Rigidbody>();
        baseScale = transform.localScale;
        growScale = 1f;
        formsPassed = 0;
        retreated = false;

        if (health != null)
            health.OnDied += End;

        active.Add(this);
        StopAllCoroutines();
        StartCoroutine(Loop());
    }

    private void End()
    {
        if (health != null)
        {
            health.OnDied -= End;
            health.IncomingDamageScale = 1f;
        }

        if (movement != null)
        {
            movement.IsHalted = false;
            movement.SpeedScale = 1f;
        }

        transform.localScale = baseScale;
        ClearDecoys();
        if (soulMark != null)
            Destroy(soulMark);

        active.Remove(this);
        Destroy(this);
    }

    private void OnDisable()
    {
        // 풀로 돌아갈 때 — 다음에 일반 적으로 나오지 않게.
        if (active.Contains(this))
            End();
    }

    private static Transform Player => QuestSpotDirector.Player;

    private int Damage => TryGetComponent(out EnemyAttack attack) ? Mathf.Max(1, attack.Damage) : 10;

    private bool Alive => health != null && !health.IsDead && isActiveAndEnabled;

    // ────────────────────────────────── 고리

    private IEnumerator Loop()
    {
        yield return new WaitForSeconds(1.5f);

        while (Alive)
        {
            if (Player == null)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            switch (kind)
            {
                case BossPatternKind.Thief:        yield return Thief(); break;
                case BossPatternKind.Fade:         yield return Fade(); break;
                case BossPatternKind.GrowWhenSeen: yield return Grow(); break;
                case BossPatternKind.CoilLunge:    yield return CoilLunge(); break;
                case BossPatternKind.Pair:         yield return Pair(); break;
                case BossPatternKind.PlagueKick:   yield return PlagueKick(); break;
                case BossPatternKind.Rooted:       yield return Rooted(); break;
                case BossPatternKind.Claw:         yield return Claw(); break;
                case BossPatternKind.Lure:         yield return Lure(); break;
                case BossPatternKind.Pounce:       yield return Pounce(); break;
                case BossPatternKind.Dive:         yield return Dive(); break;
                case BossPatternKind.IceNeedles:   yield return IceNeedles(); break;
                case BossPatternKind.Thunder:      yield return Thunder(); break;
                case BossPatternKind.FireRing:     yield return FireRing(); break;
                case BossPatternKind.Roar:         yield return Roar(); break;
                case BossPatternKind.HornCharge:   yield return HornCharge(); break;
                case BossPatternKind.Shapeshift:   yield return Shapeshift(); break;
                default: yield break;
            }
        }
    }

    private WaitForSeconds Rest() => new(BossPatternTable.Interval(kind));

    // ────────────────────────────────── 1장

    /// <summary>야광귀 — 가까이 오면 달아났다가, 멈춰 서서 숨을 고른다.</summary>
    private IEnumerator Thief()
    {
        if (Flat(Player.position - transform.position).magnitude < 6f)
        {
            yield return Move(Flat(transform.position - Player.position).normalized, 7f, 1.2f);
            if (movement != null) movement.IsHalted = true;
            yield return new WaitForSeconds(2.5f);
            if (movement != null) movement.IsHalted = false;
        }
        yield return new WaitForSeconds(0.3f);
    }

    /// <summary>달걀귀신 — 흐려졌다가(거의 맞지 않음) 플레이어 등 뒤로 돌아서 다가온다.</summary>
    private IEnumerator Fade()
    {
        yield return Rest();
        if (!Alive) yield break;

        health.IncomingDamageScale = 0.25f;
        SetAlpha(0.3f);
        yield return new WaitForSeconds(1.2f);

        Vector3 behind = Player.position - Flat(Player.forward).normalized * 3.5f;
        if (ZoneMap.CanSpawnAt(behind))
            Teleport(behind);

        SetAlpha(1f);
        health.IncomingDamageScale = 1f;
    }

    // ────────────────────────────────── 2장

    /// <summary>어둑시니 — 바라보면 커지고 단단해진다. 눈을 돌리면 줄어든다.</summary>
    private IEnumerator Grow()
    {
        Vector3 toBoss = Flat(transform.position - Player.position);
        bool seen = toBoss.sqrMagnitude > 0.01f && Vector3.Dot(Flat(Player.forward).normalized, toBoss.normalized) > BossPatternTable.SeenDot;

        growScale = BossPatternTable.NextScale(growScale, seen, 0.2f);
        transform.localScale = baseScale * growScale;
        health.IncomingDamageScale = BossPatternTable.GrowDamageTaken(growScale);
        yield return new WaitForSeconds(0.2f);
    }

    /// <summary>강철이 — 똬리를 틀고(멈춤) 둘레를 꼬리로 쓴 뒤 덮친다.</summary>
    private IEnumerator CoilLunge()
    {
        yield return Rest();
        if (!Alive) yield break;

        if (movement != null) movement.IsHalted = true;
        yield return Telegraph(transform.position, 4f, DamageElement.Physical, Damage, StatusEffectType.None);
        if (movement != null) movement.IsHalted = false;

        yield return Move(Flat(Player.position - transform.position).normalized, 8f, 0.4f);
    }

    // ────────────────────────────────── 3장

    /// <summary>처녀귀신 · 몽달귀신 — 짝이 곁에 살아 있으면 단단하다. 떼어 놓고 싸운다.</summary>
    private IEnumerator Pair()
    {
        bool near = false;
        foreach (BossPattern other in active)
            if (other != this && other.bossId == bossId && other.Alive
                && Flat(other.transform.position - transform.position).magnitude <= BossPatternTable.PairRange)
                near = true;

        health.IncomingDamageScale = BossPatternTable.PairDamageTaken(near);
        yield return new WaitForSeconds(0.3f);
    }

    /// <summary>두억시니 — 약탕기를 걷어차 역병 김을 퍼뜨린다(중독).</summary>
    private IEnumerator PlagueKick()
    {
        yield return Rest();
        if (!Alive) yield break;

        for (int i = 0; i < 3; i++)
        {
            Vector3 at = Player.position + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
            StartCoroutine(Telegraph(at, 2.5f, DamageElement.Chaos, Damage / 2, StatusEffectType.Poison, 3));
        }
        yield return new WaitForSeconds(BossPatternTable.TelegraphSeconds);
    }

    // ────────────────────────────────── 4장

    /// <summary>두두리 — 장승 사이에 고목 둥치처럼 선다(멈춤 · 거의 맞지 않음), 그다음 들이받는다.</summary>
    private IEnumerator Rooted()
    {
        yield return Rest();
        if (!Alive) yield break;

        if (movement != null) movement.IsHalted = true;
        health.IncomingDamageScale = 0.3f;
        yield return new WaitForSeconds(3f);
        health.IncomingDamageScale = 1f;
        if (movement != null) movement.IsHalted = false;

        yield return Move(Flat(Player.position - transform.position).normalized, 9f, 0.5f);
    }

    /// <summary>꺼먹살이 — 앞을 긁는다(출혈).</summary>
    private IEnumerator Claw()
    {
        yield return Rest();
        if (!Alive) yield break;

        Vector3 front = transform.position + Flat(Player.position - transform.position).normalized * 2f;
        yield return Telegraph(front, 2.2f, DamageElement.Physical, Damage, StatusEffectType.Bleed, 2);
    }

    // ────────────────────────────────── 5장

    /// <summary>창귀 — 거리를 두며 산군이 나오는 쪽으로 꾄다.</summary>
    private IEnumerator Lure()
    {
        float d = Flat(Player.position - transform.position).magnitude;
        if (d < 7f)
        {
            Vector3 away = Flat(transform.position - Player.position).normalized;
            List<Vector3> spots = ZoneMap.Anchors(MapAnchorKind.Boss);
            if (spots.Count > 1)
                away = (away + Flat(spots[1] - transform.position).normalized).normalized;
            yield return Move(away, 4f, 0.8f);
        }
        yield return new WaitForSeconds(0.4f);
    }

    /// <summary>산군 — 덮친다. 몰리면 한 번 그림 속으로 돌아가 숨을 고른다.</summary>
    private IEnumerator Pounce()
    {
        if (!retreated && health.Normalized <= BossPatternTable.RetreatBelow)
        {
            retreated = true;
            health.IncomingDamageScale = 0f;
            SetAlpha(0.15f);
            List<Vector3> spots = ZoneMap.Anchors(MapAnchorKind.Boss);
            if (spots.Count > 1)
                Teleport(spots[1]);
            yield return new WaitForSeconds(2.5f);
            health.Heal(Mathf.RoundToInt(health.Max * BossPatternTable.RetreatHeal));
            SetAlpha(1f);
            health.IncomingDamageScale = 1f;
        }

        yield return Rest();
        if (!Alive) yield break;

        Vector3 target = Player.position;
        yield return Telegraph(target, 2.2f, DamageElement.Physical, Damage * 2, StatusEffectType.None, 1, () =>
        {
            if (ZoneMap.CanSpawnAt(target))
                Teleport(target);
        });
    }

    // ────────────────────────────────── 6장

    /// <summary>삼족오 — 날아올라(맞지 않음) 내리꽂히며 불을 지른다.</summary>
    private IEnumerator Dive()
    {
        yield return Rest();
        if (!Alive) yield break;

        health.IncomingDamageScale = 0f;
        if (movement != null) movement.IsHalted = true;
        SetAlpha(0.2f);
        yield return new WaitForSeconds(1f);

        Vector3 target = Player.position;
        yield return Telegraph(target, 3f, DamageElement.Fire, Damage * 2, StatusEffectType.Ignite, 1, () =>
        {
            if (ZoneMap.CanSpawnAt(target))
                Teleport(target);
        });

        SetAlpha(1f);
        if (movement != null) movement.IsHalted = false;
        health.IncomingDamageScale = 1f;
    }

    // ────────────────────────────────── 장 보스

    /// <summary>현무 — 빗방울이 얼어 바늘이 되어 떨어진다(냉기 · 둔화).</summary>
    private IEnumerator IceNeedles()
    {
        yield return Rest();
        if (!Alive) yield break;

        for (int i = 0; i < 5; i++)
        {
            Vector3 at = Player.position + new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f));
            StartCoroutine(Telegraph(at, 1.3f, DamageElement.Cold, Damage, StatusEffectType.Chill, 2, null, 1.2f));
        }
        yield return new WaitForSeconds(1.2f);
    }

    /// <summary>청룡 — 우레가 세 번 떨어진다(감전).</summary>
    private IEnumerator Thunder()
    {
        yield return Rest();

        for (int i = 0; i < 3 && Alive; i++)
        {
            StartCoroutine(Telegraph(Player.position, 1.6f, DamageElement.Lightning, Damage, StatusEffectType.Shock, 1, null, 0.9f));
            yield return new WaitForSeconds(0.6f);
        }
    }

    /// <summary>주작 — 가마의 불이 고리로 번진다(가까이 붙으면 피한다).</summary>
    private IEnumerator FireRing()
    {
        yield return Rest();
        if (!Alive) yield break;

        Vector3 center = transform.position;
        GameObject warn = Disc(center, 6f, new Color(1f, 0.45f, 0.15f), 0.25f);
        GameObject safe = Disc(center + Vector3.up * 0.01f, 2.5f, new Color(0.2f, 0.2f, 0.2f), 0.5f);
        yield return new WaitForSeconds(BossPatternTable.TelegraphSeconds);
        Destroy(warn);
        Destroy(safe);

        if (Player != null)
        {
            float d = Flat(Player.position - center).magnitude;
            if (d > 2.5f && d <= 6f)
                HitPlayer(Damage * 2, DamageElement.Fire, StatusEffectType.Ignite, 1);
        }
        Flash(center, 6f, new Color(1f, 0.45f, 0.15f));
    }

    /// <summary>백호 — 포효로 둘레를 밀어내고 몸을 굳힌다.</summary>
    private IEnumerator Roar()
    {
        yield return Rest();
        if (!Alive) yield break;

        Vector3 center = transform.position;
        yield return Telegraph(center, 7f, DamageElement.Physical, Damage / 2, StatusEffectType.Chill, 2, () =>
        {
            if (Player == null || Flat(Player.position - center).magnitude > 7f)
                return;
            Vector3 push = Flat(Player.position - center).normalized * 4f;
            if (Player.TryGetComponent(out Rigidbody rb))
                rb.MovePosition(rb.position + push);
        });
    }

    /// <summary>해태 — 뿔을 겨눈 줄을 보이고, 그 줄로 들이받는다.</summary>
    private IEnumerator HornCharge()
    {
        yield return Rest();
        if (!Alive) yield break;

        Vector3 start = transform.position;
        Vector3 dir = Flat(Player.position - start).normalized;
        if (dir == Vector3.zero) dir = transform.forward;

        var marks = new List<GameObject>();
        for (int i = 1; i <= 6; i++)
            marks.Add(Disc(start + dir * (i * 2f), 1.2f, new Color(0.95f, 0.3f, 0.3f), 0.3f));

        if (movement != null) movement.IsHalted = true;
        yield return new WaitForSeconds(BossPatternTable.TelegraphSeconds);
        foreach (GameObject m in marks) Destroy(m);

        bool hit = false;
        float t = 0f;
        while (t < 0.5f && Alive)
        {
            t += Time.deltaTime;
            Step(dir * 24f * Time.deltaTime);
            if (!hit && Player != null && Flat(Player.position - transform.position).magnitude < 1.6f)
            {
                hit = true;
                HitPlayer(Damage * 2, DamageElement.Physical, StatusEffectType.None, 0);
            }
            yield return null;
        }
        if (movement != null) movement.IsHalted = false;
    }

    /// <summary>
    /// 구미호 — 체력이 깎일 때마다 새색시 → 방앗간지기 → 참봉 → 할머니로 둔갑하며 가짜 둘과 함께 선다.
    /// 넋불(머리 위의 작은 불)이 진짜 위에 깜빡여 가린다 — 가짜는 한 대에 흩어진다.
    /// </summary>
    private IEnumerator Shapeshift()
    {
        int index = BossPatternTable.FormIndex(health.Normalized);
        if (index > formsPassed)
        {
            formsPassed = index;
            string form = BossPatternTable.Forms[index - 1].form;
            StoryDialogueUI.ShowBanner($"구미호가 {Josa.EuroRo(form)} 둔갑했다.", 2f);
            SpawnDecoys();
        }

        // 넋불이 진짜를 가린다.
        if (decoys.Count > 0)
        {
            if (soulMark == null)
                soulMark = Sphere(transform, new Vector3(0f, 2.6f, 0f), 0.35f, new Color(1f, 0.95f, 0.7f));
            soulMark.SetActive(!soulMark.activeSelf);
        }
        else if (soulMark != null)
        {
            Destroy(soulMark);
        }

        decoys.RemoveAll(d => d == null || !d.activeInHierarchy
                              || (d.TryGetComponent(out Health h) && h.IsDead));
        yield return new WaitForSeconds(0.4f);
    }

    private void SpawnDecoys()
    {
        ClearDecoys();
        EnemyPrefabCatalog catalog = EnemyPrefabCatalog.Load();
        GameObject prefab = catalog != null && TryGetComponent(out EnemyIdentity identity) ? catalog.Get(identity.Archetype) : null;
        if (prefab == null)
            return;

        for (int i = 0; i < BossPatternTable.Decoys; i++)
        {
            Vector3 at = transform.position + new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f));
            if (!ZoneMap.CanSpawnAt(at))
                at = transform.position;

            GameObject decoy = PoolManager.EnsureInstance().Spawn(prefab, at, Quaternion.identity);
            if (decoy == null)
                continue;

            if (decoy.TryGetComponent(out EnemyIdentity id))
            {
                EnemyProfile profile = EnemyProfile.Build(id.Archetype, EnemyRarity.Normal, System.Array.Empty<EnemyAffix>());
                profile.health = 1;   // 한 대에 흩어진다
                id.Apply(RaidManager.Current.Apply(profile));
            }
            decoys.Add(decoy);
        }
    }

    private void ClearDecoys()
    {
        foreach (GameObject d in decoys)
            if (d != null && d.activeInHierarchy && d.TryGetComponent(out Health h) && !h.IsDead)
                h.TakeTickDamage(h.Current);
        decoys.Clear();
    }

    // ────────────────────────────────── 도움

    private IEnumerator Telegraph(Vector3 at, float radius, DamageElement element, int damage,
        StatusEffectType status, int stacks = 1, System.Action onHit = null, float delay = BossPatternTable.TelegraphSeconds)
    {
        Color color = ColorOf(element);
        GameObject warn = Disc(at, radius, color, 0.28f);
        yield return new WaitForSeconds(delay);
        Destroy(warn);

        onHit?.Invoke();
        Flash(at, radius, color);

        if (Player != null && Flat(Player.position - at).magnitude <= radius)
            HitPlayer(damage, element, status, stacks);
    }

    private void HitPlayer(int damage, DamageElement element, StatusEffectType status, int stacks)
    {
        if (Player == null || !Player.TryGetComponent(out Health target) || target.IsDead)
            return;

        for (int i = 0; i < stacks && status != StatusEffectType.None; i++)
            target.ApplyStatus(status, damage);

        var request = new DamageRequest
        {
            source = DamageSource.Enemy,
            baseDamage = Mathf.Max(1, damage),
            element = element,
            hitKind = HitKind.Melee,
            effectiveRange = 999f,
            distance = 0f,
            criticalMultiplier = 1f
        };
        target.TakeDamage(request, GameManager.EnemyDamageMultiplier);
    }

    private IEnumerator Move(Vector3 dir, float speed, float seconds)
    {
        if (dir == Vector3.zero)
            yield break;

        if (movement != null) movement.IsHalted = true;
        float t = 0f;
        while (t < seconds && Alive)
        {
            t += Time.deltaTime;
            Step(dir * speed * Time.deltaTime);
            yield return null;
        }
        if (movement != null) movement.IsHalted = false;
    }

    private void Step(Vector3 delta)
    {
        Vector3 next = transform.position + delta;
        if (!ZoneMap.CanSpawnAt(next))
            return;

        if (body != null) body.MovePosition(next);
        else transform.position = next;
    }

    private void Teleport(Vector3 at)
    {
        at.y = transform.position.y;
        if (body != null) body.position = at;
        transform.position = at;
    }

    private void SetAlpha(float alpha)
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (soulMark != null && r.transform.IsChildOf(soulMark.transform))
                continue;
            Color c = r.material.color;
            c.a = alpha;
            r.material.color = c;
        }
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    private static Color ColorOf(DamageElement e) => e switch
    {
        DamageElement.Fire => new Color(1f, 0.45f, 0.15f),
        DamageElement.Cold => new Color(0.6f, 0.85f, 1f),
        DamageElement.Lightning => new Color(1f, 0.95f, 0.4f),
        DamageElement.Chaos => new Color(0.45f, 0.8f, 0.2f),
        _ => new Color(0.95f, 0.3f, 0.3f)
    };

    private static GameObject Disc(Vector3 at, float radius, Color color, float alpha)
    {
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "BossTelegraph";
        Destroy(disc.GetComponent<Collider>());
        disc.transform.position = new Vector3(at.x, 0.04f, at.z);
        disc.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
        Tint(disc, color, alpha);
        return disc;
    }

    private static void Flash(Vector3 at, float radius, Color color)
    {
        GameObject f = Disc(at, radius, color, 0.65f);
        Destroy(f, 0.2f);
    }

    private static GameObject Sphere(Transform parent, Vector3 local, float size, Color color)
    {
        GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.name = "SoulFireMark";
        Destroy(s.GetComponent<Collider>());
        s.transform.SetParent(parent, false);
        s.transform.localPosition = local;
        s.transform.localScale = Vector3.one * size;
        Tint(s, color, 1f);
        return s;
    }

    private static void Tint(GameObject go, Color color, float alpha)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null)
            return;
        Material m = renderer.material;
        color.a = alpha;
        m.color = color;
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", color);
    }
}
