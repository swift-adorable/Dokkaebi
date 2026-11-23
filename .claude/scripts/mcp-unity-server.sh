#!/bin/bash
# mcp-unity 서버 실행 래퍼 — .mcp.json 이 패키지 해시 경로(@382a43a30f4d 등)를 박아 두면 mcp-unity 업데이트 · 다른 맥북에서 조용히 끊긴다.
# 설치된 판을 찾아 실행한다. 시간 초과는 UNITY_REQUEST_TIMEOUT(초) — 전체 EditMode 테스트가 약 19초라 60.
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
JS=$(ls -d "$ROOT"/Library/PackageCache/com.gamelovers.mcp-unity@*/Server~/build/index.js 2>/dev/null | head -1)
if [ -z "$JS" ]; then echo "mcp-unity 서버를 찾지 못했습니다 — Unity 로 프로젝트를 한 번 열어 패키지를 받으세요." >&2; exit 1; fi
export MCP_UNITY_SETTINGS_PATH="$ROOT/ProjectSettings/McpUnitySettings.json"
export MCP_UNITY_AUTH_TOKEN_PATH="$ROOT/Library/McpUnity/bridge-token"
export UNITY_REQUEST_TIMEOUT="${UNITY_REQUEST_TIMEOUT:-60}"
exec node "$JS"
