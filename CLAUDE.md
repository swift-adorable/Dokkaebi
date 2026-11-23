# 도깨비 (Dokkaebi) — Claude 작업 규칙

한국 설화 탑다운 루터 게임. Unity 6000.4.11f1 · URP · C#. 저장소는 **공개**다.

## 말
- 사용자에게 보이는 글은 중간 진행 한 줄까지 모두 한국어 존댓말. 코드 · 경로 · 명령만 원문.
- 확신 표시: [확인됨] 직접 보거나 돌려 봤음 · [불확실] 짐작 · [확인 불가] 볼 수 없음.

## 프로젝트
- 코드 `Assets/Scripts/<영역>/` · 에디터 전용은 `Assets/Scripts/Editor/` · 테스트 `Assets/Tests/EditMode/`.
- 에디터 메뉴는 `Dokkaebi/…` (한국어). 확인 · 점검 메뉴는 `Dokkaebi/Debug/…` — 수치나 사진으로 확인하는 메뉴를 만들어 쓴다 (예: `Editor/HouseWalkProbe.cs`).
- 그림 작업장은 저장소 밖 `~/Dokkaebi-art` (참고 사진 · 학습 자료 · 생성 스크립트). 게임에서 실제로 쓰는 그림만 `Assets/` 로 옮긴다.
- 기능 · 규칙이 바뀌면 `docs/Dokkaebi_Decisions.md` 에 `2-NNN` 줄을 적는다 (decisions-log 스킬).

## Unity 조작 (mcp-unity)
- 시간 초과 · `Connection failed` 는 실패가 아니다. Unity 는 계속 일한다. **같은 명령을 다시 보내지 말고** 기다려 확인한다:
  `unity-wait.sh mark` → 명령 → `unity-wait.sh compile | tests | build | file <경로>` (`.claude/scripts/`).
- 새 `.cs` 는 `Assets/Refresh` 메뉴 → `recompile_scripts` → `unity-wait.sh compile`.
- **Play 중에는 스크립트를 고치거나 다시 컴파일하지 않는다** (도메인 리로드로 NullRef). 멈춤 → `get_play_mode_status` 가 Edit mode → 고치기 · 컴파일 → 다시 Play.
- Play 진입 · 종료 직후 mcp 가 잠깐 끊긴다 — `get_play_mode_status` 로 다시 붙는지 보고 한 번만 다시.
- 에디터 창이 뒤에 있으면 Unity 가 느려져 시간 초과가 잦다 — 오래 걸리면 기다린다.

## 테스트
- `run_tests`(EditMode) 뒤 `unity-wait.sh tests` 로 판정. 통과 = `known-failures.txt` 밖의 실패 0 (지금 1075/1076).
- 새 로직에는 EditMode 테스트를 함께 둔다.

## 커밋
- 커밋 · 푸시는 사용자가 시킬 때만. 파일은 이름으로 add (`git add -A` · `.` 쓰지 않음). 커밋 메시지에 공동 작성자 줄을 넣지 않는다.
- 공개 저장소라 학습 자료 · 참고 사진 · 받은 팩 · 모델 · 빌드 산출물은 절대 넣지 않는다 — git pre-commit(`.githooks`)과 commit-guard 가 막는다. 막히면 그 파일을 빼고 다시.
- 게임이 구워 만드는 큰 그림(`Assets/Art/Generated` · `Assets/Resources/MapPhotos`)은 통과, 그 밖의 2MB 넘는 그림 · 안 쓰는 듯한 그림은 커밋 전에 사용자에게 묻는다.
- `Packages/manifest.json` 의 `com.unity.ai.*` 줄 · `.vscode/` 는 이 맥북 개인 설정이라 넣지 않는다.

## 폰
- 폰 빌드는 `/phone-build` 로만. 설치는 `.claude/scripts/ios-device.sh` 가 Xcode 빌드 성공을 확인한 뒤에만 한다 (device-guard 가 직접 설치를 막는다).
- 정해 둔 폰 · 개인 Team 은 `.claude/local.conf` (깃 밖). 다른 폰 · 다른 Team 으로는 빌드 · 설치하지 않는다.

## 영상 · 사진 피드백
- 사용자가 영상을 주면 video-feedback 스킬 — `video-frames.sh` 로 장면을 뽑아 본다. 반짝임은 `--diff`.

## 두 맥북
- 메인 맥북이 깃 밖 작업 파일(`~/Dokkaebi-art` · Claude 메모)을 `~/Dokkaebi-art/backup_to_drive.sh` 로 구글 드라이브에 올리고, 서브 맥북은 받기만 한다.
- 맥북마다 한 번: `.claude/local.conf` 채우기 · `.claude/scripts/install-git-hooks.sh`.

## 하네스 자체
- `.claude/rules/harness.md` — 규칙을 어디에 · 어떻게 적는지.
