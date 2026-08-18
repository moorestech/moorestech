# digest.md を文書モデルへ落とす。未知の構造・必須キー欠落は全てDigestErrorで落とす
# Parse digest.md into the document model; unknown structure and missing keys raise DigestError
from __future__ import annotations

from dataclasses import dataclass, field

VERDICTS = {"auto", "ruling", "reject", "stub"}
CATEGORIES = {"critical", "design-decision", "novelty"}
SEVERITIES = {"critical", "high", "medium", "low"}
META_KEYS = ["pr", "head", "verdict", "verdict_line", "date", "generated_at"]
NOTE_KEYS = ["must-read", "other-rulings", "suppressed", "new-shape", "criticals"]


class DigestError(Exception):
    pass


@dataclass
class Finding:
    slug: str
    title: str
    category: str
    severity: str
    summary: str
    files: list
    body_md: str
    options: list = field(default_factory=list)
    must_read: bool = False
    index_label: str = ""
    suppressed: bool = False
    suppress_reason: str = ""
    recommendation: str = ""
    label: str = ""
    id: str = ""


@dataclass
class Document:
    meta: dict
    notes: dict
    ledger_md: str
    appendix_md: str
    findings: list


def parse_yaml_block(text: str) -> dict:
    # digestが使うサブセットだけを読む。深い構造は推測せずエラーにする
    # Only the subset digest uses; deeper structures are rejected rather than guessed
    out: dict = {}
    key = None
    for raw in text.splitlines():
        if not raw.strip():
            continue
        stripped = raw.strip()
        if stripped.startswith("- "):
            if key is None:
                raise DigestError(f"リスト項目の親キーがありません: {raw!r}")
            if not isinstance(out.get(key), list):
                raise DigestError(f"キー {key} に値とリストが混在しています")
            out[key].append(stripped[2:].strip())
            continue
        if raw.startswith(" "):
            raise DigestError(f"未対応のインデント行です: {raw!r}")
        if ":" not in raw:
            raise DigestError(f"key: value 形式ではありません: {raw!r}")
        key, value = raw.split(":", 1)
        key, value = key.strip(), value.strip()
        if value == "":
            out[key] = []
        elif value.startswith("[") and value.endswith("]"):
            inner = value[1:-1].strip()
            out[key] = [v.strip() for v in inner.split(",")] if inner else []
        else:
            out[key] = value
    return out


def _fence(lines: list, i: int) -> tuple:
    # フェンスの中身と、閉じフェンスの次の行番号を返す
    # Return the fenced body and the line index just after the closing fence
    body = []
    i += 1
    while i < len(lines) and not lines[i].startswith("```"):
        body.append(lines[i])
        i += 1
    if i >= len(lines):
        raise DigestError("閉じられていないコードフェンスがあります")
    return "\n".join(body), i + 1


def _split_blocks(text: str) -> tuple:
    # 見出しレベル1/2でブロックへ割る。フェンス内の見出しは無視する
    # Split by level-1/2 headings, ignoring headings that live inside fences
    lines = text.splitlines()
    blocks, title = [], ""
    cur_level, cur_title, buf = "", "", []
    i, in_fence = 0, False
    while i < len(lines):
        line = lines[i]
        if line.startswith("```"):
            in_fence = not in_fence
        if not in_fence and line.startswith("# ") and not title and not cur_level:
            title = line[2:].strip()
            i += 1
            continue
        if not in_fence and (line.startswith("# ") or line.startswith("## ")):
            if cur_level:
                blocks.append((cur_level, cur_title, "\n".join(buf).strip()))
            cur_level = "1" if line.startswith("# ") else "2"
            cur_title = line.lstrip("#").strip()
            buf = []
            i += 1
            continue
        buf.append(line)
        i += 1
    if cur_level:
        blocks.append((cur_level, cur_title, "\n".join(buf).strip()))
    return blocks, title


def _finding_from(title: str, body: str) -> Finding:
    lines = body.splitlines()
    j = 0
    while j < len(lines) and not lines[j].strip():
        j += 1
    if j >= len(lines) or not lines[j].startswith("```yaml"):
        raise DigestError(f"finding「{title}」の直下に ```yaml ブロックがありません")
    meta_text, after = _fence(lines, j)
    meta = parse_yaml_block(meta_text)
    rest = "\n".join(lines[after:]).strip()

    suppressed = str(meta.get("suppressed", "false")).lower() == "true"
    required = ["slug", "category", "severity", "summary", "files"]
    required += ["suppress_reason"] if suppressed else ["options"]
    for key in required:
        if not meta.get(key):
            raise DigestError(f"finding「{title}」に必須キー {key} がありません")
    if meta["category"] not in CATEGORIES:
        raise DigestError(f"finding「{title}」の category が不正です: {meta['category']}")
    if meta["severity"] not in SEVERITIES:
        raise DigestError(f"finding「{title}」の severity が不正です: {meta['severity']}")
    if "recommended" in meta:
        raise DigestError(f"finding「{title}」に recommended は書けません（先頭optionが推奨です）")

    files = meta["files"] if isinstance(meta["files"], list) else [meta["files"]]
    options = meta.get("options", [])
    if not suppressed and not isinstance(options, list):
        raise DigestError(f"finding「{title}」の options はリストで書いてください")
    return Finding(
        slug=meta["slug"], title=title, category=meta["category"], severity=meta["severity"],
        summary=meta["summary"], files=files, body_md=rest, options=list(options),
        must_read=str(meta.get("must_read", "false")).lower() == "true",
        index_label=meta.get("index_label", ""), suppressed=suppressed,
        suppress_reason=meta.get("suppress_reason", ""),
        recommendation=meta.get("recommendation", ""), label=meta.get("label", ""),
    )


def parse_document(text: str) -> Document:
    # 文書ヘッダ → finding群 → 予約見出し、の順に取り出す
    # Extract the document header, then findings, then the reserved sections
    blocks, title = _split_blocks(text)
    if not title:
        raise DigestError("先頭に `# PR #<番号> <タイトル>` の見出しがありません")

    head_lines = text.splitlines()
    k = next((n for n, ln in enumerate(head_lines) if ln.startswith("```yaml")), -1)
    if k < 0:
        raise DigestError("文書ヘッダの ```yaml ブロックがありません")
    meta = parse_yaml_block(_fence(head_lines, k)[0])
    for key in META_KEYS:
        if not meta.get(key):
            raise DigestError(f"文書ヘッダに必須キー {key} がありません")
    if meta["verdict"] not in VERDICTS:
        raise DigestError(f"verdict が不正です: {meta['verdict']}")
    meta["title"] = title

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
        if zone == "注記":
            if name not in NOTE_KEYS:
                raise DigestError(f"未知の注記見出しです: ## {name}")
            notes[name] = body
        elif zone == "折りたたみ参考":
            appendix += f"\n\n## {name}\n\n{body}"
        elif zone == "判断台帳":
            ledger += f"\n\n### {name}\n\n{body}"
        else:
            findings.append(_finding_from(name, body))

    for key in NOTE_KEYS:
        if key not in notes:
            raise DigestError(f"注記に ## {key} がありません")
    if not ledger.strip():
        raise DigestError("予約見出し # 判断台帳 がありません")
    if not appendix.strip():
        raise DigestError("予約見出し # 折りたたみ参考 がありません")
    slugs = [f.slug for f in findings]
    dup = {s for s in slugs if slugs.count(s) > 1}
    if dup:
        raise DigestError(f"slug が重複しています: {sorted(dup)}")
    return Document(meta=meta, notes=notes, ledger_md=ledger.strip(),
                    appendix_md=appendix.strip(), findings=findings)
