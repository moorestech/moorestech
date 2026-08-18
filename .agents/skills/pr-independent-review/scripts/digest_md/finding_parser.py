# findingブロック1件分をFindingへ変換する
# Convert a single finding block into a Finding
from __future__ import annotations

from .sectioning import read_fence
from .errors import DigestError
from .models import Finding
from .yaml_subset import parse_yaml_block

CATEGORIES = {"critical", "design-decision", "novelty"}
SEVERITIES = {"critical", "high", "medium", "low"}


def _parse_bool(meta: dict, key: str, title: str) -> bool:
    # true/false の表記ゆれを許さず、それ以外の値は無言でfalseに潰さずエラーにする
    # Reject anything but exact true/false spelling instead of silently defaulting to false
    raw = str(meta.get(key, "false")).lower()
    if raw not in ("true", "false"):
        raise DigestError(f"finding「{title}」の {key} は true/false で書いてください: {raw}")
    return raw == "true"


def finding_from(title: str, body: str) -> Finding:
    # findingの直下は必ず```yamlメタ、それ以降がbody_md本文
    # A finding's body must open with a ```yaml meta block, then free body_md text
    lines = body.splitlines()
    j = 0
    while j < len(lines) and not lines[j].strip():
        j += 1
    if j >= len(lines) or not lines[j].startswith("```yaml"):
        raise DigestError(f"finding「{title}」の直下に ```yaml ブロックがありません")
    meta_text, after = read_fence(lines, j)
    meta = parse_yaml_block(meta_text)
    rest = "\n".join(lines[after:]).strip()
    # 案の列挙は options が正本。本文へ代替案の段落を書くと同じ案が2箇所に出て片方が古くなる
    # options is the single source for alternatives; a body paragraph would go stale on one side
    # 検出は段落見出し `**代替案` に限る。素の部分一致だと抜粋コードが語を含むだけで落ちる
    # Only the `**代替案` paragraph marker counts; a bare substring would fail on excerpts that merely quote the word
    if any(line.lstrip().startswith("**代替案") for line in rest.splitlines()):
        raise DigestError(
            f"finding「{title}」の本文に代替案を書けません。案は options: へ書いてください"
            f"（コンバータが案A/案B…として描き、先頭へ推奨マークを付けます）")

    # suppressedの真偽で必須キー集合が切り替わる（suppress_reason vs options）
    # The required key set switches on suppressed (suppress_reason vs options)
    suppressed = _parse_bool(meta, "suppressed", title)
    required = ["slug", "category", "severity", "summary", "files"]
    required += ["suppress_reason"] if suppressed else ["options"]
    for key in required:
        if not meta.get(key):
            raise DigestError(f"finding「{title}」に必須キー {key} がありません")
    if meta["category"] not in CATEGORIES:
        raise DigestError(f"finding「{title}」の category が不正です: {meta['category']}")
    if meta["severity"] not in SEVERITIES:
        raise DigestError(f"finding「{title}」の severity が不正です: {meta['severity']}")
    # recommended は先頭optionが推奨という規約に反するため予約語として拒否する
    # recommended is a reserved key: the convention is "the first option is the recommendation"
    if "recommended" in meta:
        raise DigestError(f"finding「{title}」に recommended は書けません（先頭optionが推奨です）")
    # recommendation は options 先頭から自動で埋まるため、書かせない（案の正本を1箇所に保つ）
    # recommendation is auto-filled from the first option, so writing it would split the source of truth
    if "recommendation" in meta:
        raise DigestError(f"finding「{title}」に recommendation は書けません（options先頭から自動で入ります）")

    files = meta["files"] if isinstance(meta["files"], list) else [meta["files"]]
    options = meta.get("options", [])
    if not suppressed and not isinstance(options, list):
        raise DigestError(f"finding「{title}」の options はリストで書いてください")
    return Finding(
        slug=meta["slug"], title=title, category=meta["category"], severity=meta["severity"],
        summary=meta["summary"], files=files, body_md=rest, options=list(options),
        must_read=_parse_bool(meta, "must_read", title),
        index_label=meta.get("index_label", ""), suppressed=suppressed,
        suppress_reason=meta.get("suppress_reason", ""),
        label=meta.get("label", ""),
    )
