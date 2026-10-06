// 報告一覧: 種別・投入状態・テスター・ビルド・文字列で絞り込む
// Report list filtered by kind, triage, tester, build and free text
import { emptyNote, h, KIND_LABELS, routeHref, testerName } from "../core.js";
import { reportLines } from "./overview.js";

const TRIAGE_FILTERS = { candidate: "未投入のバグ", queued: "投入済み", excluded: "遠隔実行で除外", broken: "読めない箱" };

export function renderReports(data, params) {
  const results = h("div", null);
  const refresh = () => results.replaceChildren(...resultNodes(data.reports, params));
  refresh();
  return h("div", { class: "view" }, filterBar(data.reports, params, refresh), results);
}

function resultNodes(reports, params) {
  const filtered = applyFilters(reports, params);
  return [h("p", { class: "muted count" }, `${filtered.length} / ${reports.length}件`),
    filtered.length ? reportLines(filtered) : emptyNote("条件に合う報告はありません")];
}

export function applyFilters(reports, params) {
  const query = (params.get("q") || "").toLowerCase();
  return reports.filter((r) =>
    (!params.get("kind") || r.kind === params.get("kind"))
    && (!params.get("triage") || r.triage === params.get("triage"))
    && (!params.get("tester") || r.steamId === params.get("tester"))
    && (!params.get("build") || r.buildLabel === params.get("build"))
    && (!query || `${r.description || ""} ${r.id} ${r.testerName}`.toLowerCase().includes(query)));
}

function filterBar(reports, params, refresh) {
  const testers = new Map(reports.map((r) => [r.steamId, testerName(r)]));
  const builds = [...new Set(reports.map((r) => r.buildLabel).filter(Boolean))].sort().reverse();
  const kinds = [...new Set(reports.map((r) => r.kind))];
  return h("div", { class: "filters" },
    select("kind", "種別すべて", kinds.map((k) => [k, KIND_LABELS[k] || k || "不明"]), params),
    select("triage", "投入状態すべて", Object.entries(TRIAGE_FILTERS), params),
    select("tester", "テスターすべて", [...testers], params),
    select("build", "ビルドすべて", builds.map((b) => [b, b]), params),
    searchBox(params, refresh),
    hasAny(params) ? h("a", { class: "clear", href: routeHref("reports") }, "条件を消す") : null);
}

function select(key, allLabel, options, params) {
  const el = h("select", { "aria-label": allLabel, onchange: (event) => navigate(params, key, event.target.value) },
    h("option", { value: "" }, allLabel),
    options.map(([value, label]) => h("option", { value, selected: params.get(key) === value }, label)));
  return el;
}

// 検索は入力欄を作り直さず結果だけ差し替える（打鍵中にフォーカスを失わないため）
// Search swaps only the results, never the input, so typing keeps focus
function searchBox(params, refresh) {
  return h("input", {
    type: "search", placeholder: "説明文・IDで検索", value: params.get("q") || "", "aria-label": "検索",
    oninput: (event) => {
      params.set("q", event.target.value);
      history.replaceState(null, "", routeHref("reports", [], Object.fromEntries(params)));
      refresh();
    },
  });
}

// 絞り込みは URL に持たせ、再読込・共有・戻るで同じ一覧に戻れるようにする
// Filters live in the URL so reload, sharing and Back return to the same list
function navigate(params, key, value) {
  const next = Object.fromEntries(params);
  next[key] = value;
  location.hash = routeHref("reports", [], next);
}

function hasAny(params) {
  return ["kind", "triage", "tester", "build", "q"].some((key) => params.get(key));
}
