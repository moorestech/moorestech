// 日次ダイジェスト: digests/<日付>.md を切り詰めなしで読む
// Daily digests: reads digests/<date>.md in full, untruncated
import { dayLabel, pageHead } from "../components.js";
import { emptyNote, h, linkify, routeHref } from "../core.js";

// digest.py が出す固定の見出しと順序。全部がちょうど1回ずつこの順で現れた時だけ見出しとして描く。
// テスター由来の値（感想・kind・endReason 等）に同じ行が紛れ込むと回数か順序が崩れるので、その日は見出し無しの素の文章に落とす。
// The fixed headings digest.py emits, in order. They are drawn as headings only when each appears exactly once in this order;
// a copy smuggled in through tester values (feedback, kind, endReason, ...) breaks the count or order, so that day falls back to plain text.
const DIGEST_HEADINGS = ["## プレイ報告の件数", "## 投入候補のバグ報告", "## 感想（全文）", "## 進行記録", "## 自動修正ラン"];

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
  const positions = DIGEST_HEADINGS.map((heading) => lines.indexOf(heading));
  const unique = DIGEST_HEADINGS.every((heading, i) => positions[i] >= 0 && lines.lastIndexOf(heading) === positions[i]);
  const ordered = positions.every((position, i) => i === 0 || position > positions[i - 1]);
  if (unique && ordered) return new Set(positions);
  console.warn("[dashboard] ダイジェストの見出しが想定の回数・順序でないため、見出し無しの素の文章で表示する");
  return new Set();
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

// テスター由来の感想全文が混ざるため Markdown として解釈せず、https URL だけリンクにした素の文章で出す（行頭の「- 」やコード行を解釈すると、本物そっくりのコマンドを偽装できる）
// Tester feedback is embedded verbatim, so the text is never parsed as Markdown and only https URLs become links (parsing "- " or code lines would let it forge genuine-looking commands)
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
