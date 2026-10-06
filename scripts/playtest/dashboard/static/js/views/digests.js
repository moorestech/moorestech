// 日次ダイジェスト: digests/<日付>.md を切り詰めなしで読む
// Daily digests: reads digests/<date>.md in full, untruncated
import { emptyNote, h, linkify, routeHref } from "../core.js";

export function renderDigests(data, args) {
  if (data.digests.length === 0) return h("div", { class: "view" }, emptyNote("ダイジェストはまだありません"));
  const date = data.digests.includes(args[0]) ? args[0] : data.digests[0];
  const body = h("article", { class: "panel digest" }, h("p", { class: "muted" }, "読み込み中…"));
  loadDigest(date, body);
  return h("div", { class: "view digest-layout" },
    h("nav", { class: "digest-dates" }, data.digests.map((d) =>
      h("a", { href: routeHref("digests", [d]), class: d === date ? "current" : null }, d))),
    body);
}

// テスター由来の感想全文が混ざるため Markdown として解釈せず、https URL だけリンクにした素の文章で出す
// Tester feedback is embedded verbatim, so the text is never interpreted as Markdown; only https URLs become links
// （行頭の「- 」やコード行を解釈すると、感想本文から本物と同じ見た目の見出しやコピー用コマンドを偽装できる）
// (Interpreting "- " or code lines would let feedback forge headings or copy-ready commands that look genuine)
async function loadDigest(date, body) {
  const markdown = await fetchDigest(date);
  if (markdown === null) {
    body.replaceChildren(emptyNote("読み込みに失敗しました（ブラウザのコンソールに理由）"));
    return;
  }
  body.replaceChildren(h("p", { class: "muted small" }, "投入コマンドは報告詳細画面からコピーしてください（ここでは本文をそのまま表示）"),
    h("div", { class: "digest-text" }, linkify(markdown)));
}

async function fetchDigest(date) {
  // ネットワークと応答 JSON は外部境界なので失敗を隔離し、理由をコンソールへ出す
  // Network and response JSON are an external boundary, so failures are isolated and logged to the console
  try {
    const response = await fetch(`api/digest/${encodeURIComponent(date)}`);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    return (await response.json()).markdown;
  } catch (error) {
    console.error(`[dashboard] digest ${date}:`, error);
    return null;
  }
}
