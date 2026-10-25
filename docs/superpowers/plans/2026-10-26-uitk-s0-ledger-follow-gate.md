# UITK 移行 段階0 台帳・参照キャプチャ・Web 追従ゲート G1 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** UI Toolkit 移行の台帳 `docs/uitk/TODO.md` と処遇表 `docs/uitk/disposition.md` を置き、Web 版の参照キャプチャを repo の外に残し、台帳を機械で読んで「暗転中の束に入った Web の変更の記帳」と「切替済みの束の Web の変更」を CI で落とす追従ゲート G1 を入れる。

**Architecture:** 台帳は Markdown 1 枚で、固定の見出し（`## 束`・`## 行`・`## 追従記録`）直下の表を G1 が読む。G1 は `.github/scripts/uitk_web_follow/`（Python 標準ライブラリだけ）で、PR の base と head の台帳・git 履歴を突き合わせ、新しい workflow `.github/workflows/uitk-web-follow-gate.yml` で走る。参照キャプチャは既存の Playwright mock-host を撮影ごとに別プロセスで起こして撮り、`moorestech_logs` に置いて台帳からリンクする。

**Tech Stack:** Python 3.10+（`unittest`・`subprocess`・`dataclasses`）、GitHub Actions、TypeScript＋Playwright（`moorestech_web/webui/e2e`）、git。

## Requirements

- R1 台帳 `docs/uitk/TODO.md` 1 枚がある。受入: 「## 行」の表に `moorestech_web/webui/src/features` の 20 機能すべてと、`moorestech_web/webui/src/app/App.tsx` のオーバーレイ行（背景スキット・Ctrl+U の全体非表示・カットシーン中の退避・背面の通知層・カーソル追従・常駐 HUD の可否・画面のマウント点・再接続と辞書エラー）が並ぶ
- R2 台帳の各行が、束（段階 1〜5）への割当・所有者（初版は全部 `Web`）・agent 既定・実測の結論・参照キャプチャの列を持つ。受入: G1 の台帳パーサが現行台帳を誤り 0 件で読む
- R3 束と Web パスの対応を G1 が機械で読める。受入: 「## 行」の `Web パス` 列のコードスパンを G1 が `moorestech_web/webui/` 相対のパスとして読み、ファイルの持ち主を最長一致で決める
- R4 Tier9 の #1551・#1552 が足す `moorestech_web/webui/src/features/blockInventory/details/{rocket,spaceElevator}` を、段階 4（持ち物の束の特殊族）の行として台帳に載せる
- R5 `docs/uitk/disposition.md`（写さないテストの理由を書く 1 枚）を枠だけ置く。旧 `docs/webui/MIGRATION.md` は本文を `docs/webui/archive/` へ移し、案内だけの短い文書にする
- R6 G1(1): 束が暗転から切替済みに変わる PR で、暗転の起点より後にその束の Web パスを触ったコミット（マージコミットを除く）が「## 追従記録」で全部「写した」か「捨てた（理由）」になっていなければ赤。受入: わざと足した未記帳の Web 変更で赤、記帳すると緑になるテスト
- R7 G1(2): 切替済みの束の Web パスへの追加・変更を赤にする（削除は通す）。共有パスは、そのパスを持つ全行の束が切替済みのときだけ落とす。受入: テスト用の台帳で、切替済みの束の Web ファイルを変更して赤、削除して緑、共有パスで緑になるテスト
- R8 G1(0): `moorestech_web/webui/src/` 以下の追跡ファイルが台帳のどの行にも入っていなければ赤（新しい Web 画面が入ったら台帳の行に足させるため）。受入: 未登録のディレクトリを足すと赤になるテスト
- R9 G1 は台帳を読めないとき（見出し・表の列・束 1〜5・状態・起点・判定・理由・所有者の整合）に赤になり、何が悪いかを出力する。受入: 各誤りで赤になるテスト
- R10 G1 は暗転中・切替済みの束ごとに未記帳の件数を毎回出力する（赤にはしない）。GitHub では job summary にも書く
- R11 G1 の置き場は CI。既存の `run_test.yml` の流儀（2 行コメント・`actions/checkout@v4`・concurrency・permissions）に合わせた独立 workflow で、G1 自身の unittest と、現行 repo に対する G1 の実行の両方を回す。既存のジョブは変えない
- R12 参照キャプチャ: Web 版の全画面と主要な状態を mock-host で撮る（PNG）。通知・脈動・ドラッグガイドは短い動画（webm）で残す。受入: 撮影表の全件が撮れ、`manifest.json` と `index.md`（束ごとの一覧）がそろう
- R13 参照キャプチャの置き場は `moorestech_logs`（`uitk/reference-captures/web-<撮影元コミット10桁>/`）。台帳の「## 参照キャプチャ」からリンクし、各行の `参照キャプチャ` 列に撮影 id を書く。PlayMode でしか撮れない状態は「## 未撮影」に束付きで列挙する
- R14 ドラッグガイドを撮れるよう、mock-host に `tutorialDragGuide` scenario を足す。`topicControls.ts` が 200 行を超えないよう、チュートリアルの scenario を `tutorialControls.ts` へ分ける（既存 e2e の挙動は変えない）
- R15 台帳に「## agent 既定（ユーザー未裁定）」と「## 実測の結論」の置き場を持ち、裁定 1〜3・確認・様式化の agent 既定と、実測 S1・S2・S3・S15・S9 の結論欄（初版は未着手）を書く
- やらないこと: Web UI の機能の変更（`src/` は 1 行も変えない）。UITK のコード。CI の他のジョブ（`run_test.yml` 等）の変更。他レーン（albof.1〜3・第 2 波）の持ち物。`.decisions/` への agent 既定の記録

## Global Constraints

- 1 ファイル 200 行未満。1 ディレクトリ 10 ファイルまで（AGENTS.md）
- 主要な処理の区切りに日本語→英語の 2 行コメント。各 1 行に収める（AGENTS.md「コメント」）。Python の module docstring は既存 `.github/scripts/check-test-external-dependencies.py` に倣い英語 1 行
- Python は標準ライブラリだけ。`try`/`except` を使わない（AGENTS.md の try-catch 規約に合わせ、誤りは戻り値で運ぶ）。git の失敗は `subprocess.run(check=True)` の例外のまま落とす（CI で赤になって気づく）
- 失敗・欠落は黙らない。台帳を読めない・起点が祖先に無い・必須の環境変数が無い、はすべて非 0 終了と理由の出力にする
- 台帳の `Web パス` は `moorestech_web/webui/` からの相対。末尾 `/` はディレクトリの前方一致、それ以外はファイルの完全一致
- 台帳のセルに `|` を書かない（表を壊すため）
- 状態の語は `Web`・`暗転`・`切替済み`。判定の語は `写した`・`捨てた`。所有者の語は `Web`・`UITK`。空欄は `—`
- Web の toolchain は同梱の `moorestech_web/node/mac-arm64`（`bin/node`・`pnpm`）を使う。`node_modules` は `pnpm install --frozen-lockfile` で作り、別 checkout から複製しない（`docs/development/worktree-and-review-pitfalls.md`）
- Unity Editor は使わない（本 plan に .cs の変更は無い）。使う必要が出たら `moores-wt status` で全セッション合計 3 本未満を確かめてから `uloop launch`
- 長い処理はバックグラウンドのポーリングで待たず前景で実行する。タスクごとにコミットする
- 外へ出す操作（Steam・Discord 等）はしない。`moorestech_logs` への push だけは置き場として許す（R13）

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 注目点3件（撮影ごとの別プロセス・moorestech_logs の新しい使い方・G1 のパッケージ分割）は判断記録 9〜13 に理由あり。範囲外の備考（全部 Web を固定するテストは最初の暗転 PR で赤になる）を受けてそのテストを外した
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強3・弱6・第3バケツ4 / 強は全部取り込むか段階1へ申し送り、弱は3件を取り込み3件はこのまま（判断記録「設計検査 Phase 2.6 の裁定」）

---

## File Structure

| パス | 新規/変更 | 責務 |
|---|---|---|
| `.github/scripts/uitk_web_follow/__init__.py` | 新規 | パッケージ印（空） |
| `.github/scripts/uitk_web_follow/ledger.py` | 新規 | 台帳 Markdown を `Ledger` へ読む。誤りは文字列の列で返す |
| `.github/scripts/uitk_web_follow/ownership.py` | 新規 | パス→持ち主の行・束（最長一致）、切替済みの判定、網羅の判定 |
| `.github/scripts/uitk_web_follow/git_history.py` | 新規 | git の薄い包み（merge-base・ファイル読み・差分・コミット列） |
| `.github/scripts/uitk_web_follow/checks.py` | 新規 | G1(0)(1)(2) の規則と `Violation` |
| `.github/scripts/uitk_web_follow/cli.py` | 新規 | 引数を読み、規則を回し、出力と終了コードを決める |
| `.github/scripts/check-uitk-web-follow.py` | 新規 | 入口（`cli.main` を呼ぶだけ） |
| `.github/scripts/tests/uitk_web_follow_testrepo.py` | 新規 | テスト用の一時 git repo と台帳文字列の組み立て |
| `.github/scripts/tests/test_uitk_web_follow_ledger.py` | 新規 | 台帳パーサと持ち主判定の単体テスト |
| `.github/scripts/tests/test_uitk_web_follow_gate.py` | 新規 | 一時 repo で G1 を通しで回すテスト（赤・緑のシナリオ） |
| `.github/scripts/tests/test_uitk_web_follow_real_ledger.py` | 新規 | 実 repo の台帳を読み、`src/` の網羅を確かめるテスト |
| `.github/workflows/uitk-web-follow-gate.yml` | 新規 | G1 の CI |
| `docs/uitk/TODO.md` | 新規 | 台帳（束・行・追従記録・参照キャプチャ・未撮影・agent 既定・実測の結論・運用） |
| `docs/uitk/disposition.md` | 新規 | 写さないテストの処遇（枠だけ） |
| `docs/webui/archive/MIGRATION-uGUI-to-Web-2026-07-18.md` | 移動 | 旧 `docs/webui/MIGRATION.md` の本文（`git mv`） |
| `docs/webui/MIGRATION.md` | 作り直し | 案内だけ |
| `moorestech_web/webui/e2e/mock-host/topics/tutorialControls.ts` | 新規 | チュートリアル scenario（既存 6 本の移設＋`tutorialDragGuide`） |
| `moorestech_web/webui/e2e/mock-host/topics/topicControls.ts` | 変更 | チュートリアル scenario を `tutorialControls` の展開に置き換える |
| `moorestech_web/webui/e2e/uitk-reference/referenceShots.ts` | 新規 | 撮影表（静止画） |
| `moorestech_web/webui/e2e/uitk-reference/referenceVideos.ts` | 新規 | 撮影表（動画） |
| `moorestech_web/webui/e2e/uitk-reference/shotRunner.ts` | 新規 | mock-host の起動・停止、画面の準備、1 件の撮影 |
| `moorestech_web/webui/e2e/uitk-reference/captureIndex.ts` | 新規 | `manifest.json` と `index.md` の書き出し |
| `moorestech_web/webui/e2e/uitk-reference/capture.ts` | 新規 | 撮影の入口 |
| `moorestech_web/webui/knip.json` | 変更 | entry に `e2e/uitk-reference/capture.ts` を足す |
| （repo 外）`$HOME/hermes-agent/data/worktrees/moorestech_logs/uitk-reference-captures/` | 新規 worktree | 参照キャプチャの置き場。`uitk/reference-captures/web-<sha10>/` と README のレイアウト 1 行 |

---

### Task 1: 台帳パーサと持ち主の判定（G1 の純ロジック）

**Files:**
- Create: `.github/scripts/uitk_web_follow/__init__.py`
- Create: `.github/scripts/uitk_web_follow/ledger.py`
- Create: `.github/scripts/uitk_web_follow/ownership.py`
- Create: `.github/scripts/tests/uitk_web_follow_testrepo.py`
- Test: `.github/scripts/tests/test_uitk_web_follow_ledger.py`

**Interfaces:**
- Consumes: なし
- Produces:
  - `ledger.py`: 定数 `STATE_WEB="Web"`・`STATE_DARK="暗転"`・`STATE_SWITCHED="切替済み"`・`VERDICT_COPIED="写した"`・`VERDICT_DISCARDED="捨てた"`・`OWNER_WEB="Web"`・`OWNER_UITK="UITK"`・`BUNDLE_IDS=(1,2,3,4,5)`・`WEB_UI_ROOT="moorestech_web/webui/"`。型 `Bundle(bundle_id:int, state:str, dark_start:str|None)`・`Row(name:str, bundle_ids:tuple[int,...], owner:str, web_paths:tuple[str,...])`（`web_paths` は repo 相対に直したもの）・`FollowRecord(bundle_id:int, commit:str, verdict:str, note:str)`・`Ledger(bundles:dict[int,Bundle], rows:tuple[Row,...], records:tuple[FollowRecord,...])`・`LedgerParse(ledger:Ledger|None, errors:tuple[str,...])`。関数 `parse_ledger(text:str) -> LedgerParse`・`empty_ledger() -> Ledger`
  - `ownership.py`: 定数 `WEB_SOURCE_ROOT="moorestech_web/webui/src/"`。関数 `owning_rows(path:str, rows:tuple[Row,...]) -> tuple[Row,...]`・`owning_bundles(path:str, rows:tuple[Row,...]) -> frozenset[int]`・`is_retired(path:str, ledger:Ledger) -> bool`・`uncovered_sources(paths:tuple[str,...], rows:tuple[Row,...]) -> tuple[str,...]`
  - `uitk_web_follow_testrepo.py`: `ledger_text(states:dict[int,tuple[str,str]], rows:list[tuple[str,str,str,str]], records:list[tuple[int,str,str,str]]) -> str`・`class TestRepo`（`root:Path`・`git(*args)->str`・`commit(message:str, files:dict[str,str|None])->str`・`cleanup()`）。`sys.path` に `.github/scripts` を足す

- [x] **Step 1: パッケージ印を置く**

`.github/scripts/uitk_web_follow/__init__.py` は空ファイル。

- [x] **Step 2: `ledger.py` を書く**

```python
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
        if bundle is not None:
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
    if heading not in lines:
        errors.append(f"見出し「{heading}」が無い")
        return []
    index = lines.index(heading) + 1
    while index < len(lines) and not lines[index].startswith("|") and not lines[index].startswith("## "):
        index += 1
    table = []
    while index < len(lines) and lines[index].startswith("|"):
        table.append(tuple(cell.strip() for cell in lines[index].strip().strip("|").split("|")))
        index += 1
    if not table or table[0] != header:
        errors.append(f"「{heading}」の表の見出しが「{' | '.join(header)}」ではない")
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
    if not raw_id.isdigit() or int(raw_id) not in BUNDLE_IDS:
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
    if not ids or any(not value.isdigit() or int(value) not in bundles for value in ids):
        errors.append(f"行「{name}」の束「{raw_bundles}」が 1〜5 の空白区切りではない")
        return None
    paths = tuple(WEB_UI_ROOT + path for path in CODE_SPAN.findall(raw_paths))
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
    if not raw_id.isdigit() or int(raw_id) not in bundles:
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
```

- [x] **Step 3: `ownership.py` を書く**

```python
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
```

- [x] **Step 4: テスト用の組み立て `uitk_web_follow_testrepo.py` を書く**

```python
"""Temporary git repositories and ledger text shared by the UITK Web follow gate tests."""

import os
import subprocess
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

GIT_ENV = {
    **os.environ,
    "GIT_AUTHOR_NAME": "gate-test",
    "GIT_AUTHOR_EMAIL": "gate-test",
    "GIT_COMMITTER_NAME": "gate-test",
    "GIT_COMMITTER_EMAIL": "gate-test",
}
BUNDLE_NAMES = {1: "命綱", 2: "常駐表示", 3: "建築", 4: "持ち物", 5: "最後"}


def ledger_text(states, rows, records) -> str:
    # states: {束: (状態, 起点)}、rows: (行, 束, 所有者, Web パス)、records: (束, コミット, 判定, 理由)
    # states: {bundle: (state, start)}, rows: (name, bundles, owner, paths), records: (bundle, commit, verdict, note)
    lines = ["# 台帳", "", "## 束", "", "| 束 | 中身 | 状態 | 暗転の起点 | 物差し |", "|---|---|---|---|---|"]
    for bundle_id in (1, 2, 3, 4, 5):
        state, start = states.get(bundle_id, ("Web", "—"))
        lines.append(f"| {bundle_id} | {BUNDLE_NAMES[bundle_id]} | {state} | {start} | — |")
    lines += ["", "## 行", "", "| 行 | 種別 | 束 | 所有者 | Web パス | agent 既定 | 実測の結論 | 参照キャプチャ |", "|---|---|---|---|---|---|---|---|"]
    lines += [f"| {name} | 機能 | {bundles} | {owner} | {paths} | — | — | — |" for name, bundles, owner, paths in rows]
    lines += ["", "## 追従記録", "", "| 束 | コミット | 判定 | 写し先・理由 |", "|---|---|---|---|"]
    lines += [f"| {bundle_id} | `{commit}` | {verdict} | {note} |" for bundle_id, commit, verdict, note in records]
    return "\n".join(lines) + "\n"


class TestRepo:
    def __init__(self):
        self._workspace = tempfile.TemporaryDirectory()
        self.root = Path(self._workspace.name)
        self.git("init", "-q", "-b", "master")

    def cleanup(self):
        self._workspace.cleanup()

    def git(self, *args: str) -> str:
        return subprocess.run(["git", *args], cwd=self.root, env=GIT_ENV, check=True, capture_output=True, text=True).stdout.strip()

    def commit(self, message: str, files: dict) -> str:
        # 値が None のファイルは削除し、それ以外は書いて add する
        # Remove files mapped to None; write and stage the rest
        for path, content in files.items():
            if content is None:
                self.git("rm", "-q", path)
                continue
            target = self.root / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(content, encoding="utf-8")
            self.git("add", path)
        self.git("commit", "-q", "-m", message)
        return self.git("rev-parse", "HEAD")
```

- [x] **Step 5: 単体テスト `test_uitk_web_follow_ledger.py` を書く**

```python
import unittest

from uitk_web_follow_testrepo import ledger_text
from uitk_web_follow.ledger import STATE_SWITCHED, parse_ledger
from uitk_web_follow.ownership import is_retired, owning_bundles, owning_rows, uncovered_sources

HUD = ("commonHud", "2", "Web", "`src/features/commonHud/`")
SHELL = ("アプリ殻", "5", "Web", "`src/`")
APP_OVERLAY = ("オーバーレイ: 背景スキット", "2", "Web", "`src/app/App.tsx`")
APP_SHELL = ("オーバーレイ: 再接続", "5", "Web", "`src/app/App.tsx`")
SHA = "a" * 40


class LedgerParseTest(unittest.TestCase):
    def parse_ok(self, text):
        result = parse_ledger(text)
        self.assertEqual(result.errors, ())
        return result.ledger

    def parse_error(self, text, fragment):
        result = parse_ledger(text)
        self.assertIsNone(result.ledger)
        self.assertTrue(any(fragment in error for error in result.errors), result.errors)

    def test_reads_bundles_rows_and_records(self):
        ledger = self.parse_ok(ledger_text({2: ("暗転", f"`{SHA[:10]}`")}, [HUD], [(2, SHA[:10], "写した", "UITK の Crosshair")]))
        self.assertEqual(ledger.bundles[2].dark_start, SHA[:10])
        self.assertEqual(ledger.rows[0].web_paths, ("moorestech_web/webui/src/features/commonHud/",))
        self.assertEqual(ledger.records[0].verdict, "写した")

    def test_rejects_dark_bundle_without_start(self):
        self.parse_error(ledger_text({2: ("暗転", "—")}, [HUD], []), "暗転の起点")

    def test_rejects_web_bundle_with_start(self):
        self.parse_error(ledger_text({2: ("Web", f"`{SHA[:10]}`")}, [HUD], []), "Web なのに")

    def test_rejects_unknown_state(self):
        self.parse_error(ledger_text({2: ("凍結", "—")}, [HUD], []), "状態")

    def test_rejects_owner_that_disagrees_with_bundle_state(self):
        self.parse_error(ledger_text({2: (STATE_SWITCHED, f"`{SHA[:10]}`")}, [HUD], []), "所有者")

    def test_rejects_discard_without_reason(self):
        self.parse_error(ledger_text({2: ("暗転", f"`{SHA[:10]}`")}, [HUD], [(2, SHA[:10], "捨てた", "—")]), "理由")

    def test_rejects_unknown_verdict(self):
        self.parse_error(ledger_text({2: ("暗転", f"`{SHA[:10]}`")}, [HUD], [(2, SHA[:10], "保留", "x")]), "判定")

    def test_rejects_row_without_paths(self):
        self.parse_error(ledger_text({}, [("空", "2", "Web", "—")], []), "Web パスが無い")

    def test_rejects_renamed_header(self):
        self.parse_error(ledger_text({}, [HUD], []).replace("| 束 | 中身 |", "| 段階 | 中身 |"), "表の見出し")

    def test_rejects_missing_section(self):
        self.parse_error(ledger_text({}, [HUD], []).replace("## 追従記録", "## 記録"), "追従記録")


class OwnershipTest(unittest.TestCase):
    def setUp(self):
        self.ledger = parse_ledger(ledger_text({2: (STATE_SWITCHED, f"`{SHA[:10]}`")}, [
            ("commonHud", "2", "UITK", "`src/features/commonHud/`"), SHELL,
            ("オーバーレイ: 背景スキット", "2", "UITK", "`src/app/App.tsx`"), APP_SHELL,
        ], [])).ledger

    def test_longest_prefix_wins(self):
        rows = owning_rows("moorestech_web/webui/src/features/commonHud/Crosshair.tsx", self.ledger.rows)
        self.assertEqual([row.name for row in rows], ["commonHud"])

    def test_equal_paths_share_ownership(self):
        self.assertEqual(owning_bundles("moorestech_web/webui/src/app/App.tsx", self.ledger.rows), frozenset({2, 5}))

    def test_retired_only_when_every_owner_bundle_is_switched(self):
        self.assertTrue(is_retired("moorestech_web/webui/src/features/commonHud/Crosshair.tsx", self.ledger))
        self.assertFalse(is_retired("moorestech_web/webui/src/app/App.tsx", self.ledger))

    def test_uncovered_sources_lists_only_src_paths_without_rows(self):
        ledger = parse_ledger(ledger_text({}, [HUD], [])).ledger
        paths = ("moorestech_web/webui/src/features/commonHud/a.tsx", "moorestech_web/webui/src/features/newScreen/a.tsx", "moorestech_web/webui/e2e/x.ts")
        self.assertEqual(uncovered_sources(paths, ledger.rows), ("moorestech_web/webui/src/features/newScreen/a.tsx",))


if __name__ == "__main__":
    unittest.main()
```

- [x] **Step 6: テストを実行して通ることを確認する**

Run: `python3 -m unittest discover -s .github/scripts/tests -p 'test_uitk_web_follow_ledger.py' -v`
Expected: 全件 `ok`、末尾 `OK`

- [x] **Step 7: コミットする**

```bash
git add .github/scripts/uitk_web_follow/__init__.py .github/scripts/uitk_web_follow/ledger.py .github/scripts/uitk_web_follow/ownership.py .github/scripts/tests/uitk_web_follow_testrepo.py .github/scripts/tests/test_uitk_web_follow_ledger.py
git commit -m "feat(uitk): 移行台帳のパーサと Web パスの持ち主判定を足す（G1 の純ロジック）"
```

---

### Task 2: G1 の規則・git の読み取り・入口（赤と緑のシナリオテスト）

**Files:**
- Create: `.github/scripts/uitk_web_follow/git_history.py`
- Create: `.github/scripts/uitk_web_follow/checks.py`
- Create: `.github/scripts/uitk_web_follow/cli.py`
- Create: `.github/scripts/check-uitk-web-follow.py`
- Test: `.github/scripts/tests/test_uitk_web_follow_gate.py`

**Interfaces:**
- Consumes: Task 1 の `parse_ledger`・`empty_ledger`・`Ledger`・`STATE_SWITCHED`・`owning_bundles`・`is_retired`・`uncovered_sources`・`WEB_SOURCE_ROOT`・`ledger_text`・`TestRepo`
- Produces:
  - `git_history.py`: `ChangedFile(status:str, path:str)`・`CommitFiles(sha:str, files:tuple[str,...])`・`merge_base(root:Path, base:str, head:str) -> str`・`resolve_commit(root:Path, rev:str) -> str|None`・`is_ancestor(root:Path, ancestor:str, descendant:str) -> bool`・`read_file_at(root:Path, rev:str, path:str) -> str|None`・`changed_files(root:Path, base:str, head:str) -> tuple[ChangedFile,...]`・`web_commits(root:Path, start:str, head:str) -> tuple[CommitFiles,...]`・`tracked_files(root:Path, rev:str, prefix:str) -> tuple[str,...]`
  - `checks.py`: `RULE_COVERAGE="G1-0 台帳の網羅"`・`RULE_SWITCH="G1-1 切替の記帳"`・`RULE_RETIRED="G1-2 切替済みの束"`・`Violation(rule:str, message:str)`・`check_coverage(head:Ledger, tracked_sources:tuple[str,...]) -> tuple[Violation,...]`・`check_retired_changes(base:Ledger, head:Ledger, changed:tuple[ChangedFile,...]) -> tuple[Violation,...]`・`switching_bundles(base:Ledger, head:Ledger) -> tuple[int,...]`・`unrecorded_commits(head:Ledger, bundle_id:int, commits:tuple[CommitFiles,...]) -> tuple[str,...]`・`check_switch_records(bundle_id:int, unrecorded:tuple[str,...]) -> tuple[Violation,...]`
  - `cli.py`: `LEDGER_PATH="docs/uitk/TODO.md"`・`main(argv:list[str]) -> int`（0=緑・1=赤）。引数 `--root`（既定 `.`）・`--base`（必須）・`--head`（必須）
  - 入口: `python3 .github/scripts/check-uitk-web-follow.py --base <rev> --head <rev>`

- [ ] **Step 1: `git_history.py` を書く**

```python
"""Thin git wrappers the UITK Web follow gate reads history through."""

import subprocess
from dataclasses import dataclass
from pathlib import Path

WEB_ROOT = "moorestech_web/"


@dataclass(frozen=True)
class ChangedFile:
    status: str
    path: str


@dataclass(frozen=True)
class CommitFiles:
    sha: str
    files: tuple[str, ...]


def _git(root: Path, *args: str) -> str:
    # git は外部プロセス。失敗は CalledProcessError のまま上げて CI を赤にする
    # git is an external process; failures surface as CalledProcessError and turn CI red
    return subprocess.run(["git", "-c", "core.quotePath=false", *args], cwd=root, check=True, capture_output=True, text=True).stdout


def _succeeds(root: Path, *args: str) -> bool:
    return subprocess.run(["git", *args], cwd=root, capture_output=True).returncode == 0


def merge_base(root: Path, base: str, head: str) -> str:
    return _git(root, "merge-base", base, head).strip()


def resolve_commit(root: Path, rev: str) -> str | None:
    if not _succeeds(root, "rev-parse", "--verify", "--quiet", f"{rev}^{{commit}}"):
        return None
    return _git(root, "rev-parse", f"{rev}^{{commit}}").strip()


def is_ancestor(root: Path, ancestor: str, descendant: str) -> bool:
    # 終了コード 0=祖先・1=祖先でない。それ以外は git の失敗として落とす
    # Exit 0 means ancestor and 1 means not; anything else is a git failure and raises
    result = subprocess.run(["git", "merge-base", "--is-ancestor", ancestor, descendant], cwd=root, capture_output=True, text=True)
    if result.returncode not in (0, 1):
        raise subprocess.CalledProcessError(result.returncode, result.args, result.stdout, result.stderr)
    return result.returncode == 0


def read_file_at(root: Path, rev: str, path: str) -> str | None:
    # rev は解決済みの前提。ファイルが無いときだけ None
    # The revision is already resolved; None means only that the file is absent
    if not _succeeds(root, "cat-file", "-e", f"{rev}:{path}"):
        return None
    return _git(root, "show", f"{rev}:{path}")


def changed_files(root: Path, base: str, head: str) -> tuple[ChangedFile, ...]:
    # rename は削除と追加に分けて読む（追加側だけが検査に回る）
    # Renames split into delete plus add so only the added side is checked
    output = _git(root, "diff", "--name-status", "--no-renames", base, head, "--", WEB_ROOT)
    return tuple(ChangedFile(*line.split("\t", 1)) for line in output.splitlines() if line)


def web_commits(root: Path, start: str, head: str) -> tuple[CommitFiles, ...]:
    # start より後で Web を触った非マージコミットを、触ったファイルごと返す
    # Return non-merge commits after start that touched Web, with the files each touched
    output = _git(root, "log", "--no-merges", "--no-renames", "--format=@%H", "--name-only", f"{start}..{head}", "--", WEB_ROOT)
    commits: list[CommitFiles] = []
    sha: str | None = None
    files: list[str] = []
    for line in output.splitlines():
        if line.startswith("@"):
            if sha is not None:
                commits.append(CommitFiles(sha, tuple(files)))
            sha, files = line[1:], []
        elif line:
            files.append(line)
    if sha is not None:
        commits.append(CommitFiles(sha, tuple(files)))
    return tuple(commits)


def tracked_files(root: Path, rev: str, prefix: str) -> tuple[str, ...]:
    return tuple(line for line in _git(root, "ls-tree", "-r", "--name-only", rev, "--", prefix).splitlines() if line)
```

- [ ] **Step 2: `checks.py` を書く**

```python
"""UITK Web follow gate (G1) rules over the base and head ledgers and git history."""

from dataclasses import dataclass

from .git_history import ChangedFile, CommitFiles
from .ledger import Ledger, STATE_SWITCHED
from .ownership import is_retired, owning_bundles, uncovered_sources

RULE_COVERAGE = "G1-0 台帳の網羅"
RULE_SWITCH = "G1-1 切替の記帳"
RULE_RETIRED = "G1-2 切替済みの束"


@dataclass(frozen=True)
class Violation:
    rule: str
    message: str


def check_coverage(head: Ledger, tracked_sources: tuple[str, ...]) -> tuple[Violation, ...]:
    return tuple(
        Violation(RULE_COVERAGE, f"{path} が台帳のどの行の Web パスにも入っていない。docs/uitk/TODO.md の「## 行」に足す")
        for path in uncovered_sources(tracked_sources, head.rows))


def check_retired_changes(base: Ledger, head: Ledger, changed: tuple[ChangedFile, ...]) -> tuple[Violation, ...]:
    # 削除は通す。base と head の両方で切替済みのパスだけ落とす（切替を戻す PR は通る）
    # Deletions pass; only paths retired in both base and head fail, so a rollback PR passes
    return tuple(
        Violation(RULE_RETIRED, f"{change.path}（{change.status}）は切替済みの束 {sorted(owning_bundles(change.path, head.rows))} の Web パス。描かれないコードなので UITK 側を直す")
        for change in changed
        if change.status != "D" and is_retired(change.path, base) and is_retired(change.path, head))


def switching_bundles(base: Ledger, head: Ledger) -> tuple[int, ...]:
    return tuple(
        bundle_id for bundle_id, bundle in head.bundles.items()
        if bundle.state == STATE_SWITCHED and base.bundles[bundle_id].state != STATE_SWITCHED)


def unrecorded_commits(head: Ledger, bundle_id: int, commits: tuple[CommitFiles, ...]) -> tuple[str, ...]:
    # その束が持つファイルを 1 つでも触ったコミットのうち、記録の無いもの
    # Commits touching any file the bundle owns that have no follow record for it
    recorded = tuple(record.commit for record in head.records if record.bundle_id == bundle_id)
    return tuple(
        commit.sha for commit in commits
        if any(bundle_id in owning_bundles(path, head.rows) for path in commit.files)
        and not any(commit.sha.startswith(prefix) for prefix in recorded))


def check_switch_records(bundle_id: int, unrecorded: tuple[str, ...]) -> tuple[Violation, ...]:
    return tuple(
        Violation(RULE_SWITCH, f"束 {bundle_id} の暗転の起点より後のコミット {sha[:10]} がその束の Web パスを触ったが「## 追従記録」に無い。写したか捨てた（理由）を書く")
        for sha in unrecorded)
```

- [ ] **Step 3: `cli.py` を書く**

```python
"""Command line entry of the UITK Web follow gate (G1)."""

import argparse
import os
import sys
from pathlib import Path

from . import git_history
from .checks import RULE_SWITCH, Violation, check_coverage, check_retired_changes, check_switch_records, switching_bundles, unrecorded_commits
from .ledger import STATE_WEB, empty_ledger, parse_ledger
from .ownership import WEB_SOURCE_ROOT

LEDGER_PATH = "docs/uitk/TODO.md"


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="UITK Web follow gate (G1)")
    parser.add_argument("--root", default=".")
    parser.add_argument("--base", required=True)
    parser.add_argument("--head", required=True)
    args = parser.parse_args(argv)
    root = Path(args.root).resolve()
    base_rev = git_history.merge_base(root, args.base, args.head)

    # head の台帳は必須。base に無いのは台帳を入れる PR だけなので全束 Web とみなす
    # The head ledger is required; only the PR introducing it lacks a base ledger, read as all-Web
    head_text = git_history.read_file_at(root, args.head, LEDGER_PATH)
    if head_text is None:
        print(f"G1: {LEDGER_PATH} が head に無い", file=sys.stderr)
        return 1
    base_text = git_history.read_file_at(root, base_rev, LEDGER_PATH)
    head_parse = parse_ledger(head_text)
    base_parse = parse_ledger(base_text) if base_text is not None else None
    errors = head_parse.errors + (base_parse.errors if base_parse is not None else ())
    if errors:
        for error in errors:
            print(f"G1: 台帳を読めない: {error}", file=sys.stderr)
        return 1
    head = head_parse.ledger
    base = base_parse.ledger if base_parse is not None else empty_ledger()
    if base_parse is None:
        print(f"G1: base {base_rev[:10]} に台帳が無いので全束を Web とみなす")

    # 網羅と切替済みの束の変更を見る
    # Check ledger coverage and changes to switched bundles
    violations: list[Violation] = [
        *check_coverage(head, git_history.tracked_files(root, args.head, WEB_SOURCE_ROOT)),
        *check_retired_changes(base, head, git_history.changed_files(root, base_rev, args.head)),
    ]

    # 起点を持つ束ごとに未記帳を数え、今回切り替える束だけ赤にする
    # Count unrecorded commits per bundle with a start; only bundles switching in this PR fail
    report: list[str] = []
    switching = switching_bundles(base, head)
    for bundle_id, bundle in sorted(head.bundles.items()):
        # 状態で分岐する。起点は Web 以外の束にだけあることをパーサが保証している
        # Branch on the state; the parser guarantees a start exists exactly for non-Web bundles
        if bundle.state == STATE_WEB or bundle.dark_start is None:
            continue
        start = git_history.resolve_commit(root, bundle.dark_start)
        if start is None or not git_history.is_ancestor(root, start, args.head):
            violations.append(Violation(RULE_SWITCH, f"束 {bundle_id} の暗転の起点 {bundle.dark_start} が head の祖先に無い"))
            continue
        unrecorded = unrecorded_commits(head, bundle_id, git_history.web_commits(root, start, args.head))
        report.append(f"束 {bundle_id}（{bundle.state}・起点 {bundle.dark_start}）: 未記帳 {len(unrecorded)} 件")
        if bundle_id in switching:
            violations.extend(check_switch_records(bundle_id, unrecorded))
    _emit(report, violations)
    return 1 if violations else 0


def _emit(report: list[str], violations: list[Violation]) -> None:
    lines = ["## UITK Web follow gate (G1)", ""]
    lines += [f"- {line}" for line in report] or ["- 起点を持つ束は無い"]
    lines += [f"- **{violation.rule}**: {violation.message}" for violation in violations]
    for line in report:
        print(f"G1: {line}")
    for violation in violations:
        print(f"G1 {violation.rule}: {violation.message}", file=sys.stderr)
    if not violations:
        print("G1: 違反なし")

    # GitHub では job summary にも同じ内容を書く
    # On GitHub, mirror the same content into the job summary
    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary:
        with open(summary, "a", encoding="utf-8") as stream:
            stream.write("\n".join(lines) + "\n")
```

- [ ] **Step 4: 入口 `check-uitk-web-follow.py` を書く**

```python
#!/usr/bin/env python3
"""Run the UITK Web follow gate (G1): ledger coverage, switch records, and switched bundle paths."""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from uitk_web_follow.cli import main  # noqa: E402

sys.exit(main(sys.argv[1:]))
```

- [ ] **Step 5: シナリオテスト `test_uitk_web_follow_gate.py` を書く**

一時 repo で「master に台帳と Web ファイル → master で Web 変更 → PR ブランチで台帳を書き換え」を作り、`main(["--root", root, "--base", "master", "--head", "pr"])` の戻り値と出力を見る。

```python
import contextlib
import io
import unittest

from uitk_web_follow_testrepo import TestRepo, ledger_text
from uitk_web_follow.cli import LEDGER_PATH, main

HUD_FILE = "moorestech_web/webui/src/features/commonHud/Crosshair.tsx"
APP_FILE = "moorestech_web/webui/src/app/App.tsx"
NEW_FILE = "moorestech_web/webui/src/features/newScreen/index.tsx"
SHELL = ("アプリ殻", "5", "Web", "`src/`")


def rows(hud_owner="Web", app_owner="Web"):
    return [("commonHud", "2", hud_owner, "`src/features/commonHud/`"), SHELL,
            ("オーバーレイ: 背景スキット", "2", app_owner, "`src/app/App.tsx`"),
            ("オーバーレイ: 再接続", "5", "Web", "`src/app/App.tsx`")]


class FollowGateTest(unittest.TestCase):
    def setUp(self):
        self.repo = TestRepo()
        self.start = self.repo.commit("web", {HUD_FILE: "v1\n", APP_FILE: "v1\n", LEDGER_PATH: ledger_text({}, rows(), [])})

    def tearDown(self):
        self.repo.cleanup()

    def run_gate(self):
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = main(["--root", str(self.repo.root), "--base", "master", "--head", "pr"])
        return code, out.getvalue(), err.getvalue()

    def dark_master_then_pr(self, master_files, pr_ledger):
        # master で束 2 を暗転にし、起点より後に Web を変え、PR で台帳を書き換える
        # Darken bundle 2 on master, change Web after the start, then rewrite the ledger on a PR branch
        self.repo.commit("dark", {LEDGER_PATH: ledger_text({2: ("暗転", f"`{self.start[:10]}`")}, rows(), [])})
        changed = self.repo.commit("web fix", master_files)
        self.repo.git("checkout", "-q", "-b", "pr")
        self.repo.commit("pr", {LEDGER_PATH: pr_ledger(changed)})
        return changed

    def switched(self, records):
        # 束 2 だけを持つ行（commonHud と背景スキット）の所有者を UITK にする
        # Rows owned only by bundle 2 (commonHud and the background skit) become UITK
        return ledger_text({2: ("切替済み", f"`{self.start[:10]}`")}, rows("UITK", "UITK"), records)

    def test_switch_with_unrecorded_web_change_is_red(self):
        changed = self.dark_master_then_pr({HUD_FILE: "v2\n"}, lambda sha: self.switched([]))
        code, _, err = self.run_gate()
        self.assertEqual(code, 1)
        self.assertIn("G1-1", err)
        self.assertIn(changed[:10], err)

    def test_switch_with_recorded_web_change_is_green(self):
        self.dark_master_then_pr({HUD_FILE: "v2\n"}, lambda sha: self.switched([(2, sha[:10], "写した", "UITK の Crosshair")]))
        self.assertEqual(self.run_gate()[0], 0)

    def test_switch_with_discarded_web_change_is_green(self):
        self.dark_master_then_pr({HUD_FILE: "v2\n"}, lambda sha: self.switched([(2, sha[:10], "捨てた", "UITK では別の形で描く")]))
        self.assertEqual(self.run_gate()[0], 0)

    def test_shared_app_file_counts_for_switching_bundle(self):
        self.dark_master_then_pr({APP_FILE: "v2\n"}, lambda sha: self.switched([]))
        self.assertEqual(self.run_gate()[0], 1)

    def test_change_before_dark_start_needs_no_record(self):
        self.repo.commit("old fix", {HUD_FILE: "v0\n"})
        start = self.repo.git("rev-parse", "HEAD")
        self.repo.commit("dark", {LEDGER_PATH: ledger_text({2: ("暗転", f"`{start[:10]}`")}, rows(), [])})
        self.repo.git("checkout", "-q", "-b", "pr")
        self.repo.commit("pr", {LEDGER_PATH: ledger_text({2: ("切替済み", f"`{start[:10]}`")}, rows("UITK", "UITK"), [])})
        self.assertEqual(self.run_gate()[0], 0)

    def test_dark_bundle_reports_unrecorded_count_without_failing(self):
        self.dark_master_then_pr({HUD_FILE: "v2\n"}, lambda sha: ledger_text({2: ("暗転", f"`{self.start[:10]}`")}, rows(), []))
        code, out, _ = self.run_gate()
        self.assertEqual(code, 0)
        self.assertIn("未記帳 1 件", out)

    def test_start_not_in_history_is_red(self):
        self.repo.git("checkout", "-q", "-b", "pr")
        self.repo.commit("pr", {LEDGER_PATH: ledger_text({2: ("暗転", "`0123456789`")}, rows(), [])})
        code, _, err = self.run_gate()
        self.assertEqual(code, 1)
        self.assertIn("祖先に無い", err)


class RetiredBundleTest(unittest.TestCase):
    def setUp(self):
        self.repo = TestRepo()
        self.start = self.repo.commit("web", {HUD_FILE: "v1\n", APP_FILE: "v1\n"})
        self.switched_ledger = ledger_text({2: ("切替済み", f"`{self.start[:10]}`")}, rows("UITK", "UITK"), [])
        self.repo.commit("switch", {LEDGER_PATH: self.switched_ledger})
        self.repo.git("checkout", "-q", "-b", "pr")

    def tearDown(self):
        self.repo.cleanup()

    def run_gate(self):
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = main(["--root", str(self.repo.root), "--base", "master", "--head", "pr"])
        return code, err.getvalue()

    def test_change_to_switched_bundle_web_path_is_red(self):
        self.repo.commit("late web fix", {HUD_FILE: "v2\n"})
        code, err = self.run_gate()
        self.assertEqual(code, 1)
        self.assertIn("G1-2", err)

    def test_delete_of_switched_bundle_web_path_is_green(self):
        self.repo.commit("delete", {HUD_FILE: None})
        self.assertEqual(self.run_gate()[0], 0)

    def test_shared_path_with_unswitched_owner_is_green(self):
        self.repo.commit("app fix", {APP_FILE: "v2\n"})
        self.assertEqual(self.run_gate()[0], 0)

    def test_rollback_to_dark_allows_web_fix(self):
        start = self.repo.git("rev-parse", "master")
        self.repo.commit("rollback", {HUD_FILE: "v2\n", LEDGER_PATH: ledger_text({2: ("暗転", f"`{start[:10]}`")}, rows(), [])})
        self.assertEqual(self.run_gate()[0], 0)

    def test_uncovered_new_screen_is_red(self):
        # アプリ殻（`src/` の受け皿）を外した台帳で、どの行にも入らない新しい画面を足す
        # Drop the shell row (the `src/` catch-all) and add a screen no row covers
        no_shell = [row for row in rows("UITK", "UITK") if row[0] != "アプリ殻"]
        self.repo.commit("new screen", {NEW_FILE: "x\n", LEDGER_PATH: ledger_text({2: ("切替済み", f"`{self.start[:10]}`")}, no_shell, [])})
        code, err = self.run_gate()
        self.assertEqual(code, 1, err)
        self.assertIn("G1-0", err)


if __name__ == "__main__":
    unittest.main()
```

注: 赤を期待するテストは、別の理由（台帳の読み違い）で赤になっていないことを `assertIn("G1-1" / "G1-2" / "G1-0", err)` で必ず確かめる。緑を期待するテストが落ちたら原因を読めるよう、`assertEqual(code, 0, err)` の形で `err` を渡す（`run_gate` が `err` を返すように揃える）

- [ ] **Step 6: テストを実行して通ることを確認する**

Run: `python3 -m unittest discover -s .github/scripts/tests -p 'test_uitk_web_follow_*.py' -v`
Expected: 全件 `ok`。赤のシナリオ（`..._is_red`）は G1 が 1 を返し、緑は 0 を返して通る

- [ ] **Step 7: コミットする**

```bash
git add .github/scripts/uitk_web_follow/git_history.py .github/scripts/uitk_web_follow/checks.py .github/scripts/uitk_web_follow/cli.py .github/scripts/check-uitk-web-follow.py .github/scripts/tests/test_uitk_web_follow_gate.py
git commit -m "feat(uitk): Web 追従ゲート G1 の規則と入口を足し、未記帳と切替済みの束の変更で赤になることをテストする"
```

---

### Task 3: 台帳・処遇表・旧 MIGRATION の案内・G1 の CI

**Files:**
- Create: `docs/uitk/TODO.md`
- Create: `docs/uitk/disposition.md`
- Move: `docs/webui/MIGRATION.md` → `docs/webui/archive/MIGRATION-uGUI-to-Web-2026-07-18.md`
- Create: `docs/webui/MIGRATION.md`（案内）
- Create: `.github/workflows/uitk-web-follow-gate.yml`
- Test: `.github/scripts/tests/test_uitk_web_follow_real_ledger.py`

**Interfaces:**
- Consumes: Task 1 の `parse_ledger`・`uncovered_sources`、Task 2 の `cli.main` と入口
- Produces: `docs/uitk/TODO.md`（Task 5 が `## 参照キャプチャ`・`## 未撮影`・各行の `参照キャプチャ` 列を埋める）

- [ ] **Step 1: 台帳 `docs/uitk/TODO.md` を書く**

全文（Task 5 で参照キャプチャの節と列を埋める。それ以外はこのまま）:

````markdown
# UI Toolkit 移行の台帳

計画 `docs/uitk-migration/plan.md` の段階 1〜5（束）と、現行 Web UI の全機能の対応表。束ごとの暗転・切替と、移行中に Web へ入った変更の記帳もここで持つ。CI の追従ゲート G1（`.github/workflows/uitk-web-follow-gate.yml`）が「## 束」「## 行」「## 追従記録」の表を読む。

## 読み方と運用

- 束 = 計画 §1 の段階 1〜5。状態は `Web`（まだ作っていない）→ `暗転`（UITK 版を配布画面に出さずに作っている）→ `切替済み`（所有表で UITK に切り替えた）
- 暗転に入るとき: その束の `状態` を `暗転` にし、`暗転の起点` にその時点の master のコミット（10 桁）を書く
- 暗転中に Web のその束のパスを触るコミットが入ったら: 「## 追従記録」に `写した`（UITK 側の写し先）か `捨てた`（理由）を書く。G1 は暗転中の未記帳の件数を毎回出す
- 切替 PR: `状態` を `切替済み`、その束だけを持つ行の `所有者` を `UITK` にする。G1 は、起点より後にその束の Web パスを触った非マージコミットが全部記帳されていなければ落とす
- 切替済みの束の Web パスへの追加・変更は G1 が落とす（描かれないコードへの変更は失われる。直すなら UITK 側）。削除は通す。複数の束が持つパスは、全部の束が切替済みになってから落ちる
- 切替を戻すときは `状態` を `暗転` に戻す（G1 は base と head の両方で切替済みのパスだけを落とす）
- `Web パス` は `moorestech_web/webui/` からの相対。末尾 `/` はディレクトリ、それ以外はファイル。ファイルの持ち主は最長一致の行（同じ長さなら全部）
- `moorestech_web/webui/src/` の下のファイルはどれかの行に入っていなければならない（G1 が落とす）。新しい Web 画面を足すときは、ここに行を足すか既存の行のパスに入れる
- `e2e/` の下に新しい spec・fixture・撮影スクリプトを足すときは、その画面の行の `Web パス` に足す。足さないと受け皿の「Web の検証基盤」（束 5）の持ち物になり、その束の切替で記帳を求められない（G1 は `e2e/` の網羅を検査しない）
- セルに `|` を書かない。空欄は `—`

## 束

| 束 | 中身 | 状態 | 暗転の起点 | 物差し |
|---|---|---|---|---|
| 1 | 基盤と命綱（ポーズ・設定・バグ報告・トースト） | Web | — | `uitk-lifeline` |
| 2 | 常駐表示（入力の無い HUD・通知・ツールチップ・ワールドピン・チュートリアル枠・スキット） | Web | — | `uitk-opening-hud` |
| 3 | 建築の束（ビルドメニュー・ホットバー・BP 名入力） | Web | — | `uitk-build-bp` |
| 4 | 持ち物の束（インベントリ・装備・掴み・レシピ・ブロック UI 全族・研究） | Web | — | `uitk-production-loop`・`uitk-research-train` |
| 5 | 最後の束と撤去（チャレンジ一覧・言語ゲート・チュートリアル枠・Web の横断層） | Web | — | `uitk-challenge-gate` |

## 行

所有者の列が所有表の初版（全部 Web）。`agent 既定` は行に効く agent 既定（全体は「## agent 既定（ユーザー未裁定）」）、`実測の結論` は行に効く実測（全体は「## 実測の結論」）。

| 行 | 種別 | 束 | 所有者 | Web パス | agent 既定 | 実測の結論 | 参照キャプチャ |
|---|---|---|---|---|---|---|---|
| pauseMenu（ポーズ・バグ報告・入れ子のポーズ） | 機能 | 1 | Web | `src/features/pauseMenu/` `e2e/tests/system/pauseMenu.spec.ts` `e2e/tests/system/pauseMenuSettingsLayout.spec.ts` `e2e/tests/system/feedbackReportLabels.spec.ts` `e2e/mock-host/pauseMenuActions.ts` `e2e/mock-host/topics/pauseMenuControls.ts` | 裁定 2=A（第一弾は命綱） | — | — |
| settings（画質・言語・ダッシュ方式） | 機能 | 1 | Web | `src/features/settings/` | 裁定 2=A | — | — |
| toast | 機能 | 1 | Web | `src/features/toast/` `e2e/tests/regression/actionErrors.spec.ts` | 裁定 2=A。様式化 (a) | — | — |
| commonHud（クロスヘア） | 機能 | 2 | Web | `src/features/commonHud/` `e2e/tests/system/commonHud.spec.ts` | — | — | — |
| keyHint | 機能 | 2 | Web | `src/features/keyHint/` | — | — | — |
| modeHud（配置 HUD・削除モードの帯） | 機能 | 2 | Web | `src/features/modeHud/` `e2e/tests/modeHud/` `e2e/support/operationHudAssertions.ts` | — | — | — |
| progress（採掘ゲージ） | 機能 | 2 | Web | `src/features/progress/` `e2e/tests/progress.spec.ts` `e2e/capture-mining-progress.ts` | — | — | — |
| trainHud（乗車中の分岐表示） | 機能 | 2 | Web | `src/features/trainHud/` | 様式化 (a) | — | — |
| notification（獲得通知） | 機能 | 2 | Web | `src/features/notification/` `e2e/tests/notification/` `e2e/captures/capture-connection-line-notifications.ts` | 確認=演出は実時間 | — | — |
| skit（会話窓・選択肢・ツールバー・暗転） | 機能 | 2 | Web | `src/features/skit/` `src/app/skitTokens.css` `e2e/tests/skit.spec.ts` `e2e/mock-host/skitActions.ts` `e2e/capture-skit.ts` | 確認=演出は実時間 | — | — |
| challenge: CurrentChallengeHud | 機能 | 2 | Web | `src/features/challenge/CurrentChallengeHud.tsx` `src/features/challenge/CurrentChallengeHud.module.css` `e2e/tests/challenge/` `e2e/challenge-hud/` `e2e/support/challengeHudAssertions.ts` | — | — | — |
| tutorial: KeyControlHintHud | 機能 | 2 | Web | `src/features/tutorial/KeyControlHintHud.tsx` `src/features/tutorial/KeyControlHintHud.test.ts` `src/features/tutorial/keyControlHint.module.css` | 確認=演出は実時間 | — | — |
| tutorial: WorldPinOverlay | 機能 | 2 | Web | `src/features/tutorial/WorldPinOverlay.tsx` `src/features/tutorial/worldPin.module.css` `e2e/tests/system/worldPin.spec.ts` `e2e/capture-world-pin-arrow.ts` | 確認=演出は実時間 | — | — |
| CursorTooltip | 共通部品 | 2 | Web | `src/shared/tooltip/` | — | — | — |
| オーバーレイ: 背景スキット | オーバーレイ | 2 | Web | `src/app/App.tsx` `e2e/tests/phaseC4.spec.ts` | 前回は状態機械だけで洗い出して見落とした（`docs/uitk-migration/research/understand-shipping.md` §6.2） | — | — |
| オーバーレイ: Ctrl+U の全体非表示 | オーバーレイ | 2 | Web | `src/app/App.tsx` | — | — | — |
| オーバーレイ: カットシーン中の退避 | オーバーレイ | 2 | Web | `src/app/App.tsx` | — | — | — |
| オーバーレイ: 背面の通知層（stage の裏） | オーバーレイ | 2 | Web | `src/app/App.tsx` | — | — | — |
| オーバーレイ: カーソル追従（ツールチップ・掴みの絵） | オーバーレイ | 2 4 | Web | `src/app/App.tsx` | — | — | — |
| オーバーレイ: 常駐 HUD の可否（ホットバー・装備） | オーバーレイ | 3 4 | Web | `src/app/App.tsx` | — | — | — |
| オーバーレイ: 画面のマウント点と背景ディム | オーバーレイ | 1 2 3 4 5 | Web | `src/app/App.tsx` `src/app/App.module.css` `src/app/App.architecture.test.ts` | 裁定 1=A（束単位で暗転のまま作り所有表で切り替える） | — | — |
| オーバーレイ: 再接続・辞書エラー・エラー境界 | オーバーレイ | 5 | Web | `src/app/App.tsx` `src/app/AppErrorBoundary.tsx` `e2e/tests/regression/connection.spec.ts` | — | — | — |
| buildMenu | 機能 | 3 | Web | `src/features/buildMenu/` `e2e/tests/regression/buildMenu.spec.ts` `e2e/tests/regression/buildMenuLayout.spec.ts` `e2e/capture-buildmenu.ts` `e2e/mock-host/fixtures/buildMenuFixtures.ts` | — | — | — |
| hotbar | 機能 | 3 | Web | `src/features/hotbar/` `e2e/tests/hotbar/` `e2e/mock-host/fixtures/hotbarFixtures.ts` | — | — | — |
| modal（BP 名入力） | 機能 | 3 | Web | `src/features/modal/` `e2e/tests/modal.spec.ts` | — | — | — |
| placementTarget（配置対象の表示名） | 共通部品 | 2 3 4 | Web | `src/shared/placementTarget/` | — | — | — |
| pointerGesture（押下・ドラッグの判定） | 共通部品 | 3 4 5 | Web | `src/shared/pointerGesture/` | — | — | — |
| inventory（インベントリ・装備・掴み） | 機能 | 4 | Web | `src/features/inventory/` `e2e/tests/inventory/` `e2e/mock-host/inventoryModel.ts` | 裁定 1=A（持ち物の束は束ごと一度に切り替える） | — | — |
| recipe（レシピ・アイテム一覧・長押しクラフト） | 機能 | 4 | Web | `src/features/recipe/` `e2e/tests/recipe/` `e2e/mock-host/fixtures/recipeFixtures.ts` `e2e/craft-chrome/` `e2e/support/craftChromeAssertions.ts` | 確認=演出は実時間 | — | — |
| blockInventory（汎用族・特殊族・列車族） | 機能 | 4 | Web | `src/features/blockInventory/` `e2e/tests/block/` `e2e/tests/train/` `e2e/tests/train.spec.ts` `e2e/tests/regression/sectionStack.spec.ts` `e2e/mock-host/fixtures/blocks/` `e2e/mock-host/blockDetailFixtures.ts` `e2e/mock-host/detailActions.ts` `e2e/mock-host/detailActions.test.ts` `e2e/mock-host/train/` `e2e/mock-host/fixtures/trainInventoryFixtures.ts` `e2e/capture-machine-qa.ts` `e2e/capture-train-timetable.ts` `e2e/fixtures/` | — | — | — |
| blockInventory 特殊族: ロケット（Tier9 #1551） | 機能 | 4 | Web | `src/features/blockInventory/details/rocket/` | — | — | — |
| blockInventory 特殊族: 宇宙エレベーター（Tier9 #1552） | 機能 | 4 | Web | `src/features/blockInventory/details/spaceElevator/` | — | — | — |
| research（研究ツリー・詳細ペイン） | 機能 | 4 | Web | `src/features/research/` `e2e/tests/research/` `e2e/mock-host/researchFixtures.ts` `e2e/capture-research-qa.ts` `e2e/support/panSettle.ts` | — | — | — |
| tutorial: DragGuide（インベントリ内） | 機能 | 4 | Web | `src/features/tutorial/overlay/DragGuide.tsx` | 確認=演出は実時間 | — | — |
| itemMove（掴みの計画） | 共通部品 | 4 | Web | `src/shared/itemMove/` `e2e/tests/regression/itemSlot.spec.ts` | — | — | — |
| ownedCounts | 共通部品 | 4 | Web | `src/shared/ownedCounts.ts` `src/shared/ownedCounts.test.ts` | — | — | — |
| treeView（パン・ズーム・線） | 共通部品 | 4 5 | Web | `src/shared/treeView/` | — | — | — |
| challenge: ChallengePanel | 機能 | 5 | Web | `src/features/challenge/` `e2e/tests/challenge.spec.ts` | 様式化 (a) | — | — |
| eventLanguageGate（出展モードの言語選択） | 機能 | 5 | Web | `src/features/eventLanguageGate/` | — | — | — |
| tutorial: TutorialOverlay（ハイライト枠） | 機能 | 5 | Web | `src/features/tutorial/` `e2e/tests/system/tutorial.spec.ts` `e2e/tests/system/tutorialHighlightClip.spec.ts` `e2e/support/pulseFreeze.ts` `e2e/mock-host/topics/tutorialControls.ts` | 確認=演出は実時間 | — | — |
| tutorialAnchor（アンカーの登録と解決） | 共通部品 | 1 2 3 4 5 | Web | `src/shared/tutorialAnchor/` | — | — | — |
| shared/ui（枠・スロット・ボタン・ゲージ） | 共通部品 | 1 2 3 4 5 | Web | `src/shared/ui/` `src/shared/clamp01.ts` `src/shared/clamp01.test.ts` `src/shared/materialTooltipText.ts` `src/app/slotTokens.css` `src/app/iconTextOutlineDesign.test.ts` `e2e/capture-iconbutton.ts` `e2e/capture-icon-text-outline.ts` `e2e/support/layoutAssertions.ts` | 様式化 (a) | — | — |
| i18n | 横断 | 1 2 3 4 5 | Web | `src/shared/i18n/` `scripts/` `e2e/tests/system/i18n.spec.ts` `e2e/mock-host/localization/` `e2e/mock-host/topics/dictionaryControl.ts` | — | — | — |
| uiState（画面ルーティング・入力排他） | 横断 | 1 2 3 4 5 | Web | `src/shared/uiState/` `e2e/tests/uiState.spec.ts` `e2e/tests/regression/inputLayer.spec.ts` | 裁定 1=A | — | — |
| uiScale（stage の一様拡縮） | 横断 | 2 5 | Web | `src/shared/uiScale/` `e2e/tests/system/portalLayerScaling.spec.ts` `e2e/captures/portal-scaling.ts` | — | — | — |
| bridge（通信・契約・ストア） | 横断 | 5 | Web | `src/bridge/` | 裁定 3=(a) `HttpListener`（Web ホストの撤去と同じ段階） | — | — |
| アプリ殻（起動・トークン・層） | 横断 | 5 | Web | `src/app/` `src/main.tsx` `src/vite-env.d.ts` `index.html` `e2e/tests/system/textSelection.spec.ts` | — | — | — |
| Web の検証基盤（mock-host・Playwright・撮影） | 横断 | 5 | Web | `e2e/` | — | — | — |

## 追従記録

暗転の起点より後に、暗転中の束の Web パスを触ったコミットの処遇。コミットは 10 桁以上。`捨てた` には理由が要る。

| 束 | コミット | 判定 | 写し先・理由 |
|---|---|---|---|

## 参照キャプチャ

（Task 5 が埋める）

## 未撮影

（Task 5 が埋める）

## agent 既定（ユーザー未裁定）

計画 §0 の残りの裁定は、ユーザーの指示「動く前提で並列で開始して」により、計画の推奨を agent 既定として進めている。ユーザー裁定ではない（`.decisions/` には書かない）。覆ったらここと該当行を直す。

| 項目 | agent 既定 | 帰結 |
|---|---|---|
| 裁定 1 移行中の出荷形 | A: 束単位で暗転のまま作り、所有表で切り替える | 見た目の混在が届く。インベントリ系の CEF 不具合は段階 4 の終わりまで残る |
| 裁定 2 第一弾の画面 | A: 命綱（ポーズ・設定・バグ報告・トースト） | CEF が死んでも Esc でセーブと報告ができる |
| 裁定 3 遠隔実行の待受 | (a) `HttpListener`。配布ビルドで動かなければ (b) 遠隔実行専用の最小 Kestrel | ADR 0072 の改訂が要る |
| 確認 UI の演出の時間源 | 実時間（パネルのスケジューラ） | AGENTS.md の時間の規則はサーバーのゲームロジックの規則と読む |
| 様式化 Mantine の素の見た目の 4 画面 | 未裁定（計画の推奨は (a) 移すときに様式化） | 該当行に「様式化 (a)」と書いた |

裁定 4（Web の扱い）は 2026-10-26 にユーザー裁定済み（PR #1555 の `.decisions/2026-10-26-UITKが安定するまでWeb-UIの変更を続け現行機能をすべてUITKへ移す.md`）。G1 はその帰結の追従ゲート。

## 実測の結論

計画 §4.8 の実測。結論は各レーンが書く（証拠のパス付き）。

| 実測 | 中身 | 担当 | 結論 |
|---|---|---|---|
| S1 | CEF と UITK の前後 | bd `moorestech-albof.1` | 未着手 |
| S2 | 録画とバグ報告動画への写り込み | bd `moorestech-albof.1` | 未着手 |
| S3 | 入力注入 | bd `moorestech-albof.1` | 未着手 |
| S15 | 委譲中の Web の入力停止 | bd `moorestech-albof.1` | 未着手 |
| S9 | 日本語 IME（人手） | bd `moorestech-albof.8` | 未着手 |
````

- [ ] **Step 2: 処遇表 `docs/uitk/disposition.md` を書く（枠だけ）**

```markdown
# 写さない Web テストの処遇

段階ごとの削除 PR で、消えた Web のテスト名（Playwright・vitest）と増えた NUnit のテスト名をスクリプトで突き合わせる（計画 `docs/uitk-migration/plan.md` §4.1）。写さないテストはここに 1 件 1 行で理由を書く。目視で済ませるものは証跡のパス、捨てるものは理由。見た目の意図は `docs/uitk/TODO.md` の参照キャプチャで辿る。

| 束 | Web のテスト（ファイル・テスト名） | 処遇（目視・捨てる） | 証跡のパスか理由 | PR |
|---|---|---|---|---|
```

- [ ] **Step 3: 旧 MIGRATION.md を archive へ移し、案内を置く**

```bash
git mv docs/webui/MIGRATION.md docs/webui/archive/MIGRATION-uGUI-to-Web-2026-07-18.md
```

新しい `docs/webui/MIGRATION.md`:

```markdown
# Web UI 移行の入口（案内）

uGUI から Web UI（CEF＋React）への移行は終わっている。当時の入口文書は `docs/webui/archive/MIGRATION-uGUI-to-Web-2026-07-18.md` に移した（`docs/webui/TODO.md`・`disposition.md`・`plans/` はその時の記録）。

Web UI から UI Toolkit への移行（裁定 `.decisions/2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する.md`）は次を読む。

- 計画: `docs/uitk-migration/README.md` → `plan.md`
- 台帳（束・所有者・追従記録・参照キャプチャ）: `docs/uitk/TODO.md`
- 写さないテストの処遇: `docs/uitk/disposition.md`
```

- [ ] **Step 4: 実 repo の台帳テスト `test_uitk_web_follow_real_ledger.py` を書く**

```python
import subprocess
import unittest
from pathlib import Path

import uitk_web_follow_testrepo  # noqa: F401  (sys.path に .github/scripts を足す)
from uitk_web_follow.cli import LEDGER_PATH
from uitk_web_follow.ledger import parse_ledger
from uitk_web_follow.ownership import WEB_SOURCE_ROOT, uncovered_sources

REPO_ROOT = Path(__file__).resolve().parents[3]
FEATURES = "moorestech_web/webui/src/features/"


class RealLedgerTest(unittest.TestCase):
    def setUp(self):
        result = parse_ledger((REPO_ROOT / LEDGER_PATH).read_text(encoding="utf-8"))
        self.assertEqual(result.errors, ())
        self.ledger = result.ledger
        listing = subprocess.run(["git", "ls-files", "--", WEB_SOURCE_ROOT], cwd=REPO_ROOT, check=True, capture_output=True, text=True)
        self.sources = tuple(line for line in listing.stdout.splitlines() if line)

    def test_every_web_source_is_covered(self):
        self.assertEqual(uncovered_sources(self.sources, self.ledger.rows), ())

    def test_every_feature_directory_has_its_own_row(self):
        # 20 機能それぞれのディレクトリが、どこかの行の Web パスに名前で現れる
        # Each of the 20 feature directories appears by name in some row's Web paths
        features = sorted({path[len(FEATURES):].split("/")[0] for path in self.sources if path.startswith(FEATURES)})
        listed = " ".join(path for row in self.ledger.rows for path in row.web_paths)
        self.assertEqual([feature for feature in features if f"{FEATURES}{feature}/" not in listed], [])
        self.assertGreaterEqual(len(features), 20)

    def test_tier9_special_family_rows_are_bundle_four(self):
        for name in ("details/rocket/", "details/spaceElevator/"):
            rows = [row for row in self.ledger.rows if any(path.endswith(name) for path in row.web_paths)]
            self.assertEqual([row.bundle_ids for row in rows], [(4,)])


if __name__ == "__main__":
    unittest.main()
```

注: 所有表の初版が全部 Web であることや機能の数をちょうど 20 に固定することはしない（最初の暗転 PR や、行付きで足した新しい機能で無関係に赤になるため）。束の状態と所有者の整合はパーサが、新しい機能の行の有無は G1(0) と上の機能ごとの照合が毎回見る。

- [ ] **Step 5: CI `.github/workflows/uitk-web-follow-gate.yml` を書く**

```yaml
name: UITK Web Follow Gate
on:
  # 台帳（.md）だけの PR でも走らせるため paths-ignore を付けない
  # No paths-ignore, so PRs touching only the ledger (.md) still run the gate
  pull_request: {}

# PR更新時に同一PRの古い実行をキャンセルする
# Cancel outdated runs of the same PR on update
concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true

permissions:
  contents: read

jobs:
  # UITK 移行の追従ゲート G1。暗転中の束に入った Web 変更の記帳と、切替済みの束の Web 変更を検査する
  # UITK migration follow gate G1: checks records for Web changes in dark bundles and rejects changes to switched bundles
  uitk-web-follow-gate:
    name: UITK Web follow gate (G1)
    runs-on: ubuntu-latest
    steps:
      # 既定の PR マージ参照を全履歴で取る。台帳より前に分岐した PR でも base 側の台帳で検査できる
      # Check out the default PR merge ref with full history, so PRs branched before the ledger still read it from base
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Test the gate itself
        run: python3 -m unittest discover -s .github/scripts/tests -p 'test_uitk_web_follow_*.py' -v

      - name: Run G1 against this pull request
        env:
          BASE_SHA: ${{ github.event.pull_request.base.sha }}
        run: python3 .github/scripts/check-uitk-web-follow.py --base "$BASE_SHA" --head HEAD
```

- [ ] **Step 6: 実 repo で G1 とテストを回して通ることを確認する**

Run:
```bash
python3 -m unittest discover -s .github/scripts/tests -p 'test_uitk_web_follow_*.py' -v
git add -A docs/uitk docs/webui .github/workflows/uitk-web-follow-gate.yml .github/scripts/tests/test_uitk_web_follow_real_ledger.py
git commit -m "docs(uitk): 移行台帳・処遇表・旧 MIGRATION の案内と G1 の CI を置く"
python3 .github/scripts/check-uitk-web-follow.py --base origin/master --head HEAD
```
Expected: unittest は全件 `ok`。G1 は `G1: base … に台帳が無いので全束を Web とみなす` と `G1: 起点を持つ束は無い`（summary 側）・`G1: 違反なし` を出して終了コード 0。違反が出たら台帳の行を直す（ゲートを緩めない）

- [ ] **Step 7: 手で赤を 1 回見る（台帳を壊したら赤）**

Run: `sed 's/| 1 | 基盤と命綱（ポーズ・設定・バグ報告・トースト） | Web |/| 1 | 基盤と命綱（ポーズ・設定・バグ報告・トースト） | 凍結 |/' docs/uitk/TODO.md > "$TMPDIR/todo-broken.md" && python3 -c "import sys; sys.path.insert(0, '.github/scripts'); from uitk_web_follow.ledger import parse_ledger; print(parse_ledger(open('$TMPDIR/todo-broken.md', encoding='utf-8').read()).errors)"`
Expected: `束 1 の状態「凍結」は …` を含む誤りが出る。作業ツリーの台帳は変えない

- [ ] **Step 8: コミット（Step 6 で済み。追加の修正があればここでコミット）**

```bash
git status --short
git commit -am "fix(uitk): 台帳の行を実 repo の Web ファイルに合わせる" # 直したときだけ
```

---

### Task 4: mock-host のチュートリアル scenario を分け、ドラッグガイドを足す

**Files:**
- Create: `moorestech_web/webui/e2e/mock-host/topics/tutorialControls.ts`
- Modify: `moorestech_web/webui/e2e/mock-host/topics/topicControls.ts`（`tutorialOutline`・`tutorialResearchNode`・`tutorialRecipeItem`・`tutorialEmpty`・`tutorialOutlineWithLabel`・`tutorialKeyControl` と `TUTORIAL_RESEARCH_NODE_PADDING_PX` を移す）

**Interfaces:**
- Consumes: 既存 `../fixtures`（`researchableNodeGuid`・`TUTORIAL_RECIPE_ITEM_ID`・`OUTLINE_LABEL_TUTORIAL_GUID`・`KEY_CONTROL_TUTORIAL_GUID`）、`../../../src/shared/tutorialAnchor/anchorIds`（`recipeItemAnchorId`・`researchNodeAnchorId`・`TutorialAnchorIds`）
- Produces: `export const tutorialControls`（キー: 既存 6 本＋`tutorialDragGuide`）、`export const TUTORIAL_RESEARCH_NODE_PADDING_PX = 8`。`topicControls.ts` は `...tutorialControls` を展開し、`export { TUTORIAL_RESEARCH_NODE_PADDING_PX } from "./tutorialControls";` で既存 import（`e2e/tests/system/tutorialHighlightClip.spec.ts`）を保つ。`TopicScenario` に `"tutorialDragGuide"` が入る（Task 5 が使う）

- [ ] **Step 1: `tutorialControls.ts` を書く**

既存の `pauseMenuControls.ts` と同じ形（`{ kind: "set", topic, data }` を返す関数の表）で、`topicControls.ts` の 6 本を中身を変えずに移し、`tutorialDragGuide` を足す:

```ts
import { Topics, type TopicPayloads } from "../../../src/bridge/transport/protocol";
import { recipeItemAnchorId, researchNodeAnchorId, TutorialAnchorIds } from "../../../src/shared/tutorialAnchor/anchorIds";
import * as fx from "../fixtures";

// 研究ノード枠の余白。spec と共有する正本
// Padding of the research node outline; the single value shared with the spec
export const TUTORIAL_RESEARCH_NODE_PADDING_PX = 8;

type TutorialData = TopicPayloads[typeof Topics.tutorialPresentation];
const tutorial = (data: TutorialData) => ({ kind: "set" as const, topic: Topics.tutorialPresentation, data });

// チュートリアル表示の scenario 群。topicControls から展開する
// Tutorial presentation scenarios, spread into topicControls
export const tutorialControls = {
  // ↓ topicControls.ts の 6 本（tutorialOutline〜tutorialKeyControl）をここへ移す
  // ↓ Move the six scenarios (tutorialOutline … tutorialKeyControl) from topicControls.ts here
  // レシピ一覧のセルからホットバーへのドラッグ誘導。参照キャプチャの動画で使う
  // Drag guide from a recipe list cell to the hotbar, used by the reference capture video
  tutorialDragGuide: () => tutorial({
    revision: 5,
    sessions: [{
      tutorialSessionId: "tutorial-session-drag-guide", challengeId: "tutorial-challenge-drag-guide",
      elements: [{
        kind: "dragGuide" as const, elementId: "tutorial-drag-guide-1",
        fromAnchorId: recipeItemAnchorId(fx.TUTORIAL_RECIPE_ITEM_ID), toAnchorId: TutorialAnchorIds.hotbarHud,
      }],
    }],
  }),
};
```

上のブロックの「↓ … ここへ移す」の 2 行は作業指示なので、ファイルには写さず、`topicControls.ts` の 6 本（`tutorialOutline`・`tutorialResearchNode`・`tutorialRecipeItem`・`tutorialEmpty`・`tutorialOutlineWithLabel`・`tutorialKeyControl`）の定義に置き換える。各定義の `control(Topics.tutorialPresentation, X)` を `tutorial(X)` に変えるだけで、中身と付いているコメントはそのまま移す。`researchNodeAnchorId` は移した `tutorialResearchNode` が使う。`tutorialDragGuide` の `data` が `TutorialPresentationData` の型に合わなければ（`elements` の dragGuide に必須の項目が足りない等）、`src/bridge/contract/schemas/presentation.ts` の `TutorialDragGuideSchema` に合わせて直す。

- [ ] **Step 2: `topicControls.ts` から 6 本と定数を消し、展開と再 export を足す**

- `export const TUTORIAL_RESEARCH_NODE_PADDING_PX = 8;` とその直前のコメント 2 行を消し、`export { TUTORIAL_RESEARCH_NODE_PADDING_PX } from "./tutorialControls";` に置き換える
- `const controls = {` の直後の `...pauseMenuControls,` の次行に `...tutorialControls,` を足し、`import { tutorialControls } from "./tutorialControls";` を足す
- 6 本の定義（コメントごと）を消す。使われなくなった import（`recipeItemAnchorId`・`researchNodeAnchorId`）を消す
- `wc -l` で 200 行未満を確かめる

- [ ] **Step 3: 型検査と既存の e2e（チュートリアル系）を回す**

Run:
```bash
cd moorestech_web/webui
../node/mac-arm64/pnpm install --frozen-lockfile
../node/mac-arm64/pnpm exec tsc -p e2e/tsconfig.json --noEmit
../node/mac-arm64/pnpm exec playwright install chromium
../node/mac-arm64/pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/system/tutorial.spec.ts e2e/tests/system/tutorialHighlightClip.spec.ts e2e/tests/system/worldPin.spec.ts
```
Expected: tsc はエラー 0。playwright は全件 passed。落ちる spec があれば、移設前のコミットに対して同じ 3 本を回して比べる（`git log -1 --format=%H -- moorestech_web/webui/e2e/mock-host/topics/topicControls.ts` の 1 つ前のコミットを `git worktree` ではなく `git checkout <sha> -- moorestech_web/webui/e2e/mock-host/topics/` で一時的に戻して回し、`git checkout HEAD -- moorestech_web/webui/e2e/mock-host/topics/` で戻す）。移設前から落ちるものは移設の原因ではないので、件名を `bd note moorestech-albof.4` に残して進む。移設で落ちたものは直す

- [ ] **Step 4: コミットする**

```bash
git add moorestech_web/webui/e2e/mock-host/topics/tutorialControls.ts moorestech_web/webui/e2e/mock-host/topics/topicControls.ts
git commit -m "test(webui): mock-host のチュートリアル scenario を分け、ドラッグガイドの scenario を足す"
```

---

### Task 5: 参照キャプチャを撮って moorestech_logs に置き、台帳からリンクする

**Files:**
- Create: `moorestech_web/webui/e2e/uitk-reference/referenceShots.ts`
- Create: `moorestech_web/webui/e2e/uitk-reference/referenceVideos.ts`
- Create: `moorestech_web/webui/e2e/uitk-reference/shotRunner.ts`
- Create: `moorestech_web/webui/e2e/uitk-reference/captureIndex.ts`
- Create: `moorestech_web/webui/e2e/uitk-reference/capture.ts`
- Modify: `moorestech_web/webui/knip.json`（entry に `"e2e/uitk-reference/capture.ts"`）
- Modify: `docs/uitk/TODO.md`（`## 参照キャプチャ`・`## 未撮影`・各行の `参照キャプチャ` 列）
- repo 外: `$HOME/hermes-agent/data/worktrees/moorestech_logs/uitk-reference-captures/`（`README.md` のレイアウト 1 行、`uitk/reference-captures/web-<sha10>/`）

**Interfaces:**
- Consumes: Task 4 の `TopicScenario`（`tutorialDragGuide` を含む）、既存 `../support/mockControl` の `requestMockControl(page, baseUrl, path)`、`../mock-host/server.ts`（`MOCK_PORT`・`MOCK_DEMO`、標準出力 `mock-host on <port>`）
- Produces: `referenceShots: ReferenceShot[]`・`referenceVideos: ReferenceVideo[]`・`ShotStep`・`ShotSetup`（`referenceShots.ts`）、`startMockHost(webuiRoot:string, port:number): Promise<ChildProcess>`・`stopMockHost(child:ChildProcess): Promise<void>`・`openShot(page:Page, baseUrl:string, setup:ShotSetup): Promise<void>`・`referenceViewport`（`shotRunner.ts`）、`writeCaptureIndex(outputDirectory:string, entries:CaptureEntry[], sourceCommit:string): Promise<void>`・`CaptureEntry`（`captureIndex.ts`）。成果物: `<出力先>/<id>.png`・`<id>.webm`・`manifest.json`・`index.md`

- [ ] **Step 1: `referenceShots.ts`（撮影表）を書く**

```ts
import type { TopicScenario } from "../mock-host/topics/topicControls";

// 参照キャプチャの撮影表。束・台帳の行・mock-host の制御・画面操作を 1 件ずつ持つ
// Reference capture shot table: bundle, ledger row, mock-host controls, and page steps per shot

export type ShotStep =
  | { kind: "control"; path: string }
  | { kind: "click"; testId: string }
  | { kind: "rightClickFirstChild"; testId: string }
  | { kind: "waitTestId"; testId: string }
  | { kind: "revealSkit" }
  | { kind: "wait"; ms: number };

export type ShotSetup = { controls: string[]; steps: ShotStep[] };

// 台帳 docs/uitk/TODO.md の「## 行」の行名。撮影表と台帳を同じ文字列でつなぐ（Step 5 で照合する）
// Row names of the ledger's "## 行" table; the same strings join shots to the ledger (checked in Step 5)
export const LedgerRows = {
  pauseMenu: "pauseMenu（ポーズ・バグ報告・入れ子のポーズ）",
  settings: "settings（画質・言語・ダッシュ方式）",
  toast: "toast",
  commonHud: "commonHud（クロスヘア）",
  modeHud: "modeHud（配置 HUD・削除モードの帯）",
  progress: "progress（採掘ゲージ）",
  trainHud: "trainHud（乗車中の分岐表示）",
  notification: "notification（獲得通知）",
  skit: "skit（会話窓・選択肢・ツールバー・暗転）",
  currentChallengeHud: "challenge: CurrentChallengeHud",
  keyControlHint: "tutorial: KeyControlHintHud",
  worldPin: "tutorial: WorldPinOverlay",
  cursorTooltip: "CursorTooltip",
  backgroundSkit: "オーバーレイ: 背景スキット",
  buildMenu: "buildMenu",
  hotbar: "hotbar",
  modal: "modal（BP 名入力）",
  inventory: "inventory（インベントリ・装備・掴み）",
  recipe: "recipe（レシピ・アイテム一覧・長押しクラフト）",
  blockInventory: "blockInventory（汎用族・特殊族・列車族）",
  research: "research（研究ツリー・詳細ペイン）",
  dragGuide: "tutorial: DragGuide（インベントリ内）",
  challengePanel: "challenge: ChallengePanel",
  eventLanguageGate: "eventLanguageGate（出展モードの言語選択）",
  tutorialOverlay: "tutorial: TutorialOverlay（ハイライト枠）",
  reconnect: "オーバーレイ: 再接続・辞書エラー・エラー境界",
  i18n: "i18n",
} as const;
export type LedgerRow = (typeof LedgerRows)[keyof typeof LedgerRows];
// 束の番号。撮影表・動画表・manifest で同じ型を使う
// Bundle number, shared by the shot table, the video table, and the manifest
export type BundleId = 1 | 2 | 3 | 4 | 5;

export type ReferenceShot = ShotSetup & { id: string; bundle: BundleId; ledgerRow: LedgerRow };

export const ui = (state: string, subState?: string) => subState === undefined ? `/__uistate?state=${state}` : `/__uistate?state=${state}&subState=${subState}`;
export const topic = (scenario: TopicScenario) => `/__topic-control?scenario=${scenario}`;
const block = (type: string) => `/__block?type=${type}`;
const after = (path: string): ShotStep => ({ kind: "control", path });

// ブロック UI は mock-host の全フィクスチャを撮る（httpHandler.ts の BLOCK_FIXTURES と同じ並び）
// Block UIs cover every mock-host fixture, in the order of BLOCK_FIXTURES in httpHandler.ts
const blockTypes = ["chest", "tank", "machine", "gearMachine", "generator", "miner", "filterSplitter", "gearMiner", "pump", "pumpNoVein",
  "gearPump", "generic", "gearGenerator", "electricToGear", "train", "trainTimetable", "trainError", "trainPlatform", "trainFluidPlatform", "electricPole"] as const;

export const referenceShots: ReferenceShot[] = [
  // 束 1 命綱
  { id: "1-pause-top", bundle: 1, ledgerRow: LedgerRows.pauseMenu, controls: [topic("pauseConnected"), ui("PauseMenu")], steps: [] },
  { id: "1-pause-disconnected", bundle: 1, ledgerRow: LedgerRows.pauseMenu, controls: [topic("pauseDisconnected"), ui("PauseMenu")], steps: [] },
  { id: "1-pause-delivery-pending", bundle: 1, ledgerRow: LedgerRows.pauseMenu, controls: [topic("deliveryPending"), ui("PauseMenu")], steps: [] },
  { id: "1-pause-in-skit", bundle: 1, ledgerRow: LedgerRows.pauseMenu, controls: [ui("Story", "PauseMenuScreen")], steps: [] },
  { id: "1-pause-in-train", bundle: 1, ledgerRow: LedgerRows.pauseMenu, controls: [ui("TrainHUDScreen", "PauseMenuScreen")], steps: [] },
  { id: "1-bug-report", bundle: 1, ledgerRow: LedgerRows.pauseMenu, controls: [ui("PauseMenu")], steps: [{ kind: "click", testId: "pause-menu-open-bug-report" }] },
  { id: "1-settings", bundle: 1, ledgerRow: LedgerRows.settings, controls: [ui("PauseMenu")], steps: [{ kind: "click", testId: "pause-menu-open-settings" }] },
  { id: "1-settings-graphics-recovered", bundle: 1, ledgerRow: LedgerRows.settings, controls: [topic("graphicsRecovered"), ui("PauseMenu")], steps: [{ kind: "click", testId: "pause-menu-open-settings" }] },
  { id: "1-toast", bundle: 1, ledgerRow: LedgerRows.toast, controls: [block("chest"), "/__action-error?type=block_inventory.split&error=invalid_slot", ui("SubInventory")],
    steps: [{ kind: "rightClickFirstChild", testId: "chest-grid" }, { kind: "waitTestId", testId: "toast-host" }] },
  // 束 2 常駐表示
  { id: "2-game-hud", bundle: 2, ledgerRow: LedgerRows.commonHud, controls: [topic("crosshairVisible"), topic("challengeActive"), ui("GameScreen")], steps: [] },
  { id: "2-mining", bundle: 2, ledgerRow: LedgerRows.progress, controls: [topic("mining"), ui("GameScreen")], steps: [] },
  { id: "2-progress-labeled", bundle: 2, ledgerRow: LedgerRows.progress, controls: [topic("progressLabeled"), ui("GameScreen")], steps: [] },
  { id: "2-placement", bundle: 2, ledgerRow: LedgerRows.modeHud, controls: [topic("placement"), ui("PlaceBlock")], steps: [] },
  { id: "2-placement-unavailable", bundle: 2, ledgerRow: LedgerRows.modeHud, controls: [topic("placementUnavailable"), ui("PlaceBlock")], steps: [] },
  { id: "2-placement-connect-tool", bundle: 2, ledgerRow: LedgerRows.modeHud, controls: [topic("placementConnectTool"), ui("PlaceBlock")], steps: [] },
  { id: "2-delete-bar", bundle: 2, ledgerRow: LedgerRows.modeHud, controls: [ui("DeleteBar")], steps: [] },
  { id: "2-train-hud", bundle: 2, ledgerRow: LedgerRows.trainHud, controls: ["/__train-riding?riding=1&count=3&selected=1", ui("TrainHUDScreen", "GameScreen")], steps: [] },
  { id: "2-notifications", bundle: 2, ledgerRow: LedgerRows.notification, controls: [ui("GameScreen")],
    steps: [after(topic("notificationItemEarned")), after(topic("notificationAchievement")), after(topic("notificationItemUnlocked")), after(topic("notificationDenied"))] },
  { id: "2-tooltip", bundle: 2, ledgerRow: LedgerRows.cursorTooltip, controls: [topic("tooltip"), ui("GameScreen")], steps: [] },
  { id: "2-tooltip-multiline", bundle: 2, ledgerRow: LedgerRows.cursorTooltip, controls: [topic("tooltipMultiLine"), ui("GameScreen")], steps: [] },
  { id: "2-challenge-hud-multiple-long", bundle: 2, ledgerRow: LedgerRows.currentChallengeHud, controls: [topic("challengeMultipleLong"), ui("GameScreen")], steps: [] },
  { id: "2-skit-background", bundle: 2, ledgerRow: LedgerRows.backgroundSkit, controls: [ui("GameScreen"), "/__skit?stage=background"], steps: [] },
  { id: "2-skit-text", bundle: 2, ledgerRow: LedgerRows.skit, controls: [ui("Story", "GameScreen"), "/__skit?stage=text"], steps: [{ kind: "revealSkit" }] },
  { id: "2-skit-choices", bundle: 2, ledgerRow: LedgerRows.skit, controls: [ui("Story", "GameScreen"), "/__skit?stage=choices"], steps: [] },
  { id: "2-skit-transition", bundle: 2, ledgerRow: LedgerRows.skit, controls: [ui("Story", "GameScreen"), "/__skit?stage=transition"], steps: [] },
  { id: "2-world-pin-onscreen", bundle: 2, ledgerRow: LedgerRows.worldPin, controls: [ui("GameScreen"), "/__worldpin?x=0.6&y=0.4"], steps: [] },
  { id: "2-world-pin-offscreen", bundle: 2, ledgerRow: LedgerRows.worldPin, controls: [ui("GameScreen"), "/__worldpin?on=0&dx=1&dy=0.3"], steps: [] },
  { id: "2-key-control-hint", bundle: 2, ledgerRow: LedgerRows.keyControlHint, controls: [topic("tutorialKeyControl"), ui("GameScreen")], steps: [] },
  // 束 3 建築
  { id: "3-build-menu", bundle: 3, ledgerRow: LedgerRows.buildMenu, controls: [ui("BuildMenu")], steps: [] },
  { id: "3-hotbar-equipment-empty", bundle: 3, ledgerRow: LedgerRows.hotbar, controls: [topic("equipmentEmpty"), ui("GameScreen")], steps: [] },
  { id: "3-modal", bundle: 3, ledgerRow: LedgerRows.modal, controls: [ui("GameScreen"), "/__modal?show=1"], steps: [] },
  // 束 4 持ち物
  { id: "4-player-inventory", bundle: 4, ledgerRow: LedgerRows.inventory, controls: [ui("PlayerInventory")], steps: [] },
  { id: "4-player-inventory-full", bundle: 4, ledgerRow: LedgerRows.inventory, controls: [topic("inventoryEveryRowFilled"), ui("PlayerInventory")], steps: [] },
  { id: "4-item-list-large", bundle: 4, ledgerRow: LedgerRows.recipe, controls: [topic("itemListLarge"), ui("PlayerInventory")], steps: [] },
  ...blockTypes.map((type): ReferenceShot => ({ id: `4-block-${type}`, bundle: 4, ledgerRow: LedgerRows.blockInventory, controls: [block(type), ui("SubInventory")], steps: [] })),
  { id: "4-block-train-timetable-tab", bundle: 4, ledgerRow: LedgerRows.blockInventory, controls: [block("trainTimetable"), ui("SubInventory")], steps: [{ kind: "click", testId: "train-tab-timetable" }] },
  { id: "4-block-machine-recipe-selection", bundle: 4, ledgerRow: LedgerRows.blockInventory, controls: [topic("machineRecipesOverflow"), block("machine"), ui("SubInventory")], steps: [{ kind: "click", testId: "machine-selected-recipe" }] },
  { id: "4-research", bundle: 4, ledgerRow: LedgerRows.research, controls: [topic("researchOwnedItems"), ui("ResearchTree")], steps: [] },
  // 束 5 最後
  { id: "5-challenge-list", bundle: 5, ledgerRow: LedgerRows.challengePanel, controls: [topic("challengeActive"), ui("ChallengeList")], steps: [] },
  { id: "5-event-language-gate", bundle: 5, ledgerRow: LedgerRows.eventLanguageGate, controls: [topic("eventLanguageGateWaiting"), ui("GameScreen")], steps: [{ kind: "waitTestId", testId: "event-language-gate" }] },
  { id: "5-tutorial-outline-label", bundle: 5, ledgerRow: LedgerRows.tutorialOverlay, controls: [topic("tutorialOutlineWithLabel"), ui("GameScreen")], steps: [] },
  { id: "5-tutorial-recipe-item", bundle: 5, ledgerRow: LedgerRows.tutorialOverlay, controls: [topic("tutorialRecipeItem"), ui("PlayerInventory")], steps: [] },
  { id: "5-tutorial-research-node", bundle: 5, ledgerRow: LedgerRows.tutorialOverlay, controls: [topic("tutorialResearchNode"), ui("ResearchTree")], steps: [] },
  { id: "5-reconnect", bundle: 5, ledgerRow: LedgerRows.reconnect, controls: [ui("GameScreen")],
    steps: [after("/__disconnect?holdMs=8000"), { kind: "waitTestId", testId: "reconnect-overlay" }] },
  { id: "5-japanese-inventory", bundle: 5, ledgerRow: LedgerRows.i18n, controls: [topic("japanese"), ui("PlayerInventory")], steps: [] },
];
```

`LedgerRows` の値は台帳の `行` 列の文字列と完全に一致させる（Step 5 で照合する）。

- [ ] **Step 2: `referenceVideos.ts`（動画の撮影表）を書く**

```ts
import { LedgerRows, topic, ui, type BundleId, type LedgerRow, type ShotSetup } from "./referenceShots";

// 動きでしか意図が伝わらない演出の短い動画
// Short videos for effects whose intent only shows in motion
export type ReferenceVideo = ShotSetup & { id: string; bundle: BundleId; ledgerRow: LedgerRow; durationMs: number };

export const referenceVideos: ReferenceVideo[] = [
  { id: "2-notification-enter-aggregate-exit", bundle: 2, ledgerRow: LedgerRows.notification, controls: [ui("GameScreen")], durationMs: 9000,
    steps: [{ kind: "control", path: topic("notificationItemEarned") }, { kind: "wait", ms: 1500 }, { kind: "control", path: topic("notificationItemEarnedAgain") },
      { kind: "wait", ms: 1000 }, { kind: "control", path: topic("notificationAchievement") }] },
  { id: "2-key-control-hint-pulse", bundle: 2, ledgerRow: LedgerRows.keyControlHint, controls: [topic("tutorialKeyControl"), ui("GameScreen")], durationMs: 3000, steps: [] },
  { id: "2-world-pin-pulse", bundle: 2, ledgerRow: LedgerRows.worldPin, controls: [ui("GameScreen"), "/__worldpin?x=0.6&y=0.4"], durationMs: 3000, steps: [] },
  { id: "4-drag-guide", bundle: 4, ledgerRow: LedgerRows.dragGuide, controls: [topic("tutorialDragGuide"), ui("PlayerInventory")], durationMs: 4000, steps: [] },
  { id: "5-tutorial-pulse", bundle: 5, ledgerRow: LedgerRows.tutorialOverlay, controls: [topic("tutorialOutlineWithLabel"), ui("GameScreen")], durationMs: 3000, steps: [] },
];
```


- [ ] **Step 3: `shotRunner.ts`（mock-host の起動・停止と画面の準備）を書く**

```ts
import { spawn, type ChildProcess } from "node:child_process";
import type { Page } from "@playwright/test";
import { requestMockControl } from "../support/mockControl";
import type { ShotSetup, ShotStep } from "./referenceShots";

export const referenceViewport = { width: 1280, height: 720 } as const;
// スキットの blocking fixture の本文（capture-skit.ts と同じ定数）
// Body text of the blocking skit fixture (same constant as capture-skit.ts)
const BLOCKING_BODY = "Blocking message";

// 撮影ごとに mock-host を別プロセスで起こし、前の撮影の状態を持ち越さない（外部プロセス境界）
// Start a fresh mock-host process per shot so no state leaks between shots (external process boundary)
export async function startMockHost(webuiRoot: string, port: number): Promise<ChildProcess> {
  const child = spawn(process.execPath, ["--import", "tsx", "e2e/mock-host/server.ts"], {
    cwd: webuiRoot, env: { ...process.env, MOCK_PORT: String(port), MOCK_DEMO: "1" }, stdio: ["ignore", "pipe", "inherit"],
  });
  // 待受の合図か、先に終了か、15 秒の期限のどれかで決着させる
  // Settle on the listening line, an early exit, or a 15 second deadline
  await new Promise<void>((resolve, reject) => {
    const deadline = setTimeout(() => {
      child.kill();
      reject(new Error(`mock-host did not listen within 15s on ${port}`));
    }, 15_000);
    child.once("exit", (code) => {
      clearTimeout(deadline);
      reject(new Error(`mock-host exited before listening (code ${code})`));
    });
    child.stdout?.on("data", (chunk: Buffer) => {
      if (!chunk.toString().includes(`mock-host on ${port}`)) return;
      clearTimeout(deadline);
      resolve();
    });
  });
  return child;
}

export async function stopMockHost(child: ChildProcess): Promise<void> {
  if (child.exitCode !== null) return;
  const exited = new Promise<void>((resolve) => child.once("exit", () => resolve()));
  child.kill();
  await exited;
}

// 制御を当ててから開き、手順を流し、カーソルを隅へ退けて落ち着くのを待つ
// Apply controls, open the page, run the steps, park the cursor, and let the frame settle
export async function openShot(page: Page, baseUrl: string, setup: ShotSetup): Promise<void> {
  for (const path of setup.controls) await requestMockControl(page, baseUrl, path);
  await page.goto(baseUrl);
  await page.evaluate("document.fonts.ready.then(() => undefined)");
  await page.getByTestId("app-stage").waitFor({ state: "attached" });
  for (const step of setup.steps) await runStep(page, baseUrl, step);
  await page.mouse.move(2, 2);
  await page.waitForTimeout(600);
}

async function runStep(page: Page, baseUrl: string, step: ShotStep): Promise<void> {
  switch (step.kind) {
    case "control": return requestMockControl(page, baseUrl, step.path);
    case "click": return page.getByTestId(step.testId).first().click();
    case "rightClickFirstChild": return page.getByTestId(step.testId).locator("> div").first().click({ button: "right" });
    case "waitTestId": return page.getByTestId(step.testId).first().waitFor();
    case "revealSkit": return revealSkitBody(page);
    case "wait": return page.waitForTimeout(step.ms);
  }
}

// タイプライターを実ユーザーと同じくクリックで全文表示へ進める（capture-skit.ts と同じ手順）
// Reveal the full typewriter line with a click like a real user (same procedure as capture-skit.ts)
async function revealSkitBody(page: Page): Promise<void> {
  const skitWindow = page.getByTestId("blocking-skit");
  await skitWindow.waitFor();
  for (let attempt = 0; attempt < 20; attempt += 1) {
    if ((await skitWindow.textContent())?.includes(BLOCKING_BODY)) return;
    await skitWindow.click();
    await page.waitForTimeout(100);
  }
  throw new Error(`skit body never completed: ${BLOCKING_BODY}`);
}
```

`5-event-language-gate` は `app-stage` の待機で止まらないか確かめる（ゲートは stage の外に重なるだけで stage は出る想定。止まったら `openShot` の `app-stage` 待機をその撮影だけ外すのではなく、ゲートの撮影の `steps` 先頭の `waitTestId` で代える形に直す）。

- [ ] **Step 4: `captureIndex.ts` と入口 `capture.ts` を書く**

`captureIndex.ts`:

```ts
import { writeFile } from "node:fs/promises";
import { join } from "node:path";
import type { BundleId, LedgerRow } from "./referenceShots";

export type CaptureEntry = { id: string; bundle: BundleId; ledgerRow: LedgerRow; file: string; kind: "image" | "video" };

// manifest と束ごとの一覧（GitHub でそのまま見られる Markdown）を書く
// Write the manifest and a per-bundle index Markdown that renders on GitHub
export async function writeCaptureIndex(outputDirectory: string, entries: CaptureEntry[], sourceCommit: string): Promise<void> {
  await writeFile(join(outputDirectory, "manifest.json"), JSON.stringify({ sourceCommit, viewport: "1280x720", mockHostDemo: true, entries }, null, 2));
  const lines = [`# Web UI 参照キャプチャ（moorestech ${sourceCommit}）`, "",
    "UI Toolkit 移行で Web 版の「意図した見た目」を辿るための撮影。mock-host（DEMO）で撮った。台帳は moorestech の `docs/uitk/TODO.md`。", ""];
  for (const bundle of [1, 2, 3, 4, 5] as const) {
    lines.push(`## 束 ${bundle}`, "");
    for (const entry of entries.filter((candidate) => candidate.bundle === bundle)) {
      lines.push(`### ${entry.id}（${entry.ledgerRow}）`, "", entry.kind === "image" ? `![${entry.id}](${entry.file})` : `動画: [${entry.file}](${entry.file})`, "");
    }
  }
  await writeFile(join(outputDirectory, "index.md"), lines.join("\n"));
}
```

`capture.ts`:

```ts
import { execFileSync } from "node:child_process";
import { mkdir, readdir, rm } from "node:fs/promises";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "@playwright/test";
import { writeCaptureIndex, type CaptureEntry } from "./captureIndex";
import { referenceShots } from "./referenceShots";
import { referenceVideos } from "./referenceVideos";
import { openShot, referenceViewport, startMockHost, stopMockHost } from "./shotRunner";

const webuiRoot = fileURLToPath(new URL("../..", import.meta.url));
const port = Number(process.env.UITK_REFERENCE_PORT ?? 5390);
const baseUrl = `http://127.0.0.1:${port}`;

async function main(): Promise<void> {
  // 出力先は必須。既存の撮影と混ざらないよう空のディレクトリだけを受ける
  // The output directory is required and must be empty so runs never mix
  const outputDirectory = process.env.UITK_REFERENCE_OUT;
  if (outputDirectory === undefined) throw new Error("UITK_REFERENCE_OUT is required");
  await mkdir(outputDirectory, { recursive: true });
  if ((await readdir(outputDirectory)).length > 0) throw new Error(`output directory is not empty: ${outputDirectory}`);
  const sourceCommit = execFileSync("git", ["rev-parse", "--short=10", "HEAD"], { cwd: webuiRoot, encoding: "utf8" }).trim();
  const entries: CaptureEntry[] = [];
  const browser = await chromium.launch();
  // ブラウザは成功・失敗のどちらでも閉じる
  // Close the browser on both the success and the failure path
  try {

  // 静止画: 撮影ごとに mock-host と context を作り直す
  // Stills: recreate the mock-host and the context for every shot
  for (const shot of referenceShots) {
    const host = await startMockHost(webuiRoot, port);
    // 失敗しても子プロセスと context を残さない（前例 challenge-hud/capture.ts の finally と同じ）
    // Never leak the child process or the context on failure (same as the finally in challenge-hud/capture.ts)
    try {
      const context = await browser.newContext({ viewport: referenceViewport });
      const page = await context.newPage();
      await openShot(page, baseUrl, shot);
      await page.screenshot({ path: join(outputDirectory, `${shot.id}.png`) });
      await context.close();
    } finally {
      await stopMockHost(host);
    }
    entries.push({ id: shot.id, bundle: shot.bundle, ledgerRow: shot.ledgerRow, file: `${shot.id}.png`, kind: "image" });
  }

  // 動画: recordVideo の context で尺だけ待ち、閉じてから保存する
  // Videos: wait the duration inside a recordVideo context, then save after closing it
  for (const video of referenceVideos) {
    const host = await startMockHost(webuiRoot, port);
    try {
    const context = await browser.newContext({ viewport: referenceViewport, recordVideo: { dir: join(outputDirectory, ".video-tmp"), size: referenceViewport } });
    const page = await context.newPage();
    await openShot(page, baseUrl, video);
    await page.waitForTimeout(video.durationMs);
    const recording = page.video();
    await context.close();
    if (recording === null) throw new Error(`video was not recorded: ${video.id}`);
    await recording.saveAs(join(outputDirectory, `${video.id}.webm`));
    await recording.delete();
    } finally {
      await stopMockHost(host);
    }
    entries.push({ id: video.id, bundle: video.bundle, ledgerRow: video.ledgerRow, file: `${video.id}.webm`, kind: "video" });
  }
  } finally {
    await browser.close();
  }
  await rm(join(outputDirectory, ".video-tmp"), { recursive: true, force: true });
  await writeCaptureIndex(outputDirectory, entries, sourceCommit);
}

// 外部境界（ブラウザ・子プロセス）の失敗を終了コードへ変える
// Convert external-boundary failures (browser, child processes) into an exit code
void main().catch((error: unknown) => {
  console.error(error);
  process.exitCode = 1;
});
```

注: 上の `try`/`finally`（`catch` は無い）は子プロセス・ブラウザという外部境界の後片付けで、失敗は握り潰さずそのまま `main().catch` まで上げて終了コード 1 にする。字下げは写すときに整える。失敗した撮影があればその場で全体を止める（黙って次へ進まない）。止まったら撮影表のその 1 件を直し、出力先を空にして最初から撮り直す。

`knip.json` の `entry` に `"e2e/uitk-reference/capture.ts"` を足す（`"e2e/challenge-hud/capture.ts"` の次の行）。

- [ ] **Step 5: 撮影表と台帳の行名の照合を型検査とスクリプトで確かめる**

Run:
```bash
cd moorestech_web/webui
../node/mac-arm64/pnpm exec tsc -p e2e/tsconfig.json --noEmit
../node/mac-arm64/bin/node --import tsx -e 'import("./e2e/uitk-reference/referenceShots.ts").then(async ({ referenceShots }) => { const { referenceVideos } = await import("./e2e/uitk-reference/referenceVideos.ts"); const fs = await import("node:fs"); const ledger = fs.readFileSync("../../docs/uitk/TODO.md", "utf8"); const names = new Set(ledger.split("\n").filter((l) => l.startsWith("| ")).map((l) => l.split("|")[1].trim())); const missing = [...referenceShots, ...referenceVideos].filter((s) => !names.has(s.ledgerRow)).map((s) => s.id + " → " + s.ledgerRow); console.log(missing.length === 0 ? "ledgerRow OK" : missing.join("\n")); process.exitCode = missing.length === 0 ? 0 : 1; })'
```
Expected: tsc エラー 0、`ledgerRow OK`

- [ ] **Step 6: moorestech_logs の作業場所を作り、README を読む**

```bash
LOGS=$HOME/hermes-agent/data/repos/moorestech_logs
CAP_WT=$HOME/hermes-agent/data/worktrees/moorestech_logs/uitk-reference-captures
cat "$LOGS/README.md"
git -C "$LOGS" fetch origin main
git -C "$LOGS" worktree add "$CAP_WT" -b uitk/reference-captures origin/main
```
本体 clone（`$LOGS`）は logs-sync hook が自動コミットするので、そこでは書かない（rebase 中断の隙間でファイルが消えた事故がある。README の「落とし穴」）。

- [ ] **Step 7: 撮影する**

```bash
CAP_WT=$HOME/hermes-agent/data/worktrees/moorestech_logs/uitk-reference-captures
cd "$(git rev-parse --show-toplevel)/moorestech_web/webui"
../node/mac-arm64/pnpm build
SHA10=$(git rev-parse --short=10 HEAD)
UITK_REFERENCE_OUT="$CAP_WT/uitk/reference-captures/web-$SHA10" ../node/mac-arm64/bin/node --import tsx e2e/uitk-reference/capture.ts
ls "$CAP_WT/uitk/reference-captures/web-$SHA10" | wc -l
du -sh "$CAP_WT/uitk/reference-captures/web-$SHA10"
find "$CAP_WT/uitk/reference-captures/web-$SHA10" -size +20M
```
Expected: 終了コード 0。PNG が撮影表の件数（Step 1 の配列長）・webm が 5 本・`manifest.json`・`index.md` がある。20MB を超えるファイルが無い（あれば viewport を変えず PNG を `pngquant` 等で縮めず、原因の撮影を調べる）

- [ ] **Step 8: 撮った絵を 1 枚ずつ目で確かめる**

全 PNG を Read ツールで開き、各撮影が `id` の意図した画面（例: `1-settings` は設定ページ、`4-block-pump` はポンプの UI、`2-notifications` は通知 4 件）を写していることを確かめる。動画は `ffprobe` で尺（`durationMs` ±1 秒）を確かめ、`ffmpeg -ss 1 -i <webm> -frames:v 1 <tmp>.png` で 1 コマ取り出して開く。意図と違う絵（空の画面・別の画面・接続中の表示）があれば、撮影表を直して出力先を消してから Step 7 を撮り直す。最後に `$TMPDIR` の確認用 PNG を消す。

- [ ] **Step 9: moorestech_logs にコミットして push する**

`$CAP_WT/README.md` のレイアウトのコードブロックに 1 行足す:

```
uitk/reference-captures/web-<sha10>/    # UI Toolkit 移行の参照キャプチャ（Web 版の全画面の PNG と演出の webm、manifest.json・index.md）。本体repoの docs/uitk/TODO.md から辿る
```

```bash
CAP_WT=$HOME/hermes-agent/data/worktrees/moorestech_logs/uitk-reference-captures
SHA10=$(git rev-parse --short=10 HEAD)
git -C "$CAP_WT" add README.md uitk/reference-captures
git -C "$CAP_WT" commit -m "uitk: Web UI の参照キャプチャ（moorestech $SHA10）"
git -C "$CAP_WT" push origin HEAD:main
```
push が non-fast-forward で拒否されたら `git -C "$CAP_WT" pull --rebase origin main` してから push し直す。`git -C "$CAP_WT" log origin/main -1 --stat` で入ったことを確かめる。

- [ ] **Step 10: 台帳に参照キャプチャ・未撮影・各行の撮影 id を書く**

`docs/uitk/TODO.md` の「## 参照キャプチャ」を次に置き換える（`<sha10>` は Step 7 の値）:

```markdown
## 参照キャプチャ

Web 版の全画面と主要な状態（移行後も「意図した見た目」を辿るため）。mock-host（DEMO の高密度データとプレースホルダアイコン）で撮った。画素の一致は目標にしない（計画 §4.6）。

- 撮影元: moorestech `<sha10>`。撮り方: `moorestech_web/webui/e2e/uitk-reference/capture.ts`（撮影表は同じディレクトリの `referenceShots.ts`・`referenceVideos.ts`）
- 置き場: `moorestech_logs` の `uitk/reference-captures/web-<sha10>/`（一覧は `index.md`、機械向けは `manifest.json`）
- URL: https://github.com/moorestech/moorestech_logs/tree/main/uitk/reference-captures/web-<sha10>
- 各行の `参照キャプチャ` 列は撮影 id（PNG は `<id>.png`、動画は `<id>.webm`）。束を切り替える前に撮り直したら、ここの撮影元と置き場を新しい方へ書き換える
```

「## 未撮影」を次に置き換える:

```markdown
## 未撮影

mock-host では撮れず、PlayMode（unityプレイ録画テスト）でしか撮れない状態。各束の暗転を始めるときに、その束の分を撮って上の置き場に足す。

| 束 | 行 | 状態 | 撮れない理由 |
|---|---|---|---|
| 1〜5 | 全行 | 実ゲームの 3D 画面に CEF が合成された見え方 | mock-host は Web 単体で、合成と透過が無い |
| 1〜5 | 全行 | 実マスタのアイコン・名前・研究ノード | DEMO はプレースホルダのアイコンと合成データ |
| 2 | オーバーレイ: Ctrl+U の全体非表示 | Ctrl+U 中に 3D だけが映ること | Web 側は全部 unmount するだけで絵が無い |
| 2 | オーバーレイ: カットシーン中の退避 | カットシーン中に UI が消えること | 同上 |
| 2 | tutorial: WorldPinOverlay | 動く対象への追従・画面外矢印の回転 | mock は固定座標 |
| 2 | skit（会話窓・選択肢・ツールバー・暗転） | 実キャラクターの背景とタイプライターの動き | mock は固定文と固定背景 |
| 2 | trainHud（乗車中の分岐表示） | 実走行中の分岐表示 | 乗車は PlayMode でしか起きない |
| 3 | buildMenu | ビルドメニューからホットバーへのドラッグの最中 | ドラッグ中の合成と配置の実挙動 |
| 4 | inventory（インベントリ・装備・掴み） | 掴み・分割ドラッグ・右ドラッグ配りの最中、ホイールでの装備選択 | ゲーム側の入力と実インベントリ |
| 4 | recipe（レシピ・アイテム一覧・長押しクラフト） | 長押しクラフトの進捗の動き | 実クラフトの進行 |
| 4 | research（研究ツリー・詳細ペイン） | パン・ズームの慣性の動き | 動画は mock でも撮れるが、実ノード 7 件のアンカーと合わせて PlayMode で撮る |
| 4 | blockInventory 特殊族: ロケット（Tier9 #1551） | 全状態 | 撮影時点の master に画面が無い（#1551 のマージ後に撮る） |
| 4 | blockInventory 特殊族: 宇宙エレベーター（Tier9 #1552） | 全状態 | 同上（#1552） |
| 4 | blockInventory（汎用族・特殊族・列車族） | 機械 UI を開いたまま中身が更新される動き | 実機械の稼働 |
| 1〜5 | i18n | IME の変換中の入力欄（駅名・BP 名・検索・バグ報告） | 人手の確認（S9） |
| 1〜5 | オーバーレイ: 画面のマウント点と背景ディム | CEF が固まって最後のフレームが残る状態 | CEF の実プロセスが要る |
```

「## 行」の各行の `参照キャプチャ` 列に、その行を `ledgerRow` に持つ撮影 id をバッククォート無しで空白区切りに書く（動画は `動画 <id>`）。撮影が無い行は `未撮影` か、「## 未撮影」に載せた行は `未撮影（下表）`。`agent 既定`・`実測の結論` の列は Task 3 のまま。

- [ ] **Step 11: G1 と台帳テストを回し直してコミットする**

Run:
```bash
cd "$(git rev-parse --show-toplevel)"
python3 -m unittest discover -s .github/scripts/tests -p 'test_uitk_web_follow_*.py'
git add moorestech_web/webui/e2e/uitk-reference moorestech_web/webui/knip.json docs/uitk/TODO.md
git commit -m "docs(uitk): Web 版の参照キャプチャを撮って moorestech_logs に置き、台帳からリンクする"
python3 .github/scripts/check-uitk-web-follow.py --base origin/master --head HEAD
```
Expected: unittest `OK`、G1 は `違反なし`・終了コード 0

---

### Task 6: 全ブランチレビュー

- [ ] **Step 1: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

Codex ホストでは moores-code-review の `references/codex-host.md` の runner（`scripts/codex_workflow_runner/run.mjs`）で同じ台本を回す。確定した機械的修正は当て、設計判断は PR 本文の「ユーザーに見てほしい判断」へ書く。指摘の反映がソースの判定経路（G1 の規則・台帳パーサ）に触れたら、Task 2・3 の unittest と実 repo の G1 実行を回し直してから完了にする。

### Task 7: PR を作り、セッションを閉じられる状態にする

- [ ] **Step 1: セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える**

PR 本文に必ず入れる:
- 冒頭に「PR #1555（裁定 4 のファイルと計画の追従ゲート版）に依存する。#1555 を先にマージする」の 1 行（台帳が #1555 の裁定ファイルと計画 §4.7 の追従ゲート版を出典にするため）
- 「ユーザーに見てほしい判断」節に、上の agent 既定に加えて次の帰結 2 点: (a) G1(0) の網羅のため台帳の行は 20 機能＋オーバーレイだけでなく共有部品・横断層まで約 48 行になった (b) 束 1〜5 を全部持つ共有行（`shared/ui`・`tutorialAnchor`・`i18n`・`uiState`・画面のマウント点）は、同じコミットを束ごとに最大 5 回記帳することになる（各束の切替で Web 側の変更を見直すため。重いと判断されたら共有行の束を減らす）
- 「本PR外のリファクタ提案」節に、判断記録の第3バケツ 4 件
- 「ユーザーに見てほしい判断」節: このplanの `## 判断記録（ADR）` のうち「agent 既定（ユーザー未裁定）」の項目を全部
- 参照キャプチャの URL（https://github.com/moorestech/moorestech_logs/tree/main/uitk/reference-captures/web-<sha10>）
- G1 の赤のテスト名（`test_switch_with_unrecorded_web_change_is_red`・`test_change_to_switched_bundle_web_path_is_red`・`test_uncovered_new_screen_is_red`）と、PR の CI で G1 のジョブが緑であること
- `.cs` の変更が無いので Unity のコンパイルは不要であること

PR を作ったら `bd note moorestech-albof.4 "PR #<番号> 作成。台帳・G1・参照キャプチャ web-<sha10>"` を書く。全レーン共通の前提どおり、PR のマージはしない（moorestech は PR 作成まで）。

---

## 判断記録（ADR）

設計の前提: 裁定 `.decisions/2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する.md`、計画 `docs/uitk-migration/plan.md`（v3）§2.1 0-8・0-9・§4.7 G1、ADR は `docs/adr/` に本件の新規は無い。

**ユーザー裁定（2026-10-26。PR #1555、master 未マージのため原文を写す）**
- 原文: 「とりあえずUI T Kが安定稼働するまでは、ひとまずウェブUIのほうも引き続き変更行っていくつもりではある 現状の機能全部UKに置き換えて欲しい」
- 決定（PR #1555 の `.decisions/2026-10-26-UITKが安定するまでWeb-UIの変更を続け現行機能をすべてUITKへ移す.md`）: UITK が安定稼働するまで Web UI への変更（不具合修正・新機能）は続ける。移行の範囲は現行 Web UI の全機能。G1 は凍結ゲートから追従ゲートに変える（束ごとに暗転の起点コミットを台帳に書き、切替 PR で起点以降の Web 差分が全部「写した」か「捨てた（理由）」になっているかを機械で確かめる。切替済みの束の Web パスへの変更は落とす）。追従ゲートの形は agent 案

**agent 既定（ユーザー未裁定）— 計画 §0 の残りの裁定**（ユーザー指示「動く前提で並列で開始して」により、計画の推奨を採る。`.decisions/` には書かない）
- 裁定 1=A（束単位で暗転のまま作り、所有表で切り替える）／裁定 2=A（第一弾は命綱）／裁定 3=(a) `HttpListener`（配布ビルドで動かなければ (b) 遠隔実行専用の最小 Kestrel）／確認=UI の演出は実時間で動かす。様式化は未裁定のまま、台帳の該当行に推奨 (a) を書くだけにした

**agent 既定（ユーザー未裁定）— このplanで決めた G1 と台帳の形**
1. 台帳は Markdown の固定見出し（`## 束`・`## 行`・`## 追従記録`）直下の表を機械で読む。別ファイルの JSON/YAML にしない。理由: 台帳は「1 枚」で人も読む。表の見出し行を固定し、読めなければ G1 が赤になるので、壊れたまま黙ることはない
2. ファイルの持ち主は最長一致の Web パスの行（同じ長さなら全部）。1 行が複数の束を持てる（共有部品・`App.tsx`）。理由: `challenge`・`tutorial` は束 2 と 5 に割れる（計画 §2.3 の HUD は束 2、一覧と枠は束 5）。共有部品を 1 束に寄せると、その束の切替後に他の束がまだ使うファイルを G1(2) が落としてしまう
3. G1(2) は「持ち主の束が全部切替済み」のパスだけを落とし、削除は通す。base と head の両方で切替済みのときだけ落とす。理由: 削除 PR（計画 R3 の「切替 PR → 1 周期 → 削除 PR」）と切替を戻す PR を通すため
4. G1(1) は今回の PR で切替済みになった束だけを赤にし、暗転中の束は未記帳の件数を出すだけにする（計画 §6 リスク 7 の指標）。記録はコミットの前方一致（7 桁以上、台帳の書き方は 10 桁）。マージコミットは数えない。理由: squash しない運用（AGENTS.md「マージは通常のマージコミット」）なので、PR のコミットの SHA が master に残る
5. G1(0) 網羅: `moorestech_web/webui/src/` 以下の追跡ファイルが台帳のどの行にも入っていなければ赤。依頼の (1)(2) に無い追加の規則。理由: 裁定「現状の機能全部」と 0-9「新しい Web 画面が入ったら台帳のその束の行に足す」を機械で守るため。既存の機能ディレクトリの下に足すファイルは自動で入るので、止まるのは `src/` 直下や `features/` に新しいディレクトリを足すときだけ
6. 束の Web パスには、`src/` だけでなくその束の e2e テスト・mock-host の fixture・撮影スクリプトも入れる。理由: 暗転中に Web のテストが増えたら、UITK 側（NUnit）にも写す対象になる（計画 §4.1 のテスト名の写し）
7. 共有の割当: `shared/ui`・`tutorialAnchor`・`i18n`・`uiState`・`App.tsx` のマウント点は束 1〜5 全部、`bridge`・アプリ殻・e2e の基盤は束 5。理由: 前者は各束の切替で Web 側の変更を見直す必要がある（見た目・アンカー・文言・ルーティング）。`bridge` は Web ホストの撤去と一緒に消え、状態は C# 側（albof.3 が DI へ移す）から読むので、束ごとの写しは要らない
8. 台帳の `Web パス` は `moorestech_web/webui/` 相対で書く。理由: 50 行近くに同じ接頭辞が並ぶと読めない
9. CI は PR のマージ参照（`actions/checkout` の既定）を head にして回す。PR の head そのものを使うと、台帳が入る前に分岐した PR（例: Tier9 #1551・#1552）の head に `docs/uitk/TODO.md` が無く、無関係に赤になるため。マージ参照は `--no-merges` で数えないので、記帳の対象は PR と master の実コミットだけになる
10. G1 は Python 標準ライブラリだけで `.github/scripts/` に置き、新しい workflow で走らせる。前例: `.github/scripts/check-test-external-dependencies.py` とその `tests/`。`run_test.yml` に足さない理由: (a) 他のジョブを変えない指示、(b) `run_test.yml` は `paths-ignore: '**/*.md'` で、台帳だけの PR で G1 が走らない
11. 参照キャプチャは mock-host を撮影ごとに別プロセスで起こす。前例 `e2e/challenge-hud/capture.ts` は 1 プロセスで撮るが、mock-host の状態はモジュールの大域変数で撮影をまたいで残る。撮影 60 件近くを互いに独立させるため、前例から外す
12. 置き場は `moorestech_logs`（`share-put` ではない）。理由: 移行の終わり（段階 5）まで数か月残す必要があり、git で残り、チームが GitHub から辿れる。書き込みは本体 clone ではなく別 worktree から行う（logs-sync hook との競合を避ける。README の落とし穴）
13. mock-host のチュートリアル scenario を `tutorialControls.ts` へ分ける。理由: `tutorialDragGuide` を足すと `topicControls.ts` が 200 行を超える。前例: `pauseMenuControls.ts` の分け方
14. 写す・捨てるの記録の中身（写し先の書き方）は自由文にし、`捨てた` だけ理由を必須にする。理由: 裁定の文言が「写した」か「捨てた（理由）」

**設計検査 Phase 2.6 の裁定**（AskUserQuestion の代わりに agent が推奨で決めた。agent 既定（ユーザー未裁定））
- 強〈5-A1〉`Bundle` の状態と `dark_start: str|None` の 2 表現 → 取り込む（一部）: cli は `state` で分岐し、起点は Web 以外にだけあることをパーサが保証する。Python で判別 union の型を作るのは見送る（不変条件は境界のパーサ 1 か所で検査し、誤りは赤になる）
- 強〈5-I2〉撮影表・動画表・manifest の束番号と行名の型がばらばら → 取り込む: `BundleId` と `LedgerRow` を `referenceShots.ts` に 1 本置き、3 か所で使う
- 強〈検査7-1〉台帳の所有者列と計画 §3.3 R2（所有表は (画面状態, 面)→所有者 のコード表）の食い違い → 両立させる: 台帳の所有者列は束の状態から決まる写しで、正はコード表。コード表は段階 1 で作るので、そのときに「コード表と台帳の所有者列が一致する」テストを足す（段階 1 の plan への申し送り。bd `moorestech-albof.4` の note にも残す）。今はコード表が無いので、計画 0-8 の「所有表の初版（全部 Web）もここで置く」に従う
- 弱〈5-C3〉git の「該当なし」と「失敗」を同じ False に畳む → `is_ancestor` を取り込む（終了コード 0/1 以外は例外）。`read_file_at`・`resolve_commit` はこのまま（rev は先に解決済みで、`cat-file -e` の失敗はファイルが無いことだけを表す）
- 弱〈5-F5〉撮影の後片付けが成功経路だけ → 取り込む（`try`/`finally`。前例 `challenge-hud/capture.ts`）
- 弱〈5-F6〉mock-host の起動待ちに期限が無い → 取り込む（15 秒）。撮影中の `waitForTimeout` は尺そのものなのでこのまま
- 弱〈5-C6〉台帳の 3 状態（無し・読めない・読めた）を `None` と誤りの列で分ける／〈5-C4〉スキットの本文の未着／〈検査6-4〉「全束が切替済み」の判定が 2 か所 → このまま（どれも 1 関数の中で閉じていて、取り違えると赤になる側に倒れる）

**本PR外のリファクタ提案（第3バケツ。PR 本文の「本PR外のリファクタ提案」節に写す。この PR では直さない）**
- `e2e/uitk-reference/shotRunner.ts` の `revealSkitBody` ⇔ `e2e/capture-skit.ts` の `showBlockingBody`（同じ手順）
- `e2e/uitk-reference/referenceShots.ts` の `ui()`・`topic()`・`block()` ⇔ `e2e/support/mockControl.ts` の `setUiState`・`setTopicScenario`・`setBlock`（同じ URL の組み立て。前者は撮影表のデータとして文字列が要る）
- `e2e/uitk-reference/shotRunner.ts` の `startMockHost`・`openShot` ⇔ `e2e/challenge-hud/serverLifecycle.ts`・`pageCapture.ts`（判断記録 11 で別プロセスへ逸脱）
- `.github/scripts/uitk_web_follow/git_history.py` の `tracked_files` ⇔ `.github/scripts/check-test-external-dependencies.py` の `tracked_test_sources`

**user-simulator review（Fable 判事）の反映**: 適用推奨 0・Warning 4・要裁定 0。Warning は全部取り込んだ（PR #1555 への依存を PR 本文に書く・`e2e/` の新しいファイルを行に足す運用を台帳に書く・共有行の最大 5 回の記帳と約 48 行への増加を PR 本文の「ユーザーに見てほしい判断」に書く）。出所: agent 判断（判事の予測による）

**unityプレイ録画テストを入れない**: この plan はランタイム挙動（ゲームプレイ・入力・カメラ・UI・エンティティ表示）に触れない。変えるのは CI・docs・Web の e2e の撮影スクリプトと mock-host（テスト基盤）だけで、`src/` と C# は 1 行も変えない。

**Task 分割**: G1 の純ロジック（Task 1）と git を読む規則（Task 2）を分けた。台帳の形（パーサ）を承認しつつ規則を差し戻す、という分け方がレビューで意味を持つため。台帳・CI（Task 3）は G1 が実 repo で緑になることの受入なので 1 つにまとめた。


### agent 既定（ユーザー未裁定）— 実装時の補正（Task 1）

- R9 の fail-closed を補強し、束番号と対象見出しの重複、表の区切り行の欠落・不正、行末の欠落を拒否する。サンプルでは束1を二重に書いても辞書で上書きされ、`errors=()` で受理された。追従記録の区切りを消すと先頭の記録が区切りとして読み捨てられるため、列数だけでは不足する。
- Web パスは絶対パス・親参照・現在位置・空セグメント・逆スラッシュを拒否する。サンプルは `../src/` を受理し、実在する repo 相対パスとの一致が失われる。正規の Web UI 相対パスだけを境界で受理する。
- 数字には ASCII を要求する。Python の `isdigit()` は `²` に真を返すが `int()` は失敗するため、台帳の誤りを戻り値で運ぶ契約を保つ。

### agent 既定（ユーザー未裁定）— Task 1 レビュー補正

- 表の読取りは次のレベル2見出しまで確認する。先頭パイプ欠落は理由付きで拒否し、前後の空白は除いて解析する。広い `src/` のWeb所有行の後で切替済みHUDの行の先頭パイプが消えると、従来は行を読み捨ててHUDをWeb所有に戻していたため。先頭パイプ欠落は台帳エラー、インデント付きHUD行は所有を維持する回帰テストを追加した。

**Task 2 サンプル補正（agent 既定・ユーザー未裁定）**: 暗転中の件数報告テストは、PR の台帳が master と同文だと `git commit` が「差分なし」で失敗しゲートまで到達しない。PR 台帳の先頭に HTML コメントを加え、状態を変えずに実差分を作る。緑のテストには stderr を渡し、共有パスの赤は G1-1 由来であることも検証する。
