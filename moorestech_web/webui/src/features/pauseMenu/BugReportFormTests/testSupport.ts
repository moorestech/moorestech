import { readFileSync } from "node:fs";
import { createElement, type ReactElement, type ReactNode } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, vi } from "vitest";
import { setDictionaries } from "@/shared/i18n/i18nStore";
import type { BugReportDraft } from "../useBugReportDraft";
import { useBugReportDraft } from "../useBugReportDraft";
// Node用パーサーをテストでも使う
// Reuse the Node parser in tests
// @ts-expect-error -- The build script is intentionally a plain ESM module.
import { parseLocalizationCsv } from "../../../../scripts/generate-localization-keys.mjs";

const mocks = vi.hoisted(() => ({
  dispatchAction: vi.fn(async (): Promise<boolean> => true),
  emitToast: vi.fn(),
}));

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  dispatchAction: mocks.dispatchAction,
}));
vi.mock("@/features/toast", () => ({ emitToast: mocks.emitToast }));
// node環境ではMantineのcolor scheme hookがwindowを触るため、素のbuttonへ差し替える（前例: 共有UIのスタブ）
// Mantine's color-scheme hook touches window under the node env, so it is swapped for a bare button (precedent: stubbing shared UI)
vi.mock("@mantine/core", () => ({
  Button: ({ children, ...rest }: { children: ReactNode }) => createElement("button", rest, children),
}));
// ModeSwitchは前例と同UIスタブ
// ModeSwitch stubbed same as precedent (LanguageSelect)
vi.mock("@/shared/ui", () => ({
  ModeSwitch: ({ value, onChange, disabled, testId }: { value: string; onChange: (v: string) => void; disabled?: boolean; testId?: string }) =>
    createElement("mock-mode-switch", { value, onChange, disabled, "data-testid": testId }),
}));

import { BugReportForm } from "../BugReportForm";

type Status = { kind: "noSession" | "capturing" | "submitting" | "ready"; missing: string[] };

const dictionary = {
  "ui.bugReport.placeholder": "何が起きた？",
  "ui.bugReport.send": "バグ報告を送信",
  "ui.bugReport.sent": "書き出しました",
  "ui.feedbackReport.send": "感想を送信",
  "ui.feedbackReport.sent": "感想を書き出しました",
  "ui.feedbackReport.sending": "感想を書き出しています…",
  "ui.bugReport.capturePending": "記録を確保しています…",
  "ui.bugReport.missing": "欠けている項目: {items}",
  "ui.bugReport.noSession": "ポーズメニューを開き直してください",
  "ui.bugReport.sending": "書き出しています…",
  "ui.playtest.reportKind.label": "報告の種別",
  "ui.playtest.reportKind.bug": "バグ",
  "ui.playtest.reportKind.feedback": "感想",
};

afterEach(() => {
  vi.clearAllMocks();
  mocks.dispatchAction.mockImplementation(async () => true);
});

export function getMocks() {
  return mocks;
}

function FormWithDraft({ status }: { status: Status }) {
  const draft = useBugReportDraft();
  return createElement(BugReportForm, { status, draft });
}

export async function render(status: Status): Promise<ReactTestRenderer> {
  return renderElement(createElement(FormWithDraft, { status }));
}

// 描画の初期化を共用する
// Share renderer initialization
async function renderElement(element: ReactElement): Promise<ReactTestRenderer> {
  setDictionaries("japanese", dictionary, {}, {});
  let renderer: ReactTestRenderer;
  await act(async () => {
    renderer = create(element);
  });
  return renderer!;
}

export function sendButton(renderer: ReactTestRenderer) {
  return renderer.root.find((node) => node.type === "button" && node.props["data-testid"] === "bug-report-send");
}

export function modeSwitch(renderer: ReactTestRenderer) {
  return renderer.root.findByProps({ "data-testid": "bug-report-kind" });
}

export function statusTexts(renderer: ReactTestRenderer): string[] {
  return renderer.root.findAll((node) => node.type === "span" && node.props["data-testid"] === "bug-report-status").map((node) => node.children.join(""));
}

export async function renderMutableDraft(status: Status, draft: BugReportDraft): Promise<ReactTestRenderer> {
  return renderElement(createElement(BugReportForm, { status, draft }));
}

// 正規CSVの辞書で再描画する
// Rerender with dictionaries from the production CSV
export function loadProductionDictionaries(locale: string): void {
  const csv: { languageCodes: string[]; rows: { key: string; texts: string[]; source: string }[] } =
    parseLocalizationCsv(readFileSync(new URL("../../../../../../Localization/localization.csv", import.meta.url), "utf8"));
  const languageIndex = csv.languageCodes.indexOf(locale);
  const englishIndex = csv.languageCodes.indexOf("english");
  setDictionaries(locale,
    Object.fromEntries(csv.rows.map((row) => [row.key, row.texts[languageIndex]])),
    Object.fromEntries(csv.rows.map((row) => [row.key, row.texts[englishIndex]])),
    Object.fromEntries(csv.rows.map((row) => [row.key, row.source])));
}
