// 画面をまたいで使う表示部品（ページ見出し・カード・日付まとめ・報告行・状態表示）
// Display parts shared across views (page header, card, day groups, report rows, status labels)
import { fmtTime, h, KIND_LABELS, routeHref, testerName } from "./core.js";

const WEEKDAYS = ["日", "月", "火", "水", "木", "金", "土"];

export function pageHead(title, subtitle, ...actions) {
  return h("header", { class: "page-head" },
    h("div", null, h("h1", null, title), subtitle ? h("p", { class: "page-sub" }, subtitle) : null),
    actions.length ? h("div", { class: "page-actions" }, actions) : null);
}

export function card(title, options, ...children) {
  const { count, action, className } = options || {};
  return h("section", { class: `card${className ? ` ${className}` : ""}` },
    h("div", { class: "card-head" },
      h("h2", null, title, count !== undefined ? h("span", { class: "count" }, String(count)) : null),
      action || null),
    ...children);
}

export function moreLink(href, label) {
  return h("a", { class: "more", href }, label);
}

// "2026-10-02" → "10月2日（金）"。日付の無い行は「日付不明」にまとめる
// "2026-10-02" → "10月2日（金）"; rows without a date are grouped under "日付不明"
export function dayLabel(date) {
  if (!date) return "日付不明";
  const [y, m, d] = date.split("-").map(Number);
  const weekday = WEEKDAYS[new Date(Date.UTC(y, m - 1, d)).getUTCDay()];
  return `${m}月${d}日（${weekday}）`;
}

// 並び順を保ったまま同じ日付の行をまとめる（呼び出し側で新しい順に並べておく）。
// 件数を切り詰めて渡す時は totals に日付ごとの本当の件数を渡す（見出しの件数が表示行数に化けないように）
// Groups consecutive rows by date while keeping order (callers sort newest first);
// when rows are truncated, totals carries the true per-day count so headers do not show the visible count
export function dayGroups(rows, dateOf, renderRow, totals) {
  const groups = [];
  for (const row of rows) {
    const date = dateOf(row);
    if (!groups.length || groups[groups.length - 1].date !== date) groups.push({ date, rows: [] });
    groups[groups.length - 1].rows.push(row);
  }
  return h("div", { class: "day-groups" }, groups.map((group) => h("div", { class: "day-group" },
    h("div", { class: "day-label" }, dayLabel(group.date), h("span", null, `${totals?.get(group.date) ?? group.rows.length}件`)),
    h("ul", { class: "rows" }, group.rows.map(renderRow)))));
}

// 報告1行: 本文を主役にし、種別・状態は一覧の文脈で自明でない時だけ出す
// One report row: the text leads; kind and status appear only when the list context does not already imply them
export function reportRow(report, show) {
  return h("li", null, h("a", { class: "row", href: routeHref("report", [report.boxSteamId, report.boxId]) },
    show.kind ? kindMark(report.kind) : null,
    h("span", { class: "row-text" }, rowText(report)),
    // 右側は固定幅の列にして、録画や状態の有無で行ごとに位置がずれないようにする
    // The right side uses fixed-width columns so rows never shift with or without video/status
    h("span", { class: "row-meta" },
      h("span", { class: "media-mark", title: report.media.includes("video.mp4") ? "録画あり" : null },
        report.media.includes("video.mp4") ? "▶" : ""),
      h("span", { class: "who" }, testerName(report)),
      h("span", { class: "time" }, fmtTime(report.readyAt)),
      show.status ? h("span", { class: "status-col" }, statusText(report)) : null)));
}

function rowText(report) {
  if (report.problem && !report.description) return `⚠ ${report.problem}`;
  return (report.description || "").trim().split("\n")[0] || "（説明文が空）";
}

export function kindMark(kind) {
  const known = KIND_LABELS[kind] ? kind : "unknown";
  return h("span", { class: `kind kind-${known}` }, KIND_LABELS[kind] || kind || "不明");
}

// 状態は色付きの点＋文字。強い色は「未投入」と「読めない」だけに使う
// Status is a dot plus text; strong color is reserved for "not enqueued" and "unreadable"
export function statusText(report) {
  const label = statusLabel(report);
  if (!label) return null;
  return h("span", { class: `status status-${report.triage}` }, label);
}

function statusLabel(report) {
  if (report.triage === "candidate") return "未投入";
  if (report.triage === "excluded") return "除外（遠隔実行）";
  if (report.triage === "broken") return "読めない箱";
  if (report.triage === "queued") return report.run ? `修正ラン ${runStatusLabel(report.run.status)}` : "投入済み";
  return "";
}

// fix-result.json の無いランは実行中か異常終了か区別できないので、そう書く
// A run without fix-result.json may be running or dead, and the label says so
export function runStatusLabel(status) {
  return status === "noResult" ? "結果なし（実行中か異常終了）" : status;
}

// 直近7日と前の7日を比べた差分の表示（前週比）
// Change versus the previous 7 days
export function deltaText(current, previous, unit) {
  const diff = current - previous;
  if (diff === 0) return "前週と同じ";
  const rounded = Number.isInteger(diff) ? diff : diff.toFixed(1);
  return `前週比 ${diff > 0 ? "+" : ""}${rounded}${unit}`;
}

export function stat(label, value, unit, sub, options) {
  const { href, tone } = options || {};
  return h(href ? "a" : "div", { class: `stat${tone ? ` stat-${tone}` : ""}`, href },
    h("span", { class: "stat-label" }, label),
    h("span", { class: "stat-value" }, String(value), h("small", null, unit)),
    sub ? h("span", { class: "stat-sub" }, sub) : null);
}
