#!/bin/bash
# ─────────────────────────────────────────────────────────────
# Blob 단위 테스트 실행 스크립트
#
# ⚠️ Unity 에디터를 먼저 완전히 종료해야 합니다.
#    Unity는 같은 프로젝트를 동시에 두 번 열 수 없습니다.
#
# 사용법:  ./run-tests.sh  [EditMode|PlayMode]
# ─────────────────────────────────────────────────────────────
set -u

PROJECT_PATH="$(cd "$(dirname "$0")" && pwd)"
PLATFORM="${1:-EditMode}"

UNITY_VERSION=$(awk '/m_EditorVersion:/ {print $2}' "$PROJECT_PATH/ProjectSettings/ProjectVersion.txt")
UNITY_BIN="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"

RESULT_FILE="$PROJECT_PATH/TestResults-$PLATFORM.xml"
LOG_FILE="$PROJECT_PATH/TestRun-$PLATFORM.log"

echo "Unity 버전 : $UNITY_VERSION"
echo "테스트 대상: $PLATFORM"
echo "결과 파일  : $RESULT_FILE"
echo

if [ ! -x "$UNITY_BIN" ]; then
  echo "❌ Unity 실행 파일을 찾을 수 없습니다:"
  echo "   $UNITY_BIN"
  echo
  echo "   Unity Hub의 설치 경로가 다르면 이 스크립트의 UNITY_BIN을 수정하세요."
  exit 1
fi

rm -f "$RESULT_FILE"

echo "테스트 실행 중... (수 분 소요될 수 있습니다)"

"$UNITY_BIN" \
  -batchmode \
  -runTests \
  -projectPath "$PROJECT_PATH" \
  -testPlatform "$PLATFORM" \
  -testResults "$RESULT_FILE" \
  -logFile "$LOG_FILE"

EXIT_CODE=$?

echo
if [ -f "$RESULT_FILE" ]; then
  echo "── 결과 요약 ──"
  grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"[^>]*' "$RESULT_FILE" | head -1
  echo
  echo "실패한 테스트:"
  grep -o 'name="[^"]*" fullname="[^"]*" [^>]*result="Failed"' "$RESULT_FILE" | head -20 || echo "  (없음)"
else
  echo "❌ 결과 파일이 생성되지 않았습니다. 로그를 확인하세요: $LOG_FILE"
fi

echo
echo "Unity 종료 코드: $EXIT_CODE  (0 = 전체 통과)"
exit $EXIT_CODE
