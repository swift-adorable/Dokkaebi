# Blob — 기획 문서

게임 타이틀 **《젤리바디: 에어리어 0》** / 코드네임 **Blob**
Unity 6 Mobile · Top-Down Shooter + Roguelite + Extraction Looting

## 문서

| 문서 | 담당 | 한 줄 |
|---|---|---|
| [Master_Prompt](Blob_Master_Prompt.md) | 최상위 규약 | 어떻게 일하는가 |
| [Story](Blob_Story.md) | 스토리 · 6장 · 단서 | **왜 그렇게 되었는가** |
| [Combat_Baseline](Blob_Combat_Baseline.md) | 수치 | **얼마나 아픈가** |
| [Skill_System](Blob_Skill_System.md) | 스킬 53종 | 무엇으로 죽이는가 |
| [Equipment_System](Blob_Equipment_System.md) | 장비 · 무기 | 무엇을 입고 가는가 |
| [Hunting_System](Blob_Hunting_System.md) | 적 | 무엇을 상대하는가 |
| [Progression_System](Blob_Progression_System.md) | 진행 | 왜 다시 들어가는가 |
| [Passive_System](Blob_Passive_System.md) | 패시브 · 계정 5계열 | **런을 넘어 무엇이 남는가** |

개발 환경 → [`../CONTRIBUTING.md`](../CONTRIBUTING.md)
레퍼런스 조사 원문 → [`research/`](research/)

## 읽는 순서

처음이면 **Story → Master_Prompt → Combat_Baseline** 순으로 읽는다.
Story가 "왜"를, Master_Prompt가 "규칙"을, Combat_Baseline이 "숫자"를 준다. 나머지는 참조용이다.

## 두 가지 불변 규칙

1. **수치는 `Combat_Baseline.md`에만 존재한다.** 다른 문서는 참조만 하고 공식을 복제하지 않는다.
2. **시스템 경계를 넘지 않는다.**
   스킬은 메커니즘을, 장비는 능력치를, 사냥은 상대를, 진행은 남는 것을 정한다.
   무기는 기본값만, Core는 속성만, Support는 궤도만 정한다.
   **스킬은 거는 쪽, 장비는 막는 쪽이다.**
