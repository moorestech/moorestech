import { createElement } from "react";
import { act } from "react-test-renderer";
import { expect, it } from "vitest";
import { getMocks, modeSwitch, renderMutableDraft, sendButton, statusTexts } from "./testSupport";
import { BugReportForm } from "../BugReportForm";

it("応答待ちが終了してもホスト送信中は感想の種別と本文を保護する", async () => {
  const mocks = getMocks();
  let resolveSubmit: (accepted: boolean) => void = () => {};
  mocks.dispatchAction.mockImplementationOnce(() => new Promise((resolve) => { resolveSubmit = resolve; }));
  const draft = { kind: "feedback" as const, description: "感想", setKind: () => {}, setDescription: () => {}, reset: () => {} };
  const renderer = await renderMutableDraft({ kind: "ready", missing: [] }, draft);

  // ホスト処理はローカル応答待ちより長く続く
  // Host processing outlives the local response wait
  act(() => { void sendButton(renderer).props.onClick(); });
  act(() => renderer.update(createElement(BugReportForm, { status: { kind: "submitting", missing: [] }, draft })));
  await act(async () => { resolveSubmit(false); });
  expect(modeSwitch(renderer).props.disabled).toBe(true);
  expect(renderer.root.findByProps({ "data-testid": "bug-report-description" }).props.disabled).toBe(true);
  expect(sendButton(renderer).props.disabled).toBe(true);
  expect(statusTexts(renderer)).toEqual(["感想を書き出しています…"]);
  expect(mocks.dispatchAction).toHaveBeenCalledWith("bug_report.submit", { description: "感想", kind: "feedback" });
  expect(mocks.emitToast).not.toHaveBeenCalled();

  // ホスト処理完了で書きかけを編集できる
  // Editing resumes when host processing finishes
  act(() => renderer.update(createElement(BugReportForm, { status: { kind: "ready", missing: [] }, draft })));
  expect(modeSwitch(renderer).props.disabled).toBe(false);
  expect(renderer.root.findByProps({ "data-testid": "bug-report-description" }).props.disabled).toBe(false);
  expect(sendButton(renderer).props.disabled).toBe(false);
  act(() => renderer.unmount());
});
