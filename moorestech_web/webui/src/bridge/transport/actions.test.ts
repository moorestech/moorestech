import { afterEach, describe, it, expect, vi, beforeEach } from "vitest";

vi.mock("./notify", () => ({ notify: vi.fn() }));

import { shouldToastFailure, dispatchAction, dispatchActionOutcome } from "./actions";
import { PauseMenuReportKinds } from "./actionContract";
import * as webSocketClient from "./webSocketClient";
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

  // block/modal も action type ごとの良性コードだけ抑止する
  // block/modal also suppress only the benign codes defined per action type
  it("block/modal の良性失敗は抑止する", () => {
    expect(shouldToastFailure("block_inventory.move_item", "empty_slot")).toBe(false);
    expect(shouldToastFailure("block_inventory.move_item", "insufficient_count")).toBe(false);
    expect(shouldToastFailure("block_inventory.split", "grab_not_empty")).toBe(false);
    expect(shouldToastFailure("block_inventory.split", "empty_slot")).toBe(false);
    expect(shouldToastFailure("ui.modal.respond", "no_pending_modal")).toBe(false);
  });

  it("block/modal でも実バグ由来の失敗は表示する", () => {
    expect(shouldToastFailure("block_inventory.move_item", "invalid_slot")).toBe(true);
    expect(shouldToastFailure("ui.modal.respond", "invalid_id")).toBe(true);
  });

  it("非インベントリ操作の失敗は常に表示する", () => {
    expect(shouldToastFailure("craft.execute", "anything")).toBe(true);
    expect(shouldToastFailure("debug.echo", undefined)).toBe(true);
  });

  // blueprint削除のstale失敗は抑止し、通信失敗は表示する
  // Suppress the stale-delete failure but still toast a communication failure
  it("blueprintのstale削除は抑止し通信失敗は表示する", () => {
    expect(shouldToastFailure("blueprint.delete", "blueprint_delete_not_found")).toBe(false);
    expect(shouldToastFailure("blueprint.delete", "blueprint_delete_request_failed")).toBe(true);
  });
});

describe("dispatchAction の toast 配線", () => {
  const ref = { area: "main", slot: 0 } as const;
  const movePayload = { from: ref, to: { area: "main", slot: 1 }, count: 1 } as const;

  beforeEach(() => {
    vi.mocked(notify).mockClear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("良性失敗では notify せず false を返す", async () => {
    vi.spyOn(webSocketClient, "sendAction").mockResolvedValue({ ok: false, error: "empty_slot" });
    const ok = await dispatchAction("inventory.move_item", movePayload);
    expect(ok).toBe(false);
    expect(notify).not.toHaveBeenCalled();
  });

  it("実バグ失敗では notify する", async () => {
    vi.spyOn(webSocketClient, "sendAction").mockResolvedValue({ ok: false, error: "invalid_slot" });
    await dispatchAction("inventory.move_item", movePayload);
    expect(notify).toHaveBeenCalledOnce();
  });

  // 既定の5秒だと ffmpeg 結合とgit起動を待つ bug_report.submit が成功しても失敗として表示される
  // At the 5s default, bug_report.submit (ffmpeg concat plus git spawns) reports a success as a failure
  it("bug_report.submit だけ 120 秒の待ち時間で送る", async () => {
    const sendAction = vi.spyOn(webSocketClient, "sendAction").mockResolvedValue({ ok: true });
    const submitPayload = { description: "ベルトが止まる", kind: PauseMenuReportKinds.bug };
    await dispatchAction("bug_report.submit", submitPayload);
    expect(sendAction).toHaveBeenCalledWith("bug_report.submit", submitPayload, 120000);

    await dispatchAction("inventory.move_item", movePayload);
    expect(sendAction).toHaveBeenLastCalledWith("inventory.move_item", movePayload, 5000);
  });

  // 退避物の同期コピーは既定の5秒を超える。既定のままだと箱は書けているのに失敗表示が出る
  // The synchronous salvage copy exceeds the 5s default; at the default the box is written yet a failure is shown
  it("playtest.crash_report.respond も 120 秒の待ち時間で送る", async () => {
    const sendAction = vi.spyOn(webSocketClient, "sendAction").mockResolvedValue({ ok: true });
    const respondPayload = { send: true, description: "" };
    await dispatchAction("playtest.crash_report.respond", respondPayload);
    expect(sendAction).toHaveBeenCalledWith("playtest.crash_report.respond", respondPayload, 120000);
  });

  // 「サーバーが断った」と「届かなかった」を真偽値へ潰すと、ゲートが無効な指示（もう一度押す）を出す
  // Collapsing "the server refused" and "it never arrived" into a boolean makes the gate print an invalid instruction
  it("dispatchActionOutcome は拒否理由と到達不能を区別して返す", async () => {
    vi.spyOn(webSocketClient, "sendAction").mockResolvedValue({ ok: false, error: "already_responded" });
    expect(await dispatchActionOutcome("playtest.crash_report.respond", { send: true, description: "" }))
      .toEqual({ kind: "rejected", error: "already_responded" });

    vi.spyOn(webSocketClient, "sendAction").mockRejectedValue(new Error("timeout"));
    expect(await dispatchActionOutcome("playtest.consent.acknowledge", {}))
      .toEqual({ kind: "unreachable", reason: "timeout" });

    vi.spyOn(webSocketClient, "sendAction").mockResolvedValue({ ok: true, payload: { missing: ["video"] } });
    expect(await dispatchActionOutcome("playtest.consent.acknowledge", {}))
      .toEqual({ kind: "accepted", payload: { missing: ["video"] } });
  });
});
