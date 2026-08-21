# id採番と findings.json 生成。推奨案は「optionsの先頭」で機械的に決まる
# Id assignment and findings.json generation; the recommended option is always options[0]
from __future__ import annotations

from .code_card.lines import code_card_lines, iter_code_cards
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
            "files": f.files, "excerpt": _excerpt(f),
            "recommendation": f.options[0] if f.options else "",
            "options": options, "suppressed": f.suppressed, "suppress_reason": f.suppress_reason,
        })
    return {"pr": int(doc.meta["pr"]), "head": doc.meta["head"], "verdict": doc.meta["verdict"],
            "generated_at": doc.meta["generated_at"], "findings": out}


def _excerpt(f: Finding) -> str:
    # 最初のcode-cardをPR後の現行コードとして抜き出す（削除行はpr-adjudicated-applyの誤読を招くので落とす）
    # Take the first code-card as the post-PR code; deletions are dropped so pr-adjudicated-apply never misreads them
    # HTMLエスケープはしない契約。行の読み方は code_card_lines と共有する
    # No HTML escaping by contract; line parsing is shared with code_card_lines
    cards = iter_code_cards(f.body_md)
    if not cards:
        return ""
    kept = [code for _, kind, _, code in code_card_lines(cards[0]) if kind != "del"]
    # 削除行だけのカードは excerpt が空になる。applyが修正対象を突き止められなくなるので出荷させない
    # A deletion-only card empties the excerpt, leaving apply with no anchor on the code, so it must not ship
    if not kept:
        raise DigestError(f"{f.id}: 最初のcode-cardが削除行だけで excerpt が空になります"
                          f"（文脈行か追加行を1行は入れてください）")
    return "\n".join(kept)
