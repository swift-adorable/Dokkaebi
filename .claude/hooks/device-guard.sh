#!/bin/bash
# PreToolUse(Bash, if: xcodebuild / xcrun devicectl) — 폰 빌드 사고 막기.
#   · 회사 Team(BLOCKED_TEAMS) 이 명령에 있으면 막음
#   · xcodebuild 의 DEVELOPMENT_TEAM 이 개인 Team 이 아니면 막음
#   · devicectl 로 앱을 직접 설치하면 막음 → .claude/scripts/ios-device.sh 로만 (빌드 성공을 확인한 뒤에만 설치한다)
#   · devicectl 의 --device 가 정해 둔 폰(DEVICE_UDID)이 아니면 막음
trap 'echo "device-guard 내부 오류 — 명령을 멈췄습니다 (.claude/hooks/device-guard.sh)" >&2; exit 2' ERR
input=$(cat)
. "$CLAUDE_PROJECT_DIR/.claude/scripts/lib.sh"
python3 -I - "$input" "${DEVICE_UDID:-}" "${PERSONAL_TEAM:-}" "${BLOCKED_TEAMS:-}" <<'PY' || exit $?   # || 가 있으면 의도한 exit 2 가 ERR trap(내부 오류)으로 바뀌지 않는다
import json, re, shlex, sys
cmd = json.loads(sys.argv[1]).get("tool_input", {}).get("command", "")
udid, personal, blocked = sys.argv[2], sys.argv[3], [t for t in sys.argv[4].split(",") if t]
def stop(msg):
    sys.stderr.write(msg + " (CLAUDE.md 「폰」 · /phone-build)\n"); sys.exit(2)
for t in blocked:
    if t in cmd: stop(f"쓰지 않기로 한 Team({t})이 명령에 있습니다.")
for m in re.finditer(r"DEVELOPMENT_TEAM=(\S+)", cmd):
    if not personal: stop(".claude/local.conf 에 PERSONAL_TEAM 이 없습니다.")
    if m.group(1).strip("'\"") != personal: stop(f"개인 Team({personal}) 이 아닌 DEVELOPMENT_TEAM 입니다.")
for m in re.finditer(r"-destination\s+['\"]?[^'\"\s]*id=([0-9A-Fa-f-]+)", cmd):
    if udid and m.group(1) != udid: stop(f"정해 둔 폰({udid})이 아닌 기기로 빌드합니다.")
if "devicectl" in cmd:
    if re.search(r"devicectl\s+device\s+install", cmd):
        stop("앱 설치는 .claude/scripts/ios-device.sh 로만 합니다 — Xcode 빌드 성공을 확인한 뒤에만 설치하기 위해서입니다.")
    for m in re.finditer(r"--device\s+(\S+)", cmd):
        if not udid: stop(".claude/local.conf 에 DEVICE_UDID 가 없습니다.")
        if m.group(1).strip("'\"") != udid: stop(f"정해 둔 폰({udid})이 아닌 기기입니다.")
sys.exit(0)
PY
