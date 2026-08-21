import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { FluidMasterEntry } from "@/bridge";
import { setDictionaries } from "@/shared/i18n/i18nStore";
import styles from "./style.module.css";

vi.mock("@mantine/core", () => ({
  Tooltip: (props: object) => createElement("mock-tooltip", props),
}));

const FLUID_GUID = "60000000-0000-4000-8000-000000000001";
const mockState = vi.hoisted(() => ({
  master: null as Map<string, FluidMasterEntry> | null,
}));

vi.mock("@/bridge", async (importOriginal) => ({
  ...await importOriginal<typeof import("@/bridge")>(),
  useFluidMaster: () => mockState.master,
}));

import FluidSlot from "./index";

describe("FluidSlot icon", () => {
  beforeEach(() => {
    mockState.master = new Map([[FLUID_GUID, { fluidId: 10, fluidGuid: FLUID_GUID, color: "#2A6FE0" }]]);
  });

  it("満タンでない液体スロットはフィルとアイコンの両方を描く", () => {
    act(() => setDictionaries("japanese", {}, {}, {}));
    let renderer: ReactTestRenderer;
    act(() => {
      renderer = create(createElement(FluidSlot, {
        fluid: { kind: "filled", fluidId: 10, amount: 500, capacity: 1000, fluidGuid: FLUID_GUID },
      }));
    });

    const img = renderer!.root.findByType("img");
    expect(img.props.src).toBe(`/api/fluid-icons/${FLUID_GUID}.png`);

    const fillDiv = renderer!.root.findAll((node) => node.type === "div" && node.props.className === styles.fill);
    expect(fillDiv.length).toBe(1);
    expect(fillDiv[0].props.style.backgroundColor).toBe("#2A6FE0");

    act(() => renderer.unmount());
  });

  it("液体マスタ未取得の間はフィルを描かない（フォールバック色を使わない）", () => {
    mockState.master = null;
    act(() => setDictionaries("japanese", {}, {}, {}));
    let renderer: ReactTestRenderer;
    act(() => {
      renderer = create(createElement(FluidSlot, {
        fluid: { kind: "filled", fluidId: 10, amount: 500, capacity: 1000, fluidGuid: FLUID_GUID },
      }));
    });

    const fillDiv = renderer!.root.findAll((node) => node.type === "div" && node.props.className === styles.fill);
    expect(fillDiv.length).toBe(0);
    // マスタ未取得でもアイコンは通常どおり描く
    // The icon still renders normally even while the master is unloaded
    expect(renderer!.root.findAllByType("img").length).toBe(1);

    act(() => renderer.unmount());
  });

  it("空スロット(kind: empty)はフィルもアイコンも描かない", () => {
    act(() => setDictionaries("japanese", {}, {}, {}));
    let renderer: ReactTestRenderer;
    act(() => {
      renderer = create(createElement(FluidSlot, {
        fluid: { kind: "empty", capacity: 1000 },
      }));
    });

    expect(renderer!.root.findAllByType("img").length).toBe(0);
    const fillDiv = renderer!.root.findAll((node) => node.type === "div" && node.props.className === styles.fill);
    expect(fillDiv.length).toBe(0);

    act(() => renderer.unmount());
  });

  it("アイコン読み込み失敗後はimgもidテキストも出さずフィルだけ残る", () => {
    act(() => setDictionaries("japanese", {}, {}, {}));
    let renderer: ReactTestRenderer;
    act(() => {
      renderer = create(createElement(FluidSlot, {
        fluid: { kind: "filled", fluidId: 10, amount: 500, capacity: 1000, fluidGuid: FLUID_GUID },
      }));
    });

    const img = renderer!.root.findByType("img");
    act(() => img.props.onError());

    expect(renderer!.root.findAllByType("img").length).toBe(0);
    // fallback={kind:"none"}のためimg以外の追加要素(idテキストのspan等)が増えない。増えたら金額バッジ1個のみのはずが2個以上になる
    // fallback={kind:"none"} adds no extra element (e.g. id-text span); a regression would push the span count above the single amount badge
    expect(renderer!.root.findAllByType("span").length).toBe(1);
    const fillDiv = renderer!.root.findAll((node) => node.type === "div" && node.props.className === styles.fill);
    expect(fillDiv.length).toBe(1);

    act(() => renderer.unmount());
  });
});
