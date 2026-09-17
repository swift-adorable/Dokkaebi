# CONTRIBUTING — 개발 환경

기획 문서는 [`docs/`](docs/)에 있다. 이 문서는 **코드 작업 환경**만 다룬다.

## 환경

Unity **6000.4.11f1** / URP 17.4.0 / Input System 1.19.0 / com.unity.test-framework 1.6.0

## 어셈블리

| 어셈블리 | 용도 |
|---|---|
| `Blob.Runtime` | 게임 코드 |
| `Blob.Editor` | 에디터 전용 |
| `Blob.Tests.EditMode` | 테스트 코드 (Editor 전용) |
| `Blob.Tests.Fixtures` | 테스트용 MonoBehaviour (전 플랫폼, `UNITY_INCLUDE_TESTS`) |

## 폴더 구조

```
Assets/
├── Art/  Audio/  Material/  Prefabs/  Scenes/  Settings/
├── Data/
│   ├── ScriptableObjects/
│   │   ├── Skills/        스킬 정의
│   │   ├── Equipment/     장비·무기 정의 (슬롯별 하위 폴더)
│   │   ├── Hunting/       원형 / 등급 배율 / 몬스터 속성 / 스폰 테이블
│   │   └── Progression/   장 / 해금 노드 / 의뢰 / 레코더
│   └── SaveData/          도감, 적재, 보유 장비, 계정 레벨, 해금 상태
├── Resources/
│   ├── SkillCatalog.asset       에디터 도구가 자동 생성. 직접 편집 금지
│   └── EquipmentCatalog.asset   동상
├── Scripts/
│   ├── Core/
│   │   ├── Combat/        피해 공식, 방어도, 사거리, 상태이상
│   │   ├── Skills/        태그, 정의, 필터, 충돌 우선순위 큐, 합성 발사
│   │   ├── Equipment/     슬롯, 옵션, 무게·내구도, 게이트
│   │   ├── Hunting/       원형, 등급, 속성, 진영, 드랍, 스폰
│   │   └── Progression/   장, 해금 트리, 의뢰, 레코더, 추출
│   ├── Managers/  Player/  Enemy/  Weapon/  UI/  Camera/  Debug/  Editor/
└── Tests/
    ├── EditMode/
    └── Fixtures/
```

## 알려진 함정

- **에디터 전용 어셈블리의 MonoBehaviour는 `AddComponent`가 조용히 null을 반환한다.** 테스트 픽스처 MonoBehaviour는 반드시 `Fixtures` 어셈블리에 둔다. (ScriptableObject는 무관)
- MonoBehaviour / ScriptableObject의 클래스명은 파일명과 일치해야 한다.
- **파일을 이동할 때 `.meta`를 함께 옮겨야 GUID가 보존된다.**
- 셸에서 파일을 옮긴 뒤에는 Unity에서 `Assets/Refresh`를 실행해야 `CS2001`이 나지 않는다.
- **문서를 스크립트로 치환할 때** 앵커 문자열이 문서 앞쪽에도 있으면 구간이 복제된다. 절 헤더처럼 유일한 문자열을 앵커로 쓰고 `end > start`를 단언한다.
- `git`이 `.git/index.lock`을 지우지 못하면 커밋이 막힌다. (셸에 삭제 권한이 없는 환경)
- **Unity MCP의 `run_tests`는 전체 실행이 타임아웃된다.** 테스트가 200개를 넘으면서 MCP 응답 한도를 넘었다. `testFilter`에 클래스 전체 이름(`Blob.Tests.XxxTests`)을 넣어 클래스 단위로 나눠 돌린다. 부분 일치 필터(`Blob.Tests.Projectile`)는 0건을 반환하므로 쓰지 않는다.

## MCP Unity 연동

- **가능** — 테스트 실행, 컴파일, 콘솔 로그, 씬 계층 조회, 컴포넌트 추가/수정, Play 모드 제어, 메뉴 실행
- **불가** — 씬 오브젝트 참조 설정(에셋 참조만 가능), 씬 오브젝트를 프리팹으로 저장, 컴포넌트의 List 필드 채우기(에디터 스크립트로 우회)
- 파괴적 조작은 반드시 사전에 알리고 진행한다.
- 응답이 없으면 2~3회 이상 재시도하지 않고 사용자에게 상태 확인을 요청한다.

## 테스트

- 코드 작성과 함께 단위 테스트를 작성한다. 예외 처리·엣지 케이스를 1개 이상 포함한다.
- **작성 후 실제로 실행하여 결과를 보고한다.** 실행하지 못했으면 "미실행"이라고 쓴다. 통과했다고 쓰지 않는다.
- 테스트하기 어려운 영역은 순수 클래스로 분리한다.
- **확정된 계약(카테고리 수, 통화 수, 슬롯 상한)은 스키마 계약 테스트로 고정한다.** 문서만 고치고 코드를 안 고치면 깨지도록. 예: `SkillSchemaTests`

### 마지막 전체 검증

**EditMode 223/223 통과 (커밋 `5-G`)** — `Assets/Refresh` → `recompile` 0 warning → 18개 테스트 클래스를 개별 필터로 전부 실행해 확인.
전체 일괄 실행은 MCP 타임아웃으로 불가하다 (위 함정 참조).
