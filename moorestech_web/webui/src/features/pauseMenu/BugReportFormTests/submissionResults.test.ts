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
    expect(mocks.dispatchActionOutcome).toHaveBeenCalledWith("bug_report.submit", { description: "ベルトが止まる", kind: "bug" });
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
    expect(mocks.dispatchActionOutcome).toHaveBeenCalledTimes(1);
    expect(mocks.dispatchActionOutcome).not.toHaveBeenCalledWith("ui_state.request", expect.anything());
    expect(mocks.dispatchActionOutcome).not.toHaveBeenCalledWith("pause_menu.show_page", expect.anything());
    act(() => renderer.unmount());
  });

  // 書き出し中の二度押しは同じ確保から2箱を作り、同じ報告のdraft PRが2本出る
  // A second click while writing makes two boxes from one capture and raises two draft PRs for one report
  it("書き出し中は二度目の送信を受け付けない", async () => {
    let resolveSubmit: (ok: boolean) => void = () => {};
    mocks.dispatchActionOutcome.mockImplementationOnce(() => new Promise((resolve) => {
      resolveSubmit = (ok) => resolve(ok ? { kind: "accepted", payload: { missing: [] } } : { kind: "rejected", error: "failed" });
    }));
    const renderer = await render({ kind: "ready", missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));

    act(() => { void sendButton(renderer).props.onClick(); });
    expect(sendButton(renderer).props.disabled).toBe(true);
    await act(async () => sendButton(renderer).props.onClick());
    expect(mocks.dispatchActionOutcome).toHaveBeenCalledTimes(1);

    await act(async () => { resolveSubmit(true); });
    expect(mocks.dispatchActionOutcome).toHaveBeenCalledWith("bug_report.submit", { description: "ベルトが止まる", kind: "bug" });
    act(() => renderer.unmount());
  });

  // 送信成功直後は次の記録へ再確保されるため、送った箱の欠損はaction結果から読む
  // A successful send immediately recaptures the next records, so read the sent bundle's gaps from the action result
  it("欠損付きで書けたときは成功トーストと別の文言を出す", async () => {
    const renderer = await render({ kind: "ready", missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));

    mocks.dispatchActionOutcome.mockResolvedValueOnce({ kind: "accepted", payload: { missing: ["video"] } });
    await act(async () => sendButton(renderer).props.onClick());

    expect(mocks.emitToast).toHaveBeenCalledWith("欠けている項目: video", "error");
    expect(mocks.emitToast).not.toHaveBeenCalledWith("書き出しました", "info");
    act(() => renderer.unmount());
  });

  it("応答payloadの契約違反時は成功表示せず入力を残す", async () => {
    const renderer = await render({ kind: "ready", missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));
    mocks.dispatchActionOutcome.mockResolvedValueOnce({ kind: "rejected", error: "invalid_response" });

    await act(async () => sendButton(renderer).props.onClick());

    expect(mocks.emitToast).not.toHaveBeenCalled();
    expect(textarea.props.value).toBe("ベルトが止まる");
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
    expect(mocks.dispatchActionOutcome).toHaveBeenCalledWith("bug_report.submit", { description: "序盤が長い", kind: "feedback" });
    act(() => renderer.unmount());
  });
});
