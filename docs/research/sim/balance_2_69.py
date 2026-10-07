# 결정 2-69 — Normal · 보통 플레이어(적 공격의 1/3을 맞는다)가 15분 한 판에 소환단 2~4개가 되게 맞춘 값.
# 다시 돌리기: python3 balance_2_69.py  (combat_sim.py의 모형을 그대로 쓴다)
exec(open('combat_sim.py').read().split("out=[]")[0])
import statistics as st, random
HR=1/3
NORMAL_ONLY={1:{'절굿공이귀'}}
PACK={c:(2,5) for c in range(1,7)}
SWARM=(8,12)
RAR={c:[('일반',75),('마법',20),('희귀',5)] for c in range(1,7)}
def encounter2(ch,tier):
    first=pick(POOL[ch]); lo,hi=PACK[ch]; n=random.randint(lo,hi)
    if ch==4 and first=='잡귀': n=random.randint(*SWARM)
    grp=[]
    for i in range(n):
        b=first if i==0 else pick(POOL[ch])
        g='일반' if b in NORMAL_ONLY.get(ch,()) else pick(RAR[ch])
        grp.append((b,g))
    alive=[(b,g,T[b][0]*G[g][0]) for b,g in grp]
    alive.sort(key=lambda x:x[2]/dps_vs(x[0],x[1],ch,tier,'화염'))
    t=0;taken=0
    while alive:
        b,g,hp=alive[0]; k=hp/dps_vs(b,g,ch,tier,'화염')
        rate=min(sum(incoming(x,y,ch,hitrate=HR)[0] for x,y,_ in alive),100/INVULN)
        taken+=rate*k;t+=k;alive.pop(0)
    return t,taken
def run(ch,n=1500):
    D=[];W=[]
    for _ in range(n):
        d=0;w=0
        for _ in range(15):
            t,x=encounter2(ch,max(1,ch-1)); d+=x; w=max(w,x)
        D.append(d);W.append(w)
    return st.median(D)/60, sum(1 for w in W if w>=100)/n*100
def report(tag=''):
    print(tag,' | '.join(f"{c}장 {a:.1f}개 {b:.0f}%" for c,(a,b) in ((c,run(c)) for c in range(1,7))))

R={'A':(90,9,1),'B':(85,13,2),'C':(80,17,3),'D':(75,20,5),'E':(70,23,7),'G':(60,30,10)}
CFG={1:((2,3),'A'),2:((2,4),'B'),3:((2,3),'D'),4:((2,4),'C'),5:((2,3),'E'),6:((3,5),'G')}
SWARM=(5,8)
for c,(pk,k) in CFG.items():
    PACK[c]=pk; a,b,cc=R[k]; RAR[c]=[('일반',a),('마법',b),('희귀',cc)]
print('| 장 | 무리 | 등급(일반 · 마법 · 희귀) | 한 판 소환단 | 한 조우에 쓰러질 위험 |')
print('|---|---|---|---|---|')
for c in range(1,7):
    a,b=run(c,1500); pk,k=CFG[c]
    print(f"| {c} | {pk[0]}~{pk[1]}{' (잡귀 떼 5~8)' if c==4 else ''} | {' · '.join(map(str,R[k]))} | {a:.1f}개 | {b:.1f}% |")
