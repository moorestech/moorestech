// 開始ゲートは辞書を配る WebUiGameBinder より前に出る。辞書前文言が無いと文言ゼロの黒画面になる（実機で再現）
// The start gates precede WebUiGameBinder's dictionary; without pre-dictionary copy they leave a blank black screen (reproduced on a real boot)
import { readFileSync } from "node:fs";
import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import { PreDictionaryText } from "@/shared/i18n/preDictionaryText";
// @ts-expect-error -- The build script is intentionally a plain ESM module.
import { parseLocalizationCsv } from "../../../scripts/generate-localization-keys.mjs";

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  dispatchActionOutcome: vi.fn(),
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

import { CrashReportGate } from "./CrashReportGate";
import { PlaytestConsentGate } from "./PlaytestConsentGate";

// このファイルは辞書を一度も配らないので、実物の t() が辞書未着（dictionaryAbsent）の経路を通る
// This file never delivers a dictionary, so the real t() runs through the dictionary-absent path
describe("開始ゲートの辞書前文言", () => {
  it("辞書が来ていなくても同意ゲートの見出し・本文・ボタンに文言が出る", async () => {
    const renderer = await render(PlaytestConsentGate);

    expect(allTexts(renderer)).toContain(PreDictionaryText["ui.playtest.consent.title"]);
    expect(allTexts(renderer)).toContain(PreDictionaryText["ui.playtest.consent.body"]);
    expect(allTexts(renderer)).toContain(PreDictionaryText["ui.playtest.consent.agree"]);
    expect(titleClassName(renderer, "playtest-consent-gate-title")).toContain("title");
    act(() => renderer.unmount());
  });

  it("辞書が来ていなくてもクラッシュゲートの見出し・本文・両ボタンと入力欄の説明に文言が出る", async () => {
    const renderer = await render(CrashReportGate);

    expect(allTexts(renderer)).toContain(PreDictionaryText["ui.playtest.crashGate.title"]);
    expect(allTexts(renderer)).toContain(PreDictionaryText["ui.playtest.crashGate.body"]);
    expect(allTexts(renderer)).toContain(PreDictionaryText["ui.playtest.crashGate.send"]);
    expect(allTexts(renderer)).toContain(PreDictionaryText["ui.playtest.crashGate.skip"]);
    expect(descriptionPlaceholder(renderer)).toBe(PreDictionaryText["ui.playtest.crashGate.placeholder"]);
    expect(titleClassName(renderer, "crash-report-gate-title")).toContain("title");
    act(() => renderer.unmount());
  });
});

// 辞書経由の文言（csv）と辞書前文言は二重管理になる。ゲートを読むテスターは常に辞書前文言を読み、
// 編集されるのはcsv側なので、両者が食い違っても誰も気づけない。この突き合わせが唯一の検査点
// The csv copy and the pre-dictionary copy are kept twice: testers always read the pre-dictionary copy while edits land in the csv,
// so a divergence would go unnoticed. This comparison is the only place the two are checked against each other
describe("開始ゲートの辞書前文言とlocalization.csvの突き合わせ", () => {
  const csvPath = new URL("../../../../../Localization/localization.csv", import.meta.url);
  const csv = parseLocalizationCsv(readFileSync(csvPath, "utf8")) as {
    languageCodes: string[];
    rows: { key: string; texts: string[] }[];
  };
  const englishColumn = csv.languageCodes.indexOf("english");
  const japaneseColumn = csv.languageCodes.indexOf("japanese");
  const germanColumn = csv.languageCodes.indexOf("german");
  const gateKeys = csv.rows.map((row) => row.key).filter((key) => /^ui\.playtest\.(crashGate|consent|gate)\./.test(key));
  const preDictionary = PreDictionaryText as Readonly<Record<string, string>>;

  it("csv に german 列がある", () => {
    expect(germanColumn).toBeGreaterThanOrEqual(0);
  });

  it.each(gateKeys)("%s の辞書前文言は csv の english / japanese / german と一致する", (key) => {
    const row = csv.rows.find((candidate) => candidate.key === key)!;
    expect(preDictionary[key]).toBe(`${row.texts[englishColumn]} / ${row.texts[japaneseColumn]} / ${row.texts[germanColumn]}`);
  });

  // ゲート用キーを足して辞書前文言を足し忘れると、辞書配信前の画面だけ文言ゼロで出る
  // Adding a gate key without its pre-dictionary copy leaves the pre-dictionary screen with no copy at all
  it("csv の ui.playtest.crashGate.* / consent.* / gate.* と辞書前文言表の playtest キーは過不足なく一致する", () => {
    const tableKeys = Object.keys(PreDictionaryText).filter((key) => key.startsWith("ui.playtest."));
    expect([...gateKeys].sort()).toEqual(tableKeys.sort());
  });
});

async function render(gate: (props: { visible: boolean }) => JSX.Element | null): Promise<ReactTestRenderer> {
  let renderer!: ReactTestRenderer;
  await act(async () => {
    renderer = create(createElement(gate, { visible: true }));
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

// 見出しの幅制約が落ちると、辞書未確定の多言語併記が画面端に接触して折り返す（目視QA 2026-09-14 に発現）
// Without the heading's width constraint the dictionary-less multi-language pairing touches the screen edge and wraps (seen in the 2026-09-14 visual QA)
function titleClassName(renderer: ReactTestRenderer, testId: string): string {
  return renderer.root.findAll((node) => node.props["data-testid"] === testId)[0].props.className;
}

function descriptionPlaceholder(renderer: ReactTestRenderer): string {
  return renderer.root.findAll((node) => node.props["data-testid"] === "crash-report-description")[0].props.placeholder;
}
