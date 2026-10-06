// 概要: 左に「要対応（未投入のバグ）」、右に「直近の活動」と「最新の感想」を置く
// Overview: "needs action" (un-enqueued bugs) on the left, recent activity and latest feedback on the right
import { stackedDayChart } from "../charts.js";
import { card, dayGroups, deltaText, moreLink, pageHead, reportRow, stat } from "../components.js";
import { countableSessions, emptyNote, fmtDateTime, h, isKnownKind, jstToday, linkify, routeHref, testerName } from "../core.js";

const CHART_DAYS = 14;
const LIST_LIMIT = 12;
const KIND_SERIES = [
  { key: "bug", label: "バグ", color: "--series-1" },
  { key: "feedback", label: "感想", color: "--series-2" },
  { key: "crash", label: "クラッシュ", color: "--series-3" },
  { key: "other", label: "その他", color: "--series-4" },
];

export function renderOverview(data) {
  const sessions = countableSessions(data.sessions);
  const candidates = data.reports.filter((r) => r.triage === "candidate");
  const latest = [...data.reports, ...data.sessions].map((row) => row.readyAt).filter(Boolean).sort().pop();
  return h("div", { class: "view" },
    pageHead("概要", latest ? `最新の受信 ${fmtDateTime(latest)}` : "まだ受信がありません"),
    weekStats(data, sessions, candidates.length),
    h("div", { class: "layout-main-side" },
      card("未投入のバグ報告", { count: candidates.length, action: candidates.length > LIST_LIMIT ? moreLink(routeHref("reports", [], { triage: "candidate" }), "すべて見る") : null },
        candidates.length
          ? dayGroups(candidates.slice(0, LIST_LIMIT), (r) => r.date, (r) => reportRow(r, { kind: false, status: false }), dayTotals(candidates))
          : emptyNote("未投入のバグ報告はありません")),
      h("div", { class: "side-stack" },
        card(`活動（直近${CHART_DAYS}日）`, null, activityCharts(data.reports, sessions)),
        card("最新の感想", { action: moreLink(routeHref("reports", [], { kind: "feedback" }), "一覧") }, feedbackQuotes(data.reports)))));
}

function dayTotals(rows) {
  const totals = new Map();
  for (const row of rows) totals.set(row.date, (totals.get(row.date) || 0) + 1);
  return totals;
}

// 数字は「未投入（累計）」以外を直近7日に揃え、前の7日との差を添える
// Every figure except the cumulative backlog uses the last 7 days, with the change versus the 7 days before
function weekStats(data, sessions, candidateCount) {
  const unreadCandidates = data.reports.filter((r) => r.triage === "candidate" && !r.readAt).length;
  const since = jstToday(6);
  const before = jstToday(13);
  const inWeek = (d) => d >= since;
  const inPrev = (d) => d >= before && d < since;
  const reportsNow = data.reports.filter((r) => inWeek(r.date)).length;
  const reportsPrev = data.reports.filter((r) => inPrev(r.date)).length;
  const hours = (rows) => rows.reduce((sum, s) => sum + (s.playSeconds || 0), 0) / 3600;
  const playNow = hours(sessions.filter((s) => inWeek(s.date)));
  const playPrev = hours(sessions.filter((s) => inPrev(s.date)));
  // 遠隔実行で除外した報告は、セッションと同じく人数にも数えない
  // Reports excluded for remote exec are not counted toward testers, matching the sessions
  const counted = data.reports.filter((r) => r.triage !== "excluded");
  const testers = (pick) => new Set([...counted.filter((r) => pick(r.date)), ...sessions.filter((s) => pick(s.date))]
    .map((row) => row.steamId)).size;
  return h("div", { class: "stats" },
    stat("未投入のバグ", candidateCount, "件", `うち未読 ${unreadCandidates}件`,
      { href: routeHref("reports", [], { triage: "candidate" }), tone: candidateCount > 0 ? "attention" : null }),
    stat("報告（7日）", reportsNow, "件", deltaText(reportsNow, reportsPrev, "件"), { href: routeHref("reports") }),
    stat("プレイ時間（7日）", playNow.toFixed(1), "時間", deltaText(Number(playNow.toFixed(1)), Number(playPrev.toFixed(1)), "時間"),
      { href: routeHref("sessions") }),
    stat("テスター（7日）", testers(inWeek), "人", deltaText(testers(inWeek), testers(inPrev), "人"), { href: routeHref("testers") }));
}

function activityCharts(reports, sessions) {
  const days = Array.from({ length: CHART_DAYS }, (_, i) => jstToday(CHART_DAYS - 1 - i));
  const counts = new Map();
  for (const r of reports) {
    const key = `${r.date}|${isKnownKind(r.kind) ? r.kind : "other"}`;
    counts.set(key, (counts.get(key) || 0) + 1);
  }
  // 「その他」は該当がある時だけ凡例に出す
  // "Other" joins the legend only when something falls into it
  const series = KIND_SERIES.filter((s) => s.key !== "other" || reports.some((r) => !isKnownKind(r.kind)));
  const minutes = new Map();
  for (const s of sessions) minutes.set(s.date, (minutes.get(s.date) || 0) + (s.playSeconds || 0) / 60);
  return h("div", { class: "chart-stack" },
    h("h3", null, "報告数"),
    stackedDayChart(days, series, (day, kind) => counts.get(`${day}|${kind}`) || 0, "件", 130),
    h("h3", null, "プレイ時間（分）"),
    stackedDayChart(days, [{ key: "play", label: "プレイ時間", color: "--series-1" }],
      (day) => Math.round(minutes.get(day) || 0), "分", 110));
}

function feedbackQuotes(reports) {
  const rows = reports.filter((r) => r.kind === "feedback").slice(0, 3);
  if (rows.length === 0) return emptyNote("感想はまだありません");
  return h("ul", { class: "quotes" }, rows.map((r) => h("li", null,
    h("blockquote", null, linkify((r.description || "").trim() || "（説明文が空）")),
    h("a", { class: "quote-meta", href: routeHref("report", [r.boxSteamId, r.boxId]) },
      `${testerName(r)}・${fmtDateTime(r.readyAt)}`))));
}
