import { describe, it, expect, vi } from "vitest";

// webSocketClient はモジュール読み込み時に location.host を参照するため node 環境で stub する
// Stub webSocketClient because it touches location.host at import time, which is absent in node
vi.mock("./webSocketClient", () => ({ sendAction: vi.fn() }));

import { shouldToastFailure } from "./actions";

describe("shouldToastFailure", () => {
  // インベントリ操作はクリック連鎖で良性の失敗が出るためトーストしない
  // Inventory ops produce benign failures from click chains, so they are not toasted
  it("インベントリ操作の失敗はトーストしない", () => {
    expect(shouldToastFailure("inventory.move_item")).toBe(false);
    expect(shouldToastFailure("inventory.split")).toBe(false);
    expect(shouldToastFailure("inventory.collect")).toBe(false);
    expect(shouldToastFailure("inventory.sort")).toBe(false);
  });

  it("非インベントリ操作の失敗はトーストする", () => {
    expect(shouldToastFailure("craft.execute")).toBe(true);
    expect(shouldToastFailure("debug.echo")).toBe(true);
  });
});
