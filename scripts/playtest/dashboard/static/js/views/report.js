// 報告詳細: 説明全文・スクショ・動画・ビルド情報・投入コマンド
// Report detail: full text, screenshot, video, build info and the enqueue command
import {
  emptyNote, fmtDateTime, h, kindBadge, linkify, mediaUrl, routeHref, runBadge, runStatusLabel, section, testerName, triageBadge,
} from "../core.js";

const REPO_URL = "https://github.com/moorestech/moorestech";

export function renderReport(data, args) {
  const index = data.reports.findIndex((r) => r.boxSteamId === args[0] && r.boxId === args[1]);
  if (index < 0) return h("div", { class: "view" }, emptyNote("この報告は見つかりません"), backLink());
  const report = data.reports[index];
  return h("div", { class: "view" },
    h("div", { class: "detail-nav" }, backLink(),
      neighbour(data.reports[index - 1], "← 新しい報告"), neighbour(data.reports[index + 1], "古い報告 →")),
    h("header", { class: "detail-head" },
      h("div", { class: "line-meta" }, kindBadge(report.kind), triageBadge(report), runBadge(report)),
      h("h1", null, `${fmtDateTime(report.readyAt)}・${testerName(report)}`),
      h("p", { class: "description" }, linkify((report.description || "").trim() || "（説明文が空）")),
      report.problem ? h("p", { class: "warn" }, `⚠ ${report.problem}`) : null),
    enqueueBlock(report),
    h("div", { class: "grid-2" }, mediaBlock(report), section("情報", infoTable(report))));
}

function backLink() {
  return h("a", { href: routeHref("reports") }, "← 報告一覧");
}

function neighbour(report, label) {
  return report ? h("a", { href: routeHref("report", [report.boxSteamId, report.boxId]) }, label) : h("span");
}

// 投入の判断は人が持つ（ADR 0061）。画面は実行せず、貼れるコマンドを出すだけにする
// Enqueueing stays a human decision (ADR 0061); the page only offers a pasteable command
function enqueueBlock(report) {
  if (report.triage !== "candidate") return null;
  const command = `scripts/playtest/enqueue-autofix.sh ${shellQuote(report.boxSteamId)} ${shellQuote(report.boxId)}`;
  const button = h("button", { type: "button", onclick: () => copy(command, button) }, "コピー");
  return section("自動修正ランへ投入", h("div", { class: "command" }, h("code", null, command), button));
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
  return section("記録", ...items, links.length ? h("p", { class: "file-links" }, links) : null);
}

function infoTable(report) {
  const rows = [
    ["テスター", report.profileUrl ? externalLink(report.profileUrl, testerName(report)) : testerName(report)],
    ["SteamID", report.steamId],
    ["報告ID", report.id],
    ["送信", fmtDateTime(report.createdAt || report.readyAt)],
    ["ビルド", report.buildLabel || "不明"],
    ["コミット", report.commit ? externalLink(`${REPO_URL}/commit/${report.commit}`, report.commit.slice(0, 10)) : "不明"],
    ["プラットフォーム", report.platform || "不明"],
    ["UI状態", report.uiState || "不明"],
    ["報告時tick", report.reportTick ?? "不明"],
    ["動画の長さ", report.videoSeconds ? `${report.videoSeconds.toFixed(0)}秒` : "—"],
    ["遠隔実行", report.remoteExecReason ? `${report.remoteExec}（${report.remoteExecReason}）` : (report.remoteExec || "不明")],
    ["修正ラン", runCell(report)],
  ];
  return h("dl", { class: "info" }, rows.map(([label, value]) => [h("dt", null, label), h("dd", null, value)]));
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
