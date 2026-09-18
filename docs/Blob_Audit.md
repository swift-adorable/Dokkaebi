# 문서 ↔ 코드 전수 대조 (2026-09-18)

> 기획 문서 11종과 `Assets/Scripts` 전체를 대조한 감사 기록이다.
> 조치가 끝난 항목은 이 문서에서 **지운다.** 남아 있는 것이 곧 남은 부채다.
> 모든 항목은 `파일:줄번호`로 확인했다. 확인하지 못한 것은 그렇게 적었다.

## 요약

| 등급 | 수 | 뜻 |
|---|---|---|
| **A. 끊긴 배선** | ~~10~~ **6** | 코드가 있고 테스트도 통과하는데 **아무도 부르지 않는다** |
| **B. 문서 간 모순** | ~~4~~ **2** | 문서끼리 다른 말을 한다. 결정이 필요하다 |
| **C. 수치 불일치** | 9 | 문서와 코드의 숫자가 다르다 |
| **D. 데이터 모델 공백** | ~~6~~ **4** | 문서가 요구하는 것을 담을 필드가 없다 |
| **E. 문서 TBD인데 코드가 확정** | 13 | 코드가 먼저 값을 정했다. 문서를 맞춰야 한다 |

**가장 큰 발견** — 순수 로직 + EditMode 테스트 전략이 **정확성은 검증했지만 연결은 검증하지 못했다.**
`DamageResolver`는 맞게 계산하고, `EngagementPlanner`는 맞게 판단하고, 각인 24종은 규칙을 지킨다.
그런데 그중 상당수가 **게임 실행 경로에 연결되어 있지 않다.** 테스트가 전부 초록인데 게임에는 없다.

---

## A. 끊긴 배선 — 코드는 있는데 부르는 곳이 없다

우선순위 순. 각 항목은 **호출부 한 곳만 이으면** 살아난다.

| # | 무엇이 | 어디에 있는데 | 호출부 |
|---|---|---|---|
| ~~A1~~ | ~~장비 착용~~ | **해결 (6-H)** — 가방 화면에서 장비를 골라 8슬롯에 착용·해제한다 | `UI/InventoryScreenEquip.cs` |
| ~~A2~~ | ~~사망 시 소지품 소실~~ | **해결 (6-H)** — `BlobController.HandleDied`가 호출한다 | `Player/BlobController.cs` |
| ~~A3~~ | ~~런 초기화~~ | **해결 (6-H)** — 사망 처리에서 함께 호출한다 | `Player/BlobController.cs` |
| ~~A4~~ | ~~과중량 페널티~~ | **해결 (6-H)** — `PlayerMovement.SpeedScale` · `PlayerDash.DistanceScale` | `Core/Equipment/LoadoutSnapshot.cs` |
| **A5** | **기폭(원소 작렬)** | `Core/Combat/DetonationResolver.cs` | **테스트뿐.** 기폭 Core 3종이 아무 효과도 내지 않는다 |
| **A6** | **잔류물 5종** | `Core/Combat/GroundEffectTable.cs` | **없음.** 「마름쇠」·「유지되는 대지」·「잔류물 효력」 3종이 기댈 대상이 없다 |
| **A7** | **난이도 배율 6단** | `Core/Combat/DifficultyLevel.cs:26-51` | **테스트뿐.** `Health.TakeDamage`의 배율 인자를 아무도 넘기지 않는다 |
| **A8** | **내성 「가장 낮은 것 하나만」** | `ElementalResistances.TakeLowest:78-88` | **테스트뿐.** 몬스터 속성을 내성에 합성하는 경로 자체가 없다 |
| ~~A+~~ | ~~상태이상 면역~~ | **해결 (6-I)** — `Health.IsImmuneTo`가 항상 false였다. 역치 각인 3종 + 얼굴 마스크 15종이 이제 작동한다 | `Player/PlayerLoadout.cs` |
| **A9** | **패시브 효과 17종 중 13종** | `Core/Progression/PassiveEffectType.cs` | 실제로 읽히는 것은 `CarrySlots` · `CarryWeight` · `AbsorbAmount` · `LootRolls` **4종뿐** |
| ~~A10~~ | ~~장비 에셋 82종~~ | **해결 (6-H)** — `PlayerWeapon.ApplyProfile` → 탄에 `SetWeaponBase` | `Player/PlayerLoadout.cs` |

> **A1 · A2 · A3 · A4 · A10은 6-H에서 해결했다.**
> 이제 「입고 → 쏘고 → 무거우면 느리고 → 죽으면 잃는다」 한 바퀴가 돈다.
> 남은 것은 **A5~A9** — 기폭 · 잔류물 · 난이도 · 내성 합성 · 패시브 효과 13종이다.

---

## B. 문서 간 모순 — 결정이 필요하다

### ~~B1. 해금 트리 vs 패시브~~ — 해결 (6-H)

`Progression_System.md` 2절의 해금 트리 7계열과 `Passive_System.md` 5절의 17효과가
**가방 · 소지 중량 · 흡수 범위 · 흡수 효율 · 시체 회수 5개 축에서 정면으로 겹친다.**

코드는 패시브만 구현했다. 해금 트리는 존재하지 않는다.

→ **결정: `Progression_System.md` 2절의 해금 트리 표를 삭제하고 패시브로 일원화했다.**

### ~~B2. 체력과 시야는 누구의 것인가~~ — 해결 (6-H)

| 문서 | 주장 |
|---|---|
| `Passive_System.md` 0절 | 체력 · 시야는 **패시브 금지** |
| `Progression_System.md` 2절 | 해금 트리에 「조직 강화 +30 체력」 「시야 확장 +15」 |
| `Combat_Baseline.md` 1절 | 「체력은 장비와 **계정 레벨**로만 오른다」 |

코드는 `Passive` 편을 들었고(테스트가 `Health`를 막는다), 그 결과
`CombatConstants.PlayerHealthCap = 180`의 **「계정 트리 30」이 근거 없는 30으로 남았다.**

→ **결정: 체력 100 고정.** `PlayerHealthCap = PlayerBaseHealth = 100`.
>
> 근거 — 최대 체력을 주는 장비 에셋이 **0종**이고(각인 4종이 깎기만 한다),
> `Equipment_System.md`는 방어구에 체력을 주지 않으며, 패시브는 금지한다.
> 「장비 50」과 「계정 트리 30」 **둘 다 어디에도 근거가 없었다.**
> 장비가 주는 생존은 방어도이며 피해 공식이 이미 그 역할을 한다.
>
> 되돌리려면 `CombatConstants.PlayerHealthCap` 한 줄과
> `LoadoutSnapshotTests.장비는_최대_체력을_올리지_않는다`만 고치면 된다.
> 각인이 깎고 난 최저치는 `PlayerMinHealth = 40`으로 막았다.

### B3. 내성 중첩 — 곱연산인가 최저값인가

- `Combat_Baseline.md` 2-2절: 「곱연산으로 중첩하지 않고 **가장 낮은 배율 하나만**」
- `Hunting_System.md`: 「경화 외피 레이드 + 경화 적 → 물리 내성 **×0.25**」 ← 곱연산 예시

코드는 `TakeLowest`를 구현했으나 호출부가 없다(A8).

→ **결정 필요.** `Combat_Baseline`이 전투 정본이므로 `Hunting` 쪽을 고치는 것이 맞다고 본다.

### B4. 점화 중첩 — 표와 검산이 서로 맞지 않는다

같은 4절 안에서 표는 「최대 중첩 3」인데 검산은 「점화 2.5/초(총 10)」로 **1중첩을 전제**한다.
3중첩이면 총 30이다. 중첩당 피해가 비례 증가하는지, 방어도 −0.5만 누적되는지 정해져 있지 않다.

감전 · 냉각 · 출혈도 같은 문제다 — 최대 중첩만 있고 **중첩당 효과가 없다.**

---

## C. 수치 불일치

| # | 항목 | 문서 | 코드 | 근거 |
|---|---|---|---|---|
| C1 | **「냉각」이라는 상태가 코드에 없다** | 냉각(−40%, 6중첩) → 6중첩 시 **동결**(행동 불능) | `Freeze` 하나가 −40% 감속을 한다. 행동 불능이 없다 | `Core/Skills/StatusEffectType.cs:18`, `StatusEffectTable.cs:70` |
| C2 | 점화 최대 중첩 | 3 | **1** (갱신형) | `StatusEffectTable.cs:53` |
| C3 | 감전 최대 중첩 | 6 | **1** | `StatusEffectTable.cs:66` |
| C4 | 냉각 최대 중첩 | 6 | **1** | `StatusEffectTable.cs:70` |
| C5 | 시체 회수 해금 | 계정 Lv10 | Lv **9** + 선행 `rec_safe_1` | `Editor/PassiveAssetGenerator.cs:136-140` |
| C6 | 흡수 범위 단위 | +20% | **미터** (+1.5m) | `PassiveEffectType.cs:32`, `PassiveAssetGenerator.cs:104` |
| C7 | 얼굴 방어구 티어 | 1~6 전 티어 | **1 · 2 · 4 · 6만.** 3 · 5가 없다 | `Editor/ArmourAssetGenerator.cs:293-314` |
| C8 | 몸통 일반 적재 | +2~3 | 3,3,2,2,**1,0** — 티어 5·6이 범위 밖 | `ArmourAssetGenerator.cs:44-52` |
| C9 | 지혈 붕대 | 0.05kg / 스택 3 | **0.2kg / 스택 5** | `Editor/LootAssetGenerator.cs:58` |

**C1~C4가 한 덩어리다.** 상태이상 중첩 체계가 문서와 코드에서 통째로 다르다.
게다가 **테스트가 코드 쪽을 계약으로 고정하고 있다** (`StatusEffectStateTests.cs:95,179-185` —
"점화는 갱신형입니다", "동결은 속도를 40퍼센트 깎는다"). 고치려면 테스트도 같이 고쳐야 한다.

### C10. 수리 규칙이 반대로 구현되어 있다

문서: 「수리해도 최대 내구도까지 돌아오지 않는다. 장비가 자연 소멸하는 경제 싱크다」
코드: `ItemStack.Repair()`가 최대치까지 **완전 회복**시킨다 (`Core/Items/ItemStack.cs:102-118`).
테스트가 이것을 계약으로 고정했다 (`InventoryTests.cs:237`).

> 구조적 문제: `MaxDurability`가 `ItemDefinition`(공유 에셋)에만 있어
> **개체별 상한 감소를 표현할 자리가 없다.** `ItemStack`에 필드 추가가 필요하다.

---

## D. 데이터 모델 공백 — 담을 자리가 없다

### ~~D1. 스킬에 「증가%」 축이 없다~~ — 해결 (6-I)

`Combat_Baseline` 2절의 피해 공식에 `× (1 + Σ 증가%)` 항이 있는데,
`SkillDefinition`에 **피해 증가율 · 상태이상 위력 · 치명타 필드가 하나도 없다.**
`WeaponModifiers`가 표현할 수 있는 것은 궤도 · 탄 수 · 간격 · 수명 · 속도 · 적재 속성뿐이다.

결과: **Support 35종 중 21종이 대가만 적용되고 효과가 적용되지 않는다.**
「불난 집 부채질」 「치명적인 중독」 「기세」 등을 끼우면 **손해만 본다.**

> **해결** — `SkillDefinition`에 `damageIncrease` · `ailmentPower` ·
> `ailmentDurationMultiplier` · `rangeMultiplier` · 조건부 3필드를 추가했다.
> `WeaponModifiers`가 합산하고 `BulletController`가 명중 시점에 적용한다.
> Support 21종에 값을 채웠고 `SkillEffectTests`가 「효과 없는 Support」를 막는다.
>
> **남은 공백 2종** — 「화염 조율」·「원소 융합」은 속성 전환 축이 없어
> 여전히 효과가 없다. 테스트의 `KnownGaps`에 남겨 계속 보이게 했다.

### ~~D2. `lifetimeMultiplier` 하나가 두 의미를 겸한다~~ — 해결 (6-I)

문서의 대가 「지속시간」(상태이상·잔류물)과 「유효 사거리」가 같은 필드를 쓴다.
그래서 「유지되는 대지」(잔류물 +100%)가 **투사체 사거리를 2배로 만들었다.**

> **해결** — `ailmentDurationMultiplier`(상태이상·잔류물)와 `lifetimeMultiplier`(투사체 수명)를
> 분리하고, 「유효 사거리」 대가는 `rangeMultiplier`로 옮겼다.
> `지속시간_Support가_투사체_수명을_건드리지_않는다` 테스트가 재발을 막는다.

### D3. 차단형 Support 4종이 구조적으로 영구 무효

「번제」(화염 차단) 같은 차단형은 요구 태그 때문에 **그 상태를 만드는 유일한 Core에만** 장착 가능한데,
`IsStatusBlocked`가 **빌드 전역**이라 그 Core의 상태 생성이 꺼진다. 같은 Core 중복도 금지다.
→ 상태 발생원이 사라져 영구히 작동하지 않는다 (`SocketedBuild.cs:584-607`).

### D4. 소모품 분류를 담을 필드가 없다

`ItemKind.Consumable` 하나뿐이라 「같은 분류는 덮어쓴다」를 판정할 수 없다.

### D5. 부착물의 옵션 축이 없다

`EquipmentStatType`에 반동 · 소음 · 장탄 · 조준 · 탄퍼짐이 없다.
`WeaponDefinition.attachmentSlots`만 있고 부착물 자체가 없다.

### D6. 아이템 태그 「반출 불가」 「등록 불가」 「거래 불가」가 없다

`ItemDefinition`에 플래그가 없다. `SurvivesDeath`(각인)만 있다.

---

## E. 문서 TBD인데 코드가 값을 정해버린 것

문서를 코드에 맞춰 확정하거나, 코드를 되돌려야 한다.

| # | 항목 | 코드가 정한 값 | 위치 |
|---|---|---|---|
| E1 | 과중량 3단계 임계치 · 페널티 | 1.0/1.25/1.5 → ×0.8/×0.6/×0.35 | `Core/Items/EncumbranceLevel.cs:30-51` |
| E2 | 기본 적재 · 소지 중량 | **12칸 / 20kg** | `Player/PlayerInventory.cs:16,19` |
| E3 | 치명타 기본 배율 | **1.5** | `CombatConstants.cs:47` |
| E4 | 방어도 · 방어 관통 상한 | **7** | `CombatConstants.cs:44` |
| E5 | 최소 피해 하한 | **1** (면역이 아니면 반드시 1 이상) | `DamageResolver.cs:83-87` |
| E6 | 재화 이름 · 기호 | **「크레딧」 ₡** | `Managers/PassiveManager.cs:25-27` |
| E7 | 동시 공격 제한 | **2** (임대 3초) | `Core/AI/AttackTokenPool.cs:22,25` |
| E8 | 적 감지 거리 · 반응 시간 | **18m / 0.35초** | `Enemy/EnemyBrain.cs:24,35` |
| E9 | 적 예비동작 · 쿨다운 | **0.35초 / 1.2초** | `Enemy/EnemyAttack.cs:55-59` |
| E10 | 젬의 무게 · 가치 | Core 0.8 / Support 0.4 / Meta · 전령 0.6 kg | `Editor/SkillGemAssetGenerator.cs:26,101` |
| E11 | 젬 드랍 확률 | **18%** | `Managers/SkillManager.cs:33` |
| E12 | 무기별 부착 슬롯 | 0/1/2/3/4/6 | `Editor/WeaponAssetGenerator.cs` |
| E13 | 마모(33%) 시 방어 옵션 | **절반** | `EquipmentModifiers.cs:46-53` |
| E14 | 기폭 반경 · 중첩 배수 | **3m / 중첩당 1.2배** | `DetonationResolver.cs:49,52` — **어느 문서에도 근거가 없다** |

---

## F. 미구현 (로드맵상 정상 — 참고용)

7단계 이후로 예정된 것들이다. 지금 문제 삼지 않는다.

- **추출 성공 판정 자체** (8단계) — 추출 지점 · 정산 진입점이 코드에 한 줄도 없다
- 적 원형 9종 · 등급 4 · 몬스터 속성 13종 — **코드에 0종**
- 진영 5종 (`Team`은 Player/Enemy/Neutral 3종뿐), 시설 상태 3종, 레이드 특성 10종
- 계정 레벨 경험치 · 정산 · 세이브 (현재 인스펙터 값)
- 벙커 전부 (건물 · NPC · 상점 · 제작 · 의뢰)
- 소모품 22종 중 19종, 소모품 사용 API, 퀵슬롯 기능
- 장 · 게이트 · 표식 · 레코더 · 업적 · 엔딩
- Meta 5종의 에너지 축적, 전령 5종의 처치 시 연쇄
- 세트 계열 · 격리 방호 장비 (`ContainmentWard` 판정은 있으나 **주는 에셋이 0종**)

---

## 그 외 — 문서에 없는데 코드에 있는 것 (일부)

| 항목 | 위치 | 비고 |
|---|---|---|
| 상태이상 피해는 무적을 무시한다 | `Core/Health.cs:172-174` | 무적 0.6초의 예외. 문서 미기재 |
| 내성 0 = 완전 면역 (최소 피해 1 예외) | `DamageResolver.cs:69-71` | 하한 1과의 우선순위가 문서에 없다 |
| 머리/몸통 방어도 분리 | `DefenceProfile.cs:12-17` | `Combat_Baseline`은 방어도 1개를 전제한다 |
| 소켓에 Core가 없으면 Support 장착 불가 | `SocketedBuild.cs:192` | 소켓 규칙 표에 없는 제약 |
| 가방이 꽉 차면 젬을 뺄 수 없다 | `SkillManager.cs:441-458` | 문서는 「탈착 가능」까지만 |
| 상호 배타 쌍이 문서보다 3쌍 많다 | `SkillAssetGenerator.cs:303/415, 428/435, 444/451` | 문서는 1쌍만 적는다 |
| 전리품 창 격자 5×2 = 10칸 | `UI/LootWindowUI.cs:19` | 컨테이너는 8칸이라 2칸이 안 그려진다 |
| 가방 화면 격자 6×6 = 36칸 | `UI/InventoryScreenUI.cs:29` | 적재가 36을 넘으면 안 그려진다 |
| 적 원형 이름 「포자충 · 사냥개 · 감시자」 | `Enemy/EnemyAttack.cs:6,9` | 문서의 9종 이름에 없다 |

### 코드 주석의 문서 절 번호가 전반적으로 어긋나 있다

문서를 감량하면서 절 번호가 바뀌었는데 주석이 따라가지 못했다.
`DamageResolver.cs:15`, `SocketUnlockTable.cs:38`, `SkillCodex.cs:5`, `SkillManager.cs:7`,
`EquipmentLoadout.cs:119`, `EncumbranceLevel.cs:3` 등 다수.

→ 절 번호 대신 **절 제목**을 인용하도록 바꾸면 다시 어긋나지 않는다.

---

## 확인 불가

- 세이브/로드 계층 — `PassiveState.Restore`라는 복원 진입점만 있고 저장 시스템 파일을 찾지 못했다
- 생성된 에셋이 생성기 실행 이후 손으로 수정되었는지 — 대조는 **생성기 코드 기준**이다
- PlayMode 테스트 통과 여부 — 이번 감사는 읽기 전용이다
