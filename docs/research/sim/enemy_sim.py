# 도깨비 적 구성 시뮬레이션 — Combat_Baseline 1·2·3·5절 값 그대로 + [가정] 표시 값
T={ # 유형: hp dmg pen armor 근/원 물리배율 화염배율 번개배율
 '잡귀':(20,8,0,0,'근',1,1,1),'절굿공이귀':(90,14,1,4,'근',1,1,2),'번개귀':(30,10,1,0,'원',1,1,1),
 '수귀':(25,16,2,0,'근',1,1,1),'왕지네':(45,6,0,0,'장판',1,1,1),'침귀':(60,14,3,1,'원',1,1,1),
 '허깨비':(120,20,0,0,'근',.66,1.5,1),'순라귀':(70,12,2,3,'원',1,1,2),'무주귀':(80,16,5,0,'근',.66,1,1)}
G={'일반':(1,1,0),'마법':(2,1.2,1),'희귀':(4.5,1.4,2),'고유':(15,1.8,3)}
W={1:(10,.40,0),2:(13,.35,1),3:(16,.32,2),4:(22,.45,3),5:(28,.50,5),6:(34,.45,6)}
BUILD={1:1.0,2:1.2,3:1.5,4:1.8,5:2.2,6:2.6}   # [가정] 보조 젬 · 패시브로 오르는 화력
ARM={1:1.5,2:2.5,3:3.25,4:4.25,5:5.25,6:6.25} # 방어구 티어 중간값 (머리=원거리 / 몸통=근접)
HITRATE=0.5; ATKINT=1.55                      # [가정] 공격의 절반을 맞는다 · 쿨다운 1.2 + 예비동작 0.35 (Audit E9)
af=lambda a,p:2/(max(a-p,0)+2)
def ttk(bodies,grade,ch,tier,elem='화염'):
    tot=0
    for b in bodies:
        hp,dmg,pen,ar,kind,ph,fi,li=T[b]; gh,gd,ga=G[grade]
        wd,wi,wp=W[tier]; mult={'물리':ph,'화염':fi,'번개':li}[elem]
        dps=wd/wi*BUILD[ch]*af(ar+ga,wp)*mult
        tot+=hp*gh/dps
    return tot
def ttd(bodies,grade,ch):
    inc=0
    for b in bodies:
        hp,dmg,pen,ar,kind,*_=T[b]; gh,gd,ga=G[grade]
        inc+=dmg*gd*af(ARM[ch],pen)/ATKINT*HITRATE
    return 100/inc, 100/(inc/HITRATE*ATKINT)  # 버티는 초, 맞아도 되는 대수(전부 맞을 때)
