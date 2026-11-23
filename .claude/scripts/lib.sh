#!/bin/bash
# 하네스 공용 — local.conf 읽기 · 경로 · Unity 로그 표시점.
# 스크립트는 모두 「한 줄 요약」 을 찍고 성공 0 / 실패 1 / 시간 초과 124 로 끝난다.

HARNESS_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONF="$HARNESS_ROOT/.claude/local.conf"
if [ -f "$CONF" ]; then
  # shellcheck disable=SC1090
  set -a; . "$CONF"; set +a
fi

TEST_XML="$HOME/Library/Application Support/Sensei Lee/Dokkaebi/TestResults.xml"
MARK_FILE="${TMPDIR:-/tmp}/dokkaebi-unity-mark"
XCODE_PROJ="$HARNESS_ROOT/Builds/iOS/Dokkaebi_dev"
APP_ID="com.sensei.lee.dokkaebi"

# Unity 는 다른 인스턴스가 잠깐 떴다 꺼지면 Editor.log 를 Editor-prev.log 로 돌린다 — 실제로 쓰이는 쪽(최근 수정)을 고른다.
editor_log() {
  local a="$HOME/Library/Logs/Unity/Editor.log" b="$HOME/Library/Logs/Unity/Editor-prev.log"
  if [ -f "$b" ] && { [ ! -f "$a" ] || [ "$b" -nt "$a" ]; }; then echo "$b"; else echo "$a"; fi
}

# 표시점 = (로그 파일, 그때 크기, 시각). 이 뒤에 나온 줄만 본다 — 지난 결과를 이번 결과로 착각하지 않게.
mark() {
  local log; log=$(editor_log)
  printf '%s\n%s\n%s\n' "$log" "$(stat -f %z "$log" 2>/dev/null || echo 0)" "$(date +%s)" > "$MARK_FILE"
}
mark_log()  { sed -n 1p "$MARK_FILE" 2>/dev/null; }
mark_size() { sed -n 2p "$MARK_FILE" 2>/dev/null || echo 0; }
mark_time() { sed -n 3p "$MARK_FILE" 2>/dev/null || echo 0; }

# 표시점 이후 로그 (로그가 바뀌었으면 새 로그 전체)
since_mark() {
  local log size; log=$(editor_log); size=$(mark_size)
  if [ "$log" != "$(mark_log)" ] || [ -z "$size" ]; then cat "$log"; else tail -c +"$((size + 1))" "$log"; fi
}

mtime() { stat -f %m "$1" 2>/dev/null || echo 0; }
