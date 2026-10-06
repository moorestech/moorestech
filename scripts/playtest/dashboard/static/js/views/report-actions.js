// 報告詳細で人が操作する部品: 既読の切り替え・ビューワー URL のコピー・関連チケットの紐付け
// Human actions on the report detail: read toggle, copying the viewer URL, and linking related tickets
// 詳細画面は動画再生中かもしれないので、操作後は画面全体を描き直さず、サーバーの保存結果を引き直して自分の部品だけを直す
// The detail page may be playing a video, so after an action it re-reads what the server saved and redraws only its own parts
import { card } from "../components.js";
import { emptyNote, fmtDateTime, fetchSavedReport, h, postState, reportKey } from "../core.js";

const VIEWER_BASE = "https://review.moores.tech/playtest/#/report";

export function detailActions(report) {
  const readButton = h("button", { type: "button" });
  const paintRead = () => {
    readButton.textContent = report.readAt ? "既読 ✓（未読に戻す）" : "既読にする";
    readButton.classList.toggle("is-on", Boolean(report.readAt));
  };
  readButton.addEventListener("click", async () => {
    if (await postState("read", { items: [reportKey(report)], read: !report.readAt }, false)) await syncFromServer(report, paintRead);
  });
  paintRead();
  const copyButton = h("button", { type: "button", onclick: () => copyViewerUrl(report, copyButton) }, "ビューワーのURLをコピー");
  return h("div", { class: "detail-actions" }, readButton, copyButton);
}

// 保存結果の既読とリンクを、画面が持っている報告オブジェクトへ写してから描き直す
// Copies the saved read mark and links onto the page's report object, then repaints
async function syncFromServer(report, repaint) {
  const saved = await fetchSavedReport(report);
  if (saved === null) return;
  report.readAt = saved.readAt;
  report.links = saved.links;
  repaint();
}

export function viewerUrl(report) {
  return `${VIEWER_BASE}/${encodeURIComponent(report.boxSteamId)}/${encodeURIComponent(report.boxId)}`;
}

function copyViewerUrl(report, button) {
  navigator.clipboard.writeText(viewerUrl(report)).then(
    () => { button.textContent = "コピーしました"; },
    (error) => { button.textContent = "コピー失敗"; console.error("[dashboard] clipboard", error); });
}

// チケット（Notion・GitHub 等）との紐付け。ここで追加するか、Mac mini で scripts/playtest/link-ticket.sh を叩く
// Links to tickets (Notion, GitHub, ...); add them here or run scripts/playtest/link-ticket.sh on the Mac mini
export function ticketsCard(report) {
  const list = h("div", null);
  const paint = () => list.replaceChildren(report.links.length
    ? h("ul", { class: "tickets" }, report.links.map((link) => ticketItem(report, link, paint)))
    : emptyNote("まだ紐付いたチケットはありません"));
  paint();
  return card("関連チケット", null, list, addForm(report, paint));
}

function ticketItem(report, link, paint) {
  const remove = h("button", {
    type: "button", class: "ghost", title: "紐付けを外す", "aria-label": "紐付けを外す",
    onclick: async () => {
      // 失敗（別経路で既に外された 404 等）でも保存結果に揃え、外せない表示を残さない
      // Even on failure (e.g. 404 when already removed elsewhere) the list is synced to storage, so no stale entry remains
      await postState("links/remove", { ...reportKey(report), url: link.url }, false);
      await syncFromServer(report, paint);
    },
  }, "外す");
  const href = /^https:\/\//.test(link.url) ? link.url : null;
  return h("li", null,
    h("div", { class: "ticket-text" },
      href ? h("a", { href, target: "_blank", rel: "noopener noreferrer" }, link.title || link.url) : (link.title || link.url),
      h("span", { class: "muted small" }, fmtDateTime(link.addedAt))),
    remove);
}

function addForm(report, paint) {
  const url = h("input", { type: "url", placeholder: "https://www.notion.so/...", "aria-label": "チケットのURL", required: true });
  const title = h("input", { type: "text", placeholder: "チケット名（任意）", "aria-label": "チケット名", maxlength: "200" });
  const form = h("form", { class: "ticket-form" }, url, title, h("button", { type: "submit" }, "紐付け"));
  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    const entry = { url: url.value.trim(), title: title.value.trim() };
    if (await postState("links/add", { ...reportKey(report), ...entry }, false)) {
      url.value = "";
      title.value = "";
      await syncFromServer(report, paint);
    }
  });
  return form;
}
