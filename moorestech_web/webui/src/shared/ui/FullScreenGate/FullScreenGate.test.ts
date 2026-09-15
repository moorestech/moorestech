// 外殻は描画だけを持つ。見せるかの判断はapp層から visible で届く
// The shell only renders; whether to show arrives from the app layer as visible
import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";

vi.mock("@mantine/core", () => ({
  Overlay: ({ children, ...props }: { children: unknown }) => createElement("mock-overlay", props, children as never),
  Portal: ({ children }: { children: unknown }) => children as never,
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Title: ({ children, ...props }: { children: unknown }) => createElement("mock-title", props, children as never),
}));

import FullScreenGate from ".";

describe("FullScreenGate", () => {
  it("visible でなければ何も描かない", async () => {
    const renderer = await render(false);

    expect(renderer.toJSON()).toBeNull();
    act(() => renderer.unmount());
  });

  it("visible なら不透明面に見出しと本体を描く", async () => {
    const renderer = await render(true);

    const overlay = renderer.root.findByType("mock-overlay" as never);
    expect(overlay.props["data-testid"]).toBe("gate");
    expect(overlay.props.color).toBe("var(--full-screen-gate-face)");
    expect(overlay.props.zIndex).toBe("var(--z-portal-full-screen-gate)");
    expect(renderer.root.findByType("mock-title" as never).props.children).toBe("見出し");
    expect(renderer.root.findAll((node) => node.props["data-testid"] === "gate-body")).toHaveLength(1);
    act(() => renderer.unmount());
  });
});

async function render(visible: boolean): Promise<ReactTestRenderer> {
  let renderer!: ReactTestRenderer;
  await act(async () => {
    renderer = create(createElement(FullScreenGate, {
      visible,
      testId: "gate",
      title: "見出し",
      children: createElement("mock-body", { "data-testid": "gate-body" }),
    }));
  });
  return renderer;
}
