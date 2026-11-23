#!/bin/bash
# EditMode 테스트 결과(TestResults.xml) 요약 — known-failures.txt 밖의 실패가 있으면 1.
. "$(dirname "$0")/lib.sh"
[ -f "$TEST_XML" ] || { echo "실패: 결과 파일이 없습니다 ($TEST_XML)"; exit 1; }
python3 -I - "$TEST_XML" "$(dirname "$0")/known-failures.txt" <<'PY'
import sys, re, time, os, xml.etree.ElementTree as ET
xml, known_path = sys.argv[1], sys.argv[2]
root = ET.parse(xml).getroot()
known = set(l.strip() for l in open(known_path, encoding="utf-8") if l.strip() and not l.startswith("#")) if os.path.exists(known_path) else set()
fails = []
for tc in root.iter("test-case"):
    if tc.get("result") == "Failed":
        msg = tc.find("./failure/message")
        fails.append((tc.get("fullname") or tc.get("name"), (msg.text or "").strip().splitlines()[0][:160] if msg is not None and msg.text else ""))
new = [f for f in fails if f[0] not in known and f[0].split(".")[-1] not in known]
age = int(time.time() - os.path.getmtime(xml))
print(f"테스트 {root.get('passed')}/{root.get('total')} 통과 · 실패 {len(fails)} (알려진 것 {len(fails)-len(new)}) · 결과 {age}초 전")
for name, m in new: print(f"  새 실패 · {name} — {m}")
sys.exit(1 if new else 0)
PY
