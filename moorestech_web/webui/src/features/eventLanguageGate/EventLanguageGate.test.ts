import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { setDictionaries } from "@/shared/i18n/i18nStore";

const mocks = vi.hoisted(() => ({
  dispatchAction: vi.fn(),
  waiting: true,
  entries: null as unknown,
}));

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  useTopicSelector: (_topic: unknown, select: (data: unknown) => unknown) => select({ waiting: mocks.waiting }),
  dispatchAction: mocks.dispatchAction,
  // 一覧の出所はストアなので、フックの代わりに購読結果だけを差し替える
  // The list comes from the store, so only the subscription result is swapped in place of the hook
  useLanguageList: () => (mocks.entries === null
    ? { status: "loading" }
    : { status: "ready", entries: mocks.entries }),
}));
vi.mock("@mantine/core", () => ({
  Button: ({ children, ...props }: { children: unknown }) => createElement("mock-button", props, children as never),
  Group: ({ children, ...props }: { children: unknown }) => createElement("mock-group", props, children as never),
  Overlay: ({ children, ...props }: { children: unknown }) => createElement("mock-overlay", props, children as never),
  Portal: ({ children }: { children: unknown }) => children as never,
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
  Title: ({ children, ...props }: { children: unknown }) => createElement("mock-title", props, children as never),
}));

import { EventLanguageGate } from "./EventLanguageGate";

const languageEntries = [
  { code: "english", displayName: "English" },
  { code: "japanese", displayName: "日本語" },
  { code: "german", displayName: "Deutsch" },
];

beforeEach(() => {
  // 辞書は空のまま。見出し・案内がt()経由なら壊れるので、リテラルであることの証明になる
  // The dictionary stays empty, so anything routed through t() would break: this proves the literal path
  setDictionaries("english", {}, {}, {});
  mocks.dispatchAction.mockResolvedValue(true);
  mocks.entries = languageEntries;
});

afterEach(() => {
  mocks.dispatchAction.mockReset();
  mocks.waiting = true;
});

describe("EventLanguageGate", () => {
  it("待機中は母国語表記のボタンと英語固定の見出しを描く", async () => {
    const renderer = await renderGate();

    expect(optionLabels(renderer)).toEqual(["English", "日本語", "Deutsch"]);
    expect(headingTexts(renderer)).toEqual(["Select Language"]);
    act(() => renderer.unmount());
  });

  it("ボタン押下でlocale付きのselect_languageを送る", async () => {
    const renderer = await renderGate();
    await act(async () => { optionAt(renderer, 1).props.onClick(); });

    expect(mocks.dispatchAction).toHaveBeenCalledWith("event_mode.select_language", { locale: "japanese" });
    act(() => renderer.unmount());
  });

  it("待機していなければ何も描かず一覧も購読しない", async () => {
    mocks.waiting = false;

    const renderer = await renderGate();

    expect(renderer.toJSON()).toBeNull();
    act(() => renderer.unmount());
  });

  it("一覧が届くまでは辞書非依存リテラルの読み込み中を出す", async () => {
    mocks.entries = null;

    const renderer = await renderGate();

    expect(testIds(renderer)).toContain("event-language-gate-loading");
    expect(optionNodes(renderer)).toHaveLength(0);
    act(() => renderer.unmount());
  });

  it("選択が受理されなければ辞書非依存リテラルの失敗行を出す", async () => {
    mocks.dispatchAction.mockResolvedValue(false);

    const renderer = await renderGate();
    await act(async () => { optionAt(renderer, 0).props.onClick(); });

    expect(textsByTestId(renderer, "event-language-gate-select-failed"))
      .toEqual(["Could not start. Please press again. / 開始できませんでした。もう一度押してください。"]);
    expect(optionNodes(renderer).every((node) => node.props.disabled === false)).toBe(true);
    act(() => renderer.unmount());
  });

  it("応答待ちの間は選択ボタンを押せなくする", async () => {
    let resolveDispatch: ((accepted: boolean) => void) | undefined;
    mocks.dispatchAction.mockImplementation(() => new Promise<boolean>((resolve) => { resolveDispatch = resolve; }));

    const renderer = await renderGate();
    await act(async () => { optionAt(renderer, 0).props.onClick(); });

    expect(optionNodes(renderer).every((node) => node.props.disabled === true)).toBe(true);

    await act(async () => { resolveDispatch!(true); });

    // 受理後もゲートが消えるまで押下不可のままにし、二重クリックを通さない
    // Presses stay blocked after acceptance until the gate disappears, so a double click cannot go through
    expect(optionNodes(renderer).every((node) => node.props.disabled === true)).toBe(true);
    act(() => renderer.unmount());
  });
});

async function renderGate(): Promise<ReactTestRenderer> {
  let renderer!: ReactTestRenderer;
  await act(async () => { renderer = create(createElement(EventLanguageGate)); });
  return renderer;
}

function optionNodes(renderer: ReactTestRenderer) {
  return renderer.root.findAllByType("mock-button" as never)
    .filter((node) => String(node.props["data-testid"]).startsWith("event-language-gate-option-"));
}

function optionAt(renderer: ReactTestRenderer, index: number) {
  return optionNodes(renderer)[index];
}

function optionLabels(renderer: ReactTestRenderer): string[] {
  return optionNodes(renderer).map((node) => String(node.props.children));
}

function headingTexts(renderer: ReactTestRenderer): string[] {
  return renderer.root.findAllByType("mock-title" as never).map((node) => String(node.props.children));
}

function textsByTestId(renderer: ReactTestRenderer, testId: string): string[] {
  return renderer.root.findAll((node) =>
    typeof node.type === "string" && node.props["data-testid"] === testId)
    .map((node) => String(node.props.children));
}

function testIds(renderer: ReactTestRenderer): string[] {
  return renderer.root.findAll(() => true)
    .map((node) => node.props?.["data-testid"])
    .filter((id): id is string => typeof id === "string");
}
