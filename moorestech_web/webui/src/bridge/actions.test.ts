import { describe, it, expect, vi, beforeEach } from "vitest";

// webSocketClient はモジュール読み込み時に location.host を参照するため node 環境で stub する
// Stub webSocketClient because it touches location.host at import time, which is absent in node
vi.mock("./webSocketClient", () => ({ sendAction: vi.fn() }));
vi.mock("./notify", () => ({ notify: vi.fn() }));

import { shouldToastFailure, dispatchAction } from "./actions";
import { sendAction } from "./webSocketClient";
import { notify } from "./notify";

describe("shouldToastFailure", () => {
  // インベントリのクリック連鎖由来の良性失敗だけ抑止する
  // Suppress only the benign click-chain failures of inventory ops
  it("インベントリの良性失敗(stale race)は抑止する", () => {
    expect(shouldToastFailure("inventory.move_item", "empty_slot")).toBe(false);
    expect(shouldToastFailure("inventory.move_item", "insufficient_count")).toBe(false);
    expect(shouldToastFailure("inventory.split", "grab_not_empty")).toBe(false);
  });

  // 実バグ由来(invalid_* / 未知)はインベントリでも表示する
  // Genuine failures (invalid_* / unknown) still toast even for inventory ops
  it("インベントリでも実バグ由来の失敗は表示する", () => {
    expect(shouldToastFailure("inventory.move_item", "invalid_slot")).toBe(true);
    expect(shouldToastFailure("inventory.collect", "invalid_payload")).toBe(true);
    expect(shouldToastFailure("inventory.move_item", undefined)).toBe(true);
  });

  it("非インベントリ操作の失敗は常に表示する", () => {
    expect(shouldToastFailure("craft.execute", "anything")).toBe(true);
    expect(shouldToastFailure("debug.echo", undefined)).toBe(true);
  });
});

describe("dispatchAction の toast 配線", () => {
  const ref = { area: "main", slot: 0 } as const;
  const movePayload = { from: ref, to: { area: "main", slot: 1 }, count: 1 } as const;

  beforeEach(() => {
    vi.mocked(notify).mockClear();
  });

  it("良性失敗では notify せず false を返す", async () => {
    vi.mocked(sendAction).mockResolvedValue({ ok: false, error: "empty_slot" });
    const ok = await dispatchAction("inventory.move_item", movePayload);
    expect(ok).toBe(false);
    expect(notify).not.toHaveBeenCalled();
  });

  it("実バグ失敗では notify する", async () => {
    vi.mocked(sendAction).mockResolvedValue({ ok: false, error: "invalid_slot" });
    await dispatchAction("inventory.move_item", movePayload);
    expect(notify).toHaveBeenCalledOnce();
  });
});
