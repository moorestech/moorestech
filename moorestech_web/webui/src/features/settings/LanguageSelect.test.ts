import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import { setDictionaries } from "@/shared/i18n/i18nStore";

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  useTopic: () => ({ locale: "english", revision: 1 }),
  dispatchAction: vi.fn(),
}));
vi.mock("@mantine/core", () => ({
  Title: ({ children, ...props }: { children: unknown }) => createElement("mock-title", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
  Button: ({ children, ...props }: { children: unknown }) => createElement("mock-button", props, children as never),
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
}));
vi.mock("@/shared/ui", () => ({
  ModeSwitch: (props: object) => createElement("mock-mode-switch", props),
}));

import { LanguageSelect } from "./LanguageSelect";

const languagesResponse = [
  { code: "english", displayName: "English" },
  { code: "japanese", displayName: "日本語" },
];

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("LanguageSelect", () => {
  it("一覧取得に成功したら配信順のoptionsを並べる", async () => {
    setDictionaries("english", {}, {}, {});
    vi.stubGlobal("fetch", vi.fn(() =>
      Promise.resolve({ ok: true, json: () => Promise.resolve(languagesResponse) })));

    const renderer = await renderLanguageSelect();

    expect(optionValues(renderer)).toEqual(["english", "japanese"]);
    expect(textsByTestId(renderer, "language-list-error")).toEqual([]);
    act(() => renderer.unmount());
  });

  it("一覧取得に失敗したら辞書非依存リテラルのエラーと再試行ボタンを描画する", async () => {
    // 辞書は空のままなのでt()経由なら描画は壊れる。リテラルであることの証明になる
    // The dictionary stays empty, so anything routed through t() would break: this proves the literal path
    setDictionaries("english", {}, {}, {});
    vi.stubGlobal("fetch", vi.fn(() => Promise.resolve({ ok: false, status: 500 })));

    const renderer = await renderLanguageSelect();

    expect(textsByTestId(renderer, "language-list-error"))
      .toEqual(["Failed to load the language list. / 言語一覧の読み込みに失敗しました。"]);
    expect(textsByTestId(renderer, "language-list-retry")).toEqual(["Retry / 再試行"]);
    expect(renderer.root.findAllByType("mock-mode-switch" as never)).toEqual([]);
    act(() => renderer.unmount());
  });

  it("選択肢ゼロ件はエラー扱いになりModeSwitchを描かない", async () => {
    setDictionaries("english", {}, {}, {});
    vi.stubGlobal("fetch", vi.fn(() => Promise.resolve({ ok: true, json: () => Promise.resolve([]) })));

    const renderer = await renderLanguageSelect();

    expect(textsByTestId(renderer, "language-list-error")).toHaveLength(1);
    expect(renderer.root.findAllByType("mock-mode-switch" as never)).toEqual([]);
    act(() => renderer.unmount());
  });

  it("再試行ボタンで再取得し、成功したらoptionsへ復帰する", async () => {
    setDictionaries("english", {}, {}, {});
    const fetchMock = vi.fn()
      .mockImplementationOnce(() => Promise.resolve({ ok: false, status: 500 }))
      .mockImplementationOnce(() => Promise.resolve({ ok: true, json: () => Promise.resolve(languagesResponse) }));
    vi.stubGlobal("fetch", fetchMock);

    const renderer = await renderLanguageSelect();
    expect(textsByTestId(renderer, "language-list-error")).toHaveLength(1);

    await act(async () => {
      retryButton(renderer).props.onClick();
    });

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(optionValues(renderer)).toEqual(["english", "japanese"]);
    expect(textsByTestId(renderer, "language-list-error")).toEqual([]);
    act(() => renderer.unmount());
  });
});

async function renderLanguageSelect(): Promise<ReactTestRenderer> {
  let renderer: ReactTestRenderer;
  await act(async () => {
    renderer = create(createElement(LanguageSelect));
  });
  return renderer!;
}

function optionValues(renderer: ReactTestRenderer): string[] {
  const modeSwitch = renderer.root.findAllByType("mock-mode-switch" as never)[0];
  return (modeSwitch.props.options as Array<{ value: string }>).map((option) => option.value);
}

function textsByTestId(renderer: ReactTestRenderer, testId: string): string[] {
  return hostNodesByTestId(renderer, testId).map((node) => String(node.children[0]));
}

function retryButton(renderer: ReactTestRenderer) {
  return hostNodesByTestId(renderer, "language-list-retry")[0];
}

function hostNodesByTestId(renderer: ReactTestRenderer, testId: string) {
  return renderer.root.findAll((node) =>
    typeof node.type === "string" && node.props["data-testid"] === testId);
}
