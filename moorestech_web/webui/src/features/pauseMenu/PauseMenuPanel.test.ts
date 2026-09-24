import { createElement, type ReactNode } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import { setDictionaries } from "@/shared/i18n/i18nStore";

const mocks = vi.hoisted(() => ({
  dispatchAction: vi.fn(async () => true),
  readTopic: vi.fn(() => null as unknown),
  pauseMenu: { current: null as unknown },
}));

vi.mock("@/bridge", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/bridge")>();
  return {
    ...actual,
    dispatchAction: mocks.dispatchAction,
    readTopic: mocks.readTopic,
    useTopic: (topic: string) => (topic === actual.Topics.pauseMenu ? mocks.pauseMenu.current : null),
  };
});
vi.mock("@/features/toast", () => ({ emitToast: vi.fn() }));
vi.mock("@mantine/core", () => ({
  Button: ({ children, ...rest }: { children: ReactNode }) => createElement("button", rest, children),
  Stack: ({ children, ...rest }: { children: ReactNode }) => createElement("div", rest, children),
  Text: ({ children }: { children: ReactNode }) => createElement("p", null, children),
  Title: ({ children }: { children: ReactNode }) => createElement("h1", null, children),
}));
vi.mock("@/shared/ui", async () => ({
  PanelActionButton: (await import("@/shared/ui/PanelActionButton")).default,
  ModeSwitch: ({ value, onChange, testId }: { value: string; onChange: (v: string) => void; testId?: string }) =>
    createElement("mock-mode-switch", { value, onChange, "data-testid": testId }),
}));
vi.mock("@/features/settings", () => ({
  LanguageSelect: () => createElement("mock-language-select", { "data-testid": "language-select" }),
}));

import { PauseMenuPanel } from "./PauseMenuPanel";

type Page = "top" | "settings" | "bugReport";
const ready = { kind: "ready", missing: [] as string[] };

afterEach(() => {
  vi.clearAllMocks();
  mocks.pauseMenu.current = null;
});

describe("PauseMenuPanel", () => {
  it("トップは4ボタンだけを出し、言語選択と報告欄を出さない", async () => {
    const renderer = await render("top");
    for (const [id, anchor] of [
      ["pause-menu-save", "pause.save"], ["pause-menu-save-and-quit", "pause.back"],
      ["pause-menu-open-settings", "pause.settings"], ["pause-menu-open-bug-report", "pause.bug-report"],
    ]) {
      const buttons = byTestId(renderer, id);
      expect(buttons).toHaveLength(1);
      expect(buttons[0].type).toBe("button");
      expect(buttons[0].props["data-tutorial-anchor"]).toBe(anchor);
    }
    expect(byTestId(renderer, "language-select")).toHaveLength(0);
    expect(byTestId(renderer, "bug-report-description")).toHaveLength(0);
    act(() => renderer.unmount());
  });

  it("設定ボタンはsettingsへのshow_pageを送る", async () => {
    const renderer = await render("top");
    await act(async () => byTestId(renderer, "pause-menu-open-settings")[0].props.onClick());
    expect(mocks.dispatchAction).toHaveBeenCalledWith("pause_menu.show_page", { page: "settings" });
    act(() => renderer.unmount());
  });

  it("設定画面は言語選択と戻るボタンを出し、戻るはtopへのshow_pageを送る", async () => {
    const renderer = await render("settings");
    expect(byTestId(renderer, "language-select")).toHaveLength(1);
    expect(byTestId(renderer, "pause-menu-back")[0].type).toBe("button");
    expect(byTestId(renderer, "pause-menu-back")[0].props["data-tutorial-anchor"]).toBe("pause.back-to-top");
    await act(async () => byTestId(renderer, "pause-menu-back")[0].props.onClick());
    expect(mocks.dispatchAction).toHaveBeenCalledWith("pause_menu.show_page", { page: "top" });
    act(() => renderer.unmount());
  });

  // 同じポーズの中で画面を行き来しても書きかけは残る（ADR 0069）
  // The draft survives page moves inside the same pause (ADR 0069)
  it("バグ報告の書きかけはトップへ戻って再び開いても残る", async () => {
    const renderer = await render("bugReport");
    act(() => byTestId(renderer, "bug-report-description")[0].props.onChange({ currentTarget: { value: "ベルトが止まる" } }));
    await setPage(renderer, "top");
    await setPage(renderer, "bugReport");
    expect(byTestId(renderer, "bug-report-description")[0].props.value).toBe("ベルトが止まる");
    act(() => renderer.unmount());
  });

  // ポーズを閉じるとパネルごと外れ、書きかけは捨てられる
  // Closing the pause unmounts the panel, and the draft goes with it
  it("パネルを外して付け直すと書きかけは空になる", async () => {
    const first = await render("bugReport");
    act(() => byTestId(first, "bug-report-description")[0].props.onChange({ currentTarget: { value: "ベルトが止まる" } }));
    act(() => first.unmount());
    const second = await render("bugReport");
    expect(byTestId(second, "bug-report-description")[0].props.value).toBe("");
    act(() => second.unmount());
  });
});

async function render(page: Page): Promise<ReactTestRenderer> {
  setDictionaries("japanese", {}, {}, {});
  mocks.pauseMenu.current = { disconnected: false, bugReport: ready, page };
  let renderer: ReactTestRenderer;
  await act(async () => {
    renderer = create(createElement(PauseMenuPanel));
  });
  return renderer!;
}

async function setPage(renderer: ReactTestRenderer, page: Page) {
  mocks.pauseMenu.current = { disconnected: false, bugReport: ready, page };
  await act(async () => renderer.update(createElement(PauseMenuPanel)));
}

function byTestId(renderer: ReactTestRenderer, id: string) {
  return renderer.root.findAll((node) => typeof node.type === "string" && node.props["data-testid"] === id);
}
