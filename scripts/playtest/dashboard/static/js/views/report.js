// 報告詳細: 説明全文・スクショ・動画・ビルド情報・生データの取得コマンド・既読と関連チケット
// Report detail: full text, screenshot, video, build info, raw-data fetch command, read mark and related tickets
import { card, kindMark, statusText } from "../components.js";
import { emptyNote, findReport, fmtDateTime, h, linkify, mediaUrl, routeHref, testerName } from "../core.js";
import { filterOf, loadFilter, neighbours } from "../report-filter.js";
import { detailActions, ticketsCard } from "./report-actions.js";

const REPO_URL = "https://github.com/moorestech/moorestech";

export function renderReport(data, args, params) {
  const report = findReport(data.reports, args[0], args[1]);
  if (!report) return h("div", { class: "view" }, backLink(params), emptyNote("この報告は見つかりません"));
  return h("div", { class: "view" },
    detailNav(data, report, params),
    h("header", { class: "detail-head" },
      h("div", { class: "detail-tags" }, kindMark(report.kind), statusText(report)),
      h("h1", { class: "detail-title" }, linkify((report.description || "").trim() || "（説明文が空）")),
      h("p", { class: "detail-meta" }, `${testerName(report)}・${fmtDateTime(report.readyAt)}・${report.buildLabel || "ビルド不明"}`),
      report.problem ? h("p", { class: "warn" }, `⚠ ${report.problem}`) : null,
      detailActions(report)),
    fetchBlock(report),
    h("div", { class: "layout-main-side" }, mediaBlock(report),
      h("div", { class: "side-stack" }, ticketsCard(report), card("詳細", null, infoGroups(report)))));
}

// 前後は URL の条件で辿り、一覧へは条件がなければ最後の記憶で戻る
// Page within the URL condition, but return to the last saved list when it has none
export function detailNav(data, report, params) {
  const filter = filterOf(params);
  const { newer, older } = neighbours(data.reports, report, params);
  return h("nav", { class: "detail-nav" }, backLink(params), h("span", { class: "pager" },
    neighbour(newer, "‹ 新しい報告", filter), neighbour(older, "古い報告 ›", filter)));
}

function backLink(params) {
  const filter = filterOf(params);
  const destination = Object.keys(filter).length ? filter : loadFilter();
  return h("a", { href: routeHref("reports", [], destination) }, "← 報告一覧");
}

function neighbour(report, label, filter) {
  return report ? h("a", { href: routeHref("report", [report.boxSteamId, report.boxId], filter) }, label) : h("span", { class: "muted" }, label);
}

// 生データの正本は R2 だけ（ADR 0061）。調査でワールド・スナップショットが要るときに貼れるコマンドを出す
// R2 is the only home of raw data (ADR 0061); offer a pasteable command for when an investigation needs the world or snapshots
function fetchBlock(report) {
  if (report.triage !== "bug") return null;
  const command = `scripts/playtest/fetch-report.sh ${shellQuote(report.boxSteamId)} ${shellQuote(report.boxId)}`;
  const button = h("button", { type: "button", class: "primary", onclick: () => copy(command, button) }, "コピー");
  return h("div", { class: "callout" },
    h("p", null, h("b", null, "生データ"), "　ワールド・スナップショットは R2 にだけある。調べるなら Mac mini の moorestech で次を実行"),
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
  // 動画は R2 から中継する。片付けで消した箱は 410 になり再生できないので、そう読めるように添える
  // Video is relayed from R2; a box purged after settling answers 410 and cannot play, so say so next to it
  if (report.media.includes("video.mp4")) {
    items.push(h("p", { class: "muted small" }, "動画は R2 から取得（再生できないときは片付け後の削除か受け口の障害。理由はダッシュボードのログ）"));
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
      ["報告ID", h("code", null, report.id)]]],
  ];
  return h("div", { class: "info-groups" }, groups.map(([title, rows]) => h("div", { class: "info-group" },
    h("h3", null, title),
    h("dl", { class: "info" }, rows.map(([label, value]) => [h("dt", null, label), h("dd", null, value)])))));
}

// 取り込みが書いた URL でも https 以外はリンクにしない（javascript: 等を踏ませない）
// Even ingest-written URLs are only linked when https, so javascript: and the like never become clickable
function externalLink(href, label) {
  if (!/^https:\/\//.test(href)) return label;
  return h("a", { href, target: "_blank", rel: "noopener noreferrer" }, label);
}
