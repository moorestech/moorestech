import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { FluidMasterEntry } from "@/bridge";
import { fluidNameKey } from "@/shared/i18n";
import { setDictionaries } from "@/shared/i18n/i18nStore";

vi.mock("@mantine/core", () => ({
  Tooltip: (props: object) => createElement("mock-tooltip", props),
}));

const FLUID_GUID = "60000000-0000-4000-8000-000000000001";
const mockState = vi.hoisted(() => {
  const guid = "60000000-0000-4000-8000-000000000001";
  return { master: new Map([[guid, { fluidGuid: guid, color: "#2A6FE0" } satisfies FluidMasterEntry]]) };
});

vi.mock("@/bridge", async (importOriginal) => ({
  ...await importOriginal<typeof import("@/bridge")>(),
  useFluidMaster: () => mockState.master,
}));

import FluidSlot from "./index";
const filled = { kind: "filled" as const, amount: 500, capacity: 1000, fluidGuid: FLUID_GUID };

function tooltipProps(renderer: ReactTestRenderer) {
  return renderer.root.findByType("mock-tooltip" as never).props as { label: string; disabled: boolean };
}

describe("FluidSlot localization", () => {
  it("fluidGuidの導出キーを辞書解決し、言語切替でラベルが追従する", () => {
    const key = fluidNameKey(FLUID_GUID);
    act(() => setDictionaries("japanese", {}, { [key]: "Fallback Water" }, { [key]: "Source Water" }));
    let renderer: ReactTestRenderer;
    act(() => {
      renderer = create(createElement(FluidSlot, { fluid: filled }));
    });

    expect(tooltipProps(renderer!).label).toBe("Fallback Water");

    // payload固定のまま辞書通知だけでラベルが切り替わる
    // The label switches on the dictionary notification alone, with the payload unchanged
    act(() => setDictionaries("japanese", { [key]: "水" }, {}, { [key]: "Source Water" }));
    expect(tooltipProps(renderer!).label).toBe("水");

    // 辞書ストア購読が残ると次ケースの辞書差し替えで再描画されるため破棄する
    // Unmount so the store subscription does not re-render on the next case's dictionary swap
    act(() => renderer.unmount());
  });

  it("空スロット(kind: empty)はTooltip自体を描かない", () => {
    act(() => setDictionaries("japanese", {}, {}, {}));
    let renderer: ReactTestRenderer;
    act(() => {
      renderer = create(createElement(FluidSlot, {
        fluid: { kind: "empty", capacity: 1000 },
      }));
    });

    // 空スロットは名前を持たないのでTooltipを一切マウントしない
    // An empty slot has no name, so no tooltip is mounted at all
    expect(renderer!.root.findAllByType("mock-tooltip" as never)).toHaveLength(0);
    act(() => renderer.unmount());
  });
});
