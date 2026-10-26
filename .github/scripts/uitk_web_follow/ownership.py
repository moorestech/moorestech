"""Resolve which ledger rows and bundles own a repository path; the longest matching Web path wins."""

from .ledger import Ledger, Row, STATE_SWITCHED

WEB_SOURCE_ROOT = "moorestech_web/webui/src/"


def _matches(web_path: str, path: str) -> bool:
    # 末尾 / はディレクトリの前方一致、それ以外はファイルの完全一致
    # A trailing slash means a directory prefix; otherwise an exact file match
    return path.startswith(web_path) if web_path.endswith("/") else path == web_path


def owning_rows(path: str, rows: tuple[Row, ...]) -> tuple[Row, ...]:
    # 最長一致の Web パスを書いた行だけを持ち主にする。同じ長さなら全部
    # Only rows listing the longest matching Web path own the file; ties keep every such row
    best = 0
    owners: list[Row] = []
    for row in rows:
        for web_path in row.web_paths:
            if not _matches(web_path, path):
                continue
            if len(web_path) > best:
                best, owners = len(web_path), [row]
            elif len(web_path) == best and row not in owners:
                owners.append(row)
    return tuple(owners)


def owning_bundles(path: str, rows: tuple[Row, ...]) -> frozenset[int]:
    return frozenset(bundle_id for row in owning_rows(path, rows) for bundle_id in row.bundle_ids)


def is_retired(path: str, ledger: Ledger) -> bool:
    # 持ち主の束が 1 つ以上あり、その全部が切替済みのときだけ描かれないコード
    # A path is dead code only when it has owners and every owning bundle is switched
    bundles = owning_bundles(path, ledger.rows)
    return bool(bundles) and all(ledger.bundles[bundle_id].state == STATE_SWITCHED for bundle_id in bundles)


def uncovered_sources(paths: tuple[str, ...], rows: tuple[Row, ...]) -> tuple[str, ...]:
    return tuple(path for path in paths if path.startswith(WEB_SOURCE_ROOT) and not owning_rows(path, rows))
