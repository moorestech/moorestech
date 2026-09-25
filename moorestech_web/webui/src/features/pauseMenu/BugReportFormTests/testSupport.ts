import { createElement, type ReactNode } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, vi } from "vitest";
import { setDictionaries } from "@/shared/i18n/i18nStore";
import { useBugReportDraft } from "../useBugReportDraft";

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
  setDictionaries("japanese", dictionary, {}, {});
  let renderer: ReactTestRenderer;
  await act(async () => {
    renderer = create(createElement(FormWithDraft, { status }));
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
