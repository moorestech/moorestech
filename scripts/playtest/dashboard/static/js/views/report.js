// 報告詳細: 説明全文・スクショ・動画・ビルド情報・投入コマンド・既読と関連チケット
// Report detail: full text, screenshot, video, build info, enqueue command, read mark and related tickets
import { card, kindMark, runStatusLabel, statusText } from "../components.js";
import { emptyNote, fmtDateTime, h, linkify, mediaUrl, routeHref, testerName } from "../core.js";
import { detailActions, ticketsCard } from "./report-actions.js";

const REPO_URL = "https://github.com/moorestech/moorestech";

export function renderReport(data, args) {
  const index = data.reports.findIndex((r) => r.boxSteamId === args[0] && r.boxId === args[1]);
  if (index < 0) return h("div", { class: "view" }, backLink(), emptyNote("この報告は見つかりません"));
  const report = data.reports[index];
  return h("div", { class: "view" },
    h("nav", { class: "detail-nav" }, backLink(), h("span", { class: "pager" },
      neighbour(data.reports[index - 1], "‹ 新しい報告"), neighbour(data.reports[index + 1], "古い報告 ›"))),
    h("header", { class: "detail-head" },
      h("div", { class: "detail-tags" }, kindMark(report.kind), statusText(report)),
      h("h1", { class: "detail-title" }, linkify((report.description || "").trim() || "（説明文が空）")),
      h("p", { class: "detail-meta" }, `${testerName(report)}・${fmtDateTime(report.readyAt)}・${report.buildLabel || "ビルド不明"}`),
      report.problem ? h("p", { class: "warn" }, `⚠ ${report.problem}`) : null,
      detailActions(report)),
    enqueueBlock(report),
    h("div", { class: "layout-main-side" }, mediaBlock(report),
      h("div", { class: "side-stack" }, ticketsCard(report), card("詳細", null, infoGroups(report)))));
}

function backLink() {
  return h("a", { href: routeHref("reports") }, "← 報告一覧");
}

function neighbour(report, label) {
  return report ? h("a", { href: routeHref("report", [report.boxSteamId, report.boxId]) }, label) : h("span", { class: "muted" }, label);
}

// 投入の判断は人が持つ（ADR 0061）。画面は実行せず、貼れるコマンドを出すだけにする
// Enqueueing stays a human decision (ADR 0061); the page only offers a pasteable command
function enqueueBlock(report) {
  if (report.triage !== "candidate") return null;
  const command = `scripts/playtest/enqueue-autofix.sh ${shellQuote(report.boxSteamId)} ${shellQuote(report.boxId)}`;
  const button = h("button", { type: "button", class: "primary", onclick: () => copy(command, button) }, "コピー");
  return h("div", { class: "callout" },
    h("p", null, h("b", null, "未投入"), "　自動修正ランへ回すなら、Mac mini の moorestech で次を実行"),
    h("div", { class: "command" }, h("code", null, command), button));
}

function shellQuote(value) {
  return /^[A-Za-z0-9_.-]+$/.test(value) ? value : `'${value.replaceAll("'", "'\\''")}'`;
}

function copy(text, button) {
  navigator.clipboard.writeText(text).then(
    () => { button.textContent = "コピーしました"; },
    (error) => { button.textContent = "コピー失敗"; console.error("[dashboard] clipboard", error); });
}

function mediaBlock(report) {
  const items = [];
  if (report.media.includes("video.mp4")) {
    items.push(h("video", { src: mediaUrl(report, "video.mp4"), controls: true, preload: "metadata" }));
  }
  if (report.media.includes("screenshot.png")) {
    const url = mediaUrl(report, "screenshot.png");
    items.push(h("a", { href: url, target: "_blank", rel: "noopener" }, h("img", { src: url, alt: "報告時のスクリーンショット", loading: "lazy" })));
  }
  if (items.length === 0) items.push(emptyNote("スクリーンショット・動画はありません（テスターが送信を見送った可能性）"));
  const links = ["logs/unity.log", "manifest.json"].filter((name) => report.media.includes(name))
    .map((name) => h("a", { href: mediaUrl(report, name), target: "_blank", rel: "noopener" }, name));
  return card("記録", { action: links.length ? h("span", { class: "file-links" }, links) : null }, h("div", { class: "media-list" }, items));
}

// 詳細は「誰が・どのビルドで・どんな状態で・どう扱われたか」の4群に分けて並べる
// Details are grouped as who, which build, what state, and how it was handled
function infoGroups(report) {
  const groups = [
    ["テスター", [
      ["名前", report.profileUrl ? externalLink(report.profileUrl, testerName(report)) : testerName(report)],
      ["SteamID", report.steamId]]],
    ["ビルド", [
      ["ラベル", report.buildLabel || "不明"],
      ["コミット", report.commit ? externalLink(`${REPO_URL}/commit/${report.commit}`, report.commit.slice(0, 10)) : "不明"],
      ["環境", report.platform || "不明"]]],
    ["報告時の状態", [
      ["送信", fmtDateTime(report.createdAt || report.readyAt)],
      ["画面", report.uiState || "不明"],
      ["tick", report.reportTick ?? "不明"],
      ["録画", report.videoSeconds ? `${report.videoSeconds.toFixed(0)}秒` : "なし"]]],
    ["取り扱い", [
      ["遠隔実行", report.remoteExecReason ? `${report.remoteExec}（${report.remoteExecReason}）` : (report.remoteExec || "不明")],
      ["修正ラン", runCell(report)],
      ["報告ID", h("code", null, report.id)]]],
  ];
  return h("div", { class: "info-groups" }, groups.map(([title, rows]) => h("div", { class: "info-group" },
    h("h3", null, title),
    h("dl", { class: "info" }, rows.map(([label, value]) => [h("dt", null, label), h("dd", null, value)])))));
}

function runCell(report) {
  if (!report.queued) return "未投入";
  if (!report.run) return "投入済み（inbox で待機中）";
  const pr = report.run.prNumber ? externalLink(`${REPO_URL}/pull/${report.run.prNumber}`, `#${report.run.prNumber}`) : "PRなし";
  return h("span", null, `${runStatusLabel(report.run.status)}・`, pr, report.run.summary ? `・${report.run.summary}` : "");
}

// 取り込みが書いた URL でも https 以外はリンクにしない（javascript: 等を踏ませない）
// Even ingest-written URLs are only linked when https, so javascript: and the like never become clickable
function externalLink(href, label) {
  if (!/^https:\/\//.test(href)) return label;
  return h("a", { href, target: "_blank", rel: "noopener noreferrer" }, label);
}
