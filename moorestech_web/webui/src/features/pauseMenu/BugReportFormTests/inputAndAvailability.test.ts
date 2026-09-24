import { act } from "react-test-renderer";
import { describe, expect, it } from "vitest";
import { getMocks, modeSwitch, render, sendButton, statusTexts } from "./testSupport";

const mocks = getMocks();

describe("BugReportForm input and availability", () => {
  it("記述欄と送信ボタンを描く", async () => {
    const renderer = await render({ kind: "ready", missing: [] });
    expect(renderer.root.findByProps({ "data-testid": "bug-report-description" })).toBeTruthy();
    expect(sendButton(renderer)).toBeTruthy();
    act(() => renderer.unmount());
  });

  // 確保が終わる前に送れると、世界データもパケットログも無い箱が「送信しました」として運搬される
  // Sending before the capture settles ships a box with no world data or packet log as a successful report
  it("確保中は送信ボタンを押せない", async () => {
    const renderer = await render({ kind: "capturing", missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));
    expect(sendButton(renderer).props.disabled).toBe(true);
    await act(async () => sendButton(renderer).props.onClick());
    expect(mocks.dispatchActionOutcome).not.toHaveBeenCalled();
    act(() => renderer.unmount());
  });

  it("確保セッションが無いときは押せず理由を出す", async () => {
    const renderer = await render({ kind: "noSession", missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));
    expect(sendButton(renderer).props.disabled).toBe(true);
    await act(async () => sendButton(renderer).props.onClick());
    expect(mocks.dispatchActionOutcome).not.toHaveBeenCalled();
    expect(statusTexts(renderer).some((text) => text.includes("ポーズメニューを開き直してください"))).toBe(true);
    act(() => renderer.unmount());
  });

  // 送信済みの確保をもう一度送ろうとしても門が拒むだけなので、押せないことと理由を配信値から出す
  // Re-sending a submitted capture only hits the gate's refusal, so both the block and its reason come from the delivered state
  it("送信済みの確保では押せず送信済みの文言を出す", async () => {
    const renderer = await render({ kind: "submitted", missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));
    expect(sendButton(renderer).props.disabled).toBe(true);
    expect(statusTexts(renderer).some((text) => text.includes("書き出しました"))).toBe(true);
    act(() => renderer.unmount());
  });

  it("空文字では送信ボタンを無効にして送らない", async () => {
    const renderer = await render({ kind: "ready", missing: [] });
    expect(sendButton(renderer).props.disabled).toBe(true);
    await act(async () => sendButton(renderer).props.onClick());
    expect(mocks.dispatchActionOutcome).not.toHaveBeenCalled();
    act(() => renderer.unmount());
  });

  it("空白だけの記述も無効として扱う", async () => {
    const renderer = await render({ kind: "ready", missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "   " } }));
    expect(sendButton(renderer).props.disabled).toBe(true);
    act(() => renderer.unmount());
  });

  // 同じ data-testid の span を2つ同時に描くと、e2e の getByTestId が strict mode violation で落ちる
  // Two spans sharing one data-testid make the e2e getByTestId fail with a strict-mode violation
  it("状態行は1本だけ描き、確保中は確保中の文言を出す", async () => {
    const renderer = await render({ kind: "capturing", missing: ["video", "screenshot"] });
    expect(statusTexts(renderer)).toEqual(["記録を確保しています…"]);
    act(() => renderer.unmount());
  });

  it("送れる状態なら欠損の一覧を同じ1本の行で出す", async () => {
    const renderer = await render({ kind: "ready", missing: ["video", "screenshot"] });
    expect(statusTexts(renderer)).toEqual(["欠けている項目: video, screenshot"]);
    act(() => renderer.unmount());
  });

  // 往復中に種別を変えられると、送った kind と画面の表示が食い違ったまま完了する
  // Changing the kind mid-round-trip leaves the sent kind and the on-screen selection disagreeing
  it("送信中は種別切替と記述欄を操作できなくする", async () => {
    let resolveDispatch: ((ok: boolean) => void) | undefined;
    mocks.dispatchActionOutcome.mockImplementation(() => new Promise((resolve) => {
      resolveDispatch = (ok) => resolve(ok ? { kind: "accepted", payload: { missing: [] } } : { kind: "rejected", error: "failed" });
    }));

    const renderer = await render({ kind: "ready", missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));
    await act(async () => { void sendButton(renderer).props.onClick(); });

    expect(modeSwitch(renderer).props.disabled).toBe(true);
    expect(renderer.root.findByProps({ "data-testid": "bug-report-description" }).props.disabled).toBe(true);

    await act(async () => { resolveDispatch!(true); });
    expect(modeSwitch(renderer).props.disabled).toBe(false);
    act(() => renderer.unmount());
  });
});
