import { createElement } from "react";
import { act } from "react-test-renderer";
import { describe, expect, it } from "vitest";
import { loadProductionDictionaries, renderMutableDraft, statusTexts } from "./testSupport";
import { BugReportForm } from "../BugReportForm";
import type { PauseMenuReportKind } from "@/bridge";

// 本番翻訳は独立した期待値で検査
// Check production translations against independent expectations
const expectations = [
  { locale: "english", feedback: "Writing the impressions…", bug: "Writing the bug report…" },
  { locale: "japanese", feedback: "感想を書き出しています…", bug: "バグ報告を書き出しています…" },
  { locale: "german", feedback: "Eindrücke werden geschrieben…", bug: "Fehlerbericht wird geschrieben…" },
  { locale: "korean", feedback: "감상을 작성하는 중…", bug: "버그 신고를 작성하는 중…" },
];

describe("BugReportForm production sending labels", () => {
  for (const { locale, feedback, bug } of expectations) {
    for (const kind of ["feedback", "bug"] as const) {
      it(`${locale}: ${kind}のホスト送信表示は本番CSVを使う`, async () => {
        const draft = { kind: kind as PauseMenuReportKind, description: "report", setKind: () => {}, setDescription: () => {}, reset: () => {} };
        const renderer = await renderMutableDraft({ kind: "ready", missing: [] }, draft);

        // 辞書更新後の状態遷移も検査
        // Verify a status transition after replacing dictionaries
        act(() => loadProductionDictionaries(locale));
        act(() => renderer.update(createElement(BugReportForm, { delivery: { kind: "allSent" }, status: { kind: "submitting", missing: [] }, draft })));
        expect(statusTexts(renderer)).toEqual([kind === "feedback" ? feedback : bug]);
        act(() => renderer.unmount());
      });
    }
  }
});
