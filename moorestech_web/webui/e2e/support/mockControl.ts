import type { Page } from "@playwright/test";

// mock-host 制御エンドポイントの薄いラッパ。URL リテラルの散在を防ぐ
// Thin wrappers over the mock-host control endpoints; keeps URL literals in one place
export function setBlock(page: Page, type: string) {
  return page.request.get(`/__block?type=${type}`);
}

export function setModal(page: Page, show: boolean) {
  return page.request.get(`/__modal?show=${show ? 1 : 0}`);
}

export function setUiState(page: Page, state: string) {
  return page.request.get(`/__uistate?state=${state}`);
}

export function resetResearch(page: Page) {
  return page.request.get("/__research");
}
