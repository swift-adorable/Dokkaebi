#!/bin/bash
# 사용자 영상 → 장면 사진 + 9장 모음 시트 (+ --diff 면 앞뒤 차이 큰 장면 = 반짝임 후보)
#   video-frames.sh <영상> [초당 장수=4] [--diff]
v="${1:?영상 경로}"; fps="${2:-4}"; diff="${3:-}"
name=$(basename "${v%.*}" | tr ' ' '_'); D="${TMPDIR:-/tmp}/dokkaebi-frames/$name"; rm -rf "$D"; mkdir -p "$D"
swift "$(dirname "$0")/frames.swift" "$v" "$D" "$fps" 2>/dev/null | tail -1
PY="${ART_DIR:-$HOME/Dokkaebi-art}/.venv/bin/python"; [ -x "$PY" ] || PY=python3
"$PY" -I - "$D" "$diff" <<'PYEOF'
import sys, glob, os
try:
    from PIL import Image, ImageDraw
except ImportError:
    print("PIL 이 없어 시트는 건너뜀 — 장면은 " + sys.argv[1]); sys.exit(0)
d, diff = sys.argv[1], sys.argv[2] == "--diff"
fs = sorted(glob.glob(d + "/f*.png"))
for k in range(0, len(fs), 9):
    ims = [Image.open(f).convert("RGB") for f in fs[k:k+9]]
    w, h = ims[0].size; cw = 640; ch = int(h * cw / w)
    s = Image.new("RGB", (cw * 3, ch * ((len(ims) + 2) // 3)))
    for i, (im, f) in enumerate(zip(ims, fs[k:k+9])):
        t = im.resize((cw, ch)); ImageDraw.Draw(t).text((6, 6), os.path.basename(f), fill="yellow"); s.paste(t, ((i % 3) * cw, (i // 3) * ch))
    s.save(f"{d}/sheet_{k//9:02d}.png")
print(f"시트 {(len(fs)+8)//9}장: {d}/sheet_*.png")
if diff and len(fs) > 2:
    import numpy as np
    a = [np.asarray(Image.open(f).convert("L"), float) for f in fs]
    score = [(np.mean((np.abs(a[i]-a[i-1]) > 25) & (np.abs(a[i+1]-a[i]) > 25) & ((a[i]-a[i-1])*(a[i+1]-a[i]) < 0)), i) for i in range(1, len(a)-1)]
    print("반짝임 후보(앞뒤로 튀는 화소 비율 큰 순):", ", ".join(f"f{i:04d}({v*100:.2f}%)" for v, i in sorted(score, reverse=True)[:5]))
PYEOF
