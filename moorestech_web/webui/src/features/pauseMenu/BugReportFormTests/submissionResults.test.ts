import { act } from "react-test-renderer";
import { describe, expect, it } from "vitest";
import { getMocks, modeSwitch, render, sendButton } from "./testSupport";

const mocks = getMocks();

describe("BugReportForm submission results", () => {
  it("入力後の送信で bug_report.submit を説明文付きで送りトーストを出す", async () => {
    const renderer = await render({ kind: "ready", missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));
    expect(sendButton(renderer).props.disabled).toBe(false);
    await act(async () => sendButton(renderer).props.onClick());
    expect(mocks.dispatchAction).toHaveBeenCalledWith("bug_report.submit", { description: "ベルトが止まる", kind: "bug" });
    expect(mocks.emitToast).toHaveBeenCalledWith("書き出しました", "info");
    expect(textarea.props.value).toBe("");
    act(() => renderer.unmount());
  });

  // 送信後の画面遷移はC#の送信ハンドラ1本が持つ。Web側からも遷移させると同じ判断が2箇所に増える
  // The one C# submit handler owns the page move after a send; moving from the Web too would put the same decision in two places
  it("送信後に画面遷移のactionは出さない", async () => {
    const renderer = await render({ kind: "ready", missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));
    await act(async () => sendButton(renderer).props.onClick());
    expect(mocks.dispatchAction).toHaveBeenCalledTimes(1);
    expect(mocks.dispatchAction).not.toHaveBeenCalledWith("ui_state.request", expect.anything());
    expect(mocks.dispatchAction).not.toHaveBeenCalledWith("pause_menu.show_page", expect.anything());
    act(() => renderer.unmount());
  });

  // 書き出し中の二度押しは同じ確保から2箱を作り、同じ報告のdraft PRが2本出る
  // A second click while writing makes two boxes from one capture and raises two draft PRs for one report
  it("書き出し中は二度目の送信を受け付けない", async () => {
    let resolveSubmit: (ok: boolean) => void = () => {};
    mocks.dispatchAction.mockImplementationOnce(() => new Promise((resolve) => {
      resolveSubmit = (ok) => resolve(ok);
    }));
    const renderer = await render({ kind: "ready", missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));

    act(() => { void sendButton(renderer).props.onClick(); });
    expect(sendButton(renderer).props.disabled).toBe(true);
    await act(async () => sendButton(renderer).props.onClick());
    expect(mocks.dispatchAction).toHaveBeenCalledTimes(1);

    await act(async () => { resolveSubmit(true); });
    expect(mocks.dispatchAction).toHaveBeenCalledWith("bug_report.submit", { description: "ベルトが止まる", kind: "bug" });
    act(() => renderer.unmount());
  });

  // 押した時点の欠損を表示し、書き出し中の欠損はmanifestに残す
  // Show gaps present at click time; later gaps remain in the manifest
  it("確保済みの欠損を成功トーストに出す", async () => {
    let resolveSubmit: (ok: boolean) => void = () => {};
    mocks.dispatchAction.mockImplementationOnce(() => new Promise((resolve) => {
      resolveSubmit = resolve;
    }));
    const status = { kind: "ready" as const, missing: ["video"] };
    const renderer = await render(status);
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));
    act(() => { void sendButton(renderer).props.onClick(); });
    status.missing[0] = "screenshot";
    await act(async () => { resolveSubmit(true); });
    expect(mocks.emitToast).toHaveBeenCalledWith("欠けている項目: video", "error");
    expect(mocks.emitToast).not.toHaveBeenCalledWith("欠けている項目: screenshot", "error");
    act(() => renderer.unmount());
  });

  // 種別を残すと、感想を1件送った次のバグ報告が feedback のまま箱詰めされる
  // Leaving the kind boxes the bug report that follows a feedback submission as feedback
  it("送信成功後は種別が既定のバグへ戻る", async () => {
    const renderer = await render({ kind: "ready", missing: [] });
    act(() => modeSwitch(renderer).props.onChange("feedback"));
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "序盤が長い" } }));
    await act(async () => sendButton(renderer).props.onClick());

    expect(modeSwitch(renderer).props.value).toBe("bug");
    act(() => renderer.unmount());
  });

  it("感想へ切り替えるとkindがfeedbackになる", async () => {
    const renderer = await render({ kind: "ready", missing: [] });
    act(() => modeSwitch(renderer).props.onChange("feedback"));
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "序盤が長い" } }));
    await act(async () => sendButton(renderer).props.onClick());
    expect(mocks.dispatchAction).toHaveBeenCalledWith("bug_report.submit", { description: "序盤が長い", kind: "feedback" });
    act(() => renderer.unmount());
  });
});
