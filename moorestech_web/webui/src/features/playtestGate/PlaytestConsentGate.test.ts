import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { setDictionaries } from "@/shared/i18n/i18nStore";

const mocks = vi.hoisted(() => ({
  dispatchActionOutcome: vi.fn(),
  waiting: true,
}));

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  useTopicSelector: (_topic: unknown, select: (data: unknown) => unknown) => select({ waiting: mocks.waiting }),
  dispatchActionOutcome: mocks.dispatchActionOutcome,
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

    expect(mocks.dispatchActionOutcome).toHaveBeenCalledWith("playtest.consent.acknowledge", {});
    act(() => renderer.unmount());
  });

  it("応答待ちの間はボタンを押せなくし、受理後も受け付けた旨を出したまま押させない", async () => {
    let resolveDispatch: ((outcome: unknown) => void) | undefined;
    mocks.dispatchActionOutcome.mockImplementation(() => new Promise((resolve) => { resolveDispatch = resolve; }));

    const renderer = await renderGate();
    await act(async () => { byTestId(renderer, "playtest-consent-agree").props.onClick(); });

    expect(byTestId(renderer, "playtest-consent-agree").props.disabled).toBe(true);

    await act(async () => { resolveDispatch!({ kind: "accepted" }); });
    expect(byTestId(renderer, "playtest-consent-agree").props.disabled).toBe(true);
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
    await act(async () => { byTestId(renderer, "playtest-consent-agree").props.onClick(); });

    expect(warn).toHaveBeenCalledWith("[playtest.consent.acknowledge] rejected: internal_error");
    expect(statusText(renderer)).toBe("応答できませんでした。もう一度押してください。");
    expect(byTestId(renderer, "playtest-consent-agree").props.disabled).toBe(false);

    mocks.dispatchActionOutcome.mockResolvedValue({ kind: "accepted" });
    await act(async () => { byTestId(renderer, "playtest-consent-agree").props.onClick(); });

    expect(mocks.dispatchActionOutcome).toHaveBeenCalledTimes(2);
    warn.mockRestore();
    act(() => renderer.unmount());
  });

  // 「もう一度押してください」は二重応答に対して必ず無効な指示になる（サーバーは既に答えを持っている）
  // "Press again" is always an invalid instruction for a second answer: the server already holds the answer
  it("二重応答の拒否は失敗ではなく受け付け済みとして扱う", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    mocks.dispatchActionOutcome.mockResolvedValue({ kind: "rejected", error: "already_acknowledged" });

    const renderer = await renderGate();
    await act(async () => { byTestId(renderer, "playtest-consent-agree").props.onClick(); });

    expect(statusText(renderer)).toBe("応答を受け付けました。まもなく閉じます。");
    expect(byTestId(renderer, "playtest-consent-agree").props.disabled).toBe(true);
    warn.mockRestore();
    act(() => renderer.unmount());
  });

  // 受理後の待機解除publishが落ちると、押下不可のまま全画面ゲートに無音で閉じ込められる
  // A lost waiting-release publish after acceptance would silently trap the tester behind the gate with a dead button
  it("受理後に閉じないままなら押下可へ戻して理由を出す", async () => {
    vi.useFakeTimers();
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);

    const renderer = await renderGate();
    await act(async () => { byTestId(renderer, "playtest-consent-agree").props.onClick(); });
    expect(byTestId(renderer, "playtest-consent-agree").props.disabled).toBe(true);

    await act(async () => { vi.advanceTimersByTime(10000); });

    expect(statusText(renderer)).toBe("応答は届きましたが画面が閉じません。もう一度押してください。");
    expect(byTestId(renderer, "playtest-consent-agree").props.disabled).toBe(false);
    act(() => renderer.unmount());
    warn.mockRestore();
    vi.useRealTimers();
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

function statusText(renderer: ReactTestRenderer): string {
  return String(byTestId(renderer, "playtest-consent-gate-status").props.children);
}

function headingTexts(renderer: ReactTestRenderer): string[] {
  return renderer.root.findAllByType("mock-title" as never).map((node) => String(node.props.children));
}
