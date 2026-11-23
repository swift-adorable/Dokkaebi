---
name: develop
description: 도깨비에 기능을 넣거나 바꾸는 순서. 게임 기능 · 화면 · 맵 · 집 · 시야 같은 구현 요청일 때.
---
# 기능 개발

1. 관련 코드를 찾는다 (grep · 기존 `Dokkaebi/Debug` 점검 메뉴).
2. 파일 3개 이상을 바꾸거나 새 시스템이면 짧은 계획(무엇을 · 어디를 · 확인 방법)을 보여 주고 「진행할까요」. 작으면 바로.
3. 구현. Play 중이면 먼저 멈춘다. 새 `.cs` 는 `Assets/Refresh`.
4. `unity-wait.sh mark` → `recompile_scripts` → `unity-wait.sh compile` (오류 0).
5. 눈으로 볼 것이면 점검 메뉴(`Dokkaebi/Debug/…`)로 사진을 찍어 직접 본다 — 바꾸기 전 · 후를 같은 자리 · 같은 방향에서 나란히.
6. `run_tests` → `unity-wait.sh tests` (새 실패 0).
7. decisions-log 로 결정 줄.
8. verifier 에이전트로 점검(범위 밖 변경 · .meta · Editor API 섞임).
9. 보고. 커밋 · 폰 빌드는 묻기만 한다.
