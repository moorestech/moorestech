// 開始ゲートは辞書を配る WebUiGameBinder より前に出る。t() の空文字で描くと文言ゼロの黒画面になる（実機で再現）
// The start gates precede WebUiGameBinder's dictionary, and rendering t()'s empty strings leaves a blank black screen (reproduced on a real boot)
import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { DictionaryIndependentText } from "@/shared/i18n";

const mocks = vi.hoisted(() => ({
  dispatchAction: vi.fn(),
}));

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  useTopicSelector: (_topic: unknown, select: (data: unknown) => unknown) => select({ waiting: true }),
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

// 辞書未確定の状態を再現する。t() は辞書が無い間つねに第3引数へ落ちる（i18nStore の dictionaryAbsent）
// Reproduces the not-ready state: without a dictionary t() always drops to its third argument (i18nStore's dictionaryAbsent)
vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({
    status: "loading",
    locale: "english",
    requestedLocale: "english",
    t: (_key: string, _values?: unknown, fallback?: string) => fallback ?? "",
    resolveTranslation: () => ({ kind: "dictionaryAbsent" }),
  }),
}));

import { CrashReportGate } from "./CrashReportGate";
import { PlaytestConsentGate } from "./PlaytestConsentGate";

beforeEach(() => {
  mocks.dispatchAction.mockResolvedValue(true);
});

describe("開始ゲートの辞書非依存フォールバック", () => {
  it("辞書が来ていなくても同意ゲートの見出し・本文・ボタンに文言が出る", async () => {
    const renderer = await render(PlaytestConsentGate);

    expect(allTexts(renderer)).toContain(DictionaryIndependentText.playtestConsentTitle);
    expect(allTexts(renderer)).toContain(DictionaryIndependentText.playtestConsentBody);
    expect(allTexts(renderer)).toContain(DictionaryIndependentText.playtestConsentAgree);
    expect(titleClassName(renderer, "playtest-consent-gate-title")).toContain("title");
    act(() => renderer.unmount());
  });

  it("辞書が来ていなくてもクラッシュゲートの見出し・本文・両ボタンと入力欄の説明に文言が出る", async () => {
    const renderer = await render(CrashReportGate);

    expect(allTexts(renderer)).toContain(DictionaryIndependentText.crashGateTitle);
    expect(allTexts(renderer)).toContain(DictionaryIndependentText.crashGateBody);
    expect(allTexts(renderer)).toContain(DictionaryIndependentText.crashGateSend);
    expect(allTexts(renderer)).toContain(DictionaryIndependentText.crashGateSkip);
    expect(descriptionPlaceholder(renderer)).toBe(DictionaryIndependentText.crashGatePlaceholder);
    expect(titleClassName(renderer, "crash-report-gate-title")).toContain("title");
    act(() => renderer.unmount());
  });
});

async function render(gate: () => JSX.Element | null): Promise<ReactTestRenderer> {
  let renderer!: ReactTestRenderer;
  await act(async () => {
    renderer = create(createElement(gate));
  });
  return renderer;
}

// 文言はTitle・Text・Buttonへ散っているため、描画木の文字列ノードをすべて集めて突き合わせる
// The copy is spread across Title, Text and Button, so every string node in the tree is collected and compared
function allTexts(renderer: ReactTestRenderer): string[] {
  const texts: string[] = [];
  collect(renderer.toJSON());
  return texts;

  function collect(node: unknown): void {
    if (typeof node === "string") {
      texts.push(node);
      return;
    }
    if (Array.isArray(node)) {
      node.forEach(collect);
      return;
    }
    const children = (node as { children?: unknown[] } | null)?.children;
    if (children) children.forEach(collect);
  }
}

// 見出しの幅制約が落ちると、辞書未確定のEN/JA併記が画面端に接触して折り返す（目視QA 2026-09-14 に発現）
// Without the heading's width constraint the dictionary-less EN/JA pairing touches the screen edge and wraps (seen in the 2026-09-14 visual QA)
function titleClassName(renderer: ReactTestRenderer, testId: string): string {
  return renderer.root.findAll((node) => node.props["data-testid"] === testId)[0].props.className;
}

function descriptionPlaceholder(renderer: ReactTestRenderer): string {
  return renderer.root.findAll((node) => node.props["data-testid"] === "crash-report-description")[0].props.placeholder;
}
