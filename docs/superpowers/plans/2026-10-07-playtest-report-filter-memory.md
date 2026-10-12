# プレイテスト報告一覧の絞り込み記憶 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** プレイテストダッシュボード（`https://review.moores.tech/playtest/`）の報告一覧の絞り込みを記憶し、詳細画面の「← 報告一覧」・前後リンク・ブラウザの戻るで同じ絞り込みを保つ。

**Architecture:** 絞り込み条件は今どおり URL ハッシュのクエリに持つ。それを詳細画面の URL（`#/report/<steamId>/<boxId>?<条件>`）にも引き継ぎ、詳細の戻りリンクと前後リンクを条件から作る。最後に一覧で使った条件は localStorage に記憶し、上部「報告」タブの遷移先に使う。絞り込みの純粋ロジック（条件の抽出・一致判定・前後探索・記憶）は DOM を持たない新モジュール `report-filter.js` に集め、node のテストで検証する。

**Tech Stack:** 素の ES modules（ビルドなし）、Python 標準ライブラリの HTTP サーバー（変更なし）、テストは `node --test`（node v26、追加依存なし）。

## Requirements

1. 一覧の絞り込み条件（`triage`・`kind`・`read`・`tester`・`build`・`q`）は、一覧から開いた詳細の URL に引き継がれる。受入: `#/reports?kind=feedback` の行を開くと URL が `#/report/<sid>/<id>?kind=feedback` になる
2. 詳細の「← 報告一覧」は詳細 URL の条件付き一覧へ戻る。受入: 上の詳細で押すと `#/reports?kind=feedback` が開く
3. 詳細の「‹ 新しい報告」「古い報告 ›」は条件に一致する報告だけを辿る。条件はページ遷移後も引き継がれる。受入: `kind=feedback` で開いた詳細の前後リンクが感想だけを指し、辿った先の URL にも `?kind=feedback` が付く
4. 前後の対象は取得済みの最新データに条件を当てて決める（開いた時点で固定しない）
5. 見ている報告が条件から外れても（「未読」で開いて既読にした等）その報告を表示し続け、全件の並び（新しい順）での位置から、条件に一致する一番近い新しい／古い報告を前後リンクにする。受入: 条件外の報告でも前後リンクが出る
6. 最後に一覧で使った条件をブラウザ（localStorage）に記憶し、上部「報告」タブはその条件付き一覧へ飛ぶ。ブラウザを閉じて再訪しても同じ。受入: `kind=feedback` の一覧を開いた後、概要へ移ってから「報告」タブを押すと `#/reports?kind=feedback` が開く
7. 検索語の入力（`replaceState` で URL を書き換える経路）も記憶に反映する
8. 「条件をクリア」で条件なし一覧になり、記憶も空になる（条件なしの `#/reports` を開くこと自体が「空を記憶」になる）
9. 概要・テスター画面の条件付きリンク（未投入・感想・テスター別）はそのリンクの条件だけで一覧を開き、記憶済みの条件とは合成しない。開いた一覧の条件が新しい記憶になる
10. 概要の「未投入のバグ報告」行から開いた詳細は `triage=candidate`、「最新の感想」から開いた詳細は `kind=feedback` を条件に持つ（前後もその分類の中を辿る）
11. ブラウザの戻る／進むは URL どおりに条件付きの一覧・詳細を復元する（条件が URL にあるので既存の hashchange 描画で満たされる。回帰しないことを確認する）
12. localStorage が使えない（例外・プライベートモード）ときも画面は動き、記憶だけが効かない

やらないこと: 別端末間での条件共有（サーバー保存）、詳細画面に条件の表示 UI を足すこと、Python サーバー側の変更。

## Global Constraints

- コードは1ファイル200行未満（ユーザー全体規約）。`reports.js` は現在130行
- 既存のコメント様式（日本語1行＋英語1行の対訳）に合わせる
- テスター由来の文字列は textContent で入れる（`core.js` 冒頭の規約）。innerHTML を使わない
- localStorage の読み書きはすべて try/catch で囲み、失敗時は記憶なしとして動く
- localStorage のキーは `playtest-dashboard.report-filter`
- 対象ディレクトリ: `scripts/playtest/dashboard/static/js/`、テスト: `scripts/playtest/tests/`
- 既存テストを壊さない: `python3 scripts/playtest/tests/test_dashboard.py`・`python3 scripts/playtest/tests/test_dashboard_state.py`

---

## File Structure

| ファイル | 責務 | 新規/変更 |
|---|---|---|
| `scripts/playtest/dashboard/static/js/report-filter.js` | 絞り込みの純粋ロジック: 条件キー・条件の抽出・一致判定（`applyFilters` を `reports.js` から移す）・前後探索・記憶の読み書き | 新規（同じ役割の部品は `reports.js` の `applyFilters`/`readMatches`/`EMPTY_KIND` のみ → 写さず移動して `reports.js` から import する） |
| `scripts/playtest/dashboard/static/js/views/reports.js` | 一覧。描画時と検索入力時に条件を記憶し、行リンクへ条件を渡す | 変更 |
| `scripts/playtest/dashboard/static/js/components.js` | `reportRow(report, show, filter)` に第3引数を足し、詳細リンクへ条件を付ける | 変更 |
| `scripts/playtest/dashboard/static/js/views/report.js` | 詳細。戻りリンクと前後リンクを条件から作る | 変更 |
| `scripts/playtest/dashboard/static/js/views/overview.js` | 未投入行・感想の詳細リンクに分類の条件を付ける | 変更 |
| `scripts/playtest/dashboard/static/js/app.js` | `report` ビューへ `route.params` を渡す。「報告」タブの href を記憶条件から作る | 変更 |
| `scripts/playtest/tests/test_report_filter.mjs` | `report-filter.js` の node テスト | 新規 |
| `scripts/playtest/dashboard/README.md` | 画面表の「報告」「報告詳細」行に記憶・条件引き継ぎを一文足す | 変更 |

配置と前例: 「絞り込みは URL に持たせる」は既存 `reports.js` の `hrefWith` のコメントが前例。ルート生成は既存 `core.js` の `routeHref` を呼ぶ（写さない）。DOM を持たない共通ロジックを `static/js/` 直下に置くのは `core.js`・`charts.js` と同じ階層。

### 状態の持ち主と最新化（Self-Review 7）

| 事実 | 保持者 | 書き換え操作 → 最新化経路 |
|---|---|---|
| 最後の条件 | localStorage（`report-filter.js`） | 一覧描画（hashchange で `renderReports` が走るたびに `saveFilter`）・検索入力（`searchBox` の oninput で `saveFilter`）→ 次の `render()` で app.js がタブ href を読み直す（`render()` は hashchange ごとに走る。Requirement 6） |
| 報告の既読・状態 | `app.js` の `data` | 既存の `dashboard:changed` → `refresh(true)` → `render()`。詳細の前後リンクは描画のたびに `data` から計算するので最新（Requirement 4） |

---

### Task 1: 絞り込みロジックのモジュール化とテスト

**Files:**
- Create: `scripts/playtest/dashboard/static/js/report-filter.js`
- Create: `scripts/playtest/tests/test_report_filter.mjs`
- Modify: `scripts/playtest/dashboard/static/js/views/reports.js`（`EMPTY_KIND`・`readMatches`・`applyFilters` を削除し import に置き換え）

**Interfaces:**
- Produces（`report-filter.js` の export）:
  - `FILTER_KEYS: string[]` = `["triage", "kind", "read", "tester", "build", "q"]`
  - `EMPTY_KIND: string` = `"(none)"`
  - `filterOf(params: URLSearchParams): Record<string,string>` — FILTER_KEYS のうち空でない値だけの object
  - `readMatches(report, value: string): boolean`
  - `applyFilters(reports, params: URLSearchParams): report[]`（既存と同じ挙動）
  - `neighbours(reports, current, params: URLSearchParams): { newer: report|null, older: report|null }` — `reports` は新しい順。`current` の位置から前（新しい側）・後（古い側）へ走査し、`applyFilters` と同じ条件に一致する最初の報告。`current` が条件外でも位置から探す
  - `saveFilter(filter: Record<string,string>): void` / `loadFilter(): Record<string,string>` — localStorage `playtest-dashboard.report-filter` に JSON。失敗・壊れた値は `{}`。読んだ値は FILTER_KEYS の文字列値だけに絞る

- [ ] **Step 1: 失敗するテストを書く** — `scripts/playtest/tests/test_report_filter.mjs`

```js
// report-filter.js の純粋ロジックを node で検証する
// Verifies report-filter.js pure logic under node
import assert from "node:assert/strict";
import { test } from "node:test";
import { applyFilters, filterOf, loadFilter, neighbours, saveFilter } from "../dashboard/static/js/report-filter.js";

const R = (id, kind, extra = {}) => ({ id, kind, triage: "excluded", steamId: "s", buildLabel: "b", description: "", testerName: "", readAt: null, ...extra });
const reports = [R("1", "feedback"), R("2", "bug"), R("3", "feedback", { readAt: "x" }), R("4", "bug"), R("5", "feedback")];

test("filterOf keeps only non-empty filter keys", () => {
  assert.deepEqual(filterOf(new URLSearchParams("kind=feedback&q=&foo=1&read=unread")), { kind: "feedback", read: "unread" });
});

test("applyFilters matches kind and read", () => {
  assert.deepEqual(applyFilters(reports, new URLSearchParams("kind=feedback&read=unread")).map((r) => r.id), ["1", "5"]);
});

test("neighbours skip non-matching reports", () => {
  const n = neighbours(reports, reports[2], new URLSearchParams("kind=feedback"));
  assert.equal(n.newer.id, "1");
  assert.equal(n.older.id, "5");
});

test("neighbours work when current no longer matches", () => {
  const n = neighbours(reports, reports[2], new URLSearchParams("kind=feedback&read=unread"));
  assert.equal(n.newer.id, "1");
  assert.equal(n.older.id, "5");
});

test("neighbours at the ends are null", () => {
  const n = neighbours(reports, reports[0], new URLSearchParams("kind=feedback"));
  assert.equal(n.newer, null);
  assert.equal(n.older.id, "3");
});

test("neighbours without filter use all reports", () => {
  const n = neighbours(reports, reports[1], new URLSearchParams(""));
  assert.equal(n.newer.id, "1");
  assert.equal(n.older.id, "3");
});

test("saveFilter/loadFilter round-trip and tolerate broken storage", () => {
  const store = new Map();
  globalThis.localStorage = { getItem: (k) => store.get(k) ?? null, setItem: (k, v) => store.set(k, v) };
  saveFilter({ kind: "feedback" });
  assert.deepEqual(loadFilter(), { kind: "feedback" });
  store.set("playtest-dashboard.report-filter", "{broken");
  assert.deepEqual(loadFilter(), {});
  store.set("playtest-dashboard.report-filter", JSON.stringify({ kind: 1, evil: "x", q: "a" }));
  assert.deepEqual(loadFilter(), { q: "a" });
  globalThis.localStorage = { getItem: () => { throw new Error("denied"); }, setItem: () => { throw new Error("denied"); } };
  assert.deepEqual(loadFilter(), {});
  saveFilter({ kind: "bug" });
  delete globalThis.localStorage;
  assert.deepEqual(loadFilter(), {});
});
```

- [ ] **Step 2: 失敗を確認** — Run: `node --test scripts/playtest/tests/test_report_filter.mjs` / Expected: FAIL（モジュールが無い）

- [ ] **Step 3: 実装** — `scripts/playtest/dashboard/static/js/report-filter.js`

```js
// 報告の絞り込み: 条件の抽出・一致判定・前後探索・最後の条件の記憶（DOM を持たない）
// Report filtering: condition extraction, matching, neighbour lookup and remembering the last condition (no DOM)

export const FILTER_KEYS = ["triage", "kind", "read", "tester", "build", "q"];
// 種別が空（読めない箱）は value="" だと「すべて」と区別できないので専用の値で表す
// An empty kind (unreadable box) gets its own value, since "" would mean "all"
export const EMPTY_KIND = "(none)";
const STORAGE_KEY = "playtest-dashboard.report-filter";

export function filterOf(params) {
  return Object.fromEntries(FILTER_KEYS.filter((key) => params.get(key)).map((key) => [key, params.get(key)]));
}

export function readMatches(report, value) {
  if (value === "unread") return !report.readAt;
  if (value === "read") return Boolean(report.readAt);
  return true;
}

function matcher(params) {
  const query = (params.get("q") || "").toLowerCase();
  return (r) =>
    (!params.get("kind") || (r.kind || EMPTY_KIND) === params.get("kind"))
    && (!params.get("triage") || r.triage === params.get("triage"))
    && (!params.get("tester") || r.steamId === params.get("tester"))
    && (!params.get("build") || r.buildLabel === params.get("build"))
    && readMatches(r, params.get("read") || "")
    && (!query || `${r.description || ""} ${r.id} ${r.testerName}`.toLowerCase().includes(query));
}

export function applyFilters(reports, params) {
  return reports.filter(matcher(params));
}

// 見ている報告が条件から外れても（既読にした等）、全件の並びでの位置から一番近い一致を探す
// Even when the shown report no longer matches (e.g. just marked read), search from its position in the full list
export function neighbours(reports, current, params) {
  const matches = matcher(params);
  const index = reports.indexOf(current);
  const newer = reports.slice(0, index).reverse().find(matches) || null;
  const older = reports.slice(index + 1).find(matches) || null;
  return { newer, older };
}

// 記憶はこのブラウザだけの便宜。読めない・壊れている時は記憶なしとして動く
// Memory is a per-browser convenience; unreadable or broken storage just means nothing is remembered
export function saveFilter(filter) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(filter));
  } catch (error) {
    console.warn("[dashboard] filter memory unavailable", error);
  }
}

export function loadFilter() {
  try {
    const saved = JSON.parse(localStorage.getItem(STORAGE_KEY) || "{}");
    return Object.fromEntries(FILTER_KEYS.filter((key) => typeof saved?.[key] === "string" && saved[key]).map((key) => [key, saved[key]]));
  } catch (error) {
    console.warn("[dashboard] filter memory unavailable", error);
    return {};
  }
}
```

`views/reports.js`: `EMPTY_KIND` 定数・`readMatches`・`applyFilters` の定義を削除し、冒頭に `import { EMPTY_KIND, applyFilters, readMatches } from "../report-filter.js";` を足す（`applyFilters` は他から import されていないか `grep -rn applyFilters scripts/playtest` で確認し、あれば import 元を `report-filter.js` に直す）。

- [ ] **Step 4: 通過を確認** — Run: `node --test scripts/playtest/tests/test_report_filter.mjs` / Expected: PASS（7 tests）。続けて `python3 scripts/playtest/tests/test_dashboard.py` も PASS

- [ ] **Step 5: コミット**

```bash
git add scripts/playtest/dashboard/static/js/report-filter.js scripts/playtest/dashboard/static/js/views/reports.js scripts/playtest/tests/test_report_filter.mjs
git commit -m "playtest dashboard: 絞り込みロジックを report-filter.js に分け前後探索と記憶を足す"
```

### Task 2: 画面への配線（一覧・詳細・概要・タブ）

**Files:**
- Modify: `scripts/playtest/dashboard/static/js/views/reports.js`
- Modify: `scripts/playtest/dashboard/static/js/components.js:53-54`
- Modify: `scripts/playtest/dashboard/static/js/views/report.js:9-33`
- Modify: `scripts/playtest/dashboard/static/js/views/overview.js:26,91`
- Modify: `scripts/playtest/dashboard/static/js/app.js:17-19,57-63`
- Modify: `scripts/playtest/dashboard/README.md`（画面表）

**Interfaces:**
- Consumes: Task 1 の `filterOf`・`neighbours`・`saveFilter`・`loadFilter`、既存 `routeHref(view, args, params)`
- Produces: `reportRow(report, show, filter)`（`filter` 省略時は条件なしリンク）、`renderReport(data, args, params)`

- [ ] **Step 1: `components.js` の `reportRow` に第3引数**

```js
export function reportRow(report, show, filter) {
  return h("li", { class: `row-item${report.readAt ? " is-read" : ""}` }, readToggle(report), h("a", { class: "row", href: routeHref("report", [report.boxSteamId, report.boxId], filter) },
```

（以降の行は変更しない）

- [ ] **Step 2: `reports.js` で記憶と行リンク**

`renderReports` の先頭で `saveFilter(filterOf(params));` を呼ぶ（コメント: 開いた一覧の条件を最後の条件として記憶する。条件なしで開けば空を記憶する）。`resultCard` の `reportRow(r, show)` を `reportRow(r, show, filterOf(params))` に。`searchBox` の oninput で `history.replaceState(...)` の直後に `saveFilter(filterOf(params));`。import に `filterOf, saveFilter` を足す。

- [ ] **Step 3: `report.js` の戻り・前後リンク**

```js
export function renderReport(data, args, params) {
  const index = data.reports.findIndex((r) => r.boxSteamId === args[0] && r.boxId === args[1]);
  const filter = filterOf(params);
  if (index < 0) return h("div", { class: "view" }, backLink(filter), emptyNote("この報告は見つかりません"));
  const report = data.reports[index];
  const { newer, older } = neighbours(data.reports, report, params);
  return h("div", { class: "view" },
    h("nav", { class: "detail-nav" }, backLink(filter), h("span", { class: "pager" },
      neighbour(newer, "‹ 新しい報告", filter), neighbour(older, "古い報告 ›", filter))),
```

```js
// 一覧から持ってきた条件で戻り、前後も同じ条件の中を辿る
// Return to, and page within, the list condition the detail was opened with
function backLink(filter) {
  return h("a", { href: routeHref("reports", [], filter) }, "← 報告一覧");
}

function neighbour(report, label, filter) {
  return report ? h("a", { href: routeHref("report", [report.boxSteamId, report.boxId], filter) }, label) : h("span", { class: "muted" }, label);
}
```

import に `filterOf, neighbours`（`../report-filter.js`）を足す。

- [ ] **Step 4: `app.js`**

`report: (data, route) => renderReport(data, route.args, route.params),` に変更。`render()` のタブ生成で、`view === "reports"` のときだけ href を `routeHref("reports", [], loadFilter())` にする（コメント: 報告タブは最後に使った条件の一覧へ戻す）。import に `loadFilter`（`./report-filter.js`）。

- [ ] **Step 5: `overview.js`**

未投入の行: `reportRow(r, { kind: false, status: false }, { triage: "candidate" })`。感想の引用リンク: `routeHref("report", [r.boxSteamId, r.boxId], { kind: "feedback" })`（コメント: 概要から開いた詳細は、その欄の分類の中で前後を辿る）。

- [ ] **Step 6: README** — 画面表の「報告」行の「（条件は URL に残る）」を「（条件は URL に残り、最後の条件はブラウザに記憶して「報告」タブで戻る）」に、「報告詳細」行の先頭に「一覧の条件を引き継ぎ、前後・一覧へ戻るも同じ条件の中で辿る。」を足す。

- [ ] **Step 7: 静的確認** — Run: `node --test scripts/playtest/tests/test_report_filter.mjs && python3 scripts/playtest/tests/test_dashboard.py && python3 scripts/playtest/tests/test_dashboard_state.py && wc -l scripts/playtest/dashboard/static/js/*.js scripts/playtest/dashboard/static/js/views/*.js` / Expected: 全 PASS、全ファイル 200 行未満

- [ ] **Step 8: コミット**

```bash
git add scripts/playtest/dashboard
git commit -m "playtest dashboard: 詳細へ一覧の条件を引き継ぎ前後と戻りに適用し最後の条件を記憶する"
```

### Task 3: 実機 e2e（ブラウザ）

本番の 8932 と状態ファイルを汚さないため、ログを APFS クローンで複製して別ポートで立てる。

- [ ] **Step 1: 起動**

```bash
S=$(mktemp -d); cp -Rc ~/hermes-agent/data/repos/moorestech_logs "$S/logs"
python3 scripts/playtest/dashboard/server.py --port 8942 --logs "$S/logs" \
  --master ~/hermes-agent/data/repos/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master &
echo $! > "$S/pid"   # 終了時は kill $(cat "$S/pid")。pkill は使わない
```

- [ ] **Step 2: ブラウザ（Playwright / chrome-use / claude-in-chrome のいずれか）で `http://127.0.0.1:8942/playtest/#/reports?kind=feedback` を開き、次を順に確認して URL とスクショを記録する**
  1. 1行目を開く → URL が `#/report/<sid>/<id>?kind=feedback`（Req 1）
  2. 「古い報告 ›」を2回 → 辿った先がすべて感想で URL に `?kind=feedback`（Req 3）
  3. 「← 報告一覧」→ `#/reports?kind=feedback`（Req 2）
  4. 一覧→詳細→ブラウザ戻る → `#/reports?kind=feedback` で感想だけの一覧（Req 11）
  5. 「概要」タブ → 「報告」タブ → `#/reports?kind=feedback`（Req 6）。ページを再読込してから同じ操作でも同じ
  6. 検索欄に文字を入れ、概要→報告タブ → 検索語が残る（Req 7）
  7. 「条件をクリア」→ 概要→報告タブ → 条件なし（Req 8）
  8. `read=unread` で詳細を開き「既読にする」→ 報告が表示されたまま前後リンクが出る（Req 5）
  9. 概要の「最新の感想」の引用から開く → URL に `?kind=feedback`（Req 10）
- [ ] **Step 3: 合否** — ブラウザのコンソールにエラー・警告（`error` / `Uncaught` / `CSP` / `Refused`）が e2e 区間でゼロ、サーバーの標準エラーに `Traceback` / `421` / `500` がゼロであること。期待した URL が出たことだけで合格にしない
- [ ] **Step 4: 片付け** — `kill $(cat "$S/pid")`、`rm -rf "$S"`

### Task 4: 全ブランチレビューと PR（省略不可）

- [ ] 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。moorestech に固有のレビュースキル（`moores-code-review` 等）があればそれ、無ければ `all-code-review`
- [ ] レビュー指摘の反映が判定経路（`report-filter.js` の条件式・前後探索・記憶の読み書き）に触れたら、Task 3 の e2e を反映後のコードで再実施する
- [ ] plan・e2e 記録に残った「未検証」「未確認」は 1 件ずつ GitHub issue に起票し、PR 本文に issue 番号を列挙する
- [ ] push して PR を作成する（moorestech の PR 作成スキルがあればそれ）。PR 作成後は `moores-wt rm playtest-filter-memory` で worktree を畳む

---

## 判断記録（ADR）

ADR 3条件（覆すと高コスト・代替案が実在・後から見て意外）を満たす判断は無いため `docs/adr/` は作らず、ここに直接書く。

| # | 判断 | 出所 |
|---|---|---|
| D1 | 最後の条件をブラウザに記憶し、詳細 URL に条件を載せて「← 報告一覧」・前後・ブラウザ戻るで維持。「報告」タブ・再訪でも同じ条件で開く。「条件をクリア」で記憶も消える。概要・テスター画面の条件付きリンクはそのリンクの条件が優先 | ユーザー裁定 2026-10-07 原文「絞り込みを保存するようにして、戻るボタンで戻ってもそのフィルターが維持されるようにしてほしい」→ 選択「最後の条件を覚える」 |
| D2 | 前へ・次へは条件を適用した報告だけを辿る | ユーザー裁定 2026-10-07 原文「フィルター付きで詳細を開いたとき、前へ、次へ、はそのフィルターが適用された状態で表示されるようにしてほしい」 |
| D3 | 以下 D4〜D8 の詳細仕様はエージェントが既定値を選んだ | agent前提（ユーザーによる詳細設計の委任。原文「全部いい感じで。計画書作ったらcode移譲で進めて」） |
| D4 | 前後の対象は取得済みの最新データに条件を当てて決める（開いた時点で固定しない） | agent前提（ユーザーによる詳細設計の委任） |
| D5 | 見ている報告が条件から外れても表示を続け、全件の並びでの位置から一番近い一致を前後にする（自動で移動しない） | agent前提（ユーザーによる詳細設計の委任） |
| D6 | 条件付きリンクはそのリンクの条件だけで開き、記憶済み条件と合成しない | agent前提（ユーザーによる詳細設計の委任。D1「リンクの条件が優先」の解釈） |
| D7 | 概要の未投入行・感想から開いた詳細は、その欄の分類（`triage=candidate` / `kind=feedback`）を条件に持つ | agent前提（ユーザーによる詳細設計の委任） |
| D8 | 記憶先は localStorage（端末間共有はしない）。条件なしの `#/reports` を開くと空を記憶する | agent前提（ユーザーによる詳細設計の委任。既存の「絞り込みは URL に持たせる」方針と、サーバー状態を書き換えない最小構成） |
| D9 | 純粋ロジックを `report-filter.js` に分け node でテストする | agent判断（既存に JS テストが無く、DOM 無しで検証できる単位に切るため） |
