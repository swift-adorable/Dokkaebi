---
name: video-feedback
description: 사용자가 준 화면 녹화(.mp4 · .mov)를 장면 사진으로 뽑아 문제를 찾는다.
context: fork
allowed-tools: Bash(.claude/scripts/video-frames.sh *) Read
---
# 영상 피드백

1. `.claude/scripts/video-frames.sh "<영상>" 4` (반짝임 · 깜빡임이면 `15 --diff`).
2. 나온 시트(`sheet_*.png`)를 전부 Read 로 본다. 반짝임 후보 장면은 앞뒤 장면을 크게 잘라 비교한다.
3. 결과만 돌려준다: 문제마다 「장면 번호(초) · 무엇이 · 어디서 · 짐작 원인」, 그리고 확인한 것과 짐작을 구분.
