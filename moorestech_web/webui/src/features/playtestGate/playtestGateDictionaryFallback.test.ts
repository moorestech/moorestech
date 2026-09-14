// 開始ゲートは辞書を配る WebUiGameBinder より前に出る。t() の空文字で描くと文言ゼロの黒画面になる（実機で再現）
// The start gates precede WebUiGameBinder's dictionary, and rendering t()'s empty strings leaves a blank black screen (reproduced on a real boot)
import { readFileSync } from "node:fs";
import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { DictionaryIndependentText } from "@/shared/i18n";
// @ts-expect-error -- The build script is intentionally a plain ESM module.
import { parseLocalizationCsv } from "../../../scripts/generate-localization-keys.mjs";

const mocks = vi.hoisted(() => ({
  dispatchActionOutcome: vi.fn(),
  waitingTopic: "",
}));

// 外殻は3ゲート全ての待機を読んで手前の1枚だけ描くため、待機させるゲートを1つに絞って返す
// The shell reads all three gates' waiting flags and draws only the frontmost, so exactly one gate is put into waiting
vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  useTopicSelector: (topic: string, select: (data: unknown) => unknown) => select({ waiting: topic === mocks.waitingTopic }),
  dispatchActionOutcome: mocks.dispatchActionOutcome,
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

import { Topics } from "@/bridge";
import { CrashReportGate } from "./CrashReportGate";
import { PlaytestConsentGate } from "./PlaytestConsentGate";

beforeEach(() => {
  mocks.dispatchActionOutcome.mockResolvedValue({ kind: "accepted" });
});

describe("開始ゲートの辞書非依存フォールバック", () => {
  it("辞書が来ていなくても同意ゲートの見出し・本文・ボタンに文言が出る", async () => {
    mocks.waitingTopic = Topics.consentGate;
    const renderer = await render(PlaytestConsentGate);

    expect(allTexts(renderer)).toContain(DictionaryIndependentText.playtestConsentTitle);
    expect(allTexts(renderer)).toContain(DictionaryIndependentText.playtestConsentBody);
    expect(allTexts(renderer)).toContain(DictionaryIndependentText.playtestConsentAgree);
    expect(titleClassName(renderer, "playtest-consent-gate-title")).toContain("title");
    act(() => renderer.unmount());
  });

  it("辞書が来ていなくてもクラッシュゲートの見出し・本文・両ボタンと入力欄の説明に文言が出る", async () => {
    mocks.waitingTopic = Topics.crashReportGate;
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

// 辞書経由の文言（csv）とfallback定数は二重管理になる。ゲートを読むテスターは常にfallback側を読み、
// 編集されるのはcsv側なので、両者が食い違っても誰も気づけない。この表が唯一の突き合わせ点
// The csv copy and the fallback constants are kept twice: testers always read the fallback while edits land in the csv,
// so a divergence would go unnoticed. This table is the only place the two are checked against each other
const CsvKeyToFallback: Readonly<Record<string, string>> = {
  "ui.playtest.consent.title": DictionaryIndependentText.playtestConsentTitle,
  "ui.playtest.consent.body": DictionaryIndependentText.playtestConsentBody,
  "ui.playtest.consent.agree": DictionaryIndependentText.playtestConsentAgree,
  "ui.playtest.crashGate.title": DictionaryIndependentText.crashGateTitle,
  "ui.playtest.crashGate.body": DictionaryIndependentText.crashGateBody,
  "ui.playtest.crashGate.placeholder": DictionaryIndependentText.crashGatePlaceholder,
  "ui.playtest.crashGate.send": DictionaryIndependentText.crashGateSend,
  "ui.playtest.crashGate.skip": DictionaryIndependentText.crashGateSkip,
  "ui.playtest.gate.respondFailed": DictionaryIndependentText.gateRespondFailed,
  "ui.playtest.gate.answerAccepted": DictionaryIndependentText.gateAnswerAccepted,
  "ui.playtest.gate.disconnected": DictionaryIndependentText.gateDisconnected,
  "ui.playtest.gate.notClosed": DictionaryIndependentText.gateNotClosed,
};

describe("開始ゲートのfallback文言とlocalization.csvの突き合わせ", () => {
  const csvPath = new URL("../../../../../Localization/localization.csv", import.meta.url);
  const csv = parseLocalizationCsv(readFileSync(csvPath, "utf8")) as {
    languageCodes: string[];
    rows: { key: string; texts: string[] }[];
  };
  const englishColumn = csv.languageCodes.indexOf("english");
  const japaneseColumn = csv.languageCodes.indexOf("japanese");

  it.each(Object.keys(CsvKeyToFallback))("%s のfallbackは csv の english / japanese と一致する", (key) => {
    const row = csv.rows.find((candidate) => candidate.key === key);
    expect(row, `${key} が localization.csv に無い`).toBeTruthy();
    expect(CsvKeyToFallback[key]).toBe(`${row!.texts[englishColumn]} / ${row!.texts[japaneseColumn]}`);
  });

  // ゲート用キーを足してfallbackを足し忘れると、辞書配信前の画面だけ文言ゼロで出る
  // Adding a gate key without its fallback leaves the pre-dictionary screen with no copy at all
  it("csv の ui.playtest.crashGate.* / consent.* / gate.* は全てfallbackを持つ", () => {
    const gateKeys = csv.rows
      .map((row) => row.key)
      .filter((key) => /^ui\.playtest\.(crashGate|consent|gate)\./.test(key));

    expect(gateKeys.sort()).toEqual(Object.keys(CsvKeyToFallback).sort());
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
