#!/bin/bash
# PreToolUse(Bash, if: git commit) — Claude 가 커밋하기 전에 staged-check 를 돌린다.
#   막음 → exit 2 (이유를 Claude 에게) · 확인 → permissionDecision "ask" (사용자에게 묻기) · 통과 → 아무 말 없이 0
# 같은 명령에서 git add 로 막 넣는 파일은 아직 안 보인다 — 그건 git pre-commit(.githooks)이 커밋 순간에 잡는다.
trap 'echo "commit-guard 내부 오류 — 커밋을 멈췄습니다 (.claude/hooks/commit-guard.sh)" >&2; exit 2' ERR
input=$(cat)
cwd=$(printf '%s' "$input" | python3 -I -c 'import json,sys; print(json.load(sys.stdin).get("cwd",""))')
repo=$(git -C "${cwd:-.}" rev-parse --show-toplevel 2>/dev/null) || exit 0
res=$("$CLAUDE_PROJECT_DIR/.claude/scripts/staged-check.sh" --json "$repo") || true
python3 -I - "$res" <<'PY' || exit $?   # || 가 있으면 의도한 exit 2 가 ERR trap(내부 오류)으로 바뀌지 않는다
import json, sys
r = json.loads(sys.argv[1] or '{"hard":[],"soft":[]}')
if r["hard"]:
    sys.stderr.write("커밋을 막았습니다 (CLAUDE.md 「커밋」):\n" + "\n".join("· " + x for x in r["hard"]) + "\n해당 파일을 git restore --staged 로 빼고 다시 하세요.\n")
    sys.exit(2)
if r["soft"]:
    print(json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse", "permissionDecision": "ask",
          "permissionDecisionReason": "커밋 전 확인:\n" + "\n".join("· " + x for x in r["soft"])}}, ensure_ascii=False))
sys.exit(0)
PY
