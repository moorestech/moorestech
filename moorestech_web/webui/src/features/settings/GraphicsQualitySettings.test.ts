import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import { setDictionaries } from "@/shared/i18n/i18nStore";

const medium = {
  drawDistanceMeters: 300, shadows: true, renderScalePercent: 100, ssao: true, postProcessing: true,
  effects: true, mapObjects: true, terrainDetail: "fine", ocean: true,
};
const mocks = vi.hoisted(() => ({ preset: "medium" as string | undefined, dispatch: vi.fn(), outcome: vi.fn(async (): Promise<{ kind: "accepted" } | { kind: "rejected"; error: string }> => ({ kind: "accepted" })) }));
vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  useTopic: (topic: string) => topic === "settings.graphicsQuality" && mocks.preset !== undefined
    ? { preset: mocks.preset, settings: medium } : undefined,
  dispatchAction: mocks.dispatch,
  dispatchActionOutcome: mocks.outcome,
}));
vi.mock("@mantine/core", () => ({
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
  Title: ({ children, ...props }: { children: unknown }) => createElement("mock-title", props, children as never),
}));
vi.mock("@/shared/ui", () => ({ ModeSwitch: (props: object) => createElement("mock-mode-switch", props) }));
import { GraphicsQualitySettings } from "./GraphicsQualitySettings";

let renderer: ReactTestRenderer;
afterEach(() => {
  act(() => renderer.unmount());
  mocks.preset = "medium";
  mocks.dispatch.mockClear();
  mocks.outcome.mockClear();
});

const switchOf = (testId: string) => renderer.root.findAll((n) => (n.type as unknown) === "mock-mode-switch" && n.props.testId === testId)[0];
const customLabel = () => renderer.root.findAll((n) => (n.type as unknown) === "mock-text" && n.props["data-testid"] === "graphics-preset-custom");

describe("GraphicsQualitySettings", () => {
  it("プリセットと一致するときはその値を選択しカスタム表示を出さない", async () => {
    await renderSettings();
    expect(switchOf("graphics-preset").props.value).toBe("medium");
    expect(customLabel()).toHaveLength(0);
  });
  it("カスタムのときは無選択にしてカスタム表示を1件出す", async () => {
    mocks.preset = "custom";
    await renderSettings();
    expect(switchOf("graphics-preset").props.value).toBeNull();
    expect(customLabel()).toHaveLength(1);
  });
  it("項目変更は全項目の値を、プリセット選択はプリセット名を送る", async () => {
    await renderSettings();
    await act(async () => { await switchOf("graphics-shadows").props.onChange("off"); });
    expect(mocks.outcome).toHaveBeenCalledWith("settings.setGraphicsQuality", { settings: { ...medium, shadows: false } });
    act(() => switchOf("graphics-preset").props.onChange("low"));
    expect(mocks.dispatch).toHaveBeenCalledWith("settings.applyGraphicsQualityPreset", { preset: "low" });
  });
  it("数値項目は数値で送る", async () => {
    await renderSettings();
    await act(async () => { await switchOf("graphics-draw-distance").props.onChange("1000"); });
    expect(mocks.outcome).toHaveBeenCalledWith("settings.setGraphicsQuality", { settings: { ...medium, drawDistanceMeters: 1000 } });
  });
  it.each([
    ["graphics-render-scale", "75", "renderScalePercent", 75],
    ["graphics-terrain", "coarse", "terrainDetail", "coarse"],
    ["graphics-ssao", "off", "ssao", false],
    ["graphics-postProcessing", "off", "postProcessing", false],
    ["graphics-effects", "off", "effects", false],
    ["graphics-mapObjects", "off", "mapObjects", false],
    ["graphics-ocean", "off", "ocean", false],
  ])("%sの操作値を送る", async (testId, choice, key, value) => {
    await renderSettings();
    await act(async () => { await switchOf(String(testId)).props.onChange(choice); });
    expect(mocks.outcome).toHaveBeenCalledWith("settings.setGraphicsQuality", { settings: { ...medium, [key]: value } });
  });
  it("拒否理由と対象項目を表示文言へ解決する", async () => {
    mocks.outcome.mockResolvedValueOnce({ kind: "rejected", error: "invalid_graphics_quality:invalid_type:renderScalePercent" });
    await renderSettings();
    await act(async () => { await switchOf("graphics-render-scale").props.onChange("75"); });
    expect(renderer.root.findByProps({ role: "alert" }).props.children).toBe("Render resolution: Invalid value type");
  });
  it("topic未着では項目を描かない", async () => {
    mocks.preset = undefined;
    await renderSettings();
    expect(renderer.root.findAllByType("mock-mode-switch" as never)).toEqual([]);
  });
});

async function renderSettings() {
  // 描画文言は共通辞書ストアから取得する
  // Resolve visible text through the shared dictionary store
  setDictionaries("english", { "ui.settings.graphicsRenderScale": "Render resolution", "ui.settings.graphicsInvalidType": "Invalid value type" }, {}, {});
  await act(async () => { renderer = create(createElement(GraphicsQualitySettings)); });
}
