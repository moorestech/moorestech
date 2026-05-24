#!/usr/bin/env python3
"""
debug-workflow Step 3 集約スクリプト。

7 体並列起動したサブエージェントの仮説出力 (markdown) を結合した 1 つの入力テキストを
stdin で受け取り、Step 3 の集約ロジックを適用して以下を出力する:

1. verify_targets (上位 1-2 件の Critical/Warning) - Step 4 で検証する仮説
2. fallback (残り全件) - Step 4 空振り時の次候補 (折りたたみ表示用)
3. 強制文言: "次のアクション: Step 4 ログ Edit (修正 Edit ではない)"

集約ロジック:
- Claim の正規化キー (対象ファイル + 因果動詞) でグループ化
- Evidence と Recommended log placement を union
- 異なる Category ≥ 2 が同一グループを支持 → 深刻度 1 段ブースト
- (深刻度, Category 支持数) 降順ソート

使い方:
    cat all_subagent_outputs.md | python3 aggregate-hypotheses.py

入力フォーマット: hypothesis-output-format.md の "### Hypothesis Hn" ブロックを連結したもの。
"""
import re
import sys
from collections import defaultdict
from dataclasses import dataclass, field

SEVERITY_RANK = {"Critical": 3, "Warning": 2, "Info": 1}
SEVERITY_BOOST = {"Info": "Warning", "Warning": "Critical", "Critical": "Critical"}


@dataclass
class Hypothesis:
    raw_id: str
    severity: str
    category: str
    claim: str
    evidence: list[str] = field(default_factory=list)
    log_placement: list[str] = field(default_factory=list)
    falsification: str = ""

    @property
    def severity_rank(self) -> int:
        return SEVERITY_RANK.get(self.severity, 0)


def parse_hypotheses(text: str) -> list[Hypothesis]:
    """### Hypothesis Hn ブロックを切り出して Hypothesis に変換"""
    blocks = re.split(r"^### Hypothesis\s+", text, flags=re.MULTILINE)[1:]
    out = []
    for block in blocks:
        lines = block.splitlines()
        h = Hypothesis(raw_id=lines[0].strip() if lines else "?",
                       severity="Info", category="unknown", claim="")
        section = None
        for line in lines[1:]:
            m = re.match(r"-\s+\*\*(Severity|Category|Claim|Evidence|"
                         r"Recommended log placement[^*]*|Falsification)\*\*:?\s*(.*)",
                         line)
            if m:
                section = m.group(1).split(" ")[0]
                value = m.group(2).strip()
                if section == "Severity":
                    h.severity = next((s for s in SEVERITY_RANK
                                       if s.lower() in value.lower()), "Info")
                elif section == "Category":
                    h.category = value
                elif section == "Claim":
                    h.claim = value
                elif section == "Falsification":
                    h.falsification = value
                continue
            sub = re.match(r"\s+-\s+(.*)", line)
            if sub and section == "Evidence":
                h.evidence.append(sub.group(1).strip())
            elif sub and section == "Recommended":
                h.log_placement.append(sub.group(1).strip())
            elif line.strip().startswith("###") or line.strip().startswith("---"):
                break
        out.append(h)
    return out


def normalize_key(claim: str) -> str:
    """Claim を正規化キーに。対象ファイル名・主要動詞でクラスタリング"""
    files = re.findall(r"`?([\w./-]+\.(?:cs|ts|tsx|js|jsx|py|go|rs|java))`?", claim)
    verbs = []
    for v in ["null を返す", "失敗", "発火しない", "呼ばれない", "ヒットしない",
              "検出しない", "アサイン", "破棄", "応答しない", "remain", "leak"]:
        if v in claim:
            verbs.append(v)
    return "|".join(sorted(set(files))) + "::" + "|".join(sorted(set(verbs)))


def aggregate(hyps: list[Hypothesis]) -> list[dict]:
    groups: dict[str, list[Hypothesis]] = defaultdict(list)
    for h in hyps:
        groups[normalize_key(h.claim)].append(h)

    clusters = []
    for key, members in groups.items():
        categories = {m.category for m in members}
        evidence = []
        logs = []
        for m in members:
            evidence.extend(m.evidence)
            logs.extend(m.log_placement)
        max_sev = max(members, key=lambda m: m.severity_rank).severity
        boosted_sev = SEVERITY_BOOST[max_sev] if len(categories) >= 2 else max_sev
        clusters.append({
            "key": key,
            "severity": boosted_sev,
            "categories": sorted(categories),
            "claim": members[0].claim,
            "evidence": list(dict.fromkeys(evidence)),
            "log_placement": list(dict.fromkeys(logs)),
            "falsification": " / ".join(filter(None, {m.falsification for m in members})),
            "support_count": len(categories),
            "raw_ids": [m.raw_id for m in members],
        })
    clusters.sort(key=lambda c: (SEVERITY_RANK[c["severity"]], c["support_count"]),
                  reverse=True)
    return clusters


def render(clusters: list[dict]) -> str:
    verify = clusters[:2]
    fallback = clusters[2:]

    lines = ["# Step 3 集約結果\n", "## verify_targets (Step 4 検証対象)\n"]
    if not verify:
        lines.append("(仮説 0 件)\n")
    for i, c in enumerate(verify, 1):
        lines.append(f"### V{i} [{c['severity']}] {c['claim']}")
        lines.append(f"- 支持観点: {', '.join(c['categories'])} ({c['support_count']} 観点)")
        lines.append(f"- raw_ids: {', '.join(c['raw_ids'])}")
        lines.append("- Evidence:")
        for e in c["evidence"]:
            lines.append(f"  - {e}")
        lines.append("- Recommended log placement:")
        for log in c["log_placement"]:
            lines.append(f"  - {log}")
        lines.append(f"- Falsification: {c['falsification'] or '(未記載)'}")
        lines.append("")

    lines.append("## fallback (Step 4 空振り時の次候補・折りたたみ表示)\n")
    if not fallback:
        lines.append("(なし)\n")
    for i, c in enumerate(fallback, 1):
        lines.append(f"- F{i} [{c['severity']}] {c['claim'][:80]}... "
                     f"({c['support_count']} 観点 / raw: {', '.join(c['raw_ids'])})")

    lines.append("\n---\n")
    lines.append("**次のアクション: Step 4 ログ Edit (修正 Edit ではない)**")
    return "\n".join(lines)


def main():
    text = sys.stdin.read()
    if not text.strip():
        print("Usage: cat all_subagent_outputs.md | python3 aggregate-hypotheses.py",
              file=sys.stderr)
        sys.exit(2)
    hyps = parse_hypotheses(text)
    if not hyps:
        print("No '### Hypothesis ...' blocks found in input.", file=sys.stderr)
        sys.exit(1)
    clusters = aggregate(hyps)
    print(render(clusters))


if __name__ == "__main__":
    main()
