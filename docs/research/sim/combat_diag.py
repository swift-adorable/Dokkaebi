exec(open('combat_sim.py').read().split("out=[]")[0])
import statistics as st
def med(ch,tier,**kw): 
    R=[raid(ch,tier,**kw) for _ in range(1500)]; return st.median([r[1] for r in R]), sum(1 for r in R if r[2]>=100)/len(R)
base=POOL[1]
print('1장 기준', med(1,1))
POOL[1]=[('잡귀',100)]; print('1장 절굿공이귀 없음', med(1,1)); POOL[1]=base
for g in ['일반','마법','희귀']:
    hp=T['절굿공이귀'][0]*G[g][0]; print('1장 절굿공이귀',g,'처치',round(hp/dps_vs('절굿공이귀',g,1,1,'화염')),'초 · 번개',round(hp/dps_vs('절굿공이귀',g,1,1,'번개')),'초')
b4=POOL[4]; POOL[4]=[('허깨비',50),('순라귀',50)]; print('4장 잡귀 떼 없음', med(4,3)); POOL[4]=b4
b3=POOL[3]; POOL[3]=[('왕지네',100)]; print('3장 침귀 없음', med(3,2)); POOL[3]=b3
print('4장 기준',med(4,3))
