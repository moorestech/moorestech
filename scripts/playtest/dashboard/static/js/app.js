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

async function load() {
  const response = await fetch("api/data", { cache: "no-store" });
  if (!response.ok) throw new Error(`api/data HTTP ${response.status}`);
  const next = await response.json();
  // 生成時刻は毎回変わるので、中身が変わったときだけ描き直す（動画再生・入力中の画面を壊さない）
  // generatedAt always changes, so redraw only when content changes (keeps playing video and inputs intact)
  const nextSignature = JSON.stringify([next.reports.map((r) => [r.id, r.triage, r.run?.status]), next.sessions.length, next.digests[0]]);
  const changed = nextSignature !== signature;
  data = next;
  signature = nextSignature;
  document.getElementById("updated").textContent = `更新 ${fmtDateTime(next.generatedAt)}`;
  return changed;
}

function render() {
  const route = parseRoute();
  const active = route.view === "report" ? "reports" : route.view;
  document.getElementById("tabs").replaceChildren(...TABS.map(([view, label]) =>
    h("a", { href: routeHref(view), class: view === active ? "current" : null }, label)));
  const view = VIEWS[route.view] || VIEWS.overview;
  document.getElementById("main").replaceChildren(view(data, route));
}

function showError(error) {
  console.error("[dashboard]", error);
  document.getElementById("updated").textContent = `更新失敗: ${error.message}`;
}

async function refresh() {
  if (document.hidden) return;
  try {
    if (await load()) render();
  } catch (error) {
    showError(error);
  }
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
  window.addEventListener("hashchange", () => { render(); window.scrollTo(0, 0); });
  let resizeTimer = 0;
  window.addEventListener("resize", () => {
    clearTimeout(resizeTimer);
    resizeTimer = setTimeout(() => { if (parseRoute().view === "overview") render(); }, 200);
  });
  document.getElementById("reload").addEventListener("click", refresh);
  document.addEventListener("visibilitychange", refresh);
  setInterval(refresh, REFRESH_MS);
}

start();
