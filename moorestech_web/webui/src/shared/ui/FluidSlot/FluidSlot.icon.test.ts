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

  it("液体は入っているがfluidGuidが空文字の場合はアイコンを描かない", () => {
    act(() => setDictionaries("japanese", {}, {}, {}));
    let renderer: ReactTestRenderer;
    act(() => {
      renderer = create(createElement(FluidSlot, {
        fluid: { fluidId: 10, amount: 500, capacity: 1000, fluidGuid: "" },
      }));
    });

    expect(renderer!.root.findAllByType("img").length).toBe(0);

    act(() => renderer.unmount());
  });

  it("アイコン読み込み失敗後はimgもidテキストも出さずフィルだけ残る", () => {
    act(() => setDictionaries("japanese", {}, {}, {}));
    let renderer: ReactTestRenderer;
    act(() => {
      renderer = create(createElement(FluidSlot, {
        fluid: { fluidId: 10, amount: 500, capacity: 1000, fluidGuid: FLUID_GUID },
      }));
    });

    const img = renderer!.root.findByType("img");
    act(() => img.props.onError());

    expect(renderer!.root.findAllByType("img").length).toBe(0);
    // fallback={null}のためimg以外の追加要素(idテキストのspan等)が増えない。増えたら金額バッジ1個のみのはずが2個以上になる
    // fallback={null} adds no extra element (e.g. id-text span); a regression would push the span count above the single amount badge
    expect(renderer!.root.findAllByType("span").length).toBe(1);
    const fillDiv = renderer!.root.findAll((node) => node.type === "div" && node.props.className === styles.fill);
    expect(fillDiv.length).toBe(1);

    act(() => renderer.unmount());
  });
});
