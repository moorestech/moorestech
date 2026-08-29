import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import { setDictionaries } from "@/shared/i18n/i18nStore";

const mocks = vi.hoisted(() => ({ entries: null as unknown }));

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  useTopic: () => ({ locale: "english", revision: 1 }),
  dispatchAction: vi.fn(),
  // 一覧の出所はストアなので、フックの代わりに購読結果だけを差し替える
  // The list comes from the store, so only the subscription result is swapped in place of the hook
  useLanguageList: () => (mocks.entries === null
    ? { status: "loading" }
    : { status: "ready", entries: mocks.entries }),
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

const languageEntries = [
  { code: "english", displayName: "English" },
  { code: "japanese", displayName: "日本語" },
];

afterEach(() => {
  vi.restoreAllMocks();
  mocks.entries = null;
});

describe("LanguageSelect", () => {
  it("一覧が届いたら配信順のoptionsを並べる", async () => {
    setDictionaries("english", {}, {}, {});
    mocks.entries = languageEntries;

    const renderer = await renderLanguageSelect();

    expect(optionValues(renderer)).toEqual(["english", "japanese"]);
    expect(textsByTestId(renderer, "language-list-loading")).toEqual([]);
    act(() => renderer.unmount());
  });

  it("一覧が届くまでは辞書非依存リテラルの読み込み中を出しModeSwitchを描かない", async () => {
    // 辞書は空のままなのでt()経由なら描画は壊れる。リテラルであることの証明になる
    // The dictionary stays empty, so anything routed through t() would break: this proves the literal path
    setDictionaries("english", {}, {}, {});

    const renderer = await renderLanguageSelect();

    expect(textsByTestId(renderer, "language-list-loading")).toEqual(["Loading… / 読み込み中…"]);
    expect(renderer.root.findAllByType("mock-mode-switch" as never)).toEqual([]);
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

function hostNodesByTestId(renderer: ReactTestRenderer, testId: string) {
  return renderer.root.findAll((node) =>
    typeof node.type === "string" && node.props["data-testid"] === testId);
}
