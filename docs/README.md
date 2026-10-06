# Dokkaebi — 기획 문서

게임 타이틀 **《도깨비》** / 코드네임 **Dokkaebi**
(2026-10-02 세계관 교체 — 이전 《젤리바디: 에어리어 0》 문서는 [`archive/`](archive/))
Unity 6 Mobile · Top-Down Shooter + Roguelite + Extraction Looting

## 문서

| 문서 | 담당 | 한 줄 |
|---|---|---|
| [Master_Prompt](Dokkaebi_Master_Prompt.md) | 최상위 규약 | 어떻게 일하는가 |
| [Story](Dokkaebi_Story.md) | 스토리 설계 · 6장 · 단서 | **왜 그렇게 되었는가** |
| [Story_Script](Dokkaebi_Story_Script.txt) | 스토리 본문 v2.7 (초안) | 프롤로그 ~ 6장 끝 (엔딩) |
| [Naming](Dokkaebi_Naming.md) | 이름 · 화면 표기 | **무엇이라 부르는가** |
| [Glossary](Dokkaebi_Glossary.md) | 용어 · 레퍼런스 원어 대조 | 왜 그 용어인가 |
| [Mapping_2단계](Dokkaebi_Mapping_2단계.md) | 이전 이름 → 새 이름의 근거 | 왜 그 이름인가 (재확인 대기) |
| [Combat_Baseline](Dokkaebi_Combat_Baseline.md) | 수치 | **얼마나 아픈가** |
| [Skill_System](Dokkaebi_Skill_System.md) | 스킬 53종 | 무엇으로 죽이는가 |
| [Equipment_System](Dokkaebi_Equipment_System.md) | 장비 · 무기 | 무엇을 입고 가는가 |
| [Hunting_System](Dokkaebi_Hunting_System.md) | 적 | 무엇을 상대하는가 |
| [Progression_System](Dokkaebi_Progression_System.md) | 진행 | 왜 다시 들어가는가 |
| [Passive_System](Dokkaebi_Passive_System.md) | 패시브 · 계정 5계열 | **파밍을 넘어 무엇이 남는가** |
| [Imprint_System](Dokkaebi_Imprint_System.md) | 각인 8계열 | **죽어도 남는 것 — 전부 교환이다** |
| [Consumable_System](Dokkaebi_Consumable_System.md) | 소모품 4분류 | 가방 한 칸을 무엇에 쓰는가 |
| [Bunker_System](Dokkaebi_Bunker_System.md) | 벙커 · 제작 · 상점 · 퀘스트 | **파밍과 파밍 사이에 무엇을 하는가** |
| [Save_System](Dokkaebi_Save_System.md) | 저장 · 롤백 · 백업 | **무엇이 언제 남는가** |
| [Survival_System](Dokkaebi_Survival_System.md) | 수분 · 에너지 | 얼마나 버티는가 |
| [Map_System](Dokkaebi_Map_System.md) | 지도 · 미니맵 · 마커 | 어디에 있는가 |
| [Audit](Dokkaebi_Audit.md) | 문서 ↔ 코드 대조 | **지금 무엇이 어긋나 있는가** |
| [Decisions](Dokkaebi_Decisions.md) | 결정 대기 | **아직 근거 없이 굴러가는 값** |
| [Playtest_Checklist](Dokkaebi_Playtest_Checklist.md) | 플레이 검증 | **정말 되는가** |

개발 환경 → [`../CONTRIBUTING.md`](../CONTRIBUTING.md)
레퍼런스 조사 원문 → [`research/`](research/)

## 읽는 순서

처음이면 **Story → Master_Prompt → Combat_Baseline** 순으로 읽는다.
Story가 "왜"를, Master_Prompt가 "규칙"을, Combat_Baseline이 "숫자"를 준다. 나머지는 참조용이다.

## 두 가지 불변 규칙

1. **수치는 `Combat_Baseline.md`에만 존재한다.** 다른 문서는 참조만 하고 공식을 복제하지 않는다.
2. **시스템 경계를 넘지 않는다.**
   스킬은 메커니즘을, 장비는 능력치를, 사냥은 상대를, 진행은 남는 것을 정한다.
   무기는 기본값만, 핵심 젬은 속성만, 보조 젬은 궤도만 정한다.
   **스킬은 거는 쪽, 장비는 막는 쪽이다.**
