import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { setDictionaries } from "@/shared/i18n/i18nStore";

const mocks = vi.hoisted(() => ({
  dispatchActionOutcome: vi.fn(),
  waiting: true,
}));

// 見せるかはapp層が決めて visible で渡すため、ここでは待機を visible として直接与える
// Visibility is decided in the app layer and passed as visible, so the wait is handed in directly as visible
vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  dispatchActionOutcome: mocks.dispatchActionOutcome,
}));
vi.mock("@mantine/core", () => ({
  Button: ({ children, ...props }: { children: unknown }) => createElement("mock-button", props, children as never),
  Group: ({ children, ...props }: { children: unknown }) => createElement("mock-group", props, children as never),
  Overlay: ({ children, ...props }: { children: unknown }) => createElement("mock-overlay", props, children as never),
  Portal: ({ children }: { children: unknown }) => children as never,
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
  Title: ({ children, ...props }: { children: unknown }) => createElement("mock-title", props, children as never),
}));

import { CrashReportGate } from "./CrashReportGate";

const dictionary = {
  "ui.playtest.crashGate.title": "前回ゲームが正常に終了しませんでした",
  "ui.playtest.crashGate.body": "前回セッションの記録を送りますか？",
  "ui.playtest.crashGate.placeholder": "止まったとき何をしていましたか？",
  "ui.playtest.crashGate.send": "送る",
  "ui.playtest.crashGate.skip": "送らない",
  "ui.playtest.gate.respondFailed": "応答できませんでした。もう一度押してください。",
  "ui.playtest.gate.answerAccepted": "応答を受け付けました。まもなく閉じます。",
  "ui.playtest.gate.disconnected": "接続が切れています。つながったらもう一度押してください。",
  "ui.playtest.gate.notClosed": "応答は届きましたが画面が閉じません。もう一度押してください。",
};

beforeEach(() => {
  setDictionaries("japanese", dictionary, {}, {});
  mocks.dispatchActionOutcome.mockResolvedValue({ kind: "accepted" });
  mocks.waiting = true;
});

afterEach(() => {
  mocks.dispatchActionOutcome.mockReset();
});

describe("CrashReportGate", () => {
  it("待機中は見出しと説明欄と2つのボタンを描く", async () => {
    const renderer = await renderGate();

    expect(headingTexts(renderer)).toEqual(["前回ゲームが正常に終了しませんでした"]);
    expect(byTestId(renderer, "crash-report-description")).toBeTruthy();
    expect(byTestId(renderer, "crash-report-send")).toBeTruthy();
    expect(byTestId(renderer, "crash-report-skip")).toBeTruthy();
    act(() => renderer.unmount());
  });

  it("待機していなければ何も描かない", async () => {
    mocks.waiting = false;

    const renderer = await renderGate();

    expect(renderer.toJSON()).toBeNull();
    act(() => renderer.unmount());
  });

  it("送るで説明文付きのsend=trueを投げる", async () => {
    const renderer = await renderGate();
    act(() => byTestId(renderer, "crash-report-description").props.onChange({ currentTarget: { value: " 採掘中に固まった " } }));
    await act(async () => { byTestId(renderer, "crash-report-send").props.onClick(); });

    expect(mocks.dispatchActionOutcome).toHaveBeenCalledWith("playtest.crash_report.respond", { send: true, description: "採掘中に固まった" });
    act(() => renderer.unmount());
  });

  it("送らないで空の説明文とsend=falseを投げる", async () => {
    const renderer = await renderGate();
    act(() => byTestId(renderer, "crash-report-description").props.onChange({ currentTarget: { value: "書きかけ" } }));
    await act(async () => { byTestId(renderer, "crash-report-skip").props.onClick(); });

    expect(mocks.dispatchActionOutcome).toHaveBeenCalledWith("playtest.crash_report.respond", { send: false, description: "" });
    act(() => renderer.unmount());
  });

  it("応答待ちの間は両方のボタンを押せなくし、受理後も受け付けた旨を出したまま押させない", async () => {
    let resolveDispatch: ((outcome: unknown) => void) | undefined;
    mocks.dispatchActionOutcome.mockImplementation(() => new Promise((resolve) => { resolveDispatch = resolve; }));

    const renderer = await renderGate();
    await act(async () => { byTestId(renderer, "crash-report-send").props.onClick(); });

    expect(byTestId(renderer, "crash-report-send").props.disabled).toBe(true);
    expect(byTestId(renderer, "crash-report-skip").props.disabled).toBe(true);

    await act(async () => { resolveDispatch!({ kind: "accepted" }); });
    expect(byTestId(renderer, "crash-report-send").props.disabled).toBe(true);
    expect(statusText(renderer)).toBe("応答を受け付けました。まもなく閉じます。");
    expect(mocks.dispatchActionOutcome).toHaveBeenCalledTimes(1);
    act(() => renderer.unmount());
  });

  // 拒否（ok:false）を握り潰すと全画面ゲートから永久に抜けられない
  // Swallowing a rejection (ok:false) would trap the tester behind the full-screen gate forever
  it("拒否されたら失敗をゲート自身が出して再度押せる", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    mocks.dispatchActionOutcome.mockResolvedValue({ kind: "rejected", error: "internal_error" });

    const renderer = await renderGate();
    await act(async () => { byTestId(renderer, "crash-report-send").props.onClick(); });

    expect(warn).toHaveBeenCalledWith("[playtest.crash_report.respond] rejected: internal_error");
    expect(statusText(renderer)).toBe("応答できませんでした。もう一度押してください。");
    expect(byTestId(renderer, "crash-report-send").props.disabled).toBe(false);
    expect(byTestId(renderer, "crash-report-skip").props.disabled).toBe(false);

    mocks.dispatchActionOutcome.mockResolvedValue({ kind: "accepted" });
    await act(async () => { byTestId(renderer, "crash-report-skip").props.onClick(); });

    expect(mocks.dispatchActionOutcome).toHaveBeenLastCalledWith("playtest.crash_report.respond", { send: false, description: "" });
    expect(mocks.dispatchActionOutcome).toHaveBeenCalledTimes(2);
    warn.mockRestore();
    act(() => renderer.unmount());
  });

  // 「もう一度押してください」は二重応答に対して必ず無効な指示になる（サーバーは既に答えを持っている）
  // "Press again" is always an invalid instruction for a second answer: the server already holds the answer
  it("二重応答の拒否は失敗ではなく受け付け済みとして扱う", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    mocks.dispatchActionOutcome.mockResolvedValue({ kind: "rejected", error: "already_responded" });

    const renderer = await renderGate();
    await act(async () => { byTestId(renderer, "crash-report-send").props.onClick(); });

    expect(statusText(renderer)).toBe("応答を受け付けました。まもなく閉じます。");
    expect(byTestId(renderer, "crash-report-send").props.disabled).toBe(true);
    warn.mockRestore();
    act(() => renderer.unmount());
  });

  // 切断は押し直しても届かない。再接続オーバーレイはゲートの下に隠れるため理由はここでしか伝わらない
  // A press cannot arrive while disconnected, and the reconnect overlay hides beneath the gate, so only this line says why
  it("切断は失敗と別の理由として出す", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    mocks.dispatchActionOutcome.mockResolvedValue({ kind: "unreachable", reason: "disconnected" });

    const renderer = await renderGate();
    await act(async () => { byTestId(renderer, "crash-report-send").props.onClick(); });

    expect(statusText(renderer)).toBe("接続が切れています。つながったらもう一度押してください。");
    expect(byTestId(renderer, "crash-report-send").props.disabled).toBe(false);
    warn.mockRestore();
    act(() => renderer.unmount());
  });

  // 受理後の待機解除publishが落ちると、押下不可のまま全画面ゲートに無音で閉じ込められる
  // A lost waiting-release publish after acceptance would silently trap the tester behind the gate with dead buttons
  it("受理後に閉じないままなら押下可へ戻して理由を出す", async () => {
    vi.useFakeTimers();
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);

    const renderer = await renderGate();
    await act(async () => { byTestId(renderer, "crash-report-send").props.onClick(); });
    expect(byTestId(renderer, "crash-report-send").props.disabled).toBe(true);

    await act(async () => { vi.advanceTimersByTime(10000); });

    expect(statusText(renderer)).toBe("応答は届きましたが画面が閉じません。もう一度押してください。");
    expect(byTestId(renderer, "crash-report-send").props.disabled).toBe(false);
    act(() => renderer.unmount());
    warn.mockRestore();
    vi.useRealTimers();
  });
});

async function renderGate(): Promise<ReactTestRenderer> {
  let renderer!: ReactTestRenderer;
  await act(async () => { renderer = create(createElement(CrashReportGate, { visible: mocks.waiting })); });
  return renderer;
}

function byTestId(renderer: ReactTestRenderer, testId: string) {
  return renderer.root.findByProps({ "data-testid": testId });
}

function statusText(renderer: ReactTestRenderer): string {
  return String(byTestId(renderer, "crash-report-gate-status").props.children);
}

function headingTexts(renderer: ReactTestRenderer): string[] {
  return renderer.root.findAllByType("mock-title" as never).map((node) => String(node.props.children));
}
