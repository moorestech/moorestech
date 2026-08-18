# digest.md を文書モデルへ落とす。未知の構造・必須キー欠落は全てDigestErrorで落とす
# Parse digest.md into the document model; unknown structure and missing keys raise DigestError
from __future__ import annotations

from .sectioning import read_fence, split_blocks
from .errors import DigestError
from .finding_parser import finding_from
from .models import Document, Finding
from .yaml_subset import parse_yaml_block

# digest_md.parse からimportできる後続タスク向けの公開契約
# The public contract that later tasks import from digest_md.parse
__all__ = ["DigestError", "Document", "Finding", "parse_document", "parse_yaml_block"]

VERDICTS = {"auto", "ruling", "reject", "stub"}
META_KEYS = ["pr", "head", "verdict", "verdict_line", "date", "generated_at"]
NOTE_KEYS = ["must-read", "other-rulings", "suppressed", "new-shape", "criticals"]


def parse_document(text: str) -> Document:
    # 文書ヘッダ → finding群 → 予約見出し、の順に取り出す
    # Extract the document header, then findings, then the reserved sections
    blocks, title = split_blocks(text)
    if not title:
        raise DigestError("先頭に `# PR #<番号> <タイトル>` の見出しがありません")
    meta = _parse_header_meta(text)
    meta["title"] = title

    findings, notes, ledger, appendix = _collect_sections(blocks)
    _validate_sections(notes, ledger, appendix)
    _reject_duplicate_slugs(findings)
    return Document(meta=meta, notes=notes, ledger_md=ledger.strip(),
                     appendix_md=appendix.strip(), findings=findings)


def _parse_header_meta(text: str) -> dict:
    # 文書冒頭の```yamlブロックを文書ヘッダとして読む（文書全体で最初に現れる```yamlという暗黙前提）
    # Read the leading ```yaml block as the document header (assumes it's the first ```yaml in the whole doc)
    head_lines = text.splitlines()
    k = next((n for n, ln in enumerate(head_lines) if ln.startswith("```yaml")), -1)
    if k < 0:
        raise DigestError("文書ヘッダの ```yaml ブロックがありません")
    meta = parse_yaml_block(read_fence(head_lines, k)[0])
    for key in META_KEYS:
        if not meta.get(key):
            raise DigestError(f"文書ヘッダに必須キー {key} がありません")
    if meta["verdict"] not in VERDICTS:
        raise DigestError(f"verdict が不正です: {meta['verdict']}")
    # pr は外部入力（AI生成md）由来の値のため、境界で数字であることを確認する
    # pr comes from external input (AI-authored markdown), so verify it's numeric at the boundary
    if not str(meta["pr"]).isdigit():
        raise DigestError(f"文書ヘッダの pr は数字で書いてください: {meta['pr']}")
    return meta


def _collect_sections(blocks: list) -> tuple:
    # レベル1見出しでゾーンを切り替え、レベル2見出しをゾーンごとに振り分ける
    # Switch zones on level-1 headings and route level-2 headings per zone
    findings, notes, ledger, appendix, zone = [], {}, "", "", ""
    for level, name, body in blocks:
        if level == "1":
            zone = name
            if name == "判断台帳":
                ledger = body
            elif name == "折りたたみ参考":
                appendix = body
            elif name not in ("注記", ""):
                raise DigestError(f"未知の予約見出しです: # {name}")
            continue
        # レベル2見出しは直前のゾーンに応じてnotes/appendix/ledger/findingsへ振り分ける
        # Route level-2 headings to notes/appendix/ledger/findings based on the current zone
        if zone == "注記":
            if name not in NOTE_KEYS:
                raise DigestError(f"未知の注記見出しです: ## {name}")
            notes[name] = body
        elif zone == "折りたたみ参考":
            appendix += f"\n\n## {name}\n\n{body}"
        elif zone == "判断台帳":
            ledger += f"\n\n### {name}\n\n{body}"
        else:
            findings.append(finding_from(name, body))
    return findings, notes, ledger, appendix


def _validate_sections(notes: dict, ledger: str, appendix: str) -> None:
    # 注記・判断台帳・折りたたみ参考は全件必須
    # Notes, ledger, and appendix sections are all mandatory
    for key in NOTE_KEYS:
        if key not in notes:
            raise DigestError(f"注記に ## {key} がありません")
    if not ledger.strip():
        raise DigestError("予約見出し # 判断台帳 がありません")
    if not appendix.strip():
        raise DigestError("予約見出し # 折りたたみ参考 がありません")


def _reject_duplicate_slugs(findings: list) -> None:
    slugs = [f.slug for f in findings]
    dup = {s for s in slugs if slugs.count(s) > 1}
    if dup:
        raise DigestError(f"slug が重複しています: {sorted(dup)}")
