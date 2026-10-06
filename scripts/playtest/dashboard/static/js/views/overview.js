// 概要: 要対応の件数・日別の推移・最新の報告と感想
// Overview: action counts, daily trends, latest reports and feedback
import { barList, stackedDayChart } from "../charts.js";
import {
  countableSessions, emptyNote, fmtDateTime, fmtMinutes, h, jstToday, kindBadge, linkify, routeHref, runBadge,
  section, testerName, triageBadge,
} from "../core.js";

const DAYS = 30;
const KIND_SERIES = [
  { key: "bug", label: "バグ", color: "--series-1" },
  { key: "feedback", label: "感想", color: "--series-2" },
  { key: "crash", label: "クラッシュ", color: "--series-3" },
  { key: "other", label: "その他・読めない箱", color: "--series-4" },
];
const KNOWN_KINDS = new Set(["bug", "feedback", "crash"]);

export function renderOverview(data) {
  const sessions = countableSessions(data.sessions);
  const days = Array.from({ length: DAYS }, (_, i) => jstToday(DAYS - 1 - i));
  return h("div", { class: "view" },
    kpiRow(data, sessions),
    h("div", { class: "grid-2" },
      section(`日別のプレイ報告（直近${DAYS}日）`, reportsChart(data.reports, days)),
      section(`日別のプレイ時間・分（直近${DAYS}日・遠隔実行なし）`, playChart(sessions, days))),
    h("div", { class: "grid-2" },
      section("未投入のバグ報告", candidateList(data.reports)),
      section("最新の感想", feedbackList(data.reports))),
    section("離脱時のUI状態（全セッション）", dropOff(sessions)));
}

function kpiRow(data, sessions) {
  const candidates = data.reports.filter((r) => r.triage === "candidate").length;
  // テスター画面と同じく、集計対象のセッションと報告から数える
  // Counted from aggregatable sessions and reports, matching the tester view
  const testers = new Set([...data.reports.map((r) => r.steamId), ...sessions.map((s) => s.steamId)]);
  const yesterday = jstToday(1);
  const recent = data.reports.filter((r) => r.date >= yesterday).length
    + sessions.filter((s) => s.date >= yesterday).length;
  const totalSeconds = sessions.reduce((sum, s) => sum + (s.playSeconds || 0), 0);
  const broken = data.reports.filter((r) => r.triage === "broken").length + data.invalidSessions;
  return h("div", { class: "kpis" },
    kpi("未投入のバグ報告", candidates, "件", routeHref("reports", [], { triage: "candidate" }), candidates > 0),
    kpi("プレイ報告", data.reports.length, "件", routeHref("reports")),
    kpi("テスター", testers.size, "人", routeHref("testers")),
    kpi("セッション（集計対象）", sessions.length, "件", routeHref("sessions"), false, `計 ${fmtMinutes(totalSeconds)}`),
    kpi("昨日以降の新着", recent, "件", routeHref("reports")),
    broken > 0 ? kpi("読めなかった箱", broken, "件", routeHref("reports", [], { triage: "broken" }), true) : null);
}

function kpi(label, value, unit, href, attention, note) {
  return h("a", { class: `kpi${attention ? " attention" : ""}`, href },
    h("span", { class: "kpi-label" }, label),
    h("span", { class: "kpi-value" }, String(value), h("small", null, unit)),
    note ? h("span", { class: "kpi-note" }, note) : null);
}

function reportsChart(reports, days) {
  const counts = new Map();
  for (const r of reports) {
    const key = `${r.date}|${KNOWN_KINDS.has(r.kind) ? r.kind : "other"}`;
    counts.set(key, (counts.get(key) || 0) + 1);
  }
  // 「その他」は該当がある時だけ凡例に出す
  // "Other" joins the legend only when something falls into it
  const hasOther = reports.some((r) => !KNOWN_KINDS.has(r.kind));
  const series = KIND_SERIES.filter((s) => s.key !== "other" || hasOther);
  return stackedDayChart(days, series, (day, kind) => counts.get(`${day}|${kind}`) || 0, "件");
}

function playChart(sessions, days) {
  const minutes = new Map();
  for (const s of sessions) minutes.set(s.date, (minutes.get(s.date) || 0) + (s.playSeconds || 0) / 60);
  const series = [{ key: "play", label: "プレイ時間", color: "--series-1" }];
  return stackedDayChart(days, series, (day) => Math.round(minutes.get(day) || 0), "分");
}

function candidateList(reports) {
  const rows = reports.filter((r) => r.triage === "candidate").slice(0, 8);
  if (rows.length === 0) return emptyNote("未投入のバグ報告はありません");
  const total = reports.filter((r) => r.triage === "candidate").length;
  return h("div", null, reportLines(rows),
    total > rows.length ? h("a", { class: "more", href: routeHref("reports", [], { triage: "candidate" }) }, `すべて見る（${total}件）`) : null);
}

export function reportLines(rows) {
  return h("ul", { class: "report-lines" }, rows.map((r) => h("li", null,
    h("a", { href: routeHref("report", [r.boxSteamId, r.boxId]) },
      h("span", { class: "line-meta" }, kindBadge(r.kind), triageBadge(r), runBadge(r),
        h("span", { class: "muted" }, `${fmtDateTime(r.readyAt)}・${testerName(r)}`)),
      h("span", { class: "line-text" }, firstLine(r))))));
}

function firstLine(report) {
  if (report.problem && !report.description) return `⚠ ${report.problem}`;
  return (report.description || "").trim().split("\n")[0] || "（説明文が空）";
}

function feedbackList(reports) {
  const rows = reports.filter((r) => r.kind === "feedback").slice(0, 5);
  if (rows.length === 0) return emptyNote("感想はまだありません");
  return h("div", { class: "feedback" }, rows.map((r) => h("article", null,
    h("a", { class: "muted", href: routeHref("report", [r.boxSteamId, r.boxId]) }, `${fmtDateTime(r.readyAt)}・${testerName(r)}`),
    h("p", null, linkify((r.description || "").trim() || "（説明文が空）")))));
}

function dropOff(sessions) {
  if (sessions.length === 0) return emptyNote("セッションがありません");
  const counts = new Map();
  for (const s of sessions) counts.set(s.lastUiState, (counts.get(s.lastUiState) || 0) + 1);
  const rows = [...counts].sort((a, b) => b[1] - a[1]).slice(0, 8).map(([label, value]) => ({ label, value }));
  return barList(rows, "件");
}
