import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { setDictionaries } from "@/shared/i18n/i18nStore";

const mocks = vi.hoisted(() => ({
  dispatchAction: vi.fn(),
  waiting: true,
}));

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  useTopicSelector: (_topic: unknown, select: (data: unknown) => unknown) => select({ waiting: mocks.waiting }),
  dispatchAction: mocks.dispatchAction,
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
  "ui.playtest.crashGate.respondFailed": "応答できませんでした。もう一度押してください。",
};

beforeEach(() => {
  setDictionaries("japanese", dictionary, {}, {});
  mocks.dispatchAction.mockResolvedValue(true);
  mocks.waiting = true;
});

afterEach(() => {
  mocks.dispatchAction.mockReset();
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

    expect(mocks.dispatchAction).toHaveBeenCalledWith("playtest.crash_report.respond", { send: true, description: "採掘中に固まった" });
    act(() => renderer.unmount());
  });

  it("送らないで空の説明文とsend=falseを投げる", async () => {
    const renderer = await renderGate();
    act(() => byTestId(renderer, "crash-report-description").props.onChange({ currentTarget: { value: "書きかけ" } }));
    await act(async () => { byTestId(renderer, "crash-report-skip").props.onClick(); });

    expect(mocks.dispatchAction).toHaveBeenCalledWith("playtest.crash_report.respond", { send: false, description: "" });
    act(() => renderer.unmount());
  });

  it("応答待ちの間は両方のボタンを押せなくする", async () => {
    let resolveDispatch: ((accepted: boolean) => void) | undefined;
    mocks.dispatchAction.mockImplementation(() => new Promise<boolean>((resolve) => { resolveDispatch = resolve; }));

    const renderer = await renderGate();
    await act(async () => { byTestId(renderer, "crash-report-send").props.onClick(); });

    expect(byTestId(renderer, "crash-report-send").props.disabled).toBe(true);
    expect(byTestId(renderer, "crash-report-skip").props.disabled).toBe(true);

    // 受理後もゲートが消えるまで押下不可のままにし、二重応答を通さない
    // Presses stay blocked after acceptance until the gate disappears, so a second answer cannot go through
    await act(async () => { resolveDispatch!(true); });
    expect(byTestId(renderer, "crash-report-send").props.disabled).toBe(true);
    expect(mocks.dispatchAction).toHaveBeenCalledTimes(1);
    act(() => renderer.unmount());
  });

  // 拒否（WS切断・タイムアウト・ok:false）を握り潰すと全画面ゲートから永久に抜けられない
  // Swallowing a rejection (WS drop, timeout, ok:false) would trap the tester behind the full-screen gate forever
  it("dispatchが拒否されたら失敗をゲート自身が出して再度押せる", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    mocks.dispatchAction.mockResolvedValue(false);

    const renderer = await renderGate();
    await act(async () => { byTestId(renderer, "crash-report-send").props.onClick(); });

    expect(warn).toHaveBeenCalledWith("[playtest.crash_report.respond] rejected: send=true");
    expect(byTestId(renderer, "crash-report-respond-failed").props.children).toBe("応答できませんでした。もう一度押してください。");
    expect(byTestId(renderer, "crash-report-send").props.disabled).toBe(false);
    expect(byTestId(renderer, "crash-report-skip").props.disabled).toBe(false);

    mocks.dispatchAction.mockResolvedValue(true);
    await act(async () => { byTestId(renderer, "crash-report-skip").props.onClick(); });

    expect(mocks.dispatchAction).toHaveBeenLastCalledWith("playtest.crash_report.respond", { send: false, description: "" });
    expect(mocks.dispatchAction).toHaveBeenCalledTimes(2);
    warn.mockRestore();
    act(() => renderer.unmount());
  });
});

async function renderGate(): Promise<ReactTestRenderer> {
  let renderer!: ReactTestRenderer;
  await act(async () => { renderer = create(createElement(CrashReportGate)); });
  return renderer;
}

function byTestId(renderer: ReactTestRenderer, testId: string) {
  return renderer.root.findByProps({ "data-testid": testId });
}

function headingTexts(renderer: ReactTestRenderer): string[] {
  return renderer.root.findAllByType("mock-title" as never).map((node) => String(node.props.children));
}
