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
  Overlay: ({ children, ...props }: { children: unknown }) => createElement("mock-overlay", props, children as never),
  Portal: ({ children }: { children: unknown }) => children as never,
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
  Title: ({ children, ...props }: { children: unknown }) => createElement("mock-title", props, children as never),
}));

import { PlaytestConsentGate } from "./PlaytestConsentGate";

const dictionary = {
  "ui.playtest.consent.title": "このプレイテストについて",
  "ui.playtest.consent.body": "報告を送ると…",
  "ui.playtest.consent.agree": "了解",
  "ui.playtest.consent.failed": "応答できませんでした。もう一度押してください。",
};

beforeEach(() => {
  setDictionaries("japanese", dictionary, {}, {});
  mocks.dispatchAction.mockResolvedValue(true);
  mocks.waiting = true;
});

afterEach(() => {
  mocks.dispatchAction.mockReset();
});

describe("PlaytestConsentGate", () => {
  it("待機中は見出しと本文と了解ボタンを描く", async () => {
    const renderer = await renderGate();

    expect(headingTexts(renderer)).toEqual(["このプレイテストについて"]);
    expect(byTestId(renderer, "playtest-consent-body")).toBeTruthy();
    expect(byTestId(renderer, "playtest-consent-agree")).toBeTruthy();
    act(() => renderer.unmount());
  });

  it("待機していなければ何も描かない", async () => {
    mocks.waiting = false;

    const renderer = await renderGate();

    expect(renderer.toJSON()).toBeNull();
    act(() => renderer.unmount());
  });

  it("了解でacknowledgeアクションを投げる", async () => {
    const renderer = await renderGate();
    await act(async () => { byTestId(renderer, "playtest-consent-agree").props.onClick(); });

    expect(mocks.dispatchAction).toHaveBeenCalledWith("playtest.consent.acknowledge", {});
    act(() => renderer.unmount());
  });

  it("応答待ちの間はボタンを押せなくする", async () => {
    let resolveDispatch: ((accepted: boolean) => void) | undefined;
    mocks.dispatchAction.mockImplementation(() => new Promise<boolean>((resolve) => { resolveDispatch = resolve; }));

    const renderer = await renderGate();
    await act(async () => { byTestId(renderer, "playtest-consent-agree").props.onClick(); });

    expect(byTestId(renderer, "playtest-consent-agree").props.disabled).toBe(true);

    // 受理後もゲートが消えるまで押下不可のままにし、二重応答を通さない
    // Presses stay blocked after acceptance until the gate disappears, so a second answer cannot go through
    await act(async () => { resolveDispatch!(true); });
    expect(byTestId(renderer, "playtest-consent-agree").props.disabled).toBe(true);
    expect(mocks.dispatchAction).toHaveBeenCalledTimes(1);
    act(() => renderer.unmount());
  });

  // 拒否（WS切断・タイムアウト・ok:false）を握り潰すと全画面ゲートから永久に抜けられない
  // Swallowing a rejection (WS drop, timeout, ok:false) would trap the tester behind the full-screen gate forever
  it("dispatchが拒否されたら失敗をゲート自身が出して再度押せる", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    mocks.dispatchAction.mockResolvedValue(false);

    const renderer = await renderGate();
    await act(async () => { byTestId(renderer, "playtest-consent-agree").props.onClick(); });

    expect(warn).toHaveBeenCalledWith("[playtest.consent.acknowledge] rejected");
    expect(byTestId(renderer, "playtest-consent-failed").props.children).toBe("応答できませんでした。もう一度押してください。");
    expect(byTestId(renderer, "playtest-consent-agree").props.disabled).toBe(false);

    mocks.dispatchAction.mockResolvedValue(true);
    await act(async () => { byTestId(renderer, "playtest-consent-agree").props.onClick(); });

    expect(mocks.dispatchAction).toHaveBeenCalledTimes(2);
    warn.mockRestore();
    act(() => renderer.unmount());
  });
});

async function renderGate(): Promise<ReactTestRenderer> {
  let renderer!: ReactTestRenderer;
  await act(async () => { renderer = create(createElement(PlaytestConsentGate)); });
  return renderer;
}

function byTestId(renderer: ReactTestRenderer, testId: string) {
  return renderer.root.findByProps({ "data-testid": testId });
}

function headingTexts(renderer: ReactTestRenderer): string[] {
  return renderer.root.findAllByType("mock-title" as never).map((node) => String(node.props.children));
}
