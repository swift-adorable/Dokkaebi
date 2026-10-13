# 보스 패턴 · 처치 시뮬레이션 — 결정 2-110(패턴)을 재고 2-113에서 고쳤다.
# 값: Boss Data(boss_data.py · 2-68/2-69) · enemy_sim.py(무기 · 빌드 · 방어구) · 코드(BossPattern · BossPatternTable ·
#     StatusEffectTable · PlayerDash · Health 무적 0.6초 · 무기 유효 사거리 · ConsumableCaster 소환단 12 / 2초, 움직이거나 맞으면 끊김).  [가정]은 표시.
# 다시 돌리기: python3 boss_pattern_sim.py [n]  — 2-110 값.   python3 boss_pattern_sim.py [n] tuned — 2-113 값(boss_pattern_tuning.py, 코드와 같다).
import random, statistics as st, sys, math
exec(open('enemy_sim.py').read())
exec(open('boss_data.py').read().split("print('| 보스")[0])

N = int(sys.argv[1]) if len(sys.argv) > 1 else 300
MODE = sys.argv[2] if len(sys.argv) > 2 else 'now'
DT = 0.05

# ── 코드 값 ───────────────────────────────────────────────────────────
HP = 100; SPEED = 5.0; DASH = 5.0; DASH_T = 0.15; DASH_CD = 1.0; INVULN = 0.6
POTION_HEAL = 12; POTION_CAST = 2.0
RANGE = {1: 10, 2: 12, 3: 13, 4: 15, 5: 20, 6: 20}            # 무기 유효 사거리 — 반 넘으면 ×0.5, 넘으면 0
# 유형: (이동 속도, 예비동작 + 쿨다운, 원거리, 일반 공격 상태)
BODY = {'잡귀': (2.5, 1.55, False, None), '절굿공이귀': (1.4, 2.40, False, None), '번개귀': (2.6, 1.55, True, 'shock'),
        '수귀': (3.0, 1.60, False, None), '왕지네': (1.9, 2.50, True, None), '침귀': (2.2, 1.10, True, 'poison'),
        '허깨비': (1.8, 2.00, False, None), '순라귀': (2.4, 2.00, True, None), '무주귀': (2.8, 1.60, False, None)}

# ── BossPatternTable (지금 값) ────────────────────────────────────────
P = dict(
    interval={'Fade': 6, 'CoilLunge': 7, 'PlagueKick': 5, 'Rooted': 8, 'Claw': 4, 'Pounce': 6, 'Dive': 7,
              'IceNeedles': 6, 'Thunder': 5, 'FireRing': 7, 'Roar': 8, 'HornCharge': 6},
    telegraph=1.0,
    fade_scale=0.25, fade_time=1.2,
    grow_up=0.5, grow_down=0.6, grow_min=0.8, grow_max=1.8, grow_taken=(1.3, 0.5),
    pair_near=0.5, pair_far=1.2,
    rooted_scale=0.3, rooted_time=3.0,
    retreat_below=0.3, retreat_heal=0.1,
    pounce_mult=2.0, dive_mult=2.0, fire_ring_mult=2.0, horn_mult=2.0,
    thief_flee=(7.0, 1.2), thief_halt=2.5, lure_flee=(4.0, 0.8),
    decoys=2,
    dive_air=1.0, dive_delay=1.0, dive_radius=3.0, dive_ignite=2.0, fire_ring_ignite=2.0,   # 점화의 기준 피해 = 보스 한 대 × 이 값 (지금은 맞는 피해와 같다)
)
if MODE == 'tuned':
    exec(open('boss_pattern_tuning.py').read())

KIND = {'yagwanggwi': 'Thief', 'dalgyal': 'Fade', 'eodukssini': 'GrowWhenSeen', 'gangcheori': 'CoilLunge',
        'wongwi': 'Pair', 'dueoksini': 'PlagueKick', 'duduri': 'Rooted', 'kkeomeoksari': 'Claw', 'changgwi': 'Lure',
        'sangun': 'Pounce', 'samjogo': 'Dive', 'hyeonmu': 'IceNeedles', 'cheongnyong': 'Thunder', 'jujak': 'FireRing',
        'baekho': 'Roar', 'haetae': 'HornCharge', 'gumiho': 'Shapeshift'}

# ── 플레이어 셋 [가정] ────────────────────────────────────────────────
# rt: 위험 표시를 보고 움직이기까지(초, 평균 · 흩어짐) · dash: 피할 때 질주를 쓰는 비율 · move: 늘 움직이며 쏘는 비율
# hr: 보스 일반 공격을 맞는 비율(Boss Data의 기준 1/3) · grow: 어둑시니를 다루는 법 · decoy: 가짜를 가리는 데 드는 초
PROFILES = {
    '숙련': dict(rt=(0.30, 0.08), dash=0.9, move=0.8, hr=0.25, grow='flick', decoy=1.0, potion_at=40),
    '보통': dict(rt=(0.45, 0.12), dash=0.5, move=0.5, hr=1 / 3, grow='learn', decoy=1.8, potion_at=45),
    '서툰': dict(rt=(0.60, 0.15), dash=0.2, move=0.3, hr=0.5, grow='stare', decoy=3.0, potion_at=50),
}

ST = {'ignite': (0.25, 4.0, 1), 'bleed': (0.20, 3.0, 1), 'poison': (0.05, 6.0, 10)}


def af(a, p): return 2 / (max(a - p, 0) + 2)


class Player:
    def __init__(s, prof, ch):
        s.p = prof; s.ch = ch; s.hp = HP; s.inv_until = -1; s.dash_ready = 0; s.cast_until = -1; s.uses = 0
        s.dot = {}; s.chill = 0; s.chill_t = 0; s.shock = 0; s.shock_t = 0; s.frozen_until = -1; s.min_hp = HP
        s.dead = False; s.taken = 0; s.hits = 0

    def can_act(s, t): return t >= s.frozen_until

    def shooting(s, t): return s.can_act(t)

    def casting(s, t): return t < s.cast_until

    def amp(s): return 1 + 0.2 * min(s.shock, 6) / 6

    def status(s, kind, src, stacks, t):
        for _ in range(stacks):
            if kind == 'chill':
                if t < s.frozen_until: return
                s.chill += 1; s.chill_t = t + 3
                if s.chill >= 6: s.chill = 0; s.frozen_until = t + 3
            elif kind == 'shock':
                if t < s.frozen_until: return
                s.shock = min(6, s.shock + 1); s.shock_t = t + 6
                if s.shock >= 6: s.shock = 0; s.frozen_until = t + 2
            else:
                coeff, dur, mx = ST[kind]
                cur = s.dot.get(kind, [0, 0, 0])
                cur = [max(cur[0], src), min(mx, cur[1] + 1), t + dur]
                s.dot[kind] = cur

    def hit(s, raw, pen, t, element_bypass=False):
        if t < s.inv_until: return 0
        d = max(1, round(raw * af(ARM[s.ch], pen) * s.amp()))
        if s.casting(t): s.cast_until = t; s.heal_at = None; s.cancels = getattr(s, 'cancels', 0) + 1   # 맞아서 끊김
        s.hp -= d; s.taken += d; s.hits += 1; s.inv_until = t + INVULN
        return d

    def tick(s, t, moving):
        for k, (src, stacks, until) in list(s.dot.items()):
            if t >= until: del s.dot[k]; continue
            coeff = ST[k][0]
            ps = src * coeff * max(1, stacks) * (2 if k == 'bleed' and moving else 1)
            d = ps * DT * s.amp(); s.hp -= d; s.taken += d
        if t >= s.chill_t: s.chill = 0
        if t >= s.shock_t: s.shock = 0
        s.min_hp = min(s.min_hp, s.hp)
        if s.hp <= 0: s.dead = True
        # 소환단 — 위험할 때 2초 시전(그동안 쏘지 못한다)
        if getattr(s, 'heal_at', None) is not None and t >= s.heal_at:
            s.hp = min(HP, s.hp + POTION_HEAL); s.heal_at = None; s.uses += 1
        if s.hp < s.p['potion_at'] and t >= s.cast_until and s.can_act(t):
            s.cast_until = t + POTION_CAST; s.heal_at = s.cast_until


def dodge(pl, t, escape, on_player=False, delay=None):
    """escape m만큼 벗어나야 할 때 delay초 안에 벗어나는가."""
    delay = P['telegraph'] if delay is None else delay
    if escape <= 0: return True
    if not pl.can_act(t): return False
    if pl.casting(t):                                       # 마시는 중 — 움직이면 끊긴다. 위험하면 끊고 피한다
        pl.cast_until = t; pl.heal_at = None
    slow = 1 - 0.4 * min(pl.chill, 6) / 6
    if on_player and random.random() < pl.p['move']:
        return escape / (SPEED * slow) <= delay            # 이미 움직이고 있다 — 반응 없이 빠져나간다
    rt = max(0.12, random.gauss(*pl.p['rt']))
    if escape <= DASH and t >= pl.dash_ready and random.random() < pl.p['dash']:
        pl.dash_ready = t + rt + DASH_CD
        return rt + DASH_T <= delay
    return rt + escape / (SPEED * slow) <= delay


def fight(bid, prof, patterns=True):
    row = [r for r in rows if r[0] == bid][0]
    _, name, zone, ch, lvl, tgt, hits, out = row
    kind = KIND[bid] if patterns else 'None'; tier = max(1, ch - 1); wd, wi, wp = W[tier]; R = RANGE[tier]
    pl = Player(PROFILES[prof], ch)
    d_pref = 0.45 * R
    bodies = []
    for b, hpn, hit, armour, fire, cur in out:
        dps = wd / wi * BUILD[ch] * af(armour, wp) * fire
        bodies.append(dict(b=b, hp=float(hpn), max=float(hpn), hit=hit, dps=dps, next_atk=random.uniform(0.5, 1.5),
                           scale=1.0))
    t = 0.0; nextp = 1.5 + P['interval'].get(kind, 5); dist = d_pref; flee_until = -1; halt_until = -1
    grow = 1.0; looking_away = False; sep = 2.0; retreated = False; pause_until = -1; forms = 0; decoy_until = -1
    fade_until = -1; root_until = -1; dive_until = -1; pending = []   # (시각, 함수)
    shots_lost = 0.0
    now = [0.0]

    def later(when, fn): pending.append((when, fn))

    def falloff(d): return 1.0 if d <= R / 2 else (0.5 if d <= R else 0.0)

    while t < 400:
        alive = [x for x in bodies if x['hp'] > 0]
        if not alive or pl.dead: break
        moving = random.random() < pl.p['move'] and not pl.casting(t)

        # ── 받는 피해 배율 ─────────────────────
        sc = 1.0
        if kind == 'Fade' and t < fade_until: sc = P['fade_scale']
        if kind == 'Rooted' and t < root_until: sc = P['rooted_scale']
        if kind == 'Dive' and t < dive_until: sc = 0.0
        if kind == 'Pounce' and t < pause_until: sc = 0.0
        seen = True
        if kind == 'GrowWhenSeen':
            g = pl.p['grow']
            if g == 'flick': looking_away = (grow > 0.95) if not looking_away else (grow > 0.8 + 1e-6)
            elif g == 'learn': looking_away = (grow >= 1.6) if not looking_away else (grow > 1.0)
            else: looking_away = False
            seen = not looking_away
            grow = min(P['grow_max'], max(P['grow_min'], grow + (P['grow_up'] if seen else -P['grow_down']) * DT))
            lo, hi = P['grow_taken']
            sc = lo + (hi - lo) * (grow - P['grow_min']) / (P['grow_max'] - P['grow_min'])

        # ── 플레이어가 쏜다 ───────────────────
        fire_ok = pl.shooting(t) and seen and t >= decoy_until
        tgtb = alive[0]
        if kind == 'Pair' and len(alive) > 1:
            tgtb = min(alive, key=lambda x: x['hp'])
        if kind in ('Thief', 'Lure'):
            sp, dur = P['thief_flee'] if kind == 'Thief' else P['lure_flee']
            trigger = 6.0 if kind == 'Thief' else 7.0
            if t < flee_until: dist += (sp - SPEED) * DT          # 달아난다 — 쫓는다
            elif dist > d_pref: dist = max(d_pref, dist - SPEED * DT)
            if t >= flee_until and t >= halt_until and dist < trigger:
                flee_until = t + dur
                halt_until = t + dur + (P['thief_halt'] if kind == 'Thief' else 0) + (0.3 if kind == 'Thief' else 0.4)
        f = falloff(dist) if kind in ('Thief', 'Lure') else 1.0
        if fire_ok:
            mult = sc
            if kind == 'Pair':
                mult = P['pair_near'] if (len(alive) > 1 and sep <= 6) else P['pair_far']
            tgtb['hp'] -= tgtb['dps'] * mult * f * DT
        else:
            shots_lost += DT

        # 처녀 · 몽달 떨어뜨리기 — 움직이는 만큼 빠른 몸이 앞서고, 서 있으면 느린 몸이 따라붙는다
        if kind == 'Pair' and len(alive) > 1:
            sep = max(0.5, sep + (1.0 if moving else -1.8) * DT)

        # ── 일반 공격 ─────────────────────────
        for x in alive:
            if t >= x['next_atk']:
                spd, atk, ranged, stt = BODY[x['b']]
                x['next_atk'] = t + atk
                hr = pl.p['hr']; hr = 1 - (1 - hr) ** 3 if x['b'] == '순라귀' else hr
                if kind in ('Thief', 'Lure') and t < flee_until: continue
                if random.random() < hr:
                    pen = T[x['b']][2]
                    pl.hit(x['hit'], pen, t)
                    if stt: pl.status(stt, x['hit'], 1, t)
        if kind == 'Shapeshift' and t < decoy_until:   # 가짜 둘(허깨비 · 일반)이 곁에서 친다
            if random.random() < DT / 2.0 * 2 * pl.p['hr']:
                pl.hit(20, 0, t)

        # ── 패턴 ───────────────────────────────
        hpn = sum(x['hp'] for x in bodies) / sum(x['max'] for x in bodies)
        D = alive[0]['hit']
        if kind == 'Pounce' and not retreated and hpn <= P['retreat_below']:
            retreated = True; pause_until = t + 2.5
            pending.append((t + 2.5, lambda: [x.update(hp=min(x['max'], x['hp'] + x['max'] * P['retreat_heal'])) for x in bodies]))
            nextp = t + 2.5 + P['interval']['Pounce']
        if kind == 'Shapeshift':
            idx = sum(1 for a in (0.8, 0.6, 0.4, 0.2) if hpn <= a)
            if idx > forms:
                forms = idx
                if P['decoys'] > 0: decoy_until = t + pl.p['decoy'] * P['decoys'] / 2
        if t >= nextp and kind in P['interval']:
            nextp = t + P['interval'][kind]
            tel = P['telegraph']
            dd = max(1.0, random.gauss(d_pref, 0.8))   # 그 순간 보스와의 거리
            if kind == 'Fade':
                fade_until = t + P['fade_time']; nextp += P['fade_time']
            elif kind == 'CoilLunge':
                ok = dd > 4 or dodge(pl, t, 4 - dd)
                if not ok: later(t + tel, lambda: pl.hit(D, 0, now[0]))
                nextp += tel + 0.4
            elif kind == 'PlagueKick':
                rt_ok = None
                for _ in range(3):
                    off = math.hypot(random.uniform(-3, 3), random.uniform(-3, 3))
                    if off <= 2.5 and not dodge(pl, t, 2.5 - off):
                        later(t + tel, lambda: pl.status('poison', D / 2, 3, now[0])); later(t + tel, lambda: pl.hit(D / 2, 0, now[0]))
                nextp += tel
            elif kind == 'Rooted':
                root_until = t + P['rooted_time']; nextp += P['rooted_time'] + 0.5
            elif kind == 'Claw':
                # 보스 앞 2m에 반지름 2.2 — 0.0 ~ 4.2m
                if dd <= 4.2 and not dodge(pl, t, 4.2 - dd):
                    later(t + tel, lambda: pl.hit(D, 0, now[0])); later(t + tel, lambda: pl.status('bleed', D, 2, now[0]))
                nextp += tel
            elif kind == 'Pounce':
                if not dodge(pl, t, 2.2, on_player=True):
                    later(t + tel, lambda: pl.hit(D * P['pounce_mult'], 0, now[0]))
                nextp += tel
            elif kind == 'Dive':
                air, dl = P['dive_air'], P['dive_delay']
                dive_until = t + air + dl
                if not dodge(pl, t + air, P['dive_radius'], on_player=True, delay=dl):
                    later(t + air + dl, lambda: pl.hit(D * P['dive_mult'], 0, now[0])); later(t + air + dl, lambda: pl.status('ignite', D * P['dive_ignite'], 1, now[0]))
                nextp += air + dl
            elif kind == 'IceNeedles':
                for _ in range(5):
                    off = math.hypot(random.uniform(-4, 4), random.uniform(-4, 4))
                    if off <= 1.3 and not dodge(pl, t, 1.3 - off, delay=1.2):
                        later(t + 1.2, lambda: pl.status('chill', D, 2, now[0])); later(t + 1.2, lambda: pl.hit(D, 0, now[0]))
                nextp += 1.2
            elif kind == 'Thunder':
                for i in range(3):
                    if not dodge(pl, t + 0.6 * i, 1.6, on_player=True, delay=0.9):
                        later(t + 0.6 * i + 0.9, lambda: pl.status('shock', D, 1, now[0])); later(t + 0.6 * i + 0.9, lambda: pl.hit(D, 0, now[0]))
                nextp += 2.1
            elif kind == 'FireRing':
                if 2.5 < dd <= 6 and not dodge(pl, t, min(dd - 2.5, 6 - dd)):
                    later(t + tel, lambda: pl.hit(D * P['fire_ring_mult'], 0, now[0])); later(t + tel, lambda: pl.status('ignite', D * P['fire_ring_ignite'], 1, now[0]))
                nextp += tel
            elif kind == 'Roar':
                if dd <= 7 and not dodge(pl, t, 7 - dd):
                    later(t + tel, lambda: pl.hit(D / 2, 0, now[0])); later(t + tel, lambda: pl.status('chill', D / 2, 2, now[0]))
                nextp += tel
            elif kind == 'HornCharge':
                if not dodge(pl, t, 1.6, on_player=True):
                    later(t + tel + 0.2, lambda: pl.hit(D * P['horn_mult'], 0, now[0]))
                nextp += tel + 0.5

        now[0] = t
        for when, fn in [p for p in pending if p[0] <= t]:
            fn(); pending.remove((when, fn))
        pl.tick(t, moving)
        t += DT
    return dict(t=t, dead=pl.dead, uses=pl.uses, taken=pl.taken, min_hp=pl.min_hp, hits=pl.hits)


def base_ttk(bid):
    row = [r for r in rows if r[0] == bid][0]; _, name, zone, ch, lvl, tgt, hits, out = row
    tier = max(1, ch - 1); wd, wi, wp = W[tier]
    return sum(hpn / (wd / wi * BUILD[ch] * af(armour, wp) * fire) for b, hpn, hit, armour, fire, cur in out)


def q(xs, p): xs = sorted(xs); return xs[int(p * (len(xs) - 1))]


if __name__ == '__main__':
    random.seed(13)
    print(f'## 보스 패턴 시뮬레이션 — {"조정안" if MODE == "tuned" else "지금 값"} · 보스마다 플레이어마다 {N}번\n')
    print('| 보스 | 패턴 | 목표 | 처치 숙련 · 보통 · 서툰 | 패턴 없이 보통 | 보통 받는 피해 (패턴 없이) | 보통 소환단 회 (패턴 없이) | 쓰러짐 숙련 · 보통 · 서툰 | 패턴 없이 쓰러짐 보통 · 서툰 |')
    print('|---|---|---|---|---|---|---|---|---|')
    for r in rows:
        bid, name, zone, ch, lvl, tgt = r[:6]
        res = {p: [fight(bid, p) for _ in range(N)] for p in PROFILES}
        base = {p: [fight(bid, p, False) for _ in range(N)] for p in ('보통', '서툰')}
        med = lambda R, p, k: st.median([x[k] for x in R[p]])
        die = lambda R, p: sum(x['dead'] for x in R[p]) / N * 100
        print(f"| {name} | {KIND[bid]} | {tgt}초 | {med(res,'숙련','t'):.0f} · **{med(res,'보통','t'):.0f}** · {med(res,'서툰','t'):.0f}초 | {med(base,'보통','t'):.0f}초 "
              f"| {med(res,'보통','taken'):.0f} ({med(base,'보통','taken'):.0f}) | {med(res,'보통','uses'):.0f} ({med(base,'보통','uses'):.0f}) "
              f"| {die(res,'숙련'):.0f}% · {die(res,'보통'):.0f}% · {die(res,'서툰'):.0f}% | {die(base,'보통'):.0f}% · {die(base,'서툰'):.0f}% |")
