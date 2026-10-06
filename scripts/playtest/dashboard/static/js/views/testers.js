// テスター一覧: 1人1行で活動量・到達度・報告数を並べる
// Tester list: one row per tester with activity, progress and report counts
import { countableSessions, emptyNote, fmtDateTime, fmtMinutes, h, routeHref, section, testerName } from "../core.js";

export function renderTesters(data) {
  const rows = buildTesterRows(data);
  if (rows.length === 0) return h("div", { class: "view" }, emptyNote("テスターの記録がありません"));
  return h("div", { class: "view" }, section(`テスター（${rows.length}人）`, h("div", { class: "table-wrap" }, table(rows))));
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

function table(rows) {
  const head = ["テスター", "最終活動", "セッション", "プレイ時間", "ワールド累計", "到達チャレンジ", "研究", "報告"];
  return h("table", null,
    h("thead", null, h("tr", null, head.map((label) => h("th", null, label)))),
    h("tbody", null, rows.map((t) => h("tr", null,
      h("td", null, h("a", { href: routeHref("sessions", [], { tester: t.steamId }) }, t.name),
        h("div", { class: "muted small" }, t.steamId)),
      h("td", null, fmtDateTime(t.last)),
      h("td", { class: "num" }, String(t.sessions.length)),
      h("td", { class: "num" }, fmtMinutes(t.playSeconds)),
      h("td", { class: "num" }, t.worldSeconds ? fmtMinutes(t.worldSeconds) : "—"),
      h("td", null, `${t.reachedCount}件`, t.furthest ? h("div", { class: "muted small" }, `最奥: ${t.furthest.title}`) : null),
      h("td", { class: "num" }, String(t.research)),
      h("td", null, reportCell(t))))));
}

function reportCell(tester) {
  const parts = [["バグ", tester.reports.bug], ["感想", tester.reports.feedback], ["クラッシュ", tester.reports.crash],
    ["その他", tester.reports.other]].filter(([, count]) => count > 0);
  if (parts.length === 0) return "—";
  return h("a", { href: routeHref("reports", [], { tester: tester.steamId }) }, parts.map(([label, count]) => `${label}${count}`).join(" / "));
}
