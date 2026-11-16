"""Parse docs/uitk/TODO.md into bundles, rows and follow records for the UITK Web follow gate (G1)."""

import re
from dataclasses import dataclass

STATE_WEB = "Web"
STATE_DARK = "暗転"
STATE_SWITCHED = "切替済み"
STATES = (STATE_WEB, STATE_DARK, STATE_SWITCHED)
VERDICT_COPIED = "写した"
VERDICT_DISCARDED = "捨てた"
VERDICTS = (VERDICT_COPIED, VERDICT_DISCARDED)
OWNER_WEB = "Web"
OWNER_UITK = "UITK"
BUNDLE_IDS = (1, 2, 3, 4, 5)
EMPTY_CELLS = ("", "—")
WEB_UI_ROOT = "moorestech_web/webui/"

SECTION_BUNDLES = "## 束"
SECTION_ROWS = "## 行"
SECTION_RECORDS = "## 追従記録"
BUNDLE_HEADER = ("束", "中身", "状態", "暗転の起点", "物差し")
ROW_HEADER = ("行", "種別", "束", "所有者", "Web パス", "agent 既定", "実測の結論", "参照キャプチャ")
RECORD_HEADER = ("束", "コミット", "判定", "写し先・理由")
COMMIT = re.compile(r"^[0-9a-f]{7,40}$")
CODE_SPAN = re.compile(r"`([^`]+)`")


@dataclass(frozen=True)
class Bundle:
    bundle_id: int
    state: str
    dark_start: str | None


@dataclass(frozen=True)
class Row:
    name: str
    bundle_ids: tuple[int, ...]
    owner: str
    web_paths: tuple[str, ...]


@dataclass(frozen=True)
class FollowRecord:
    bundle_id: int
    commit: str
    verdict: str
    note: str


@dataclass(frozen=True)
class Ledger:
    bundles: dict[int, Bundle]
    rows: tuple[Row, ...]
    records: tuple[FollowRecord, ...]


@dataclass(frozen=True)
class LedgerParse:
    ledger: Ledger | None
    errors: tuple[str, ...]


def empty_ledger() -> Ledger:
    # 台帳がまだ無い base は全束 Web・行なしとみなす
    # A base without the ledger counts as every bundle on Web with no rows
    return Ledger({bundle_id: Bundle(bundle_id, STATE_WEB, None) for bundle_id in BUNDLE_IDS}, (), ())


def parse_ledger(text: str) -> LedgerParse:
    lines = text.splitlines()
    errors: list[str] = []

    # 束の表を先に読み、行と記録の束番号の照合に使う
    # Read the bundle table first; rows and records validate their bundle ids against it
    bundles = {}
    for cells in _table(lines, SECTION_BUNDLES, BUNDLE_HEADER, errors):
        bundle = _bundle(cells, errors)
        if bundle is not None and bundle.bundle_id in bundles:
            errors.append(f"束 {bundle.bundle_id} が重複している")
        elif bundle is not None:
            bundles[bundle.bundle_id] = bundle
    if tuple(sorted(bundles)) != BUNDLE_IDS:
        errors.append(f"「{SECTION_BUNDLES}」は束 1〜5 をちょうど 1 行ずつ持つ（読めた束: {sorted(bundles)}）")
        return LedgerParse(None, tuple(errors))

    # 行と追従記録を読み、誤りが 1 件でもあれば台帳を返さない
    # Read rows and follow records; any error withholds the ledger
    rows = tuple(row for row in (_row(cells, bundles, errors) for cells in _table(lines, SECTION_ROWS, ROW_HEADER, errors)) if row is not None)
    records = tuple(record for record in (_record(cells, bundles, errors) for cells in _table(lines, SECTION_RECORDS, RECORD_HEADER, errors)) if record is not None)
    if errors:
        return LedgerParse(None, tuple(errors))
    return LedgerParse(Ledger(bundles, rows, records), ())


def _table(lines: list[str], heading: str, header: tuple[str, ...], errors: list[str]) -> list[tuple[str, ...]]:
    # 見出し直後の最初の表を読む。見出し行が一致しなければ誤り
    # Read the first table under the heading; a mismatched header row is an error
    if lines.count(heading) != 1:
        errors.append(f"見出し「{heading}」が無いか重複している")
        return []
    # 次の見出しまで検査し、先頭パイプ欠落で読取りを打ち切らない
    # Inspect the entire section so a missing leading pipe cannot truncate ownership
    table = []
    for raw_line in lines[lines.index(heading) + 1:]:
        line = raw_line.strip()
        if line.startswith("## "):
            break
        if "|" not in line:
            continue  # silent-ok: 表外の説明と空行 / 表の構造は下で検証する
        if not line.startswith("|"):
            errors.append(f"「{heading}」の表の行頭に | が無い")
        if not line.endswith("|"):
            errors.append(f"「{heading}」の表の行末に | が無い")
        table.append(tuple(cell.strip() for cell in line[1:-1].split("|")))
    if not table or table[0] != header:
        errors.append(f"「{heading}」の表の見出しが「{' | '.join(header)}」ではない")
        return []
    if len(table) < 2 or len(table[1]) != len(header) or any(not re.fullmatch(r":?-{3,}:?", cell) for cell in table[1]):
        errors.append(f"「{heading}」の表の区切り行が不正")
        return []
    body = table[2:]
    for cells in body:
        if len(cells) != len(header):
            errors.append(f"「{heading}」の行の列数が {len(header)} ではない: {' | '.join(cells)}")
    return [cells for cells in body if len(cells) == len(header)]


def _plain(cell: str) -> str | None:
    value = cell.strip("`").strip()
    return None if value in EMPTY_CELLS else value


def _bundle(cells: tuple[str, ...], errors: list[str]) -> Bundle | None:
    raw_id, _, state, raw_start, _ = cells
    start = _plain(raw_start)
    if not raw_id.isascii() or not raw_id.isdigit() or int(raw_id) not in BUNDLE_IDS:
        errors.append(f"束の番号が 1〜5 ではない: {raw_id}")
        return None
    if state not in STATES:
        errors.append(f"束 {raw_id} の状態「{state}」は {'・'.join(STATES)} のどれでもない")
        return None
    if state == STATE_WEB and start is not None:
        errors.append(f"束 {raw_id} は Web なのに暗転の起点がある")
        return None
    if state != STATE_WEB and (start is None or not COMMIT.match(start)):
        errors.append(f"束 {raw_id} は{state}なのに暗転の起点がコミット（16 進 7〜40 桁）ではない: {raw_start}")
        return None
    return Bundle(int(raw_id), state, start)


def _row(cells: tuple[str, ...], bundles: dict[int, Bundle], errors: list[str]) -> Row | None:
    name, _, raw_bundles, owner, raw_paths, _, _, _ = cells
    ids = raw_bundles.split()
    if not ids or any(not value.isascii() or not value.isdigit() or int(value) not in bundles for value in ids):
        errors.append(f"行「{name}」の束「{raw_bundles}」が 1〜5 の空白区切りではない")
        return None
    if raw_paths not in EMPTY_CELLS and CODE_SPAN.sub("", raw_paths).strip():
        errors.append(f"行「{name}」の Web パスに引用されていない文字列がある: {raw_paths}")
        return None
    raw_web_paths = CODE_SPAN.findall(raw_paths)
    if any(path.startswith("/") or "\\" in path or any(part in ("", ".", "..") for part in path.rstrip("/").split("/")) for path in raw_web_paths):
        errors.append(f"行「{name}」の Web パスが正規の相対パスではない: {raw_paths}")
        return None
    paths = tuple(WEB_UI_ROOT + path for path in raw_web_paths)
    if not paths:
        errors.append(f"行「{name}」に Web パスが無い")
        return None

    # 所有者は束の状態から決まる。全束が切替済みのときだけ UITK
    # The owner follows bundle states: UITK only when every bundle of the row is switched
    bundle_ids = tuple(int(value) for value in ids)
    expected = OWNER_UITK if all(bundles[b].state == STATE_SWITCHED for b in bundle_ids) else OWNER_WEB
    if owner != expected:
        errors.append(f"行「{name}」の所有者「{owner}」が束の状態と合わない（{expected} のはず）")
        return None
    return Row(name, bundle_ids, owner, paths)


def _record(cells: tuple[str, ...], bundles: dict[int, Bundle], errors: list[str]) -> FollowRecord | None:
    raw_id, raw_commit, verdict, note = cells
    commit = _plain(raw_commit)
    if not raw_id.isascii() or not raw_id.isdigit() or int(raw_id) not in bundles:
        errors.append(f"追従記録の束が 1〜5 ではない: {raw_id}")
        return None
    if commit is None or not COMMIT.match(commit):
        errors.append(f"追従記録のコミットが 16 進 7〜40 桁ではない: {raw_commit}")
        return None
    if verdict not in VERDICTS:
        errors.append(f"追従記録 {commit} の判定「{verdict}」は {'・'.join(VERDICTS)} のどちらでもない")
        return None
    if verdict == VERDICT_DISCARDED and _plain(note) is None:
        errors.append(f"追従記録 {commit} は捨てたのに理由が無い")
        return None
    return FollowRecord(int(raw_id), commit, verdict, note)
