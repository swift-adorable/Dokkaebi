#!/bin/bash
# Unity 일이 「정말」 끝났는지 기다린다 — mcp-unity 응답이 시간 초과여도 Unity 는 계속 일한다. 같은 명령을 다시 보내지 말고 이걸로 확인.
#   unity-wait.sh mark                  지금을 표시점으로 (명령 보내기 「전」에)
#   unity-wait.sh compile [초]          표시점 뒤 컴파일 · 도메인 리로드 끝 (Domain Reload Profiling) → 오류 CS 출력
#   unity-wait.sh tests [초]            표시점 뒤 TestResults.xml 이 새로 써짐 → unity-tests.sh
#   unity-wait.sh build [초]            표시점 뒤 「Build Finished, Result:」 (iOS 빌드 메뉴) — Success 면 0
#   unity-wait.sh file <경로> [초]      그 파일이 표시점보다 새로워짐
# 종료: 0 성공 · 1 실패 · 124 시간 초과
. "$(dirname "$0")/lib.sh"
mode="${1:-}"; shift || true
if [ "$mode" = "mark" ]; then mark; echo "표시점: $(mark_log | xargs basename) $(mark_size)바이트"; exit 0; fi
[ -f "$MARK_FILE" ] || { echo "실패: 표시점이 없습니다 — 명령 전에 unity-wait.sh mark"; exit 1; }
deadline() { echo $(( $(date +%s) + ${1:-$2} )); }

case "$mode" in
  compile)
    end=$(deadline "${1:-}" 180)
    while [ "$(date +%s)" -lt "$end" ]; do
      out=$(since_mark)
      if echo "$out" | grep -q 'Domain Reload Profiling:\|error CS[0-9]'; then
        errs=$(echo "$out" | grep -E 'error CS[0-9]+' | sort -u | head -20)
        if [ -n "$errs" ]; then echo "컴파일 실패:"; echo "$errs"; exit 1; fi
        echo "컴파일 끝 · 오류 0"; exit 0
      fi
      sleep 2
    done; echo "시간 초과: 컴파일 끝 표지를 못 봤습니다 (Unity 가 뒤에 있으면 느려집니다)"; exit 124;;
  tests)
    end=$(deadline "${1:-}" 600)
    while [ "$(date +%s)" -lt "$end" ]; do
      [ "$(mtime "$TEST_XML")" -gt "$(mark_time)" ] && { sleep 1; exec "$(dirname "$0")/unity-tests.sh"; }
      sleep 3
    done; echo "시간 초과: 새 테스트 결과가 없습니다"; exit 124;;
  build)
    end=$(deadline "${1:-}" 900)
    while [ "$(date +%s)" -lt "$end" ]; do
      r=$(since_mark | grep -a 'Build Finished, Result:' | tail -1)
      if [ -n "$r" ]; then
        sleep 5   # 마지막 파일(lib_burst_generated.cpp 등)까지 다 쓰도록 — 이걸 안 기다려 Xcode 빌드가 깨진 적이 있다
        echo "$r" | grep -q Success && { echo "Unity 빌드 끝 · 성공"; exit 0; }
        echo "Unity 빌드 실패: $r"; since_mark | grep -aE 'error|Error' | head -10; exit 1
      fi
      sleep 5
    done; echo "시간 초과: Unity 빌드 끝 표지를 못 봤습니다"; exit 124;;
  file)
    f="${1:?경로}"; end=$(deadline "${2:-}" 300)
    while [ "$(date +%s)" -lt "$end" ]; do
      [ "$(mtime "$f")" -gt "$(mark_time)" ] && { echo "새 파일: $f"; exit 0; }
      sleep 1
    done; echo "시간 초과: $f 가 새로 써지지 않았습니다"; exit 124;;
  *) sed -n 2,8p "$0"; exit 1;;
esac
