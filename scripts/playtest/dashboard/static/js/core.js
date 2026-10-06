// DOM 生成・書式・ルーティングの共通部品
// Shared helpers for DOM building, formatting and routing
// テスター由来の文字列を扱うため、本文は必ず textContent で入れ innerHTML は使わない
// Tester-supplied text is always set via textContent; innerHTML is never used

export function h(tag, attrs, ...children) {
  const el = document.createElement(tag);
  for (const [key, value] of Object.entries(attrs || {})) {
    if (value === null || value === undefined || value === false) continue;
    if (key === "class") el.className = value;
    // CSP が style 属性を禁じるため CSSOM 経由で入れる
    // CSP forbids the style attribute, so styles go through the CSSOM
    else if (key === "style") el.style.cssText = value;
    else if (key.startsWith("on")) el.addEventListener(key.slice(2), value);
    else el.setAttribute(key, value === true ? "" : value);
  }
  appendAll(el, children);
  return el;
}

function appendAll(el, children) {
  for (const child of children.flat(Infinity)) {
    if (child === null || child === undefined || child === false) continue;
    el.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
}

const JST_DATETIME = new Intl.DateTimeFormat("ja-JP", {
  timeZone: "Asia/Tokyo", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit",
});

export function fmtDateTime(iso) {
  if (!iso) return "不明";
  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? "不明" : JST_DATETIME.format(date);
}

export function fmtMinutes(seconds) {
  if (seconds === null || seconds === undefined) return "—";
  const minutes = seconds / 60;
  if (minutes < 60) return `${minutes.toFixed(0)}分`;
  return `${(minutes / 60).toFixed(1)}時間`;
}

export function jstToday(offsetDays) {
  const date = new Date(Date.now() + 9 * 3600e3 - offsetDays * 86400e3);
  return date.toISOString().slice(0, 10);
}

export function testerName(row) {
  return row.testerName || `ID ${row.steamId.slice(-6)}`;
}

// 種別と投入状態の表示名。未知の値は値そのものを出して無音で消さない
// Display names for kind and triage; unknown values are shown verbatim, never hidden
export const KIND_LABELS = { bug: "バグ", feedback: "感想", crash: "クラッシュ" };
export const TRIAGE_LABELS = {
  candidate: "未投入", queued: "投入済み", excluded: "遠隔実行で除外", notBug: "", broken: "読めない箱",
};

export function kindBadge(kind) {
  return h("span", { class: `badge kind-${KIND_LABELS[kind] ? kind : "unknown"}` },
    h("i", { class: "dot" }), KIND_LABELS[kind] || kind || "不明");
}

export function triageBadge(report) {
  const label = TRIAGE_LABELS[report.triage];
  if (label === "") return null;
  return h("span", { class: `badge triage-${report.triage}` }, label || report.triage);
}

export function runBadge(report) {
  if (!report.queued) return null;
  if (!report.run) return h("span", { class: "badge run" }, "修正ラン待ち");
  const pr = report.run.prNumber ? ` #${report.run.prNumber}` : "";
  return h("span", { class: `badge run run-${report.run.status}` }, `修正ラン ${report.run.status}${pr}`);
}

// ハッシュルート: #/<view>/<arg...>?key=value
// Hash routes: #/<view>/<arg...>?key=value
export function parseRoute() {
  const raw = location.hash.replace(/^#\/?/, "");
  const [path, query] = raw.split("?");
  const parts = path.split("/").filter(Boolean).map(decodeURIComponent);
  return { view: parts[0] || "overview", args: parts.slice(1), params: new URLSearchParams(query || "") };
}

export function routeHref(view, args, params) {
  const path = [view, ...(args || [])].map(encodeURIComponent).join("/");
  const query = params ? new URLSearchParams(Object.entries(params).filter(([, v]) => v)).toString() : "";
  return `#/${path}${query ? `?${query}` : ""}`;
}

export function mediaUrl(report, name) {
  return `media/${encodeURIComponent(report.boxSteamId)}/${encodeURIComponent(report.boxId)}/${name}`;
}

export function countBy(rows, keyOf) {
  const counts = new Map();
  for (const row of rows) counts.set(keyOf(row), (counts.get(keyOf(row)) || 0) + 1);
  return counts;
}

// 遠隔実行あり/不明のセッションはダイジェストと同じく集計から外す
// Sessions with remote exec enabled/unknown stay out of aggregates, as in the digest
export function countableSessions(sessions) {
  return sessions.filter((s) => s.remoteExec === false);
}

// 本文中の https URL だけをリンクにし、残りは文字列のまま入れる（gyazo 等の添付を開けるように）
// Only https URLs in the text become links; everything else stays plain text (so gyazo attachments open)
export function linkify(text) {
  return text.split(/(https:\/\/[A-Za-z0-9\-._~:/?#[\]@!$&*+,;=%]+)/).map((part, i) =>
    (i % 2 === 1 ? h("a", { href: part, target: "_blank", rel: "noopener noreferrer" }, part) : part));
}

export function section(title, ...children) {
  return h("section", { class: "panel" }, h("h2", null, title), ...children);
}

export function emptyNote(text) {
  return h("p", { class: "empty" }, text);
}
