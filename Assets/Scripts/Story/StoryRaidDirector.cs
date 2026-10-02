using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 파밍 구역에 이야기 보스 · 기억의 조각 · 방을 놓는다. (로드맵 3단계 · Story 2절)
///
/// 【보스】 그 구역의 아직 쓰러뜨리지 않은 보스가 맨 앞부터 하나씩 나온다.
/// 앞의 것을 쓰러뜨리면 다음 것이 나온다(3-2 · 5-1). 장 보스는 다 쓰러뜨린 뒤에도
/// 그 구역에 들어갈 때마다 다시 나온다. 이미 만난 중간 보스는 그 장 어느 구역에서든
/// 가끔 다시 나온다 [덕코프 — 맵마다 이름 있는 보스가 여럿 나온다].
///
/// 【자리】 맵이 아직 하나라(9단계) 플레이어 둘레에 놓는다. 구역이 맵이 되면
/// 정해진 자리로 바꾼다.
/// </summary>
public class StoryRaidDirector : MonoBehaviour
{
    /// <summary>들어온 뒤 보스가 나오기까지 (초) [미검증 — 정한 값].</summary>
    public const float BossDelay = 8f;

    /// <summary>만난 중간 보스가 다시 나올 확률 (판마다) [미검증 — 정한 값].</summary>
    public const float MidBossReturnChance = 0.25f;

    /// <summary>보스가 나오는 거리 (m).</summary>
    public const float BossDistance = 12f;

    /// <summary>조각 · 방이 놓이는 거리 (m).</summary>
    public const float PickupMin = 8f;
    public const float PickupMax = 15f;

    private string zone;
    private Transform player;
    private EnemyPrefabCatalog catalog;
    private bool bossAlive;

    public void Begin(string zoneId)
    {
        zone = zoneId;

        var movement = FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Exclude);
        player = movement != null ? movement.transform : null;

        if (player == null)
        {
            GameLogger.Warning("[Story] 플레이어가 없어 이야기 보스 · 조각을 놓지 않습니다.");
            return;
        }

        catalog = EnemyPrefabCatalog.Load();

        PlacePickups();

        BossDefinition next = StoryManager.Progress.NextBossIn(zone);

        if (next == null)
            next = PickReturningMidBoss();

        if (next != null)
            Invoke(nameof(SpawnPending), BossDelay);

        pending = next;
    }

    private BossDefinition pending;

    private BossDefinition PickReturningMidBoss()
    {
        ZoneDefinition z = StoryTable.Zone(zone);

        if (z == null)
            return null;

        List<BossDefinition> met = StoryManager.Progress.MetMidBosses(z.Chapter);

        if (met.Count == 0 || Random.value >= MidBossReturnChance)
            return null;

        return met[Random.Range(0, met.Count)];
    }

    private void SpawnPending()
    {
        if (pending == null || bossAlive || player == null)
            return;

        BossDefinition boss = pending;
        pending = null;

        if (Spawn(boss))
            StoryDialogueUI.ShowBanner($"{Josa.IGa(boss.Name)} 나타났다.");
    }

    private bool Spawn(BossDefinition boss)
    {
        if (catalog == null)
        {
            GameLogger.Warning("[Story] 유형 프리팹 카탈로그가 없어 보스를 낼 수 없습니다.");
            return false;
        }

        PoolManager pool = PoolManager.EnsureInstance();
        var group = new StoryBossGroup(boss, OnGroupDefeated);
        Vector2 dir = Random.insideUnitCircle.normalized;
        if (dir == Vector2.zero) dir = Vector2.up;

        for (int i = 0; i < boss.Bodies.Length; i++)
        {
            GameObject prefab = catalog.Get(boss.Bodies[i]);

            if (prefab == null)
                continue;

            Vector2 side = new Vector2(-dir.y, dir.x) * (i - (boss.Bodies.Length - 1) * 0.5f) * 2.5f;
            Vector3 position = player.position + new Vector3(dir.x, 0f, dir.y) * BossDistance + new Vector3(side.x, 0f, side.y);

            GameObject body = pool.Spawn(prefab, position, Quaternion.identity);

            if (body == null)
                continue;

            var identity = body.GetComponent<EnemyIdentity>();

            if (identity != null)
                identity.Apply(RaidManager.Current.Apply(
                    EnemyProfile.Build(boss.Bodies[i], boss.Rarity, System.Array.Empty<EnemyAffix>())));

            var health = body.GetComponent<Health>();

            if (health != null)
                group.Track(health);
        }

        bossAlive = group.Count > 0;
        return bossAlive;
    }

    private void OnGroupDefeated(BossDefinition boss)
    {
        bossAlive = false;

        bool first = !StoryManager.Progress.HasDefeated(boss.Id);
        StoryManager.ReportBossDefeated(boss.Id);

        if (!first)
            return;

        // 같은 구역의 다음 보스 — 앞의 것을 쓰러뜨려야 나온다.
        BossDefinition next = StoryManager.Progress.NextBossIn(zone);

        if (next != null && !StoryManager.Progress.HasDefeated(next.Id))
        {
            pending = next;
            Invoke(nameof(SpawnPending), BossDelay);
        }
    }

    // ── 조각 · 방 ────────────────────────────────────────────────────

    private void PlacePickups()
    {
        PieceDefinition piece = StoryTable.PieceIn(zone);

        if (piece != null && !StoryManager.Progress.HasPiece(piece.Id))
            StoryPickup.Create(StoryPickup.Kind.Piece, piece.Id, RandomPoint());

        NoticeDefinition notice = StoryTable.NoticeIn(zone);

        if (notice != null && !StoryManager.Progress.HasNotice(notice.Id))
            StoryPickup.Create(StoryPickup.Kind.Notice, notice.Id, RandomPoint());
    }

    private Vector3 RandomPoint()
    {
        Vector2 dir = Random.insideUnitCircle.normalized;
        if (dir == Vector2.zero) dir = Vector2.right;

        float distance = Random.Range(PickupMin, PickupMax);
        return player.position + new Vector3(dir.x, 0f, dir.y) * distance;
    }
}

/// <summary>한 보스의 몸들. 전부 쓰러지면 끝난다 (처녀귀신 · 몽달귀신 · 창귀).</summary>
public sealed class StoryBossGroup
{
    private readonly BossDefinition boss;
    private readonly System.Action<BossDefinition> defeated;
    private int alive;

    public StoryBossGroup(BossDefinition boss, System.Action<BossDefinition> defeated)
    {
        this.boss = boss;
        this.defeated = defeated;
    }

    public int Count => alive;

    public void Track(Health health)
    {
        alive++;

        void Died()
        {
            // 풀로 돌아간 몸이 다음에 일반 적으로 나올 때 다시 불리지 않게 끊는다.
            health.OnDied -= Died;
            alive--;

            if (alive == 0)
                defeated?.Invoke(boss);
        }

        health.OnDied += Died;
    }
}
