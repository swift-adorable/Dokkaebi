#!/bin/bash
# PostToolUse · PostToolUseFailure(mcp__mcp-unity__.*) — 시간 초과는 실패가 아니다. 같은 명령을 다시 보내지 말고 결과를 기다려 확인하게.
python3 -I -c '
import json, sys
d = json.load(sys.stdin)
text = json.dumps(d.get("tool_response", "")) + " " + str(d.get("error", ""))
if any(k in text for k in ("timed out", "Request timed out", "test_runner_timeout", "Connection failed")):
    tool = d.get("tool_name", "")
    how = {"run_tests": "unity-wait.sh tests", "recompile_scripts": "unity-wait.sh compile", "execute_menu_item": "unity-wait.sh build (빌드 메뉴) / unity-wait.sh file <산출물>"}
    hint = next((".claude/scripts/" + v for k, v in how.items() if k in tool), "get_play_mode_status")
    print(json.dumps({"hookSpecificOutput": {"hookEventName": d.get("hook_event_name", "PostToolUse"), "additionalContext":
      f"mcp-unity 응답 없음/시간 초과 — Unity 는 아마 계속 일하는 중입니다. 같은 명령을 다시 보내지 말고 {hint} 로 확인하세요. Play 진입 · 종료 직후 「Connection failed」 는 잠깐 끊긴 것이니 get_play_mode_status 로 다시 붙는지 보고 한 번만 다시."}}, ensure_ascii=False))
' || true
exit 0
