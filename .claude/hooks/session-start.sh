#!/bin/bash
# SessionStart — 세션 첫머리에 지금 상태를 10줄 안으로 알린다 (평문 출력이 그대로 Claude 문맥에 들어간다).
. "$CLAUDE_PROJECT_DIR/.claude/scripts/lib.sh" 2>/dev/null
cd "$CLAUDE_PROJECT_DIR" || exit 0
echo "[도깨비 하네스] 세션 시작 상태"
echo "· 브랜치 $(git branch --show-current 2>/dev/null) · 커밋 안 된 파일 $(git status --porcelain 2>/dev/null | wc -l | tr -d ' ')개 · git 검사 $( [ "$(git config core.hooksPath)" = .githooks ] && echo 설치됨 || echo '없음 → .claude/scripts/install-git-hooks.sh')"
[ -f .claude/local.conf ] && echo "· local.conf: 역할 ${MACHINE_ROLE:-?} · 폰 ${DEVICE_UDID:-없음}" || echo "· local.conf 없음 → .claude/local.conf.example 을 복사해 채우세요 (폰 빌드 · 설치가 막힙니다)"
ls -d Library/PackageCache/com.gamelovers.mcp-unity@* >/dev/null 2>&1 && echo "· mcp-unity 패키지 있음 · 시간 초과 $(sed -n 's/.*"RequestTimeoutSeconds": *\([0-9]*\).*/\1/p' ProjectSettings/McpUnitySettings.json)초" || echo "· mcp-unity 패키지 없음 — Unity 로 프로젝트를 한 번 여세요"
pgrep -x Unity >/dev/null && echo "· Unity 에디터 켜짐" || echo "· Unity 에디터 꺼짐"
if [ "${MACHINE_ROLE:-}" = "sub" ]; then echo "· 서브 맥북 — 드라이브 Dokkaebi_백업 마지막 백업: $(cat "$HOME/Desktop/Google Drive/Dokkaebi_백업/마지막_백업.txt" 2>/dev/null || echo '모름')"; fi
exit 0
