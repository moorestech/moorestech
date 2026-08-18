# id採番と findings.json 生成。推奨案は「optionsの先頭」で機械的に決まる
# Id assignment and findings.json generation; the recommended option is always options[0]
from __future__ import annotations

from .parse import DigestError, Document, Finding

SEVERITY_ORDER = {"critical": 0, "high": 1, "medium": 2, "low": 3}
OPTION_KEYS = "ABCDEF"


def sort_key(f: Finding) -> tuple:
    # severity降順→ファイルパス昇順→行番号昇順で安定させる
    # Stable ordering: severity desc, then file path asc, then line number asc
    path, _, line = f.files[0].partition(":")
    return (SEVERITY_ORDER[f.severity], path, int(line) if line.isdigit() else 0)


def assign_ids(doc: Document) -> dict:
    refs = {}
    for n, f in enumerate(sorted(doc.findings, key=sort_key), start=1):
        f.id = f"F{n:02d}"
        refs[f.slug] = f.id
    return refs


def build_findings(doc: Document) -> dict:
    out = []
    for f in sorted(doc.findings, key=sort_key):
        options = []
        for n, summary in enumerate(f.options):
            if n >= len(OPTION_KEYS):
                raise DigestError(f"{f.id}: 案が{len(OPTION_KEYS)}件を超えています")
            option = {"key": OPTION_KEYS[n], "summary": summary}
            # 先頭が推奨。フラグを書く欄が無いので欠落しようがない
            # The first option is the recommended one; there is no field to forget
            if n == 0:
                option["recommended"] = True
            options.append(option)
        out.append({
            "id": f.id, "title": f.title, "severity": f.severity, "category": f.category,
            "files": f.files, "excerpt": _excerpt(f.body_md),
            "recommendation": f.recommendation or (f.options[0] if f.options else ""),
            "options": options, "suppressed": f.suppressed, "suppress_reason": f.suppress_reason,
        })
    return {"pr": int(doc.meta["pr"]), "head": doc.meta["head"], "verdict": doc.meta["verdict"],
            "generated_at": doc.meta["generated_at"], "findings": out}


def _excerpt(body_md: str) -> str:
    # code-cardの中身を行番号を落として抜粋にする（HTMLエスケープはしない契約）
    # Take the code-card body as the excerpt, dropping line numbers; no HTML escaping by contract
    lines = body_md.splitlines()
    for n, line in enumerate(lines):
        if line.startswith("```code-card"):
            body = []
            for rest in lines[n + 1:]:
                if rest.startswith("```"):
                    break
                body.append(rest.split("|", 1)[1] if "|" in rest else rest)
            return "\n".join(body)
    return ""
