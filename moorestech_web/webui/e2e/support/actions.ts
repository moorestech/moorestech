import type { Page } from "@playwright/test";

export type ActionRecord = { type: string; payload: unknown };

// 指定 type の action payload 一覧。received は全テスト横断で蓄積されるため、呼び出し側は全等値で照合する
// Payloads of a given action type; received accumulates across tests, so callers must match by full equality
export async function payloadsOf(page: Page, type: string): Promise<unknown[]> {
  const actions: ActionRecord[] = await page.request.get("/__actions").then((r) => r.json());
  return actions.filter((a) => a.type === type).map((a) => a.payload);
}

// 全 action レコードを返す（type 以外の検証や件数比較用）
// Return every recorded action (for non-type assertions and count checks)
export async function allActions(page: Page): Promise<ActionRecord[]> {
  return page.request.get("/__actions").then((r) => r.json());
}
