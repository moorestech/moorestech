// 進行: チャレンジ到達ファネルとセッション一覧（テスターで絞り込み可）
// Progress: challenge reach funnel and session list, filterable by tester
import { barList } from "../charts.js";
import { card, pageHead } from "../components.js";
import { countableSessions, emptyNote, fmtDateTime, fmtMinutes, h, routeHref, testerName } from "../core.js";
import { furthestChallenge } from "./testers.js";

export function renderSessions(data, params) {
  const tester = params.get("tester") || "";
  // 選択肢と除外件数は全セッションから作る（集計対象が0件のテスターも選べ、除外件数はそのテスターの分を出す）
  // Options and the excluded count come from all sessions, so testers with zero countable sessions stay selectable
  const scoped = tester ? data.sessions.filter((s) => s.steamId === tester) : data.sessions;
  const sessions = countableSessions(scoped);
  const excluded = scoped.length - sessions.length;
  const notes = [excluded > 0 ? `遠隔実行あり/不明の ${excluded}件は集計から除外` : "",
    data.invalidSessions > 0 ? `⚠ 読めなかった進行記録 ${data.invalidSessions}件` : ""].filter(Boolean).join("・");
  return h("div", { class: "view" },
    pageHead("進行", notes || "全セッションを集計", testerSelect(data.sessions, data.reports, tester)),
    h("div", { class: "layout-main-side" },
      card(tester ? "チャレンジ到達（到達していたセッション数）" : "チャレンジ到達（到達した人数）", null,
        funnel(sessions, data.master, !tester)),
      h("div", { class: "side-stack" },
        card("最後に開いていた画面", null, topCounts(sessions, (s) => s.lastUiState)),
        card("終了理由", null, topCounts(sessions, (s) => s.endReason)))),
    card("セッション", { count: sessions.length },
      sessions.length ? h("div", { class: "table-wrap" }, table(sessions, data.master)) : emptyNote("セッションがありません")));
}

function topCounts(sessions, keyOf) {
  if (sessions.length === 0) return emptyNote("セッションがありません");
  const counts = new Map();
  for (const s of sessions) counts.set(keyOf(s), (counts.get(keyOf(s)) || 0) + 1);
  return barList([...counts].sort((a, b) => b[1] - a[1]).slice(0, 6).map(([label, value]) => ({ label, value })), "件");
}

function testerSelect(sessions, reports, current) {
  const testers = new Map([...reports, ...sessions].map((row) => [row.steamId, testerName(row)]));
  return h("select", {
    "aria-label": "テスター",
    onchange: (event) => { location.hash = routeHref("sessions", [], { tester: event.target.value }); },
  }, h("option", { value: "" }, "すべてのテスター"),
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
  // 直前より減った所に減少数を添え、どこで脱落しているかを一目で分かるようにする
  // Mark where the count drops versus the previous step, so the drop-off points stand out
  const rows = master.challenges.slice(0, Math.min(master.challenges.length, lastReached + 2)).map((c, i, list) => {
    const value = reachers.get(c.guid).size;
    const previous = i > 0 ? reachers.get(list[i - 1].guid).size : value;
    return { label: `${i + 1}. ${c.title}`, value, note: c.category, mark: value < previous ? `−${previous - value}` : "" };
  });
  return barList(rows, byTester ? "人" : "件");
}

function table(sessions, master) {
  const head = ["開始", "テスター", "プレイ", "到達", "研究", "最後のUI", "終了理由", "ビルド"];
  return h("table", null,
    h("thead", null, h("tr", null, head.map((label) => h("th", null, label)))),
    h("tbody", null, sessions.map((s) => {
      const furthest = furthestChallenge(master, s.reachedChallenges);
      return h("tr", null,
        h("td", { class: "nowrap" }, fmtDateTime(s.sessionStart || s.readyAt)),
        h("td", null, h("a", { href: routeHref("sessions", [], { tester: s.steamId }) }, testerName(s))),
        h("td", { class: "num" }, fmtMinutes(s.playSeconds)),
        h("td", null, `${s.reachedChallenges.length}件`, furthest ? h("div", { class: "muted small" }, furthest.title) : null),
        h("td", { class: "num" }, String(s.completedResearch.length)),
        h("td", null, s.lastUiState),
        h("td", null, s.endReason),
        h("td", { class: "muted small" }, s.buildLabel || "不明"));
    })));
}
