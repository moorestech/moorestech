import { createElement, forwardRef } from "react";
import { act, create } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import { itemNameKey, L } from "@/shared/i18n";
import { createTranslator, getI18nSnapshot, setDictionaries } from "@/shared/i18n/i18nStore";

const testState = vi.hoisted(() => ({
  locale: "english",
  data: {
    visible: true,
    lines: [{ textKey: "ui.mainMenu.playLocally", textParams: [] as string[] }],
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

import { CursorTooltip, resolveTooltipLines } from "./CursorTooltip";

const ironIngotGuid = "5c2e4d9a-1b3f-4a7c-8d6e-0f1a2b3c4d5e";

describe("CursorTooltip", () => {
  afterEach(() => {
    testState.locale = "english";
    testState.data = {
      visible: true,
      lines: [{ textKey: "ui.mainMenu.playLocally", textParams: [] }],
    };
    testState.clamp.mockClear();
    vi.restoreAllMocks();
  });

  it("interpolates textParams into the localized template", () => {
    setDictionaries("english", { [L.ui.tooltip.requiredItems]: "Requires: {p0}" }, {}, {});

    expect(resolveTooltipLines({
      visible: true,
      lines: [{ textKey: L.ui.tooltip.requiredItems, textParams: ["Iron Pickaxe, Stone Pickaxe"] }],
    }, createTranslator(getI18nSnapshot()))).toEqual(["Requires: Iron Pickaxe, Stone Pickaxe"]);
  });

  it("resolves a content key from the dictionary without a raw-text fallback", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    setDictionaries("english", { [itemNameKey(ironIngotGuid)]: "Iron Ingot" }, {}, {});

    expect(resolveTooltipLines({
      visible: true,
      lines: [{ textKey: itemNameKey(ironIngotGuid), textParams: [] }],
    }, createTranslator(getI18nSnapshot()))).toEqual(["Iron Ingot"]);
    expect(warn).not.toHaveBeenCalled();
  });

  it("renders every line in order", () => {
    setDictionaries("english", {
      [L.ui.tooltip.placeBlockedByTerrain]: "Blocked by terrain",
      // 注入した書式を検証する自己充足アサーションであり、接頭辞そのものの回帰検知はC#側（localization.csvを読むテスト）が担う
      // This is a self-contained assertion over an injected format; regression of the prefix itself is covered on the C# side, which reads localization.csv
      // ここで検証しているのは{pN}補間の責務のみ
      // Only the {pN} interpolation responsibility is verified here
      [L.ui.tooltip.placeMaterialShortage]: "Missing item: {p0} {p1}/{p2}",
    }, {}, {});

    expect(resolveTooltipLines({
      visible: true,
      lines: [
        { textKey: L.ui.tooltip.placeBlockedByTerrain, textParams: [] },
        { textKey: L.ui.tooltip.placeMaterialShortage, textParams: ["Iron Plate", "3", "10"] },
      ],
    }, createTranslator(getI18nSnapshot()))).toEqual(["Blocked by terrain", "Missing item: Iron Plate 3/10"]);
  });

  it("shows a loud marker for an unknown localized key", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    setDictionaries("english", {}, {}, {});
    const data = {
      visible: true as const,
      lines: [{ textKey: "ui.tooltip.unknown", textParams: [] as string[] }],
    };

    expect(resolveTooltipLines(data, vi.fn())).toEqual(["[!ui.tooltip.unknown]"]);
    expect(resolveTooltipLines(data, vi.fn())).toEqual(["[!ui.tooltip.unknown]"]);
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
      createNodeMock: () => ({ getBoundingClientRect: () => ({ width: 120, height: 40 }) }),
    });
    const initialCalls = testState.clamp.mock.calls.length;

    act(() => {
      testState.locale = "japanese";
      renderer.update(createElement(CursorTooltip));
    });

    expect(testState.clamp.mock.calls.length).toBeGreaterThan(initialCalls);
  });
});
