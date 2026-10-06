# 전투 시뮬레이션 — 2026-10-06 (Story Lock · Zone Data · Boss Data 2-68 뒤)
# 값: Combat_Baseline 1 · 2 · 3 · 5 · 5-1절, Equipment 3절, Consumable 2절, ZoneDataTable 비중. [가정]은 표시.
import random, statistics as st
exec(open('enemy_sim.py').read())          # T(유형) · G(등급) · W(무기) · BUILD · ARM · HITRATE · ATKINT · af
exec(open('boss_data.py').read().split("print('| 보스")[0])   # B · rows (Boss Data)
random.seed(7)
NAME={'Scav':'잡귀','Crusher':'절굿공이귀','Dynamo':'번개귀','Lurker':'수귀','Chemic':'왕지네','Specimen':'침귀','Settled':'허깨비','Sentry':'순라귀','Wraith':'무주귀'}
POOL={1:[('잡귀',90),('절굿공이귀',10)],2:[('잡귀',40),('번개귀',30),('수귀',30)],3:[('왕지네',50),('침귀',50)],
      4:[('잡귀',60),('허깨비',20),('순라귀',20)],5:[('순라귀',50),('무주귀',50)],6:[('무주귀',80),('잡귀',20)]}
# 3점사(Combat 5절 · 코드 BurstShots 3 · 간격 0.14초)는 피격 무적 0.6초 안에 들어가 한 번 공격에 많아야 한 대다.
# 대신 셋 중 하나만 맞아도 맞는다 — 맞을 확률 1 − (1 − 0.5)³ = 0.875 → 한 발짜리의 1.75배
BURST={'순라귀':1.75}
RARITY=[('일반',75),('마법',20),('희귀',5)]   # 맵 1회 분포(Hunting 7절) — 고유는 따로 1~2체
INVULN=0.6
def pick(tbl): 
    r=random.uniform(0,sum(w for _,w in tbl)); a=0
    for k,w in tbl:
        a+=w
        if r<=a: return k
    return tbl[-1][0]
def dps_vs(b,grade,ch,tier,elem,build_scale=1.0,armour=None,fire=None,diff=1.0):
    hp,dmg,pen,ar,kind,ph,fi,li=T[b]; wd,wi,wp=W[tier]
    a = (ar+G[grade][2]) if armour is None else armour
    m={'물리':ph,'화염':fi if fire is None else fire,'번개':li}[elem]
    return wd/wi*BUILD[ch]*build_scale*af(a,wp)*m
def incoming(b,grade,ch,diff=1.0,dmg_override=None,hitrate=None):
    hp,dmg,pen,ar,kind,*_=T[b]
    d = dmg*G[grade][1] if dmg_override is None else dmg_override
    per_hit=max(1,d*diff*af(ARM[ch],pen))
    hr=HITRATE if hitrate is None else hitrate
    burst=1-(1-hr)**3 if b in BURST else hr
    return per_hit*burst/ATKINT, per_hit   # 초당 받는 피해, 한 발
def encounter(ch,tier,elem,diff,build_scale,hitrate=None):
    first=pick(POOL[ch]); n=random.randint(2,5)
    if ch==4 and first=='잡귀': n=random.randint(8,12)     # 잡귀 떼
    group=[(first if i==0 else pick(POOL[ch]), pick(RARITY)) for i in range(n)]
    t=0; taken=0
    alive=[(b,g,T[b][0]*G[g][0]*diff) for b,g in group]
    alive.sort(key=lambda x:x[2]/dps_vs(x[0],x[1],ch,tier,elem,build_scale))   # [가정] 빨리 잡히는 것부터 잡는다
    while alive:
        b,g,hp=alive[0]
        k=hp/dps_vs(b,g,ch,tier,elem,build_scale)
        rate=sum(incoming(x,y,ch,diff,hitrate=hitrate)[0] for x,y,_ in alive)
        rate=min(rate, 100/INVULN)              # 무적 0.6초가 받는 속도를 막는다
        taken+=rate*k; t+=k; alive.pop(0)
    return t,taken,n
def raid(ch,tier,elem='화염',diff=1.0,build_scale=1.0,encounters=15,hitrate=None):
    T_=0; D=0; worst=0; kills=0
    for _ in range(encounters):
        t,d,n=encounter(ch,tier,elem,diff,build_scale,hitrate); T_+=t; D+=d; worst=max(worst,d); kills+=n
    return T_,D,worst,kills
def q(xs,p): xs=sorted(xs); return xs[int(p*(len(xs)-1))]
out=[]
def P(s=''): out.append(s)
P('## 1. 일반 파밍 — 장마다 2,000판 (15분 · 조우 15번 [가정])')
P()
P('| 장 | 무기 | 처치 수 (중앙) | 싸우는 시간 (중앙) | 받는 피해 중앙 / 90% | 소환단 몇 개 (중앙 / 90%) | 한 조우 최악 (90% / 99%) | 한 조우에 쓰러질 확률 |')
P('|---|---|---|---|---|---|---|---|')
for ch in range(1,7):
    for tier,label in [(max(1,ch-1),f'처음(티어 {max(1,ch-1)})'),(ch,f'티어 {ch}')]:
        if tier==max(1,ch-1) and label.startswith('티어'): continue
        R=[raid(ch,tier) for _ in range(2000)]
        Tm=[r[0] for r in R]; Dm=[r[1] for r in R]; Wm=[r[2] for r in R]; K=[r[3] for r in R]
        die=sum(1 for r in R if r[2]>=100)/len(R)
        P(f"| {ch} | {label} | {st.median(K):.0f} | {st.median(Tm)/60:.1f}분 | {st.median(Dm):.0f} / {q(Dm,.9):.0f} | {st.median(Dm)/60:.1f} / {q(Dm,.9)/60:.1f} | {q(Wm,.9):.0f} / {q(Wm,.99):.0f} | {die*100:.1f}% |")
P()
P('- 「소환단 몇 개」 = 받은 피해 ÷ 60 (소환단 하나 = 12 × 5회). 조우 사이에 가득 채운다고 본다')
P('- 「한 조우에 쓰러질 확률」 = 한 판 15조우 중 한 번이라도 한 조우 피해가 100을 넘는 판의 비율 — 조우 중에 약을 먹지 않을 때')
P()
P('## 2. 같은 장 · 다른 선택')
P()
P('| 장 | 경우 | 받는 피해 중앙 | 싸우는 시간 | 비고 |')
P('|---|---|---|---|---|')
for ch in range(1,7):
    tier=max(1,ch-1)
    base=[raid(ch,tier) for _ in range(800)]
    for lab,kw in [('물리만',{'elem':'물리'}),('번개',{'elem':'번개'}),('빌드 절반',{'build_scale':0.5}),('균형 난이도 (80%)',{'diff':0.8}),('숙련 — 맞는 비율 25%',{'hitrate':0.25})]:
        R=[raid(ch,tier,**kw) for _ in range(800)]
        P(f"| {ch} | {lab} | {st.median([r[1] for r in R]):.0f} (기준 {st.median([r[1] for r in base]):.0f}) | {st.median([r[0] for r in R])/60:.1f}분 (기준 {st.median([r[0] for r in base])/60:.1f}) | |")
P()
P('## 3. 맵의 고유 일반 적 (장마다 1~2체 고정 · 유형 × 고유 ×15)')
P()
P('| 장 | 유형 | 체력 | 처치 (처음 무기 · 화염) | 처치 (물리만) | 받는 초당 피해 | 맞아도 되는 대수 |')
P('|---|---|---|---|---|---|---|')
for ch in range(1,7):
    for b,_ in POOL[ch]:
        tier=max(1,ch-1); hp=T[b][0]*15
        kf=hp/dps_vs(b,'고유',ch,tier,'화염'); kp=hp/dps_vs(b,'고유',ch,tier,'물리')
        r,ph=incoming(b,'고유',ch)
        P(f"| {ch} | {b} | {hp:,} | {kf:.0f}초 | {kp:.0f}초 | {r:.1f} | {100/ph:.1f} |")
P()
P('## 4. 이야기 보스 — Boss Data(2-68)로 다시 돌림')
P()
P('| 보스 | 장 | 처음 무기 · 화염 | 티어=장 · 화염 | 물리만 | 번개 | 빌드 절반 | 받는 피해 (처음 무기 · 화염 동안) | 소환단 몇 회 |')
P('|---|---|---|---|---|---|---|---|---|')
for bid,name,zone,ch,lvl,tgt,hits,outb in rows:
    def kill(tier,elem,bs=1.0):
        return sum(hpn/dps_vs(b,'고유' if bid!='changgwi' else '희귀',ch,tier,elem,bs,armour=armour,fire=fire) for b,hpn,hit,armour,fire,cur in outb)
    t0=kill(max(1,ch-1),'화염'); t1=kill(ch,'화염'); tp=kill(max(1,ch-1),'물리'); tl=kill(max(1,ch-1),'번개'); th=kill(max(1,ch-1),'화염',0.5)
    rate=min(sum(incoming(b,'고유',ch,dmg_override=hit)[0] for b,hpn,hit,armour,fire,cur in outb),100/INVULN)
    taken=rate*t0
    P(f"| {name} | {ch} | **{t0:.0f}초** | {t1:.0f}초 | {tp:.0f}초 | {tl:.0f}초 | {th:.0f}초 | {taken:.0f} | {taken/12:.1f} |")
print('\n'.join(out))
