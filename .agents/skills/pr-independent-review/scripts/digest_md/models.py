# digest.mdのパース結果を保持するデータモデル
# Data models that hold the parsed result of digest.md
from __future__ import annotations

from dataclasses import dataclass, field


@dataclass
class Finding:
    # 個々の指摘。id は後段のコンバータが採番するため初期値は空
    # A single finding; id stays empty here, assigned later by the converter
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
    label: str = ""
    id: str = ""


@dataclass
class Document:
    # digest.md全体を表す文書モデル
    # Document model representing the whole digest.md
    meta: dict
    notes: dict
    ledger_md: str
    appendix_md: str
    findings: list
