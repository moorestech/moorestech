// テスター一覧: 1人1行で活動量・到達度・報告数を並べる
// Tester list: one row per tester with activity, progress and report counts
import { card, pageHead } from "../components.js";
import { countableSessions, emptyNote, fmtDateTime, fmtMinutes, h, routeHref, testerName } from "../core.js";

export function renderTesters(data) {
  const rows = buildTesterRows(data);
  if (rows.length === 0) return h("div", { class: "view" }, pageHead("テスター"), emptyNote("テスターの記録がありません"));
  return h("div", { class: "view" },
    pageHead("テスター", "最終活動の新しい順・プレイ時間と到達は遠隔実行なしのセッションだけで数える"),
    card("テスター", { count: rows.length }, h("div", { class: "table-wrap" }, table(rows, data.master.challenges.length))));
}

// 到達の深さはマスタの並び順で最も奥のチャレンジで測る（件数だけだと寄り道と区別できない）
// Depth is the furthest challenge in master order; a bare count cannot tell detours from progress
export function furthestChallenge(master, reachedGuids) {
  let best = null;
  for (const guid of reachedGuids) {
    const index = master.challenges.findIndex((c) => c.guid === guid);
    if (index >= 0 && (best === null || index > best.index)) best = { index, title: master.challenges[index].title };
  }
  return best;
}

function buildTesterRows(data) {
  const testers = new Map();
  const entry = (row) => {
    if (!testers.has(row.steamId)) {
      testers.set(row.steamId, { steamId: row.steamId, name: testerName(row), profileUrl: "", last: "",
        sessions: [], reports: { bug: 0, feedback: 0, crash: 0, other: 0 } });
    }
    const tester = testers.get(row.steamId);
    if (row.testerName) tester.name = row.testerName;
    return tester;
  };
  for (const report of data.reports) {
    const tester = entry(report);
    tester.profileUrl = tester.profileUrl || report.profileUrl;
    tester.reports[report.kind in tester.reports ? report.kind : "other"] += 1;
    tester.last = maxIso(tester.last, report.readyAt);
  }
  for (const session of countableSessions(data.sessions)) {
    const tester = entry(session);
    tester.sessions.push(session);
    tester.last = maxIso(tester.last, session.sessionStart || session.readyAt);
  }
  return [...testers.values()].map((t) => summarize(t, data.master)).sort((a, b) => (b.last > a.last ? 1 : -1));
}

function maxIso(a, b) {
  return (b || "") > a ? b : a;
}

function summarize(tester, master) {
  const reached = new Set(tester.sessions.flatMap((s) => s.reachedChallenges));
  const research = Math.max(0, ...tester.sessions.map((s) => s.completedResearch.length));
  return {
    ...tester,
    playSeconds: tester.sessions.reduce((sum, s) => sum + (s.playSeconds || 0), 0),
    worldSeconds: Math.max(0, ...tester.sessions.map((s) => s.totalPlaySeconds || 0)),
    reachedCount: reached.size, furthest: furthestChallenge(master, reached), research,
  };
}

function table(rows, challengeTotal) {
  const head = ["テスター", "最終活動", "セッション", "プレイ時間", "到達チャレンジ", "研究", "報告"];
  return h("table", null,
    h("thead", null, h("tr", null, head.map((label) => h("th", null, label)))),
    h("tbody", null, rows.map((t) => h("tr", null,
      h("td", null, h("a", { class: "strong", href: routeHref("sessions", [], { tester: t.steamId }) }, t.name),
        h("div", { class: "muted small" }, t.steamId)),
      h("td", { class: "nowrap" }, fmtDateTime(t.last)),
      h("td", { class: "num" }, String(t.sessions.length)),
      h("td", { class: "num" }, fmtMinutes(t.playSeconds),
        t.worldSeconds ? h("div", { class: "muted small" }, `ワールド累計 ${fmtMinutes(t.worldSeconds)}`) : null),
      h("td", { class: "reach-cell" }, reachBar(t.reachedCount, challengeTotal), t.furthest ? h("div", { class: "muted small" }, t.furthest.title) : null),
      h("td", { class: "num" }, String(t.research)),
      h("td", null, reportCell(t))))));
}

// 到達チャレンジ数をマスタ全体に対する割合の棒で見せる（数字だけより進み具合を一目で比べられる）
// Reached challenges as a bar against the master total, easier to compare at a glance than bare numbers
function reachBar(count, total) {
  const ratio = total ? Math.min(1, count / total) : 0;
  return h("div", { class: "reach" },
    h("span", { class: "reach-track" }, h("span", { class: "reach-fill", style: `width:${ratio * 100}%` })),
    h("span", { class: "reach-num" }, total ? `${count}/${total}` : `${count}`));
}

function reportCell(tester) {
  const parts = [["バグ", tester.reports.bug], ["感想", tester.reports.feedback], ["クラッシュ", tester.reports.crash],
    ["その他", tester.reports.other]].filter(([, count]) => count > 0);
  if (parts.length === 0) return "—";
  return h("a", { href: routeHref("reports", [], { tester: tester.steamId }) }, parts.map(([label, count]) => `${label}${count}`).join(" / "));
}
