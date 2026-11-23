---
name: phone-build
description: 도깨비를 정해 둔 아이폰에 빌드 · 설치 · 실행한다. 사용자가 「폰 빌드」 「폰에 올려」 「설치해」 라고 할 때.
disable-model-invocation: true
argument-hint: "(없음)"
allowed-tools: Bash(.claude/scripts/unity-wait.sh *) Bash(.claude/scripts/ios-device.sh) Bash(.claude/scripts/ios-device.sh --check) mcp__mcp-unity__execute_menu_item mcp__mcp-unity__get_play_mode_status
---
# 폰 빌드

지금 폰: !`.claude/scripts/ios-device.sh --check || true`

단계마다 따로 실행한다 (명령을 `&&` · `;` 로 잇지 않는다).
1. Play 중이면 멈춘다 (`get_play_mode_status` 가 Edit mode 일 때까지).
2. `.claude/scripts/unity-wait.sh mark`
3. `execute_menu_item` → `Dokkaebi/Build/iOS 개발 빌드 (Xcode 프로젝트)` — 시간 초과는 정상.
4. `.claude/scripts/unity-wait.sh build` — 「Unity 빌드 끝 · 성공」 이어야 다음으로.
5. 폰이 「connected」 가 아니면 사용자에게 연결 · 잠금 해제를 부탁하고 기다린다.
6. `.claude/scripts/ios-device.sh` — Xcode 빌드가 성공해야만 설치 · 실행한다.
7. 보고: 들어간 커밋(`git log -1 --oneline`) · 설치 시각 · 폰에서 볼 것.
