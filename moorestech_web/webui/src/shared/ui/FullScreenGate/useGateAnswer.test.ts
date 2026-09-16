// 受理でゲートが消えるのが正常経路なので、「消えた後に応答が解決する」順は毎回起きうる
// The gate disappearing on acceptance is the normal path, so "the answer settles after unmount" can happen every time
import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  dispatchActionOutcome: vi.fn(),
}));

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  dispatchActionOutcome: mocks.dispatchActionOutcome,
}));

import { useGateAnswer, type GateAnswerCopy } from "./useGateAnswer";

const copy: GateAnswerCopy = { answerAccepted: "accepted", notClosed: "notClosed", disconnected: "disconnected", respondFailed: "failed" };
let pressAnswer: (() => Promise<void>) | undefined;

function Harness() {
  const { answer } = useGateAnswer("playtest.consent.acknowledge", copy);
  pressAnswer = () => answer({});
  return null;
}

afterEach(() => {
  mocks.dispatchActionOutcome.mockReset();
  pressAnswer = undefined;
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe("useGateAnswer", () => {
  it("アンマウント後に受理が解決しても閉じない見張りを張らない", async () => {
    vi.useFakeTimers();
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const info = vi.spyOn(console, "info").mockImplementation(() => undefined);
    let resolveDispatch: ((outcome: unknown) => void) | undefined;
    mocks.dispatchActionOutcome.mockImplementation(() => new Promise((resolve) => { resolveDispatch = resolve; }));

    let renderer!: ReactTestRenderer;
    await act(async () => { renderer = create(createElement(Harness)); });
    let pending!: Promise<void>;
    await act(async () => { pending = pressAnswer!(); });
    act(() => renderer.unmount());

    await act(async () => {
      resolveDispatch!({ kind: "accepted" });
      await pending;
    });

    expect(vi.getTimerCount()).toBe(0);
    await act(async () => { vi.advanceTimersByTime(10000); });
    expect(warn).not.toHaveBeenCalled();
    expect(info).toHaveBeenCalledWith("[playtest.consent.acknowledge] gate closed before the answer settled: accepted");
  });
});
