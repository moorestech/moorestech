#!/usr/bin/env python3
"""moores-code-review 第6系統: 分割深掘り調査のチャンク分割（0トークン）。

Usage: python3 split_chunks.py <PATCH_PATH> [--threshold 16] [--max 15] [--min 10]

変更ファイルからテストを完全除外し（ユーザー裁定 2026-08-03: チャンク担当はテストをReadもしない）、
残りをドメイン単位で10〜15ファイルのチャンクへ分割する。閾値未満なら何も出力しない（発火なし）。
サーバー状態同期の縫い目（Server.Protocol / Server.Event / Client.Network）は同一ドメインに束ね、
境界横断の指摘がチャンク境界で切れないようにする。

Test files are fully excluded (user ruling 2026-08-03), remaining changed files are grouped by
domain into 10-15 file chunks. No output below threshold. The protocol seam directories are
merged into one domain so cross-boundary findings are not cut at chunk edges.

出力TSV: `chunk-<n>\t<ドメインラベル>\t<ファイル1,ファイル2,...>`
"""
from __future__ import annotations

import re
import sys
from pathlib import Path, PurePosixPath

DEFAULT_THRESHOLD = 16
DEFAULT_MAX = 15
DEFAULT_MIN = 10

# 深読み対象はコード・スキーマ・マスタデータのみ（prefab/meta/シーン/画像/mdは対象外）
# Deep-read targets are code, schema, and master data only (no prefab/meta/scene/image/md)
REVIEWABLE_EXTS = (".cs", ".ts", ".tsx", ".js", ".mjs", ".py", ".sh", ".yml", ".yaml", ".json")

# サーバー状態同期3点セットの縫い目は同一ドメインへ束ねる
# Merge the server-state-sync seam directories into one domain
SEAM_DOMAINS = {
    "Server.Protocol": "protocol-sync-seam",
    "Server.Event": "protocol-sync-seam",
    "Client.Network": "protocol-sync-seam",
}


# checks_static.pyと同一のテスト判定（ユーザー裁定 2026-07-28の適用外パターン）
# Same test-path detection as checks_static.py (exemption patterns, ruling 2026-07-28)
def is_test_path(path: str) -> bool:
    parts = PurePosixPath(path).parts
    if any(p in ("e2e", "tests", "__tests__") or "Tests" in p for p in parts[:-1]):
        return True
    name = parts[-1]
    stem = re.sub(r"\.(cs|ts|tsx)$", "", name)
    return name.endswith((".test.ts", ".test.tsx", ".spec.ts")) or stem.endswith(("Test", "Tests"))


def extract_changed_files(diff: str) -> list[str]:
    files: list[str] = []
    for line in diff.splitlines():
        # スペース含みパスはgitが末尾タブを付けるため右端を除去 / strip git's trailing tab on paths with spaces
        if line.startswith("+++ b/"):
            files.append(line[6:].rstrip("\t").rstrip())
        elif line.startswith("+++ ") and line[4:].strip() != "/dev/null":
            files.append(line[4:].rstrip("\t").strip())
    # 重複除去（順序保持） / dedupe preserving order
    return list(dict.fromkeys(files))


def domain_key(path: str) -> str:
    parts = PurePosixPath(path).parts
    # Unityの Assets/Scripts 配下はasmdef相当の先頭要素をドメインとする
    # Under Assets/Scripts the asmdef-like first component is the domain
    for anchor in ("Scripts",):
        if anchor in parts:
            idx = parts.index(anchor)
            if idx + 1 < len(parts) - 1:
                head = parts[idx + 1]
                return SEAM_DOMAINS.get(head, head)
    # webuiは src 直下の1階層をドメインとする / webui: one level under src
    if "webui" in parts:
        idx = parts.index("webui")
        sub = parts[idx + 1 : idx + 3]
        return "webui/" + (sub[0] if len(sub) > 1 else "")
    # その他はパス先頭2要素 / otherwise first two path components
    return "/".join(parts[:2]) if len(parts) > 2 else (str(PurePosixPath(path).parent) or "root")


def pack_chunks(groups: dict[str, list[str]], max_files: int, min_files: int) -> list[tuple[str, list[str]]]:
    chunks: list[tuple[list[str], list[str]]] = []  # (domains, files)
    current_domains: list[str] = []
    current_files: list[str] = []

    def flush() -> None:
        nonlocal current_domains, current_files
        if current_files:
            chunks.append((current_domains, current_files))
            current_domains, current_files = [], []

    for domain in sorted(groups):
        files = sorted(groups[domain])
        # 巨大ドメインは単独で近等分割 / oversized domain splits near-evenly on its own
        if len(files) > max_files:
            flush()
            n_parts = -(-len(files) // max_files)
            size = -(-len(files) // n_parts)
            for i in range(0, len(files), size):
                chunks.append(([domain], files[i : i + size]))
            continue
        if len(current_files) + len(files) > max_files:
            flush()
        current_domains.append(domain)
        current_files.extend(files)
    flush()

    # min未満の小チャンクは隣接チャンクへ吸収（max超過しない範囲・安定するまで）
    # Absorb sub-min chunks into an adjacent chunk while staying under max, until stable
    changed = True
    while changed:
        changed = False
        for i, (domains, files) in enumerate(chunks):
            if len(files) >= min_files or len(chunks) == 1:
                continue
            neighbors = [j for j in (i - 1, i + 1)
                         if 0 <= j < len(chunks) and len(chunks[j][1]) + len(files) <= max_files]
            if not neighbors:
                continue
            j = min(neighbors, key=lambda k: len(chunks[k][1]))
            chunks[j][0].extend(domains)
            chunks[j][1].extend(files)
            chunks.pop(i)
            changed = True
            break
    return [("+".join(dict.fromkeys(d)), f) for d, f in chunks]


def main(argv: list[str]) -> int:
    args = [a for a in argv[1:] if not a.startswith("--")]
    opts = {a.split("=")[0].lstrip("-"): a.split("=")[1] for a in argv[1:] if a.startswith("--") and "=" in a}
    if not args:
        print(__doc__, file=sys.stderr)
        return 2
    threshold = int(opts.get("threshold", DEFAULT_THRESHOLD))
    max_files = int(opts.get("max", DEFAULT_MAX))
    min_files = int(opts.get("min", DEFAULT_MIN))

    diff = Path(args[0]).read_text(encoding="utf-8", errors="replace")
    targets = [
        f for f in extract_changed_files(diff)
        if f.endswith(REVIEWABLE_EXTS) and not is_test_path(f)
    ]
    if len(targets) < threshold:
        print(f"below-threshold: {len(targets)} non-test files (< {threshold})", file=sys.stderr)
        return 0

    groups: dict[str, list[str]] = {}
    for f in targets:
        groups.setdefault(domain_key(f), []).append(f)
    for i, (label, files) in enumerate(pack_chunks(groups, max_files, min_files), start=1):
        print(f"chunk-{i}\t{label}\t{','.join(files)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
