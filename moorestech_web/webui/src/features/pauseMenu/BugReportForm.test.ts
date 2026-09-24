import { createElement, type ReactNode } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import { setDictionaries } from "@/shared/i18n/i18nStore";
import { useBugReportDraft } from "./useBugReportDraft";

const mocks = vi.hoisted(() => ({
  dispatchAction: vi.fn(async () => true),
  emitToast: vi.fn(),
  readTopic: vi.fn(() => null as unknown),
}));

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  dispatchAction: mocks.dispatchAction,
  readTopic: mocks.readTopic,
}));
vi.mock("@/features/toast", () => ({ emitToast: mocks.emitToast }));
// node環境ではMantineのcolor scheme hookがwindowを触るため、素のbuttonへ差し替える（前例: 共有UIのスタブ）
// Mantine's color-scheme hook touches window under the node env, so it is swapped for a bare button (precedent: stubbing shared UI)
vi.mock("@mantine/core", () => ({
  Button: ({ children, ...rest }: { children: ReactNode }) => createElement("button", rest, children),
}));
// ModeSwitchは択一トグルの前例（LanguageSelect）と同じ共有UIをスタブする
// ModeSwitch is stubbed the same way the shared UI precedent (LanguageSelect) does
vi.mock("@/shared/ui", () => ({
  ModeSwitch: ({ value, onChange, disabled, testId }: { value: string; onChange: (v: string) => void; disabled?: boolean; testId?: string }) =>
    createElement("mock-mode-switch", { value, onChange, disabled, "data-testid": testId }),
}));

import { BugReportForm } from "./BugReportForm";

type Status = { kind: "noSession" | "capturing" | "submitting" | "ready" | "submitted"; missing: string[] };

const dictionary = {
  "ui.bugReport.placeholder": "何が起きた？",
  "ui.bugReport.send": "バグ報告を送信",
  "ui.bugReport.sent": "書き出しました",
  "ui.bugReport.capturePending": "記録を確保しています…",
  "ui.bugReport.missing": "欠けている項目: {items}",
  "ui.bugReport.noSession": "ポーズメニューを開き直してください",
  "ui.bugReport.sending": "書き出しています…",
  "ui.playtest.reportKind.label": "報告の種別",
  "ui.playtest.reportKind.bug": "バグ",
  "ui.playtest.reportKind.feedback": "感想",
};

afterEach(() => {
  vi.clearAllMocks();
  mocks.dispatchAction.mockImplementation(async () => true);
  mocks.readTopic.mockImplementation(() => null);
});

describe("BugReportForm", () => {
  it("記述欄と送信ボタンを描く", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ kind: "ready", missing: [] });
    expect(renderer.root.findByProps({ "data-testid": "bug-report-description" })).toBeTruthy();
    expect(sendButton(renderer)).toBeTruthy();
    act(() => renderer.unmount());
  });

  it("入力後の送信で bug_report.submit を説明文付きで送りトーストを出す", async () => {
    setDictionaries("japanese", dictionary, {}, {});
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
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ kind: "ready", missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));
    await act(async () => sendButton(renderer).props.onClick());
    expect(mocks.dispatchAction).toHaveBeenCalledTimes(1);
    expect(mocks.dispatchAction).not.toHaveBeenCalledWith("ui_state.request", expect.anything());
    expect(mocks.dispatchAction).not.toHaveBeenCalledWith("pause_menu.show_page", expect.anything());
    act(() => renderer.unmount());
  });

  // 確保が終わる前に送れると、世界データもパケットログも無い箱が「送信しました」として運搬される
  // Sending before the capture settles ships a box with no world data or packet log as a successful report
  it("確保中は送信ボタンを押せない", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ kind: "capturing", missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));
    expect(sendButton(renderer).props.disabled).toBe(true);
    await act(async () => sendButton(renderer).props.onClick());
    expect(mocks.dispatchAction).not.toHaveBeenCalled();
    act(() => renderer.unmount());
  });

  it("確保セッションが無いときは押せず理由を出す", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ kind: "noSession", missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));
    expect(sendButton(renderer).props.disabled).toBe(true);
    await act(async () => sendButton(renderer).props.onClick());
    expect(mocks.dispatchAction).not.toHaveBeenCalled();
    expect(statusTexts(renderer).some((text) => text.includes("ポーズメニューを開き直してください"))).toBe(true);
    act(() => renderer.unmount());
  });

  // 送信済みの確保をもう一度送ろうとしても門が拒むだけなので、押せないことと理由を配信値から出す
  // Re-sending a submitted capture only hits the gate's refusal, so both the block and its reason come from the delivered state
  it("送信済みの確保では押せず送信済みの文言を出す", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ kind: "submitted", missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));
    expect(sendButton(renderer).props.disabled).toBe(true);
    expect(statusTexts(renderer).some((text) => text.includes("書き出しました"))).toBe(true);
    act(() => renderer.unmount());
  });

  // 書き出し中の二度押しは同じ確保から2箱を作り、同じ報告のdraft PRが2本出る
  // A second click while writing makes two boxes from one capture and raises two draft PRs for one report
  it("書き出し中は二度目の送信を受け付けない", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    let resolveSubmit: (ok: boolean) => void = () => {};
    mocks.dispatchAction.mockImplementationOnce(() => new Promise<boolean>((resolve) => { resolveSubmit = resolve; }));
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

  // 書き出しで判明した欠損はC#が配り直す。押した時点のpropsを読むと欠けたことを知る機会が無い
  // C# republishes the missing items found while writing; reading the click-time props would hide the gap
  it("欠損付きで書けたときは成功トーストと別の文言を出す", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ kind: "ready", missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));

    // 送信中にC#が欠損付きの確保状態を配り直す
    // C# republishes the capture state with missing items while the send is in flight
    mocks.readTopic.mockImplementation(() => ({ disconnected: false, bugReport: { kind: "submitted", missing: ["video"] } }));
    await act(async () => sendButton(renderer).props.onClick());

    expect(mocks.emitToast).toHaveBeenCalledWith("欠けている項目: video", "error");
    expect(mocks.emitToast).not.toHaveBeenCalledWith("書き出しました", "info");
    act(() => renderer.unmount());
  });

  it("空文字では送信ボタンを無効にして送らない", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ kind: "ready", missing: [] });
    expect(sendButton(renderer).props.disabled).toBe(true);
    await act(async () => sendButton(renderer).props.onClick());
    expect(mocks.dispatchAction).not.toHaveBeenCalled();
    act(() => renderer.unmount());
  });

  it("空白だけの記述も無効として扱う", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ kind: "ready", missing: [] });
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "   " } }));
    expect(sendButton(renderer).props.disabled).toBe(true);
    act(() => renderer.unmount());
  });

  // 同じ data-testid の span を2つ同時に描くと、e2e の getByTestId が strict mode violation で落ちる
  // Two spans sharing one data-testid make the e2e getByTestId fail with a strict-mode violation
  it("状態行は1本だけ描き、確保中は確保中の文言を出す", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ kind: "capturing", missing: ["video", "screenshot"] });
    expect(statusTexts(renderer)).toEqual(["記録を確保しています…"]);
    act(() => renderer.unmount());
  });

  it("送れる状態なら欠損の一覧を同じ1本の行で出す", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ kind: "ready", missing: ["video", "screenshot"] });
    expect(statusTexts(renderer)).toEqual(["欠けている項目: video, screenshot"]);
    act(() => renderer.unmount());
  });

  // 種別を残すと、感想を1件送った次のバグ報告が feedback のまま箱詰めされる
  // Leaving the kind boxes the bug report that follows a feedback submission as feedback
  it("送信成功後は種別が既定のバグへ戻る", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ kind: "ready", missing: [] });
    act(() => modeSwitch(renderer).props.onChange("feedback"));
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "序盤が長い" } }));
    await act(async () => sendButton(renderer).props.onClick());

    expect(modeSwitch(renderer).props.value).toBe("bug");
    act(() => renderer.unmount());
  });

  // 往復中に種別を変えられると、送った kind と画面の表示が食い違ったまま完了する
  // Changing the kind mid-round-trip leaves the sent kind and the on-screen selection disagreeing
  it("送信中は種別切替と記述欄を操作できなくする", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    let resolveDispatch: ((ok: boolean) => void) | undefined;
    mocks.dispatchAction.mockImplementation(() => new Promise<boolean>((resolve) => { resolveDispatch = resolve; }));

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

  it("感想へ切り替えるとkindがfeedbackになる", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ kind: "ready", missing: [] });
    act(() => modeSwitch(renderer).props.onChange("feedback"));
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "序盤が長い" } }));
    await act(async () => sendButton(renderer).props.onClick());
    expect(mocks.dispatchAction).toHaveBeenCalledWith("bug_report.submit", { description: "序盤が長い", kind: "feedback" });
    act(() => renderer.unmount());
  });
});

function FormWithDraft({ status }: { status: Status }) {
  const draft = useBugReportDraft();
  return createElement(BugReportForm, { status, draft });
}

async function render(status: Status): Promise<ReactTestRenderer> {
  let renderer: ReactTestRenderer;
  await act(async () => {
    renderer = create(createElement(FormWithDraft, { status }));
  });
  return renderer!;
}

function sendButton(renderer: ReactTestRenderer) {
  return renderer.root.find((node) => node.type === "button" && node.props["data-testid"] === "bug-report-send");
}

function modeSwitch(renderer: ReactTestRenderer) {
  return renderer.root.findByProps({ "data-testid": "bug-report-kind" });
}

function statusTexts(renderer: ReactTestRenderer): string[] {
  return renderer.root.findAll((node) => node.type === "span" && node.props["data-testid"] === "bug-report-status").map((node) => node.children.join(""));
}
