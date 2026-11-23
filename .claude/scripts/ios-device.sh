#!/bin/bash
# 폰 빌드 · 설치 — Xcode 빌드가 성공했을 때만 정해 둔 폰(local.conf DEVICE_UDID)에 설치하고 실행한다.
#   ios-device.sh --check   폰 연결 상태만
#   ios-device.sh           Xcode 빌드 → 성공이면 설치 → 실행  (Unity 빌드는 먼저: unity-wait.sh build)
# 사고 기록: 실패한 xcodebuild 뒤에 명령을 이어 붙여 옛 앱이 깔린 적이 있다 — 그래서 성공 문구와 앱 시각을 둘 다 본다.
set -uo pipefail
. "$(dirname "$0")/lib.sh"
[ -n "${DEVICE_UDID:-}" ] && [ -n "${PERSONAL_TEAM:-}" ] || { echo "실패: .claude/local.conf 에 DEVICE_UDID · PERSONAL_TEAM 이 없습니다 (local.conf.example 참고)"; exit 1; }

state=$(xcrun devicectl list devices 2>/dev/null | grep "$DEVICE_UDID" | sed -E 's/.*\(UDID\) +//' | awk '{print $1}')
if [ "${1:-}" = "--check" ]; then echo "폰 $DEVICE_UDID: ${state:-목록에 없음}"; exit 0; fi
case "$state" in connected*) ;; *) echo "실패: 폰이 연결되어 있지 않습니다 (${state:-목록에 없음}) — 케이블 연결 · 잠금 해제 후 다시"; exit 1;; esac

[ -d "$XCODE_PROJ/Unity-iPhone.xcodeproj" ] || { echo "실패: Xcode 프로젝트가 없습니다 — Unity 메뉴 「Dokkaebi/Build/iOS 개발 빌드」 먼저"; exit 1; }
LOG="${TMPDIR:-/tmp}/dokkaebi-xcodebuild.log"
APP="$XCODE_PROJ/../dd/Build/Products/Debug-iphoneos/Dokkaebi.app"
start=$(date +%s)
( cd "$XCODE_PROJ" && xcodebuild -project Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Debug \
    -destination "id=$DEVICE_UDID" -derivedDataPath ../dd -allowProvisioningUpdates \
    DEVELOPMENT_TEAM="$PERSONAL_TEAM" CODE_SIGN_STYLE=Automatic build ) > "$LOG" 2>&1
if ! grep -q '\*\* BUILD SUCCEEDED \*\*' "$LOG" || [ "$(mtime "$APP")" -lt "$start" ]; then
  echo "실패: Xcode 빌드 — 설치하지 않았습니다"; grep -E 'error:' "$LOG" | head -8; echo "log: $LOG"; exit 1
fi
J="${TMPDIR:-/tmp}/dokkaebi-install.json"
if ! xcrun devicectl device install app --device "$DEVICE_UDID" "$APP" --json-output "$J" > /dev/null 2>&1; then
  echo "실패: 설치 — log: $J"; exit 1
fi
xcrun devicectl device process launch --device "$DEVICE_UDID" "$APP_ID" > /dev/null 2>&1 && launched="실행함" || launched="실행 못 함(폰 잠금?)"
echo "설치 끝 · $launched · 빌드 $(date '+%m-%d %H:%M') · 무료 Team 이라 7일 뒤 만료 · log: $LOG"
