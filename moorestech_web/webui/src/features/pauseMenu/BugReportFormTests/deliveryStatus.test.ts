import { act } from "react-test-renderer";
import { expect, it } from "vitest";
import type { PauseMenuData } from "@/bridge";
import { loadProductionDictionaries, renderDelivery, statusTexts } from "./testSupport";

const cases: [PauseMenuData["delivery"], string][] = [
  [{ kind: "localOnly", reason: "developerTokenMissing", pendingCount: 3 }, "未送信: 開発者トークンが未設定です（この機材に3件保存）"],
  [{ kind: "localOnly", reason: "developerTokenUnreadable", pendingCount: 3 }, "未送信: 開発者トークンを読めません（この機材に3件保存）"],
  [{ kind: "localOnly", reason: "steamNotRunning", pendingCount: 3 }, "未送信: Steamが起動していません（この機材に3件保存）"],
  [{ kind: "pending", pendingCount: 3 }, "送信待ち 3件"],
  [{ kind: "refused", pendingCount: 3 }, "受け口に拒否されました。トークン失効の可能性があります（送信待ち3件）"],
  [{ kind: "allSent" }, "すべての報告を送信しました"],
];

it.each(cases)("送信状況 %j と送信可否を同時に表示する", async (delivery, expected) => {
  const renderer = await renderDelivery({ kind: "noSession", missing: [] }, delivery);
  await act(async () => loadProductionDictionaries("japanese"));
  expect(renderer.root.findByProps({ "data-testid": "bug-report-delivery" }).children.join("")).toBe(expected);
  expect(statusTexts(renderer)).toHaveLength(1);
  act(() => renderer.unmount());
});
