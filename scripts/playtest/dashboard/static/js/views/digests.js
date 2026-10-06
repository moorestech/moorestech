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

async function loadDigest(date, body) {
  const response = await fetch(`api/digest/${date}`);
  if (!response.ok) {
    console.error(`[dashboard] digest ${date}: HTTP ${response.status}`);
    body.replaceChildren(emptyNote(`読み込みに失敗しました（HTTP ${response.status}）`));
    return;
  }
  const { markdown } = await response.json();
  body.replaceChildren(...renderMarkdown(markdown));
}

// ダイジェストが使う書式（見出し・箇条書き・字下げしたコード行・インラインコード）だけを DOM へ起こす
// Renders only the syntax the digest uses (headings, bullets, indented command lines, inline code) into DOM
function renderMarkdown(markdown) {
  const nodes = [];
  let list = null;
  for (const line of markdown.split("\n")) {
    const heading = /^(#{1,3}) (.*)$/.exec(line);
    if (heading) {
      list = null;
      nodes.push(h(`h${heading[1].length + 1}`, null, inline(heading[2])));
    } else if (line.startsWith("- ")) {
      if (!list) nodes.push(list = h("ul", null));
      list.append(h("li", null, inline(line.slice(2))));
    } else if (/^ {2}`.*`$/.test(line) && list?.lastChild) {
      list.lastChild.append(commandLine(line.trim().slice(1, -1)));
    } else if (line.trim()) {
      list = null;
      nodes.push(h("p", null, inline(line)));
    }
  }
  return nodes;
}

function inline(text) {
  return text.split(/(`[^`]+`)/).map((part) =>
    (part.startsWith("`") && part.endsWith("`") && part.length > 1 ? h("code", null, part.slice(1, -1)) : linkify(part)));
}

function commandLine(command) {
  const button = h("button", {
    type: "button",
    onclick: () => navigator.clipboard.writeText(command).then(
      () => { button.textContent = "コピーしました"; },
      (error) => { button.textContent = "コピー失敗"; console.error("[dashboard] clipboard", error); }),
  }, "コピー");
  return h("div", { class: "command" }, h("code", null, command), button);
}
