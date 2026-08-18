# findingブロック1件分をFindingへ変換する
# Convert a single finding block into a Finding
from __future__ import annotations

from .sectioning import read_fence
from .errors import DigestError
from .models import Finding
from .yaml_subset import parse_yaml_block

CATEGORIES = {"critical", "design-decision", "novelty"}
SEVERITIES = {"critical", "high", "medium", "low"}


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

    # suppressedの真偽で必須キー集合が切り替わる（suppress_reason vs options）
    # The required key set switches on suppressed (suppress_reason vs options)
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
    # recommended は先頭optionが推奨という規約に反するため予約語として拒否する
    # recommended is a reserved key: the convention is "the first option is the recommendation"
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
