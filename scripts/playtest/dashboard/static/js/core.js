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

const JST_TIME = new Intl.DateTimeFormat("ja-JP", { timeZone: "Asia/Tokyo", hour: "2-digit", minute: "2-digit" });

export function fmtTime(iso) {
  if (!iso) return "";
  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? "" : JST_TIME.format(date);
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

// 種別の表示名。未知の値は値そのものを出して無音で消さない
// Display names for kinds; unknown values are shown verbatim, never hidden
// 原型のキー（constructor 等）を種別名と取り違えないよう Map で持つ
// Held in a Map so prototype keys such as "constructor" are never mistaken for kinds
const KIND_LABELS = new Map([["bug", "バグ"], ["feedback", "感想"], ["crash", "クラッシュ"]]);

export function isKnownKind(kind) {
  return KIND_LABELS.has(kind);
}

export function kindLabel(kind) {
  return KIND_LABELS.get(kind) || kind || "不明";
}

// ハッシュルート: #/<view>/<arg...>?key=value
// Hash routes: #/<view>/<arg...>?key=value
export function parseRoute() {
  const raw = location.hash.replace(/^#\/?/, "");
  const [path, query] = raw.split("?");
  const parts = path.split("/").filter(Boolean).map(safeDecode);
  return { view: parts[0] || "overview", args: parts.slice(1), params: new URLSearchParams(query || "") };
}

// 壊れた %エスケープの URL でも画面全体を落とさず、そのままの文字列で扱う
// A malformed %-escape must not take the whole page down; the raw segment is used instead
function safeDecode(segment) {
  try {
    return decodeURIComponent(segment);
  } catch (error) {
    console.warn("[dashboard] URL の復号に失敗したため生の値で扱う", segment, error);
    return segment;
  }
}

export function routeHref(view, args, params) {
  const path = [view, ...(args || [])].map(encodeURIComponent).join("/");
  const query = params ? new URLSearchParams(Object.entries(params).filter(([, v]) => v)).toString() : "";
  return `#/${path}${query ? `?${query}` : ""}`;
}

export function mediaUrl(report, name) {
  return `media/${encodeURIComponent(report.boxSteamId)}/${encodeURIComponent(report.boxId)}/${name}`;
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

export function emptyNote(text) {
  return h("p", { class: "empty" }, text);
}

// 人が付ける状態（既読・チケットのリンク）を書き込み、成功したらアプリへ知らせる。
// redraw=false は呼び出し側が自分の表示を直す場合（詳細画面の再生中の動画を描き直しで止めないため）
// Writes human-made state (read marks, ticket links) and notifies the app on success;
// redraw=false means the caller updates its own display (so a redraw never stops a playing video on the detail page)
export async function postState(route, body, redraw) {
  // サーバーへの書き込みはネットワーク境界なので失敗を隔離し、理由を画面上部とコンソールへ出す
  // Writing to the server is a network boundary; failures are isolated and shown in the header and console
  try {
    const response = await fetch(`api/${route}`, {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-Playtest-Dashboard": "1" },
      body: JSON.stringify(body),
    });
    const result = await response.json();
    if (!response.ok || !result.ok) throw new Error(result.error || `HTTP ${response.status}`);
  } catch (error) {
    console.error(`[dashboard] ${route} に失敗`, error);
    document.getElementById("notice").textContent = `保存失敗: ${error.message}`;
    return false;
  }
  window.dispatchEvent(new CustomEvent("dashboard:changed", { detail: { redraw } }));
  return true;
}

export function reportKey(report) {
  return { steamId: report.boxSteamId, id: report.boxId };
}

// 書き込み後にサーバーが保存した最新の報告を引き直す（別経路で足されたリンクも含めて画面を保存結果に揃えるため）
// Re-reads the report as the server saved it after a write, so the page matches storage including links added elsewhere
export async function fetchSavedReport(report) {
  // サーバーへの取得はネットワーク境界なので失敗を隔離し、理由をコンソールへ出す
  // Fetching from the server is a network boundary; failures are isolated and logged to the console
  try {
    const response = await fetch("api/data", { cache: "no-store" });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    return data.reports.find((r) => r.boxSteamId === report.boxSteamId && r.boxId === report.boxId) || null;
  } catch (error) {
    console.error("[dashboard] 保存結果の読み直しに失敗", error);
    document.getElementById("notice").textContent = `読み直し失敗: ${error.message}`;
    return null;
  }
}
