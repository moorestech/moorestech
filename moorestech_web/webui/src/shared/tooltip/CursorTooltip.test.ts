import { createElement, forwardRef } from "react";
import { act, create } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import { L } from "@/shared/i18n";

const testState = vi.hoisted(() => ({
  locale: "english",
  data: {
    visible: true,
    textKey: "ui.mainMenu.playLocally",
    fontSize: 36,
    isLocalize: true,
  },
  clamp: vi.fn(() => ({ x: 12, y: 12 })),
}));

vi.mock("@mantine/core", () => ({
  Paper: forwardRef((props: Record<string, unknown>, ref) => createElement("div", { ...props, ref })),
  Portal: ({ children }: { children: unknown }) => children,
}));
vi.mock("@/bridge", () => ({
  Topics: { tooltip: "ui.tooltip" },
  useTopic: () => testState.data,
}));
vi.mock("@/shared/i18n", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/i18n")>();
  return {
    ...actual,
    useI18n: () => ({
      locale: testState.locale,
      t: () => testState.locale === "japanese" ? "日本語の長い文言" : "English",
    }),
  };
});
vi.mock("./tooltipPosition", () => ({ clampTooltipPosition: testState.clamp }));

import { CursorTooltip, resolveTooltipText } from "./CursorTooltip";

describe("CursorTooltip", () => {
  afterEach(() => {
    testState.locale = "english";
    testState.data = {
      visible: true,
      textKey: "ui.mainMenu.playLocally",
      fontSize: 36,
      isLocalize: true,
    };
    testState.clamp.mockClear();
    vi.restoreAllMocks();
  });

  it("uses the explicit localization flag instead of guessing from raw text", () => {
    const t = vi.fn(() => "translated");

    expect(resolveTooltipText({
      visible: true,
      textKey: L.ui.mainMenu.playLocally,
      fontSize: 36,
      isLocalize: false,
    }, t)).toBe(L.ui.mainMenu.playLocally);
    expect(t).not.toHaveBeenCalled();
  });

  it("shows a loud marker for an unknown localized key", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const data = {
      visible: true,
      textKey: "ui.tooltip.unknown",
      fontSize: 36,
      isLocalize: true,
    };

    expect(resolveTooltipText(data, vi.fn())).toBe("[!ui.tooltip.unknown]");
    expect(resolveTooltipText(data, vi.fn())).toBe("[!ui.tooltip.unknown]");
    expect(warn).toHaveBeenCalledOnce();
    expect(warn).toHaveBeenCalledWith("[i18n] Unknown localized external key: ui.tooltip.unknown");
  });

  it("recalculates position when locale changes the resolved text", () => {
    vi.stubGlobal("window", {
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      innerWidth: 1280,
      innerHeight: 720,
    });
    const renderer = create(createElement(CursorTooltip), {
      createNodeMock: () => ({ offsetWidth: 120, offsetHeight: 40 }),
    });
    const initialCalls = testState.clamp.mock.calls.length;

    act(() => {
      testState.locale = "japanese";
      renderer.update(createElement(CursorTooltip));
    });

    expect(testState.clamp.mock.calls.length).toBeGreaterThan(initialCalls);
  });
});
