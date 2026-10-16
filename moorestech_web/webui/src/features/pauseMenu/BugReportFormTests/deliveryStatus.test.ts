import { act } from "react-test-renderer";
import { expect, it } from "vitest";
import type { PauseMenuData } from "@/bridge";
import { loadProductionDictionaries, renderDelivery, statusTexts } from "./testSupport";

const cases: [PauseMenuData["delivery"], string][] = [
  [{ kind: "localOnly", reason: "developerTokenMissing", inventory: { kind: "known", pendingCount: 3, failedCount: 0 } }, "未送信: 開発者トークンが未設定です（この機材に3件保存）"],
  [{ kind: "localOnly", reason: "developerTokenUnreadable", inventory: { kind: "known", pendingCount: 3, failedCount: 0 } }, "未送信: 開発者トークンを読めません（この機材に3件保存）"],
  [{ kind: "localOnly", reason: "distributionUnavailable", inventory: { kind: "known", pendingCount: 3, failedCount: 0 } }, "未送信: build-infoとSteamの送信条件を確認してください（この機材に3件保存）"],
  [{ kind: "pending", inventory: { kind: "known", pendingCount: 3, failedCount: 0 } }, "送信待ち 3件"],
  [{ kind: "refused", inventory: { kind: "known", pendingCount: 3, failedCount: 0 } }, "受け口に拒否されました。トークン失効の可能性があります（送信待ち3件）"],
  [{ kind: "allSent" }, "すべての報告を送信しました"],
];

it.each(cases)("送信状況 %j と送信可否を同時に表示する", async (delivery, expected) => {
  const renderer = await renderDelivery({ kind: "noSession", missing: [] }, delivery);
  await act(async () => loadProductionDictionaries("japanese"));
  expect(renderer.root.findByProps({ "data-testid": "bug-report-delivery" }).children.join("")).toBe(expected);
  expect(statusTexts(renderer)).toHaveLength(1);
  act(() => renderer.unmount());
});


it.each(["japanese", "english", "german", "korean"])("%s辞書で配送不能件数と復旧案内を出す", async locale => {
  const renderer = await renderDelivery({ kind: "ready", missing: [] }, { kind: "pending", inventory: { kind: "known", pendingCount: 0, failedCount: 2 } });
  await act(async () => loadProductionDictionaries(locale));
  const text = renderer.root.findByProps({ "data-testid": "bug-report-delivery" }).children.join("");
  expect(text).toContain("2");
  expect(text).not.toContain("ui.bugReport.deliveryFailed");
  expect(text).toMatch(/再送復旧|recover|Wiederherstellung|재전송 복구/);
  act(() => renderer.unmount());
});

it.each(["japanese", "english", "german", "korean"])("%s辞書で件数不明を完了や0件にしない", async locale => {
  const renderer = await renderDelivery({ kind: "ready", missing: [] }, { kind: "pending", inventory: { kind: "unavailable", reason: "access denied" } });
  await act(async () => loadProductionDictionaries(locale));
  const text = renderer.root.findByProps({ "data-testid": "bug-report-delivery" }).children.join("");
  expect(text).toContain("access denied");
  expect(text).not.toContain("0");
  expect(text).not.toContain("ui.bugReport.");
  act(() => renderer.unmount());
});
