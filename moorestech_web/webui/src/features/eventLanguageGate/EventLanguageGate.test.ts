import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import { setDictionaries } from "@/shared/i18n/i18nStore";

const mocks = vi.hoisted(() => ({
  dispatchAction: vi.fn(),
  waiting: true,
}));

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  useTopicSelector: (_topic: unknown, select: (data: unknown) => unknown) => select({ waiting: mocks.waiting }),
  dispatchAction: mocks.dispatchAction,
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

const languagesResponse = [
  { code: "english", displayName: "English" },
  { code: "japanese", displayName: "日本語" },
  { code: "german", displayName: "Deutsch" },
];

afterEach(() => {
  vi.unstubAllGlobals();
  mocks.dispatchAction.mockReset();
  mocks.waiting = true;
});

describe("EventLanguageGate", () => {
  it("待機中は母国語表記のボタンと英語固定の見出しを描く", async () => {
    // 辞書は空のまま。見出しがt()経由なら壊れるので、リテラルであることの証明になる
    // The dictionary stays empty, so anything routed through t() would break: this proves the literal path
    setDictionaries("english", {}, {}, {});
    stubLanguagesFetch();

    const renderer = await renderGate();

    expect(optionLabels(renderer)).toEqual(["English", "日本語", "Deutsch"]);
    expect(headingTexts(renderer)).toEqual(["Select Language"]);
    act(() => renderer.unmount());
  });

  it("ボタン押下でlocale付きのselect_languageを送る", async () => {
    setDictionaries("english", {}, {}, {});
    stubLanguagesFetch();

    const renderer = await renderGate();
    await act(async () => { optionAt(renderer, 1).props.onClick(); });

    expect(mocks.dispatchAction).toHaveBeenCalledWith("event_mode.select_language", { locale: "japanese" });
    act(() => renderer.unmount());
  });

  it("待機していなければ何も描かない", async () => {
    mocks.waiting = false;
    setDictionaries("english", {}, {}, {});
    stubLanguagesFetch();

    const renderer = await renderGate();

    expect(renderer.toJSON()).toBeNull();
    act(() => renderer.unmount());
  });

  it("一覧取得に失敗したら辞書非依存リテラルのエラーと再試行ボタンを出す", async () => {
    setDictionaries("english", {}, {}, {});
    vi.stubGlobal("fetch", vi.fn(() => Promise.resolve({ ok: false, status: 500 })));

    const renderer = await renderGate();

    expect(testIds(renderer)).toContain("event-language-gate-error");
    expect(testIds(renderer)).toContain("event-language-gate-retry");
    act(() => renderer.unmount());
  });

  it("選択肢ゼロ件はエラー扱いになり選択ボタンを描かない", async () => {
    setDictionaries("english", {}, {}, {});
    vi.stubGlobal("fetch", vi.fn(() => Promise.resolve({ ok: true, json: () => Promise.resolve([]) })));

    const renderer = await renderGate();

    expect(testIds(renderer)).toContain("event-language-gate-error");
    expect(optionNodes(renderer)).toHaveLength(0);
    act(() => renderer.unmount());
  });

  it("再試行ボタンで再取得し成功したら選択肢へ復帰する", async () => {
    setDictionaries("english", {}, {}, {});
    const fetchMock = vi.fn()
      .mockImplementationOnce(() => Promise.resolve({ ok: false, status: 500 }))
      .mockImplementationOnce(() => Promise.resolve({ ok: true, json: () => Promise.resolve(languagesResponse) }));
    vi.stubGlobal("fetch", fetchMock);

    const renderer = await renderGate();
    expect(testIds(renderer)).toContain("event-language-gate-error");

    await act(async () => {
      renderer.root.findAllByType("mock-button" as never)
        .find((node) => node.props["data-testid"] === "event-language-gate-retry")!
        .props.onClick();
    });

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(optionLabels(renderer)).toEqual(["English", "日本語", "Deutsch"]);
    act(() => renderer.unmount());
  });
});

function stubLanguagesFetch() {
  vi.stubGlobal("fetch", vi.fn(() =>
    Promise.resolve({ ok: true, json: () => Promise.resolve(languagesResponse) })));
}

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

function testIds(renderer: ReactTestRenderer): string[] {
  return renderer.root.findAll(() => true)
    .map((node) => node.props?.["data-testid"])
    .filter((id): id is string => typeof id === "string");
}
