#!/bin/bash
# PostToolUse(Write) — Assets 아래 새 .cs 를 만들면 Unity 가 그 파일을 보게 하는 순서를 알린다.
python3 -I -c '
import json, sys
d = json.load(sys.stdin); p = d.get("tool_input", {}).get("file_path", "")
if "/Assets/" in p and p.endswith(".cs"):
    print(json.dumps({"hookSpecificOutput": {"hookEventName": "PostToolUse", "additionalContext":
      "새 .cs — Unity 가 보려면: execute_menu_item Assets/Refresh → recompile_scripts → .claude/scripts/unity-wait.sh compile (먼저 unity-wait.sh mark). Play 중이면 먼저 멈추고 하세요."}}, ensure_ascii=False))
' || true
exit 0
