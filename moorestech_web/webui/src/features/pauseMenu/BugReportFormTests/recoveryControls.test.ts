import { act } from "react-test-renderer";
import { expect, it } from "vitest";
import type { PauseMenuData } from "@/bridge";
import { getMocks, loadProductionDictionaries, renderDelivery } from "./testSupport";

const mocks = getMocks();

const failed: PauseMenuData["delivery"] = { kind: "pending", inventory: { kind: "known", pendingCount: 1, failedCount: 16 } };
const pendingOnly: PauseMenuData["delivery"] = { kind: "pending", inventory: { kind: "known", pendingCount: 2, failedCount: 0 } };
const idle: PauseMenuData["export"] = { kind: "idle" };

const byTestId = (r: ReturnType<typeof renderDelivery> extends Promise<infer T> ? T : never, id: string) =>
  r.root.findAll((n) => n.props["data-testid"] === id);

it("送信できない報告があるときだけ再送ボタンを出し、押すと action を出す", async () => {
  const r = await renderDelivery({ kind: "ready", missing: [] }, failed, idle);
  const resend = byTestId(r, "bug-report-resend-failed")[0];
  await act(async () => resend.props.onClick());
  expect(mocks.dispatchAction).toHaveBeenCalledWith("bug_report.resend_failed", {});
  act(() => r.unmount());
});

it("送信待ちだけなら再送は出さず書き出しは出す", async () => {
  const r = await renderDelivery({ kind: "ready", missing: [] }, pendingOnly, idle);
  expect(byTestId(r, "bug-report-resend-failed")).toHaveLength(0);
  const exportButton = byTestId(r, "bug-report-export-unsent")[0];
  await act(async () => exportButton.props.onClick());
  expect(mocks.dispatchAction).toHaveBeenCalledWith("bug_report.export_unsent", {});
  act(() => r.unmount());
});

it("すべて送信済みなら両方とも出さない", async () => {
  const r = await renderDelivery({ kind: "ready", missing: [] }, { kind: "allSent" }, idle);
  expect(byTestId(r, "bug-report-resend-failed")).toHaveLength(0);
  expect(byTestId(r, "bug-report-export-unsent")).toHaveLength(0);
  act(() => r.unmount());
});

it("書き出し中はボタンを押せない", async () => {
  const r = await renderDelivery({ kind: "ready", missing: [] }, failed, { kind: "running" });
  expect(byTestId(r, "bug-report-export-unsent")[0].props.disabled).toBe(true);
  act(() => r.unmount());
});

it.each(["japanese", "english", "german", "korean"])("%s辞書でボタン2つと書き出し中の文言が未翻訳キーにならない", async (locale) => {
  const r = await renderDelivery({ kind: "ready", missing: [] }, failed, { kind: "running" });
  await act(async () => loadProductionDictionaries(locale));
  for (const id of ["bug-report-resend-failed", "bug-report-export-unsent", "bug-report-export-status"]) {
    const text = byTestId(r, id)[0].children.join("");
    expect(text.length).toBeGreaterThan(0);
    expect(text).not.toContain("ui.bugReport.");
  }
  act(() => r.unmount());
});

it.each(["japanese", "english", "german", "korean"])("%s辞書で完了・欠落・開けなかった案内・失敗の文言が未翻訳キーにならない", async (locale) => {
  const done: PauseMenuData["export"] = { kind: "done", zipPath: "/exports/unsent-reports.zip", boxCount: 17, missing: [{ item: "reports/x/video.mp4", reason: "IOException" }], revealed: false };
  const r = await renderDelivery({ kind: "ready", missing: [] }, failed, done);
  await act(async () => loadProductionDictionaries(locale));
  const text = byTestId(r, "bug-report-export-status")[0].children.join("");
  expect(text).toContain("/exports/unsent-reports.zip");
  expect(text).toContain("17");
  expect(text).toContain("reports/x/video.mp4");
  expect(text).toContain("IOException");
  expect(text).not.toContain("ui.bugReport.");
  act(() => r.unmount());

  const failedRender = await renderDelivery({ kind: "ready", missing: [] }, failed, { kind: "failed", reason: "desktop folder unavailable" });
  await act(async () => loadProductionDictionaries(locale));
  const failedText = byTestId(failedRender, "bug-report-export-status")[0].children.join("");
  expect(failedText).toContain("desktop folder unavailable");
  expect(failedText).not.toContain("ui.bugReport.");
  act(() => failedRender.unmount());
});
