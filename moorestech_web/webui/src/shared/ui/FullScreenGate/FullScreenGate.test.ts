// 3ゲートは無条件マウントなので、2枚が同時に待つと排他が無ければ重なって描かれる
// The three gates mount unconditionally, so without the exclusion two simultaneous waits would draw on top of each other
import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  waitingTopics: new Set<string>(),
}));

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  useTopicSelector: (topic: string, select: (data: unknown) => unknown) => select({ waiting: mocks.waitingTopics.has(topic) }),
}));
vi.mock("@mantine/core", () => ({
  Overlay: ({ children, ...props }: { children: unknown }) => createElement("mock-overlay", props, children as never),
  Portal: ({ children }: { children: unknown }) => children as never,
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Title: ({ children, ...props }: { children: unknown }) => createElement("mock-title", props, children as never),
}));

import { Topics } from "@/bridge";
import FullScreenGate from ".";

beforeEach(() => {
  mocks.waitingTopics = new Set<string>();
});

describe("FullScreenGate", () => {
  it("自分のtopicが待機していなければ何も描かない", async () => {
    mocks.waitingTopics.add(Topics.consentGate);

    const renderer = await render(Topics.crashReportGate);

    expect(renderer.toJSON()).toBeNull();
    act(() => renderer.unmount());
  });

  it("待機が重なったら起動順の手前だけを描く", async () => {
    mocks.waitingTopics.add(Topics.consentGate);
    mocks.waitingTopics.add(Topics.crashReportGate);

    const consent = await render(Topics.consentGate);
    const crash = await render(Topics.crashReportGate);

    expect(consent.toJSON()).not.toBeNull();
    expect(crash.toJSON()).toBeNull();
    act(() => consent.unmount());
    act(() => crash.unmount());
  });

  it("言語選択ゲートは同意ゲートより手前に立つ", async () => {
    mocks.waitingTopics.add(Topics.eventLanguageGate);
    mocks.waitingTopics.add(Topics.consentGate);

    const language = await render(Topics.eventLanguageGate);
    const consent = await render(Topics.consentGate);

    expect(language.toJSON()).not.toBeNull();
    expect(consent.toJSON()).toBeNull();
    act(() => language.unmount());
    act(() => consent.unmount());
  });
});

async function render(topic: typeof Topics.eventLanguageGate | typeof Topics.consentGate | typeof Topics.crashReportGate): Promise<ReactTestRenderer> {
  let renderer!: ReactTestRenderer;
  await act(async () => {
    renderer = create(createElement(FullScreenGate, { topic, testId: "gate", title: "見出し", children: null }));
  });
  return renderer;
}
