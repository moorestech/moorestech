// 日次ダイジェスト: digests/<日付>.md を切り詰めなしで読む
// Daily digests: reads digests/<date>.md in full, untruncated
import { dayLabel, pageHead } from "../components.js";
import { emptyNote, h, linkify, routeHref } from "../core.js";

// digest.py が出す固定の見出し。感想の手前に出るものは最初の出現、後ろに出るものは最後の出現だけを見出しとして扱う
// （感想本文に同じ文字列を書かれても、本物の見出しの位置は動かない）
// Fixed headings digest.py emits. Those before the feedback section use their first occurrence, those after it their last,
// so a copy typed into feedback never displaces the genuine heading
const HEADINGS_BEFORE_FEEDBACK = ["## プレイ報告の件数", "## 投入候補のバグ報告", "## 感想（全文）"];
const HEADINGS_AFTER_FEEDBACK = ["## 進行記録", "## 自動修正ラン"];

export function renderDigests(data, args) {
  if (data.digests.length === 0) return h("div", { class: "view" }, pageHead("ダイジェスト"), emptyNote("ダイジェストはまだありません"));
  const date = data.digests.includes(args[0]) ? args[0] : data.digests[0];
  const body = h("article", { class: "card digest" }, h("p", { class: "muted" }, "読み込み中…"));
  loadDigest(date, body);
  return h("div", { class: "view" },
    pageHead("ダイジェスト", `${dayLabel(date)}の日次ダイジェスト（Discord で切り詰められた分も含む全文）`),
    h("div", { class: "digest-layout" },
      h("nav", { class: "digest-dates" }, data.digests.map((d) =>
        h("a", { href: routeHref("digests", [d]), class: d === date ? "current" : null }, dayLabel(d)))),
      body));
}

function headingLines(lines) {
  const marked = new Set();
  for (const heading of HEADINGS_BEFORE_FEEDBACK) if (lines.indexOf(heading) >= 0) marked.add(lines.indexOf(heading));
  for (const heading of HEADINGS_AFTER_FEEDBACK) if (lines.lastIndexOf(heading) >= 0) marked.add(lines.lastIndexOf(heading));
  return marked;
}

// 見出し以外は素の文章のまま、見出しの間ごとに1ブロックへまとめる（先頭のタイトル行は画面見出しと重複するので省く）
// Non-heading lines stay plain text, one block per heading; the leading title line duplicates the page head and is dropped
function digestBlocks(markdown) {
  const lines = markdown.split("\n");
  const marked = headingLines(lines);
  const blocks = [];
  let buffer = [];
  const flush = () => {
    if (buffer.join("").trim()) blocks.push(h("div", { class: "digest-text" }, linkify(buffer.join("\n").trim())));
    buffer = [];
  };
  lines.forEach((line, i) => {
    if (i === 0 && line.startsWith("# moorestech プレイテスト日次ダイジェスト")) return;
    if (!marked.has(i)) { buffer.push(line); return; }
    flush();
    blocks.push(h("h2", { class: "digest-heading" }, line.slice(3)));
  });
  flush();
  return blocks;
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
  body.replaceChildren(h("p", { class: "muted small" }, "本文はそのまま表示しています。投入コマンドは報告詳細画面からコピーしてください"),
    ...digestBlocks(markdown));
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
