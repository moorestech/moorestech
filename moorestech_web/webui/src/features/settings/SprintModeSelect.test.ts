import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import { setDictionaries } from "@/shared/i18n/i18nStore";

const mocks = vi.hoisted(() => ({ mode: "toggle" as string | undefined, recovered: false, dispatch: vi.fn() }));
vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  useTopic: (topic: string) => topic === "settings.sprintInputMode" && mocks.mode !== undefined
    ? { mode: mocks.mode, recoveredInvalidPreference: mocks.recovered } : undefined,
  dispatchAction: mocks.dispatch,
}));
vi.mock("@mantine/core", () => ({
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
  Title: ({ children, ...props }: { children: unknown }) => createElement("mock-title", props, children as never),
}));
vi.mock("@/shared/ui", () => ({ ModeSwitch: (props: object) => createElement("mock-mode-switch", props) }));
import { SprintModeSelect } from "./SprintModeSelect";

let renderer: ReactTestRenderer;
afterEach(() => {
  act(() => renderer.unmount());
  mocks.mode = "toggle";
  mocks.recovered = false;
  mocks.dispatch.mockClear();
});

describe("SprintModeSelect", () => {
  it("復旧記録を表示し選択後のtopicで解除する", async () => {
    mocks.recovered = true;
    await renderSelector();
    expect(renderer.root.findAllByType("mock-text" as never)).toHaveLength(1);
    mocks.recovered = false;
    await act(async () => renderer.update(createElement(SprintModeSelect)));
    expect(renderer.root.findAllByType("mock-text" as never)).toHaveLength(0);
  });
  it("topicの現在値を選択する", async () => {
    await renderSelector();
    expect(renderer.root.findByType("mock-mode-switch" as never).props.value).toBe("toggle");
  });
  it("ホールド選択を設定actionへ送る", async () => {
    await renderSelector();
    const select = renderer.root.findByType("mock-mode-switch" as never);
    expect(select.props.options.map((option: { value: string }) => option.value)).toEqual(["toggle", "hold"]);
    act(() => select.props.onChange(select.props.options.find((option: { value: string }) => option.value === "hold").value));
    expect(mocks.dispatch).toHaveBeenCalledWith("settings.setSprintInputMode", { mode: "hold" });
  });
  it("topicのtoggleからholdへの更新を表示する", async () => {
    await renderSelector();
    mocks.mode = "hold";
    await act(async () => renderer.update(createElement(SprintModeSelect)));
    expect(renderer.root.findByType("mock-mode-switch" as never).props.value).toBe("hold");
  });
  it("holdの初期表示からtoggleを選択できる", async () => {
    mocks.mode = "hold";
    await renderSelector();
    const select = renderer.root.findByType("mock-mode-switch" as never);
    expect(select.props.value).toBe("hold");
    const option = select.props.options.find((item: { value: string }) => item.value === "toggle");
    expect(option).toBeDefined();
    act(() => select.props.onChange(option.value));
    expect(mocks.dispatch).toHaveBeenCalledWith("settings.setSprintInputMode", { mode: "toggle" });
  });
  it("topic未着では選択を描かない", async () => {
    mocks.mode = undefined;
    await renderSelector();
    expect(renderer.root.findAllByType("mock-mode-switch" as never)).toEqual([]);
  });
});

async function renderSelector() {
  // 描画文言は共通辞書ストアから取得する
  // Resolve visible text through the shared dictionary store
  setDictionaries("english", {}, {}, {});
  await act(async () => { renderer = create(createElement(SprintModeSelect)); });
}
