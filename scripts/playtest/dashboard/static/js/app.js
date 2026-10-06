// 起動・ナビゲーション・定期更新
// Bootstrap, navigation and periodic refresh
import { fmtDateTime, h, parseRoute, routeHref } from "./core.js";
import { renderDigests } from "./views/digests.js";
import { renderOverview } from "./views/overview.js";
import { renderReport } from "./views/report.js";
import { renderReports } from "./views/reports.js";
import { renderSessions } from "./views/sessions.js";
import { renderTesters } from "./views/testers.js";

const REFRESH_MS = 120_000;
const TABS = [
  ["overview", "概要"], ["reports", "報告"], ["testers", "テスター"], ["sessions", "進行"], ["digests", "ダイジェスト"],
];
const VIEWS = {
  overview: (data) => renderOverview(data),
  reports: (data, route) => renderReports(data, route.params),
  report: (data, route) => renderReport(data, route.args),
  testers: (data) => renderTesters(data),
  sessions: (data, route) => renderSessions(data, route.params),
  digests: (data, route) => renderDigests(data, route.args),
};

let data = null;
let signature = "";
let pendingRender = false;

async function load() {
  const response = await fetch("api/data", { cache: "no-store" });
  if (!response.ok) throw new Error(`api/data HTTP ${response.status}`);
  const next = await response.json();
  fillTesterNames(next);
  // 生成時刻は毎回変わるので、中身が変わったときだけ描き直す（動画再生・入力中の画面を壊さない）
  // generatedAt always changes, so redraw only when content changes (keeps playing video and inputs intact)
  const nextSignature = JSON.stringify([next.reports.map((r) => [r.id, r.triage, r.run?.status]), next.sessions.length, next.digests[0]]);
  const changed = nextSignature !== signature;
  data = next;
  signature = nextSignature;
  document.getElementById("updated").textContent = `${fmtDateTime(next.generatedAt)} 時点`;
  document.getElementById("notice").textContent = "";
  return changed;
}

// 名前解決前の旧い箱は、同じ SteamID の他の箱で取れた表示名を借りる
// Older boxes captured before name resolution borrow the display name another box of the same SteamID has
function fillTesterNames(next) {
  const names = new Map();
  for (const row of [...next.reports, ...next.sessions]) if (row.testerName) names.set(row.steamId, row.testerName);
  for (const row of [...next.reports, ...next.sessions]) row.testerName ||= names.get(row.steamId) || "";
}

function render() {
  const route = parseRoute();
  const active = route.view === "report" ? "reports" : route.view;
  // 報告タブにだけ未投入の件数を出す（要対応がどこにあるかをどの画面からも見えるように）
  // Only the reports tab carries the un-enqueued count, so pending work is visible from every view
  const pending = data.reports.filter((r) => r.triage === "candidate").length;
  document.getElementById("tabs").replaceChildren(...TABS.map(([view, label]) =>
    h("a", { href: routeHref(view), class: view === active ? "current" : null }, label,
      view === "reports" && pending > 0 ? h("span", { class: "tab-count", title: "未投入のバグ報告" }, String(pending)) : null)));
  const view = VIEWS[route.view] || VIEWS.overview;
  document.getElementById("main").replaceChildren(view(data, route));
}

function showError(error) {
  console.error("[dashboard]", error);
  document.getElementById("notice").textContent = `更新失敗: ${error.message}`;
}

// 定期更新で新着があっても、詳細画面（動画再生中かもしれない）と入力中は描き直さず、更新ボタンで反映する
// Periodic refresh never redraws the detail view (video may be playing) or a focused input; the button applies it
function isBusy() {
  return parseRoute().view === "report" || document.activeElement?.matches("input, select");
}

async function refresh(force) {
  if (document.hidden && !force) return;
  // サーバーへの取得はネットワーク境界なので失敗を隔離し、理由を画面上部とコンソールへ出す
  // Fetching from the server is a network boundary; failures are isolated and shown in the header and console
  try {
    pendingRender = (await load()) || pendingRender;
  } catch (error) {
    showError(error);
    return;
  }
  if (!pendingRender) return;
  if (!force && isBusy()) {
    document.getElementById("notice").textContent = "新着あり（更新で反映）";
    return;
  }
  pendingRender = false;
  render();
}

async function start() {
  try {
    await load();
    render();
  } catch (error) {
    showError(error);
    document.getElementById("main").replaceChildren(h("p", { class: "warn" }, "データを読み込めませんでした。"));
    return;
  }
  window.addEventListener("hashchange", () => { pendingRender = false; render(); window.scrollTo(0, 0); });
  let resizeTimer = 0;
  window.addEventListener("resize", () => {
    clearTimeout(resizeTimer);
    resizeTimer = setTimeout(() => { if (parseRoute().view === "overview") render(); }, 200);
  });
  document.getElementById("reload").addEventListener("click", () => refresh(true));
  document.addEventListener("visibilitychange", () => refresh(false));
  setInterval(() => refresh(false), REFRESH_MS);
}

start();
