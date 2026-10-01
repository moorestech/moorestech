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
  } as { visible: boolean; lines: { textKey: string; textParams: string[] }[] },
  // 渡された pointer に応じた値を返し、どの pointer で計算したかを描画結果から読めるようにする
  // Return a pointer-dependent value so the rendered style reveals which pointer the calculation used
  clamp: vi.fn((x: number, y: number) => ({ x: x + 12, y: y + 12 })),
}));

// 本物の Portal と同じく初回描画では子を出さず、layout effect の後に出す
// Like the real Portal, render nothing on the first pass and the children only after a layout effect
vi.mock("@mantine/core", async () => {
  const { useLayoutEffect, useState } = await import("react");
  return {
    Paper: forwardRef((props: Record<string, unknown>, ref) => createElement("div", { ...props, ref })),
    Portal: ({ children }: { children: unknown }) => {
      const [mounted, setMounted] = useState(false);
      useLayoutEffect(() => setMounted(true), []);
      return mounted ? children : null;
    },
  };
});
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
      // 辞書はこのテストが注入するので、ここで検証できるのは{pN}補間の責務だけ
      // The dictionary is injected by this test, so only the {pN} interpolation responsibility is verified here
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

    // 再計算されたことだけでなく、実測サイズと画面寸法が clamp へ渡る値まで固定する
    // Pin not just that it recalculated but the measured size and viewport values handed to clamp
    expect(testState.clamp.mock.calls.length).toBeGreaterThan(initialCalls);
    expect(testState.clamp).toHaveBeenLastCalledWith(0, 0, 120, 40, 1280, 720);
  });

  it("positions from the latest pointer when the tooltip first appears without further pointer moves", () => {
    let pointerMove: ((event: { clientX: number; clientY: number }) => void) | undefined;
    vi.stubGlobal("window", {
      addEventListener: vi.fn((type: string, listener: (event: { clientX: number; clientY: number }) => void) => {
        if (type === "pointermove") pointerMove = listener;
      }),
      removeEventListener: vi.fn(),
      innerWidth: 1280,
      innerHeight: 720,
    });
    testState.data = { visible: false, lines: [] };
    // pointermove リスナーを登録する useEffect を流すため、生成を act で包む
    // Wrap creation in act so the useEffect that registers the pointermove listener is flushed
    let renderer!: ReturnType<typeof create>;
    act(() => {
      renderer = create(createElement(CursorTooltip), {
        createNodeMock: () => ({ getBoundingClientRect: () => ({ width: 120, height: 40 }) }),
      });
    });

    // 非表示中にロック前ワープの中央座標だけが届き、その後は pointermove が来ない状況を再現する
    // Reproduce only the pre-lock centered warp arriving while hidden, with no pointermove afterwards
    act(() => pointerMove?.({ clientX: 640, clientY: 360 }));
    act(() => {
      testState.data = { visible: true, lines: [{ textKey: "ui.mainMenu.playLocally", textParams: [] }] };
      renderer.update(createElement(CursorTooltip));
    });

    expect(testState.clamp).toHaveBeenLastCalledWith(640, 360, 120, 40, 1280, 720);
    expect(renderer.root.findByProps({ "data-testid": "cursor-tooltip" }).props.style).toEqual({ left: 652, top: 372 });
  });
});
