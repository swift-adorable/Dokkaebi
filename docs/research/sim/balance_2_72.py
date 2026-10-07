# 결정 2-72 — 위험(한 판 15조우 중 한 무리에게 체력 100 넘게 맞는 판의 비율) 5~10%를 먼저 맞춘 값.
# 기준: Normal · 보통 플레이어(적 공격의 1/3). 손잡이: 장마다 무리 크기 · 등급 분포 · 개체 체력 배율(Combat 5절 「개체 배율은 허용」).
# 다시 돌리기: python3 balance_2_72.py
import sys, random, statistics as st
exec(open('balance_2_69.py').read().split("R={'A'")[0])
random.seed(11)
SWARM=(5,8)
CFG={1:((2,4),(80,17,3),1.0),2:((2,3),(60,30,10),1.5),3:((2,3),(60,30,10),1.0),
     4:((2,3),(70,23,7),1.0),5:((2,4),(70,23,7),1.5),6:((2,4),(70,23,7),3.2)}
HPS={c:CFG[c][2] for c in CFG}
def enc(ch,tier,dmgx,hpx):
    first=pick(POOL[ch]); lo,hi=PACK[ch]; n=random.randint(lo,hi)
    if ch==4 and first=='잡귀': n=random.randint(*SWARM)
    grp=[]
    for i in range(n):
        b=first if i==0 else pick(POOL[ch])
        g='일반' if b in NORMAL_ONLY.get(ch,()) else pick(RAR[ch])
        grp.append((b,g))
    alive=[(b,g,T[b][0]*G[g][0]*HPS[ch]*hpx) for b,g in grp]
    alive.sort(key=lambda x:x[2]/dps_vs(x[0],x[1],ch,tier,'화염'))
    taken=0
    while alive:
        b,g,hp=alive[0]; k=hp/dps_vs(b,g,ch,tier,'화염')
        rate=min(sum(incoming(x,y,ch,diff=dmgx,hitrate=HR)[0] for x,y,_ in alive),100/INVULN)
        taken+=rate*k; alive.pop(0)
    return taken
def run(ch,n,dmgx=1.0,hpx=1.0):
    D=[];W=0
    for _ in range(n):
        d=0;w=0
        for _ in range(15):
            x=enc(ch,max(1,ch-1),dmgx,hpx); d+=x; w=max(w,x)
        D.append(d); W+= w>=100
    return st.median(D)/60, W/n*100
for c,(pk,r,h) in CFG.items():
    PACK[c]=pk; RAR[c]=[('일반',r[0]),('마법',r[1]),('희귀',r[2])]
mode=sys.argv[1] if len(sys.argv)>1 else 'normal'
if mode=='normal':
    print('| 장 | 무리 | 등급(일반 · 마법 · 희귀) | 개체 체력 배율 | 한 판 소환단 | 위험 |')
    print('|---|---|---|---|---|---|')
    for c in range(1,7):
        a,b=run(c,1200); pk,r,h=CFG[c]
        print(f"| {c} | {pk[0]}~{pk[1]}{' (잡귀 떼 5~8)' if c==4 else ''} | {' · '.join(map(str,r))} | ×{h:g} | {a:.1f}개 | {b:.1f}% |")
else:
    print('| 장 | 악몽 소환단 · 위험 | 지옥 소환단 · 위험 |')
    print('|---|---|---|')
    for c in range(1,7):
        a,b=run(c,500,1.5,1.0); x,y=run(c,500,2.0,1.25)
        print(f"| {c} | {a:.1f}개 · {b:.0f}% | {x:.1f}개 · {y:.0f}% |")
