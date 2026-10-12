using UnityEngine;

/// <summary>
/// 【모였다 터지는 구슬 — 발동 구슬을 실제로 충전하고 터뜨린다】 (결정 2-102). 플레이어에 붙는다.
///
/// 끼운 발동 구슬 하나의 에너지를 PlayerHitEvents · 적 처치로 채우고, 100%가 되면
/// SkillZoneDirector에 효과를 맡긴다. 효과를 실행하는 동안은 PlayerHitEvents를 막아
/// 「발동이 일으킨 결과가 발동을 다시 채우는」 고리를 끊는다 (Skill_System 7절).
///
/// 에너지는 판마다 0에서 시작한다(저장하지 않는다). 거점에서는 차지 않는다.
/// 차오른 정도는 AmmoHudUI의 둘째 줄이 보여 준다.
/// </summary>
public class TriggerGemDirector : MonoBehaviour
{
    private readonly TriggerCharge charge = new();
    private SkillZoneDirector zones;

    private int shownPercent = -1;
    private string shownName;
    private float firedLabelUntil;

    // 다 찬 순간에 바로 터뜨리지 않고 다음 Update에서 터뜨린다 — 충전을 일으킨 쪽(폭발 · 잔류물)이
    // 적 목록을 도는 중에 같은 목록을 다시 채우지 않게.
    private bool firePending;
    private string pendingMetaId;
    private Vector3 pendingAt;

    private void Awake() => zones = GetComponent<SkillZoneDirector>();

    private void OnEnable()
    {
        PlayerHitEvents.Inflicted += HandleInflicted;
        PlayerHitEvents.Critical += HandleCritical;
        EnemyController.Killed += HandleKilled;
    }

    private void OnDisable()
    {
        PlayerHitEvents.Inflicted -= HandleInflicted;
        PlayerHitEvents.Critical -= HandleCritical;
        EnemyController.Killed -= HandleKilled;
        AmmoHudUI.ShowTrigger(string.Empty);
    }

    private SkillDefinition EquippedMeta
    {
        get
        {
            SocketedBuild build = SkillManager.HasInstance ? SkillManager.Instance.Build : null;
            if (build == null)
                return null;

            for (int m = 0; m < SocketedBuild.MaxMetas; m++)
            {
                SkillDefinition meta = build.GetMeta(m);
                if (meta != null)
                    return meta;
            }

            return null;
        }
    }

    private bool Active => !SceneFlow.InBunker && EnemyManager.HasInstance;

    private void Update()
    {
        SkillDefinition meta = Active ? EquippedMeta : null;
        charge.Bind(meta != null ? meta.Id : null);

        if (firePending)
        {
            firePending = false;
            if (meta != null && meta.Id == pendingMetaId && zones != null)
            {
                PlayerHitEvents.BeginSuppress();
                try
                {
                    Fire(pendingMetaId, pendingAt);
                }
                finally
                {
                    PlayerHitEvents.EndSuppress();
                }

                firedLabelUntil = Time.time + 0.8f;
                shownPercent = -1;
            }
        }

        RefreshHud(meta);
    }

    // ────────────────────────────────── 충전

    private void HandleInflicted(Health target, StatusEffectType type)
        => Feed(TriggerGemTable.FromInflicted(type), target);

    private void HandleCritical(Health target) => Feed(TriggerEvent.Critical, target);

    private void HandleKilled(EnemyController enemy)
    {
        if (enemy == null || enemy.Health == null || PlayerHitEvents.IsSuppressed)
            return;

        if (!enemy.Health.KillContext.killedByPlayer)
            return;

        if (enemy.Health.Status.Has(StatusEffectType.Ignite))
            Feed(TriggerEvent.KilledIgnited, enemy.Health);
    }

    private void Feed(TriggerEvent e, Health target)
    {
        if (e == TriggerEvent.None || !Active || zones == null)
            return;

        SkillDefinition meta = EquippedMeta;
        if (meta == null)
            return;

        charge.Bind(meta.Id);
        if (!charge.Add(e))
            return;

        firePending = true;
        pendingMetaId = meta.Id;
        pendingAt = target != null ? target.transform.position : transform.position;
    }

    private void Fire(string metaId, Vector3 hitAt)
    {
        switch (metaId)
        {
            case TriggerGemTable.OnShock:
                zones.StrikeShockedOnScreen();
                break;
            case TriggerGemTable.OnIgnite:
                zones.FireCoresAt(transform.position, TriggerGemTable.AroundRadius);
                break;
            case TriggerGemTable.OnFreeze:
                zones.FreezeAround(transform.position, TriggerGemTable.FreezeRadius);
                break;
            case TriggerGemTable.OnCrit:
                zones.FireCoresAt(hitAt, TriggerGemTable.CritRadius);
                break;
            case TriggerGemTable.OnAilment:
                zones.DetonateElementalOnScreen();
                break;
        }
    }

    // ────────────────────────────────── 표시

    private void RefreshHud(SkillDefinition meta)
    {
        if (meta == null)
        {
            if (shownName != null)
            {
                AmmoHudUI.ShowTrigger(string.Empty);
                shownName = null;
                shownPercent = -1;
            }
            return;
        }

        bool firing = Time.time < firedLabelUntil;
        int percent = firing ? 101 : charge.Percent;

        if (percent == shownPercent && shownName == meta.DisplayName)
            return;

        shownPercent = percent;
        shownName = meta.DisplayName;
        AmmoHudUI.ShowTrigger(firing ? $"{meta.DisplayName} — 터졌다!" : $"{meta.DisplayName} {charge.Percent}%", firing);
    }
}
