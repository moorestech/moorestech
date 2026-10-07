// 報告一覧: 主な絞り込み（投入状態・種別）はタブ型、従の絞り込み（テスター・ビルド・検索）は1行にまとめる
// Report list: primary filters (status, kind) as segmented tabs, secondary ones (tester, build, search) in one row
import { card, dayGroups, pageHead, reportRow } from "../components.js";
import { emptyNote, h, kindLabel, postState, reportKey, routeHref, testerName } from "../core.js";

import { EMPTY_KIND, applyFilters, filterOf, readMatches, saveFilter } from "../report-filter.js";

const READ_TABS = [["", "既読・未読"], ["unread", "未読"], ["read", "既読"]];
const TRIAGE_TABS = [["", "すべて"], ["candidate", "未投入"], ["queued", "投入済み"], ["excluded", "除外"], ["broken", "読めない"]];

export function renderReports(data, params) {
  // 開いた一覧の条件を記憶し、条件なしなら記憶も空にする
  // Remember the opened list condition, including an empty condition
  saveFilter(filterOf(params));
  const results = h("div", null);
  const refresh = () => results.replaceChildren(resultCard(data.reports, params));
  refresh();
  return h("div", { class: "view" },
    pageHead("報告", `全${data.reports.length}件・新しい順`),
    h("div", { class: "filter-panel" },
      segmented("triage", TRIAGE_TABS.map(([value, label]) =>
        [value, label, value ? data.reports.filter((r) => r.triage === value).length : data.reports.length]), params),
      segmented("kind", kindTabs(data.reports), params),
      segmented("read", READ_TABS.map(([value, label]) => [value, label, data.reports.filter((r) => readMatches(r, value)).length]), params),
      secondaryFilters(data.reports, params, refresh)),
    results);
}

function resultCard(reports, params) {
  const filtered = applyFilters(reports, params);
  // 絞り込みで自明になった列は行から外す（同じ札が全行に並ぶと読む量だけ増える）
  // Columns made obvious by the filter are dropped from rows; the same tag on every row only adds reading
  const show = { kind: !params.get("kind"), status: !params.get("triage") };
  const unread = filtered.filter((r) => !r.readAt);
  return card("該当する報告", { count: filtered.length, action: unread.length ? markAllRead(unread) : null },
    filtered.length ? dayGroups(filtered, (r) => r.date, (r) => reportRow(r, show, filterOf(params)), null) : emptyNote("条件に合う報告はありません"));
}

// 表示中の未読をまとめて既読にする。サーバーの1回あたり上限（500件）ごとに送り、各回は全件か0件かになる
// Marks every unread report on screen as read, sent in chunks of the server's per-request limit (500); each chunk is all or none
const BULK_CHUNK = 500;

function markAllRead(unread) {
  const button = h("button", {
    type: "button", class: "ghost",
    onclick: async () => {
      button.disabled = true;
      for (let start = 0; start < unread.length; start += BULK_CHUNK) {
        const items = unread.slice(start, start + BULK_CHUNK).map(reportKey);
        const last = start + BULK_CHUNK >= unread.length;
        if (!(await postState("read", { items, read: true }, last))) {
          button.disabled = false;
          return;
        }
      }
    },
  }, `表示中の未読${unread.length}件を既読にする`);
  return button;
}

function kindTabs(reports) {
  const kinds = [...new Set(reports.map((r) => r.kind))];
  return [["", "全種別", reports.length], ...kinds.map((k) =>
    [k || EMPTY_KIND, kindLabel(k), reports.filter((r) => r.kind === k).length])];
}

// 件数0のタブも出す（「無い」ことが分かるのも情報）。選択中は塗りで示す
// Zero-count tabs stay visible (absence is information too); the selected one is filled
function segmented(key, tabs, params) {
  const current = params.get(key) || "";
  // 遷移先は押した時点の params から作る（検索語は replaceState で後から変わるため、描画時の href だと消える）
  // The target is built from params at click time; the search term changes later via replaceState and a render-time href would drop it
  return h("div", { class: "segmented", role: "tablist" }, tabs.map(([value, label, count]) =>
    h("a", {
      href: hrefWith(params, key, value), class: value === current ? "on" : null, role: "tab",
      onclick: (event) => { event.preventDefault(); location.hash = hrefWith(params, key, value); },
    }, label, h("span", { class: "seg-count" }, String(count)))));
}

function secondaryFilters(reports, params, refresh) {
  const testers = new Map(reports.map((r) => [r.steamId, testerName(r)]));
  const builds = [...new Set(reports.map((r) => r.buildLabel).filter(Boolean))].sort().reverse();
  return h("div", { class: "filters" },
    select("tester", "すべてのテスター", [...testers], params),
    select("build", "すべてのビルド", builds.map((b) => [b, b]), params),
    searchBox(params, refresh),
    h("a", { class: "clear", href: routeHref("reports") }, "条件をクリア"));
}

function select(key, allLabel, options, params) {
  return h("select", { "aria-label": allLabel, onchange: (event) => { location.hash = hrefWith(params, key, event.target.value); } },
    h("option", { value: "" }, allLabel),
    options.map(([value, label]) => h("option", { value, selected: params.get(key) === value }, label)));
}

// 検索は入力欄を作り直さず結果だけ差し替える（打鍵中にフォーカスを失わないため）
// Search swaps only the results, never the input, so typing keeps focus
function searchBox(params, refresh) {
  return h("input", {
    type: "search", placeholder: "本文・IDで検索", value: params.get("q") || "", "aria-label": "検索",
    oninput: (event) => {
      params.set("q", event.target.value);
      history.replaceState(null, "", routeHref("reports", [], Object.fromEntries(params)));
      saveFilter(filterOf(params));
      refresh();
    },
  });
}

// 絞り込みは URL に持たせ、再読込・共有・戻るで同じ一覧に戻れるようにする
// Filters live in the URL so reload, sharing and Back return to the same list
function hrefWith(params, key, value) {
  const next = Object.fromEntries(params);
  next[key] = value;
  return routeHref("reports", [], next);
}
