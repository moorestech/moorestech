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
  PanelActionButton: ({ children, onClick, disabled, testId }: { children: unknown; onClick: () => void; disabled?: boolean; testId?: string }) =>
    createElement("mock-button", { onClick, "data-disabled": disabled || undefined, "data-testid": testId }, children as never),
}));

import { BugReportForm } from "./BugReportForm";

const dictionary = {
  "ui.bugReport.placeholder": "何が起きた？",
  "ui.bugReport.send": "バグ報告を送信",
  "ui.bugReport.sent": "書き出しました",
  "ui.bugReport.capturePending": "記録を確保しています…",
  "ui.bugReport.missing": "欠けている項目: {items}",
  "ui.bugReport.noSession": "ポーズメニューを開き直してください",
  "ui.bugReport.sending": "書き出しています…",
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
    const renderer = await render({ hasSession: true, capturePending: false, missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));
    expect(renderer.root.findByProps({ "data-testid": "bug-report-send" }).props["data-disabled"]).toBeUndefined();
    await act(async () => renderer.root.findByProps({ "data-testid": "bug-report-send" }).props.onClick());
    expect(mocks.dispatchAction).toHaveBeenCalledWith("bug_report.submit", { description: "ベルトが止まる" });
    expect(mocks.emitToast).toHaveBeenCalledWith("書き出しました", "info");
    expect(textarea.props.value).toBe("");

    // 閉じは既存のWeb境界1本へ寄せる。別経路で閉じると現stateの検査を素通りする
    // Closing goes through the one existing web boundary; another path would bypass the current-state checks
    expect(mocks.dispatchAction).toHaveBeenCalledWith("ui_state.request", { state: "GameScreen" });
    act(() => renderer.unmount());
  });

  // 確保が終わる前に送れると、世界データもパケットログも無い箱が「送信しました」として運搬される
  // Sending before the capture settles ships a box with no world data or packet log as a successful report
  it("確保中は送信ボタンを押せない", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ hasSession: true, capturePending: true, missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));
    const button = renderer.root.findByProps({ "data-testid": "bug-report-send" });
    expect(button.props["data-disabled"]).toBe(true);
    await act(async () => button.props.onClick());
    expect(mocks.dispatchAction).not.toHaveBeenCalled();
    act(() => renderer.unmount());
  });

  it("確保セッションが無いときは押せず理由を出す", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ hasSession: false, capturePending: false, missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));
    const button = renderer.root.findByProps({ "data-testid": "bug-report-send" });
    expect(button.props["data-disabled"]).toBe(true);
    await act(async () => button.props.onClick());
    expect(mocks.dispatchAction).not.toHaveBeenCalled();
    const texts = renderer.root.findAllByProps({ "data-testid": "bug-report-status" }).map((n) => n.children.join(""));
    expect(texts.some((t) => t.includes("ポーズメニューを開き直してください"))).toBe(true);
    act(() => renderer.unmount());
  });

  // 書き出し中の二度押しは同じ確保から2箱を作り、同じ報告のdraft PRが2本出る
  // A second click while writing makes two boxes from one capture and raises two draft PRs for one report
  it("書き出し中は二度目の送信を受け付けない", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    let resolveSubmit: (ok: boolean) => void = () => {};
    mocks.dispatchAction.mockImplementationOnce(() => new Promise<boolean>((resolve) => { resolveSubmit = resolve; }));
    const renderer = await render({ hasSession: true, capturePending: false, missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));

    act(() => { void renderer.root.findByProps({ "data-testid": "bug-report-send" }).props.onClick(); });
    expect(renderer.root.findByProps({ "data-testid": "bug-report-send" }).props["data-disabled"]).toBe(true);
    await act(async () => renderer.root.findByProps({ "data-testid": "bug-report-send" }).props.onClick());
    expect(mocks.dispatchAction).toHaveBeenCalledTimes(1);

    await act(async () => { resolveSubmit(true); });
    expect(mocks.dispatchAction).toHaveBeenCalledWith("bug_report.submit", { description: "ベルトが止まる" });
    act(() => renderer.unmount());
  });

  // 書き出しで判明した欠損はC#が配り直す。成功トーストのままだと欠けたことを知る機会が無い
  // C# republishes the missing items found while writing; a plain success toast would hide the gap
  it("欠損付きで書けたときは成功トーストと別の文言を出す", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ hasSession: true, capturePending: false, missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));

    // 送信中にC#が欠損付きの確保状態を配り直す
    // C# republishes the capture state with missing items while the send is in flight
    mocks.dispatchAction.mockImplementationOnce(async () => {
      await act(async () => {
        renderer.update(createElement(BugReportForm, { status: { hasSession: true, capturePending: false, missing: ["video"] } }));
      });
      return true;
    });
    await act(async () => renderer.root.findByProps({ "data-testid": "bug-report-send" }).props.onClick());

    expect(mocks.emitToast).toHaveBeenCalledWith("欠けている項目: video", "error");
    expect(mocks.emitToast).not.toHaveBeenCalledWith("書き出しました", "info");
    act(() => renderer.unmount());
  });

  it("空文字では送信ボタンをdata-disabledにして送らない", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ hasSession: true, capturePending: false, missing: [] });
    const button = renderer.root.findByProps({ "data-testid": "bug-report-send" });
    expect(button.props["data-disabled"]).toBe(true);
    await act(async () => button.props.onClick());
    expect(mocks.dispatchAction).not.toHaveBeenCalled();
    act(() => renderer.unmount());
  });

  it("空白だけの記述も無効として扱う", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ hasSession: true, capturePending: false, missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "   " } }));
    expect(renderer.root.findByProps({ "data-testid": "bug-report-send" }).props["data-disabled"]).toBe(true);
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

async function render(status: { hasSession: boolean; capturePending: boolean; missing: string[] }): Promise<ReactTestRenderer> {
  let renderer: ReactTestRenderer;
  await act(async () => {
    renderer = create(createElement(BugReportForm, { status }));
  });
  return renderer!;
}
