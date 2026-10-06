// 進行: チャレンジ到達ファネルとセッション一覧（テスターで絞り込み可）
// Progress: challenge reach funnel and session list, filterable by tester
import { barList } from "../charts.js";
import { countableSessions, emptyNote, fmtDateTime, fmtMinutes, h, routeHref, section, testerName } from "../core.js";
import { furthestChallenge } from "./testers.js";

export function renderSessions(data, params) {
  const tester = params.get("tester") || "";
  const all = countableSessions(data.sessions);
  const sessions = tester ? all.filter((s) => s.steamId === tester) : all;
  const excluded = data.sessions.length - all.length;
  return h("div", { class: "view" },
    h("div", { class: "filters" }, testerSelect(all, tester),
      excluded > 0 ? h("span", { class: "muted" }, `遠隔実行あり/不明の ${excluded}件は除外`) : null,
      data.invalidSessions > 0 ? h("span", { class: "warn" }, `⚠ 読めなかった進行記録 ${data.invalidSessions}件`) : null),
    section(tester ? "チャレンジ到達（このテスターのセッション数）" : "チャレンジ到達ファネル（到達した人数）",
      funnel(sessions, data.master, !tester)),
    section(`セッション（${sessions.length}件）`, sessions.length ? h("div", { class: "table-wrap" }, table(sessions, data.master)) : emptyNote("セッションがありません")));
}

function testerSelect(sessions, current) {
  const testers = new Map(sessions.map((s) => [s.steamId, testerName(s)]));
  return h("select", {
    "aria-label": "テスター",
    onchange: (event) => { location.hash = routeHref("sessions", [], { tester: event.target.value }); },
  }, h("option", { value: "" }, "テスターすべて"),
  [...testers].map(([id, name]) => h("option", { value: id, selected: id === current }, name)));
}

// 横軸はマスタの定義順。全員表示では人数、1人表示ではそのチャレンジに到達していたセッション数を数える
// Ordered by master definition; counts testers for everyone, or sessions for a single tester
function funnel(sessions, master, byTester) {
  if (master.challenges.length === 0) return emptyNote("マスタのチャレンジ定義を読めないため表示できません");
  const reachers = new Map(master.challenges.map((c) => [c.guid, new Set()]));
  for (const session of sessions) {
    for (const guid of session.reachedChallenges) reachers.get(guid)?.add(byTester ? session.steamId : session.id);
  }
  let lastReached = -1;
  master.challenges.forEach((c, i) => { if (reachers.get(c.guid).size > 0) lastReached = i; });
  if (lastReached < 0) return emptyNote("到達したチャレンジはまだありません");
  // 誰も届いていない奥のチャレンジは1行だけ残し、どこで止まっているかを示す
  // Keep one unreached challenge past the frontier to show where players stall
  const rows = master.challenges.slice(0, Math.min(master.challenges.length, lastReached + 2)).map((c, i) => ({
    label: `${i + 1}. ${c.title}`, value: reachers.get(c.guid).size, note: c.category,
  }));
  return barList(rows, byTester ? "人" : "件");
}

function table(sessions, master) {
  const head = ["開始", "テスター", "プレイ", "到達", "研究", "最後のUI", "終了理由", "ビルド"];
  return h("table", null,
    h("thead", null, h("tr", null, head.map((label) => h("th", null, label)))),
    h("tbody", null, sessions.map((s) => {
      const furthest = furthestChallenge(master, s.reachedChallenges);
      return h("tr", null,
        h("td", null, fmtDateTime(s.sessionStart || s.readyAt)),
        h("td", null, h("a", { href: routeHref("sessions", [], { tester: s.steamId }) }, testerName(s))),
        h("td", { class: "num" }, fmtMinutes(s.playSeconds)),
        h("td", null, `${s.reachedChallenges.length}件`, furthest ? h("div", { class: "muted small" }, furthest.title) : null),
        h("td", { class: "num" }, String(s.completedResearch.length)),
        h("td", null, s.lastUiState),
        h("td", null, s.endReason),
        h("td", { class: "muted small" }, s.buildLabel || "不明"));
    })));
}
