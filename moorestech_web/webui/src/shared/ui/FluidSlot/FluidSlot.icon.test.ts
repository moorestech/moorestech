import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import { setDictionaries } from "@/shared/i18n/i18nStore";
import styles from "./style.module.css";

vi.mock("@mantine/core", () => ({
  Tooltip: (props: object) => createElement("mock-tooltip", props),
}));

import FluidSlot from "./index";

const FLUID_GUID = "60000000-0000-4000-8000-000000000001";

describe("FluidSlot icon", () => {
  it("満タンでない液体スロットはフィルとアイコンの両方を描く", () => {
    act(() => setDictionaries("japanese", {}, {}, {}));
    let renderer: ReactTestRenderer;
    act(() => {
      renderer = create(createElement(FluidSlot, {
        fluid: { fluidId: 10, amount: 500, capacity: 1000, fluidGuid: FLUID_GUID },
      }));
    });

    const img = renderer!.root.findByType("img");
    expect(img.props.src).toBe(`/api/fluid-icons/${FLUID_GUID}.png`);

    const fillDiv = renderer!.root.findAll((node) => node.type === "div" && node.props.className === styles.fill);
    expect(fillDiv.length).toBe(1);

    act(() => renderer.unmount());
  });

  it("空液体はfluidGuidが空文字なのでアイコンを描かない", () => {
    act(() => setDictionaries("japanese", {}, {}, {}));
    let renderer: ReactTestRenderer;
    act(() => {
      renderer = create(createElement(FluidSlot, {
        fluid: { fluidId: 0, amount: 0, capacity: 1000, fluidGuid: "" },
      }));
    });

    expect(renderer!.root.findAllByType("img").length).toBe(0);

    act(() => renderer.unmount());
  });
});
