# Boss Data — 결정 2-67 뒤 (2026-10-06 사용자 결정): 목표 처치 중간 20초 · 장 40초 · 최종 60초,
# 맞아도 되는 대수 8 · 6 · 5, 빌드 배율은 enemy_sim.py의 BUILD 그대로(바꾸면 이 스크립트를 다시 돌린다).
# 몸은 지금(StoryTable) 그대로 — 현무 방어도 2 · 구미호 화염 약점 없음만 바꾼다.
exec(open('enemy_sim.py').read())
TARGET={0:20,1:40,2:60}; HITS={0:8,1:6,2:5}
# 결정 2-69: 한 대 피해 = min(맞아도 되는 대수로 정한 값, 「버티는 시간 = 처치 시간 × 0.75」로 정한 값)
# 버티는 시간은 기준 플레이어(Normal · 적 공격의 1/3을 맞는다)로 잰다. 3점사(순라귀 몸)는 셋 중 하나만 맞아도 맞는다.
HR_BASE=1/3; TTD_RATIO=0.75; BURST_BODIES={'순라귀'}
# (id, 이름, 구역, 장, 몸들, 등급, 단계 0=중간 1=장 2=최종)
B=[('yagwanggwi','야광귀','1-1',1,['잡귀'],'고유',0),('dalgyal','달걀귀신','1-2',1,['무주귀'],'고유',0),
('hyeonmu','현무','1-3',1,['절굿공이귀'],'고유',1),('eodukssini','어둑시니','2-1',2,['허깨비'],'고유',0),
('gangcheori','강철이','2-3',2,['수귀'],'고유',0),('cheongnyong','청룡','2-4',2,['번개귀'],'고유',1),
('wongwi','처녀귀신 · 몽달귀신','3-2',3,['무주귀','허깨비'],'고유',0),('dueoksini','두억시니','3-2',3,['왕지네'],'고유',0),
('jujak','주작','3-3',3,['순라귀'],'고유',1),('duduri','두두리','4-1',4,['절굿공이귀'],'고유',0),
('kkeomeoksari','꺼먹살이','4-2',4,['잡귀'],'고유',0),('baekho','백호','4-3',4,['잡귀'],'고유',1),
('changgwi','창귀','5-1',5,['허깨비','허깨비','허깨비'],'희귀',0),('sangun','산군','5-1',5,['절굿공이귀'],'고유',0),
('haetae','해태','5-2',5,['순라귀'],'고유',1),('samjogo','삼족오','6-1',6,['번개귀'],'고유',0),
('gumiho','구미호','6-2',6,['허깨비'],'고유',2)]
ARMOUR_OVERRIDE={'hyeonmu':2}     # 1장 무기는 관통 0
FIRE_OVERRIDE={'gumiho':1.0}      # 첫 구슬(불)이 최종 보스의 약점이 되지 않게
def r10(x): return int(round(x/10.0))*10 if x>=100 else int(round(x))
rows=[]
for bid,name,zone,ch,bodies,grade,lvl in B:
    tgt=TARGET[lvl]; hits=HITS[lvl]; tier=max(1,ch-1); wd,wi,wp=W[tier]
    per=tgt/len(bodies); out=[]
    for b in bodies:
        hp,dmg,pen,ar,kind,ph,fi,li=T[b]
        armour=ARMOUR_OVERRIDE.get(bid, ar+G[grade][2])
        fire=FIRE_OVERRIDE.get(bid, fi)
        dps=wd/wi*BUILD[ch]*af(armour,wp)*fire
        hpn=r10(per*dps)
        hit_cap=100/(hits*af(ARM[ch],pen))
        eff_sum=sum((1-(1-HR_BASE)**3 if x in BURST_BODIES else HR_BASE)*af(ARM[ch],T[x][2]) for x in bodies)
        hit_ttd=100*ATKINT/(eff_sum*TTD_RATIO*tgt)
        hit=max(1,round(min(hit_cap,hit_ttd)))
        cur=hp*G[grade][0]
        out.append((b,hpn,hit,armour,fire,cur))
    rows.append((bid,name,zone,ch,lvl,tgt,hits,out))
print('| 보스 | id | 구역 | 단계 | 목표 처치 · 대수 | 몸 | 체력 | 한 대 피해 | 방어도 | 화염 배율 | 이전 체력(유형 × 등급) |')
print('|---|---|---|---|---|---|---|---|---|---|---|')
for bid,name,zone,ch,lvl,tgt,hits,out in rows:
    for i,(b,hpn,hit,armour,fire,cur) in enumerate(out):
        print(f"| {name if i==0 else '〃'} | `{bid}` | {zone} | {['중간','장','최종'][lvl]} | {tgt}초 · {hits}대 | {b} | **{hpn:,}** | {hit} | {armour:g} | {fire:g} | {cur:,.0f} |")
print()
for bid,name,zone,ch,lvl,tgt,hits,out in rows:
    hs=', '.join(str(o[1]) for o in out); ds=', '.join(str(o[2]) for o in out)
    ao=ARMOUR_OVERRIDE.get(bid); fo=FIRE_OVERRIDE.get(bid)
    extra=(f", armour: {ao}f" if ao is not None else "")+(f", fire: {fo}f" if fo is not None else "")
    print(f'        S("{bid}", new[] {{ {hs} }}, new[] {{ {ds} }}{extra}),')
