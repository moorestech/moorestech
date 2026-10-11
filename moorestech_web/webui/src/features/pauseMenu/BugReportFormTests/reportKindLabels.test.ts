import { act } from "react-test-renderer";
import { describe, expect, it } from "vitest";
import { getMocks, modeSwitch, render, renderMutableDraft, sendButton, statusTexts } from "./testSupport";

const mocks = getMocks();

describe("BugReportForm report-kind labels", () => {
  it("感想のボタンと成功通知を選び、バグのボタンへ戻せる", async () => {
    const renderer = await render({ kind: "ready", missing: [] });
    expect(sendButton(renderer).children.join("")).toBe("バグ報告を送信");
    act(() => modeSwitch(renderer).props.onChange("feedback"));
    expect(sendButton(renderer).children.join("")).toBe("感想を送信");
    act(() => renderer.root.findByProps({ "data-testid": "bug-report-description" }).props.onChange({ currentTarget: { value: "  序盤が長い  " } }));
    await act(async () => sendButton(renderer).props.onClick());
    expect(mocks.dispatchAction).toHaveBeenCalledWith("bug_report.submit", { description: "序盤が長い", kind: "feedback" });
    expect(mocks.emitToast).toHaveBeenCalledWith("感想を書き出しました", "info");
    expect(sendButton(renderer).children.join("")).toBe("バグ報告を送信");
    act(() => renderer.unmount());
  });

  // 拒否された感想は成功扱いせず、書きかけの種別と本文を残す
  // Rejected feedback keeps its draft kind and text without claiming success
  it("感想送信が失敗すると成功通知を出さずdraftを残す", async () => {
    mocks.dispatchAction.mockResolvedValueOnce(false);
    const renderer = await render({ kind: "ready", missing: [] });
    act(() => modeSwitch(renderer).props.onChange("feedback"));
    act(() => renderer.root.findByProps({ "data-testid": "bug-report-description" }).props.onChange({ currentTarget: { value: "感想" } }));
    await act(async () => sendButton(renderer).props.onClick());
    expect(mocks.emitToast).not.toHaveBeenCalled();
    expect(modeSwitch(renderer).props.value).toBe("feedback");
    expect(renderer.root.findByProps({ "data-testid": "bug-report-description" }).props.value).toBe("感想");
    expect(sendButton(renderer).props.disabled).toBe(false);
    act(() => renderer.unmount());
  });

  it("感想でも欠損時は従来の通知を出す", async () => {
    const renderer = await render({ kind: "ready", missing: ["video"] });
    act(() => modeSwitch(renderer).props.onChange("feedback"));
    act(() => renderer.root.findByProps({ "data-testid": "bug-report-description" }).props.onChange({ currentTarget: { value: "感想" } }));
    await act(async () => sendButton(renderer).props.onClick());
    expect(mocks.emitToast).toHaveBeenCalledWith("欠けている項目: video", "error");
    expect(mocks.emitToast).not.toHaveBeenCalledWith("感想を書き出しました", "info");
    act(() => renderer.unmount());
  });

  // 遅延中のdraft変更やreset後の種別に完了通知が引っ張られない
  // Completion must not follow a changed draft kind while awaiting or after resetting
  it("感想送信中の表示とロックを保ち、送信開始時の種別で完了を伝える", async () => {
    let resolveSubmit: (ok: boolean) => void = () => {};
    mocks.dispatchAction.mockImplementationOnce(() => new Promise((resolve) => { resolveSubmit = resolve; }));
    const draft = { kind: "feedback" as "feedback" | "bug", description: "感想", setKind: () => {}, setDescription: () => {}, reset: () => { draft.kind = "bug"; } };
    const renderer = await renderMutableDraft({ kind: "ready", missing: [] }, draft);
    act(() => { void sendButton(renderer).props.onClick(); });
    expect(statusTexts(renderer)).toEqual(["感想を書き出しています…"]);
    expect(sendButton(renderer).props.disabled).toBe(true);
    expect(modeSwitch(renderer).props.disabled).toBe(true);
    draft.kind = "bug";
    await act(async () => { resolveSubmit(true); });
    expect(mocks.dispatchAction).toHaveBeenCalledWith("bug_report.submit", { description: "感想", kind: "feedback" });
    expect(mocks.emitToast).toHaveBeenCalledWith("感想を書き出しました", "info");
    act(() => renderer.unmount());
  });
});
