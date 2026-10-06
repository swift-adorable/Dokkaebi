# 날씨 시뮬레이션 — 덕코프 원자료의 「단계 · 막이 · 중첩 효과」는 그대로, 우리 쪽 속도만 정한다
# 덕코프 값 [확인됨 — 비공식 위키 buff 표 · 우리 원자료 raw/net_pages.jsonl]:
#   추위 #2101  100중첩 · 중첩당 이동 -0.25% · 쏘는 속도 -0.3% · 에너지 소모 +1.3% · 냉기 피해 +0.3% · 100중첩이면 동상
#   동상 #2201  1.5초마다 1 피해
#   탈수 #1     체력 회복 -70% · 이동 -20%
#   교란 #1111  (폭풍 Ⅰ) 0.5초마다 중첩당 1 피해 · 폭풍 방어 1이면 면역 / 왜곡 #1112 (폭풍 Ⅱ) 방어 2이면 면역
# 우리 값: 체력 100 · 수분 0.10/초 · 에너지 0.06/초 (Survival 3절) · 소환단 1회 12
import math, random
HP=100; WATER_RATE=0.10; ENERGY_RATE=0.06; SOHWAN=12

def cold(deficit, minutes, k=100/900, fire_at=None, fire_secs=30):
    """deficit = 한파 단계 - 방한. k = 부족 1단계당 초당 쌓이는 추위 중첩 (15분에 100)."""
    st=0.0; hp_lost=0.0; energy=0.0; t100=None
    for s in range(int(minutes*60)):
        if fire_at and fire_at*60<=s<fire_at*60+fire_secs: st=max(0,st-5)   # 화로 · 모닥불 곁: 초당 5중첩 녹는다 [제안]
        else: st=min(100, st+deficit*k)
        if st>=100:
            t100=t100 or s/60; hp_lost+=1/1.5          # 동상 — 덕코프 그대로
        energy+=ENERGY_RATE*(1+0.013*st)
    return dict(t100=t100, hp=hp_lost, move=-0.25*st, fire=-0.3*st, energy=energy)

def dust(deficit, minutes, interval={0:None,1:18,2:2.5,3:1.0}):
    """흙비 — 덕코프 폭풍처럼 막이가 부족한 단계만큼 피해. 간격(초)은 목표에서 거꾸로 잡았다."""
    iv=interval[min(deficit,3)]
    return 0 if iv is None else minutes*60/iv

def heat(mult, minutes, start=100):
    need=WATER_RATE*mult*minutes*60
    drinks=max(0, math.ceil((need-start)/40))        # 샘물 +40
    t0=start/(WATER_RATE*mult)/60                    # 아무것도 안 마시면 탈수까지(분)
    return drinks, t0

def weather_counts(p, raids=12, trials=100000, rest=False, seed=1):
    random.seed(seed); kinds=list(p); w=list(p.values())
    any2=0; tot={k:0 for k in kinds}
    for _ in range(trials):
        seen2=False
        for _ in range(raids):
            k=random.choices(kinds,w)[0]
            if rest and k=='궂은 날 Ⅱ': k=random.choices(kinds,w)[0]   # 하룻밤 쉬고 다시 뽑기 (한 번)
            tot[k]+=1; seen2|= k=='궂은 날 Ⅱ'
        any2+=seen2
    return {k:v/trials for k,v in tot.items()}, any2/trials
