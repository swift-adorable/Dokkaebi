#!/bin/bash
# 커밋 전 검사 본체 — git pre-commit(.githooks) 과 Claude 의 commit-guard 가 같이 쓴다. 사람이 직접 돌려도 된다.
#   막음(HARD): 학습 자료 · 참고 사진 · 받은 팩 · 모델 · 빌드 산출물 · 개인 설정 / gitignore 에 걸리는 파일(-f 로 넣은 것)
#               / manifest 의 로컬 AI 패키지 줄 / 짝 .meta 없는 Assets 파일
#   확인(SOFT): 2MB 넘는 그림(게임이 구워 만드는 폴더는 통과) / 게임에서 안 쓰는 듯한 새 그림 / 코드는 바뀌었는데 결정 문서 없음
# macOS 기본 bash 3.2 에서 돈다(mapfile · 연관 배열 없이).
# 사용: staged-check.sh [--json] [저장소 경로]   종료: 0 통과 · 1 막음 · 3 확인 필요(SOFT 만)
JSON=0; [ "$1" = "--json" ] && { JSON=1; shift; }
REPO="${1:-$(git rev-parse --show-toplevel 2>/dev/null)}"
cd "$REPO" || exit 0
MAX_MB=2
BIG_OK='^(Assets/Art/Generated/|Assets/Resources/MapPhotos/)'   # 게임이 구워 만드는 큰 그림 (사용자 2026-11-23 「폴더는 통과, 나머지 확인」)

HARD=(); SOFT=()
ADDED=(); while IFS= read -r _l; do [ -n "$_l" ] && ADDED+=("$_l"); done < <(git diff --cached --name-only --diff-filter=AR)
CHANGED=(); while IFS= read -r _l; do [ -n "$_l" ] && CHANGED+=("$_l"); done < <(git diff --cached --name-only --diff-filter=ACMR)

# 1. 금지 경로 · 확장자
FORBID='(^|/)inbox/|참고전용|(^|/)train/|out/lora|_preview/|\.safetensors$|\.ckpt$|\.mcpb$|^Builds/|^Assets/_Recovery/|^\.vscode/|^\.claude/local\.conf$|Dokkaebi-art'
for f in "${CHANGED[@]}"; do
  [[ "$f" =~ $FORBID ]] && HARD+=("금지 경로: $f (학습 자료 · 참고 · 모델 · 빌드 · 개인 설정은 로컬에만)")
done

# 2. gitignore 에 걸리는데 억지로(-f) 넣은 파일 — 재배포 금지 팩(Wild · Props · Enemies · KebapGuy …) 등
IGN=(); while IFS= read -r _l; do [ -n "$_l" ] && IGN+=("$_l"); done < <(git ls-files --cached --ignored --exclude-standard 2>/dev/null)
for f in "${ADDED[@]}"; do   # 새로 넣는 것만 — 예전부터 추적하던 파일을 고치는 건 괜찮다
  for g in "${IGN[@]}"; do [ "$f" = "$g" ] && { HARD+=("gitignore 대상: $f (git add -f 로 넣은 듯 — 받은 팩은 공개 저장소 금지)"); break; }; done
done

# 3. manifest 의 로컬 AI 패키지
if printf '%s\n' "${CHANGED[@]}" | grep -qx 'Packages/manifest.json'; then
  git diff --cached -U0 Packages/manifest.json | grep -q '^+.*com\.unity\.ai\.' && HARD+=("Packages/manifest.json 에 com.unity.ai.* 줄 — 이 맥북만의 로컬 패키지")
fi

# 4. 짝 .meta (Assets 아래 새 파일 · 폴더)
for f in "${ADDED[@]}"; do
  [[ "$f" == Assets/* ]] || continue
  if [[ "$f" == *.meta ]]; then
    src="${f%.meta}"; git ls-files --cached --error-unmatch "$src" >/dev/null 2>&1 || [ -d "$src" ] || HARD+=("짝 없는 .meta: $f")
  else
    git ls-files --cached --error-unmatch "$f.meta" >/dev/null 2>&1 || HARD+=(".meta 없음: $f (서브 맥북에서 GUID 가 깨진다 — 짝 .meta 도 add)")
  fi
done

# 5. 큰 그림 · 6. 안 쓰는 듯한 새 그림
for f in "${CHANGED[@]}"; do
  [[ "$f" =~ \.(png|jpg|jpeg|psd|tga|exr)$ ]] || continue
  sz=$(git cat-file -s ":$f" 2>/dev/null || echo 0)
  if [ "$sz" -gt $((MAX_MB * 1048576)) ] && ! [[ "$f" =~ $BIG_OK ]]; then
    SOFT+=("큰 그림 $((sz / 1048576))MB: $f — 게임에서 쓰는 그림인가요?")
  fi
done
for f in "${ADDED[@]}"; do
  [[ "$f" =~ ^Assets/.*\.(png|jpg|jpeg|psd|tga)$ ]] || continue
  [[ "$f" =~ $BIG_OK ]] && continue
  guid=$(git show ":$f.meta" 2>/dev/null | sed -n 's/^guid: //p')
  name=$(basename "${f%.*}")
  used=0
  [ -n "$guid" ] && git grep -q --cached -e "$guid" -- 'Assets/*.unity' 'Assets/*.prefab' 'Assets/*.asset' 'Assets/*.mat' 'Assets/*.controller' 2>/dev/null && used=1
  [ $used = 0 ] && [[ "$f" == */Resources/* ]] && git grep -q --cached -F "\"$name\"" -- 'Assets/*.cs' 2>/dev/null && used=1
  [ $used = 0 ] && git grep -q --cached -F "$name" -- 'Assets/*.cs' 2>/dev/null && used=1
  [ $used = 0 ] && SOFT+=("안 쓰는 듯한 새 그림: $f (씬 · 재질 · 코드에서 이름 · GUID 를 못 찾음)")
done

# 7. 코드가 바뀌었는데 결정 문서가 없음
if printf '%s\n' "${CHANGED[@]}" | grep -q '^Assets/Scripts/.*\.cs$' && ! printf '%s\n' "${CHANGED[@]}" | grep -qx 'docs/Dokkaebi_Decisions.md'; then
  SOFT+=("Assets/Scripts 가 바뀌었는데 docs/Dokkaebi_Decisions.md 가 없음 — 기능 · 규칙 변경이면 결정 줄을 적으세요")
fi

if [ $JSON = 1 ]; then
  python3 -I -c 'import json,sys; h,s=sys.argv[1],sys.argv[2]; print(json.dumps({"hard":[x for x in h.split("\n") if x],"soft":[x for x in s.split("\n") if x]},ensure_ascii=False))' \
    "$(printf '%s\n' "${HARD[@]}")" "$(printf '%s\n' "${SOFT[@]}")"
else
  for x in "${HARD[@]}"; do echo "막음  · $x"; done
  for x in "${SOFT[@]}"; do echo "확인  · $x"; done
  [ ${#HARD[@]} = 0 ] && [ ${#SOFT[@]} = 0 ] && echo "커밋 검사 통과 (파일 ${#CHANGED[@]}개)"
fi
[ ${#HARD[@]} -gt 0 ] && exit 1
[ ${#SOFT[@]} -gt 0 ] && exit 3
exit 0
