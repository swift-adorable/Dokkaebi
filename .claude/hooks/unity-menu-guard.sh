#!/bin/bash
# PreToolUse(mcp__mcp-unity__execute_menu_item) — 확인 · 점검용 메뉴는 묻지 않고, 빌드 · 굽기 같은 큰 메뉴만 사용자에게 묻는다.
python3 -I -c '
import json, sys
m = json.load(sys.stdin).get("tool_input", {}).get("menuPath", "")
safe = m in ("Assets/Refresh",) or m.startswith("Dokkaebi/Debug/")
if safe:
    print(json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse", "permissionDecision": "allow", "permissionDecisionReason": "확인 · 점검 메뉴"}}, ensure_ascii=False))
' || true
exit 0
