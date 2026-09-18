import { describe, expect, it } from "vitest";
import contract from "../contract.json";
import { ACKED_MARKER, DECLARED_MARKER, READY_MARKER } from "../src/bundleMarkers";
import {
  CONTRACT_KINDS,
  DECLARATION_CONFLICT_REASON,
  DECLARATION_UNREADABLE_REASON,
  MAX_BUNDLE_BYTES,
  MAX_BUNDLE_FILES,
  MAX_FILE_BYTES,
  PREPARE_OUTCOME_ACKED,
  PREPARE_OUTCOME_PREPARED,
  RESERVED_UPLOAD_SEGMENTS,
  STEAM_IDENTITY,
  TOKEN_TTL_SECONDS,
  UPLOAD_IDLE_TIMEOUT_SECONDS,
  UPLOAD_URL_TTL_SECONDS,
} from "../src/contract";
import { KIND_PREFIX } from "../src/keys";

// contract.jsonはC#クライアントと共有する正本。Workerの実効値がそこから外れていないことを固定する
// contract.json is the source shared with the C# client; this pins the Worker's effective values to it
describe("contract.json", () => {
  it("Workerの定数がcontract.jsonと一致する", () => {
    expect(STEAM_IDENTITY).toBe(contract.steamIdentity);
    expect(MAX_FILE_BYTES).toBe(contract.maxFileBytes);
    expect([...RESERVED_UPLOAD_SEGMENTS]).toEqual(contract.reservedUploadSegments);
    expect(CONTRACT_KINDS).toEqual(contract.kinds);
    expect(TOKEN_TTL_SECONDS).toBe(contract.tokenTtlSeconds);
  });

  it("失敗分類を左右する応答語はcontract.jsonと一致する", () => {
    expect(DECLARATION_CONFLICT_REASON).toBe(contract.declarationConflictReason);
    expect(DECLARATION_UNREADABLE_REASON).toBe(contract.declarationUnreadableReason);
    expect(PREPARE_OUTCOME_PREPARED).toBe(contract.prepareOutcomes.prepared);
    expect(PREPARE_OUTCOME_ACKED).toBe(contract.prepareOutcomes.acked);
  });

  it("kindの表とマーカー名が契約から外れていない", () => {
    expect(Object.keys(KIND_PREFIX)).toEqual(contract.kinds);
    expect(contract.reservedUploadSegments).toContain(READY_MARKER);
    expect(contract.reservedUploadSegments).toContain(ACKED_MARKER);
    expect(contract.reservedUploadSegments).toContain(DECLARED_MARKER);
    expect(contract.reservedUploadSegments).toContain("complete");
    expect(contract.reservedUploadSegments).toContain("prepare");
  });

  it("契約値はこれまでの実効値を保つ", () => {
    expect(contract).toEqual({
      steamIdentity: "moorestech-playtest",
      maxFileBytes: 100 * 1024 * 1024,
      maxBundleFiles: 128,
      maxBundleBytes: 256 * 1024 * 1024,
      uploadUrlTtlSeconds: 3600,
      uploadIdleTimeoutSeconds: 60,
      reservedUploadSegments: ["READY", "ACKED", "DECLARED", "complete", "prepare"],
      kinds: ["report", "progress"],
      tokenTtlSeconds: 3600,
      declarationConflictReason: "declaration-conflict",
      declarationUnreadableReason: "declaration-unreadable",
      prepareOutcomes: { prepared: "prepared", acked: "acked" },
    });
  });
});

// 箱単位の上限・URL期限・アイドル期限は直接アップロードの土台。値の出所はcontract.json一本
// Bundle-wide limits and the URL/idle deadlines underpin direct upload; contract.json is their sole source
describe("contract constants", () => {
  it("箱単位の上限とURL期限とアイドル期限はcontract.jsonと一致する", () => {
    expect(MAX_BUNDLE_FILES).toBe(contract.maxBundleFiles);
    expect(MAX_BUNDLE_BYTES).toBe(contract.maxBundleBytes);
    expect(UPLOAD_URL_TTL_SECONDS).toBe(contract.uploadUrlTtlSeconds);
    expect(UPLOAD_IDLE_TIMEOUT_SECONDS).toBe(contract.uploadIdleTimeoutSeconds);
    expect(contract.maxBundleBytes).toBeGreaterThanOrEqual(contract.maxFileBytes);
  });
});
