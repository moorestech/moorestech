#!/usr/bin/env python3
# pred-XX.jsonとgold.jsonを突き合わせて採点し、ベースライン比較と確信度校正を出す
# Score pred-XX.json against gold.json with baseline comparison and confidence calibration
import glob
import json
import re
import sys
import unicodedata


# 推奨マーカーは日本語「（推奨）」と英語「(Recommended)」の両方を扱う（AskUserQuestion のラベル慣例が混在するため）
# Treat both the Japanese "（推奨）" and the English "(Recommended)" as the recommended marker (both label conventions coexist)
RECOMMENDED_RE = re.compile(r"[（(](?:推奨|Recommended)[)）]", re.IGNORECASE)


def norm(s):
    s = unicodedata.normalize("NFKC", s or "")
    return re.sub(r"\s", "", RECOMMENDED_RE.sub("", s))


def is_recommended(label):
    return RECOMMENDED_RE.search(unicodedata.normalize("NFKC", label or "")) is not None


def main():
    if len(sys.argv) != 2:
        print("usage: score.py <outdir>")
        sys.exit(1)
    outdir = sys.argv[1]
    gold = {g["idx"]: g for g in json.load(open(f"{outdir}/gold.json"))}
    rows = []
    for f in sorted(glob.glob(f"{outdir}/pred-*.json")):
        p = json.load(open(f))
        g = gold.get(p["idx"])
        if not g or not g["answers"]:
            continue
        answers = list(g["answers"].items())
        for i, pr in enumerate(p["predictions"]):
            if i >= len(answers):
                break
            _, actual = answers[i]
            pred = pr.get("predicted_label", "")
            if norm(pred) == norm(actual):
                mark = "O"
            elif len(norm(actual)) > 3 and (norm(actual) in norm(pred) or norm(pred) in norm(actual)):
                mark = "~"
            else:
                mark = "X"
            rows.append((p["idx"], pr.get("header", ""), pred, actual, pr.get("confidence", ""), mark))

    total = len(rows)
    hits = sum(1 for r in rows if r[5] == "O")
    # ベースライン: 常に（推奨）付き選択肢を選んだ場合の的中数 / Baseline: always pick the recommended option
    baseline = sum(1 for r in rows if is_recommended(r[3]))
    # 逸脱問: 実回答が推奨でない質問と、その的中数 / Deviations: actual answer was not the recommended option
    dev = [r for r in rows if not is_recommended(r[3])]
    dev_hits = sum(1 for r in dev if r[5] == "O")
    print(f"total={total} exact={hits} ({100 * hits // max(total, 1)}%) partial={sum(1 for r in rows if r[5] == '~')}")
    print(f"baseline(常に推奨)={baseline} ({100 * baseline // max(total, 1)}%)")
    print(f"逸脱問={len(dev)} 逸脱的中={dev_hits}  <- シミュレーターの価値源泉。目視で部分点を確定すること")
    for c in ("高", "中", "低"):
        sub = [r for r in rows if r[4] == c]
        if sub:
            h = sum(1 for r in sub if r[5] == "O")
            print(f"確信{c}: {h}/{len(sub)} = {100 * h // len(sub)}%")
    print()
    for r in rows:
        print(f"[{r[5]}] {r[0]:>2} {r[1][:14]:<14} 予測={r[2][:38]:<40} 実={r[3][:38]}")


if __name__ == "__main__":
    main()
