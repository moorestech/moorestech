import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import { setDictionaries } from "@/shared/i18n/i18nStore";

const mocks = vi.hoisted(() => ({ dispatchAction: vi.fn(async () => true), emitToast: vi.fn() }));

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  dispatchAction: mocks.dispatchAction,
}));
vi.mock("@/features/toast", () => ({ emitToast: mocks.emitToast }));
vi.mock("@/shared/ui", () => ({
  PanelActionButton: ({ children, onClick, testId }: { children: unknown; onClick: () => void; testId?: string }) =>
    createElement("mock-button", { onClick, "data-testid": testId }, children as never),
}));

import { BugReportForm } from "./BugReportForm";

const dictionary = {
  "ui.bugReport.placeholder": "何が起きた？",
  "ui.bugReport.send": "バグ報告を送信",
  "ui.bugReport.sent": "書き出しました",
  "ui.bugReport.capturePending": "記録を確保しています…",
  "ui.bugReport.missing": "欠けている項目: {items}",
};

afterEach(() => {
  vi.clearAllMocks();
});

describe("BugReportForm", () => {
  it("記述欄と送信ボタンを描く", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ hasSession: true, capturePending: false, missing: [] });
    expect(renderer.root.findByProps({ "data-testid": "bug-report-description" })).toBeTruthy();
    expect(renderer.root.findByProps({ "data-testid": "bug-report-send" })).toBeTruthy();
    act(() => renderer.unmount());
  });

  it("入力後の送信で bug_report.submit を説明文付きで送りトーストを出す", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const onSent = vi.fn();
    const renderer = await render({ hasSession: true, capturePending: false, missing: [] }, onSent);
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));
    await act(async () => renderer.root.findByProps({ "data-testid": "bug-report-send" }).props.onClick());
    expect(mocks.dispatchAction).toHaveBeenCalledWith("bug_report.submit", { description: "ベルトが止まる" });
    expect(mocks.emitToast).toHaveBeenCalledWith("書き出しました", "info");
    expect(onSent).toHaveBeenCalled();
    act(() => renderer.unmount());
  });

  it("空文字では送らない", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ hasSession: true, capturePending: false, missing: [] });
    await act(async () => renderer.root.findByProps({ "data-testid": "bug-report-send" }).props.onClick());
    expect(mocks.dispatchAction).not.toHaveBeenCalled();
    act(() => renderer.unmount());
  });

  it("確保中と欠損の文言を出す", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ hasSession: true, capturePending: true, missing: ["video", "screenshot"] });
    const texts = renderer.root.findAllByProps({ "data-testid": "bug-report-status" }).map((n) => n.children.join(""));
    expect(texts.some((t) => t.includes("記録を確保しています…"))).toBe(true);
    expect(texts.some((t) => t.includes("欠けている項目: video, screenshot"))).toBe(true);
    act(() => renderer.unmount());
  });
});

async function render(status: { hasSession: boolean; capturePending: boolean; missing: string[] }, onSent = vi.fn()): Promise<ReactTestRenderer> {
  let renderer: ReactTestRenderer;
  await act(async () => {
    renderer = create(createElement(BugReportForm, { status, onSent }));
  });
  return renderer!;
}
