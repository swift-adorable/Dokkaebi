---
name: decisions-log
description: docs/Dokkaebi_Decisions.md 에 결정 줄(2-NNN)을 적는다. 기능 · 규칙을 바꾼 뒤, 또는 사용자가 「결정 문서」 「기록해」 라고 할 때.
---
# 결정 기록

1. `grep -o '^| 2-[0-9]*' docs/Dokkaebi_Decisions.md | tail -1` 로 마지막 번호를 보고 +1.
2. 형식은 `.claude/rules/decisions.md`. 사용자 말은 「」 로 그대로 인용, 바뀐 것은 ① ② … 로 수치까지, 끝 칸에 코드 이름 · 테스트 · 남은 것.
3. 마지막 결정 줄 바로 아래에 넣고, 넣은 줄을 사용자에게 보여 준다.
