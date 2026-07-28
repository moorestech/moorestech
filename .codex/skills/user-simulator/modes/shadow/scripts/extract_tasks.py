#!/usr/bin/env python3
# transcriptからAskUserQuestionの質問・実回答ペアを抽出し、盲検予測タスクとgoldを生成する
# Extract AskUserQuestion Q&A pairs from a transcript and emit blind-prediction tasks + gold answers
import json
import os
import re
import sys


def main():
    if len(sys.argv) != 3:
        print("usage: extract_tasks.py <transcript.jsonl> <outdir>")
        sys.exit(1)
    transcript, outdir = sys.argv[1], sys.argv[2]
    os.makedirs(outdir, exist_ok=True)
    events = [json.loads(line) for line in open(transcript) if line.strip()]

    # 最初のユーザータスク文を取得（command-args優先） / First user task statement
    task = None
    for d in events:
        if d.get("type") != "user":
            continue
        c = d.get("message", {}).get("content")
        text = None
        if isinstance(c, str):
            text = c
        elif isinstance(c, list):
            text = next((b["text"] for b in c if isinstance(b, dict) and b.get("type") == "text"), None)
        if not text:
            continue
        m = re.search(r"<command-args>(.*?)</command-args>", text, re.S)
        task = (m.group(1) if m else text).strip()
        break

    # tool_useとtool_resultの突き合わせ / Pair tool_use with tool_result
    pairs = []
    pending = {}
    for d in events:
        if d.get("type") == "assistant":
            for b in d.get("message", {}).get("content") or []:
                if isinstance(b, dict) and b.get("type") == "tool_use" and b.get("name") == "AskUserQuestion":
                    pending[b["id"]] = b.get("input", {})
        if d.get("type") == "user":
            c = d.get("message", {}).get("content")
            if not isinstance(c, list):
                continue
            for bl in c:
                if isinstance(bl, dict) and bl.get("type") == "tool_result" and bl.get("tool_use_id") in pending:
                    q = pending.pop(bl["tool_use_id"])
                    res = bl.get("content")
                    txt = res if isinstance(res, str) else " ".join(
                        x.get("text", "") for x in res if isinstance(x, dict))
                    pairs.append((q, txt))

    def parse_answers(txt):
        # 拒否は除外・確定回答のみ採点対象 / Skip rejections; only settled answers are scoreable
        if "rejected" in txt:
            return None
        return dict(re.findall(r'"([^"]+)"="([^"]+)"', txt))

    gold = []
    history = []
    count = 0
    for i, (q, txt) in enumerate(pairs):
        answers = parse_answers(txt)
        questions = q.get("questions", [])
        if answers is None:
            history.append((questions, "（ユーザーがこの質問への回答を拒否した）"))
            continue
        lines = [f"# 盲検予測タスク {i}", "", "## 元タスク（ユーザーの依頼）", "", task or "(不明)", "",
                 "## ここまでの質問と実際のユーザー回答（時系列）", ""]
        for hq, ha in history:
            for qq in hq:
                lines.append(f"- Q: {qq.get('question', '')}")
                ans = ha if isinstance(ha, str) else ha.get(qq.get("question", ""), "(不明)")
                lines.append(f"  A: {ans}")
        lines += ["", "## 今回予測する質問", ""]
        for qq in questions:
            lines.append(f"### {qq.get('header', '')}")
            lines.append(qq.get("question", ""))
            lines.append("選択肢:")
            for o in qq.get("options", []):
                lines.append(f"- {o.get('label', '')}: {o.get('description', '')}")
            lines.append("(「その他」の自由回答も選べる)")
            lines.append("")
        open(f"{outdir}/task-{i:02d}.md", "w").write("\n".join(lines))
        gold.append({"idx": i, "headers": [qq.get("header") for qq in questions], "answers": answers})
        history.append((questions, answers))
        count += 1
    json.dump(gold, open(f"{outdir}/gold.json", "w"), ensure_ascii=False, indent=1)
    print(f"task files: {count} -> {outdir}")


if __name__ == "__main__":
    main()
