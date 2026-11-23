---
paths:
  - ".claude/**"
  - "CLAUDE.md"
  - ".githooks/**"
---
# 하네스 규칙

- 규칙은 한 곳에만. CLAUDE.md = 늘 지킬 짧은 원칙, rules/ = 특정 파일을 다룰 때, skills/ = 절차, hooks · git pre-commit = 예외 없이 강제할 것.
- hook 은 강제만 하고 이유는 CLAUDE.md 의 해당 절을 가리킨다. 막는 hook 은 exit 2, 묻는 것은 permissionDecision "ask" (JSON 은 stdout 에 JSON 만).
- 막는 hook 은 스크립트 오류에도 막히게: settings 에 `"onFailure": "block"`, 스크립트에 `trap … exit 2`.
- hook 은 `#!/bin/bash`(macOS 기본 3.2 — mapfile · 연관 배열 없음) 또는 `python3 -I`.
- 스크립트 출력은 한 줄 요약 + 필요하면 `log:` 경로. 종료 0 성공 · 1 실패 · 124 시간 초과.
- 맥북마다 다른 값(폰 UDID · Team · 역할)은 `.claude/local.conf` 에만. 공개 저장소에 개인 값 · 비밀을 적지 않는다.
- Claude 의 권한 규칙 · hook 은 명령 글자만 본다 — 공개 저장소 유출의 마지막 방어선은 git pre-commit 이다.
- 그림 · 학습 관련 규칙(화풍 · 참고 폴더 · LoRA)은 공개 저장소에 적지 않고 사용자 메모 · `~/Dokkaebi-art` 에 둔다.
